using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class NetworkManager
{
    [Header("Host migration")]
    [SerializeField] private bool enableHostMigration = true;
    [SerializeField] private NetworkRunner migrationRunnerPrefab;
    [SerializeField, Min(0.25f)] private float migrationCheckpointSeconds = 1f;
    [SerializeField, Min(5f)] private float migrationTimeoutSeconds = 20f;
    [SerializeField, Min(2f)] private float migrationReconnectSeconds = 8f;
    [SerializeField, Min(2f)] private float migrationMaximumCheckpointAge = 15f;

    public bool IsMigrating => migrationInProgress || migrationStartQueued || migrationPreparing;
    private const int MigrationMagic = 0x52424D31;
    private const int MigrationSnapshot = 1;
    private const int MigrationAck = 2;
    private const int MigrationDepart = 3;
    private const int MigrationResume = 4;
    private const int MigrationClose = 5;
    private const int MaximumMigrationBytes = 65536;
    private readonly string migrationIdentity = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, MigrationPlayerState> migrationMembers = new Dictionary<string, MigrationPlayerState>();
    private readonly Dictionary<PlayerRef, string> migrationPlayerIdentities = new Dictionary<PlayerRef, string>();
    private readonly Dictionary<string, PlayerRef> migrationRejoined = new Dictionary<string, PlayerRef>();
    private readonly Dictionary<PlayerRef, int> migrationPendingAcks = new Dictionary<PlayerRef, int>();
    private readonly Dictionary<PlayerRef, float> migrationPendingSince = new Dictionary<PlayerRef, float>();
    private HostMigrationCheckpoint migrationCheckpoint;
    private HostMigrationCheckpoint migrationReadyCheckpoint;
    private float migrationCheckpointReceivedAt;
    private float migrationNextCheckpointAt;
    private float migrationRestoreDeadline;
    private string migrationRoomId;
    private int migrationEpoch;
    private int migrationRevision;
    private int migrationNextJoinOrder = 1;
    private int migrationTransferSequence;
    private bool migrationEverHadTwo;
    private bool migrationRosterDirty;
    private bool migrationInProgress;
    private bool migrationStartQueued;
    private bool migrationPreparing;
    private bool migrationClosing;
    private bool migrationSceneReady;
    private bool migrationRestoreStarted;
    private bool migrationRestored;
    private bool migrationSendResume;
    private bool migrationStartupComplete;
    private bool migrationBlockMouseUntilRelease;
    private Scene migrationHoldingScene;
    [SerializeField] private NetworkObject migrationChatManagerPrefab;
    private string migrationStatus = "";

    private bool ValidateMigrationSetup()
    {
        if (!enableHostMigration)
            return true;
        if (runner == null || runner.gameObject == gameObject || !runner.transform.IsChildOf(transform))
        {
            SetStatus("Host migration: el Runner inicial debe estar en un objeto hijo del NetworkManager.", true);
            OnJoinFailed?.Invoke();
            return false;
        }
        if (migrationRunnerPrefab != null && migrationRunnerPrefab.GetComponent<NetworkSceneManagerDefault>() != null && migrationRunnerPrefab.GetComponentInChildren<NetworkManager>(true) == null)
            return true;
        SetStatus("Host migration: asigna un prefab con NetworkRunner y NetworkSceneManagerDefault, sin NetworkManager.", true);
        OnJoinFailed?.Invoke();
        return false;
    }

    private void ResetMigrationSession(bool host)
    {
        migrationMembers.Clear();
        migrationPlayerIdentities.Clear();
        migrationRejoined.Clear();
        migrationPendingAcks.Clear();
        migrationPendingSince.Clear();
        migrationCheckpoint = null;
        migrationReadyCheckpoint = null;
        migrationRoomId = host ? Guid.NewGuid().ToString("N") : null;
        migrationEpoch = 0;
        migrationRevision = 0;
        migrationNextJoinOrder = 1;
        if (host)
            migrationMembers[migrationIdentity] = new MigrationPlayerState { Identity = migrationIdentity, JoinOrder = migrationNextJoinOrder++, Connected = true };
        migrationEverHadTwo = false;
        migrationSendResume = false;
        migrationBlockMouseUntilRelease = false;
        migrationNextCheckpointAt = 0f;
        migrationClosing = false;
    }

    private void Update()
    {
        if (migrationStartQueued && !migrationInProgress)
        {
            migrationStartQueued = false;
            _ = MigrateOrderedAsync();
        }
        if (runner == null || !runner.IsRunning || migrationClosing)
            return;
        if (migrationInProgress)
        {
            if (migrationSceneReady) ReleaseMigrationHoldingScene();
            if (runner.IsServer && migrationStartupComplete && migrationSceneReady && !runner.IsSceneManagerBusy && !migrationRestoreStarted)
            {
                migrationRestoreStarted = true;
                RestoreMigrationScene();
            }
            if (runner.IsServer && migrationStartupComplete && migrationRestored)
                TryFinishMigrationHost();
            else if (!runner.IsServer && migrationSceneReady && !runner.IsSceneManagerBusy && migrationReadyCheckpoint != null)
                TryFinishMigrationClient();
            return;
        }
        if (!enableHostMigration || !runner.IsServer || !isInSession || runner.IsSceneManagerBusy || Time.unscaledTime < migrationNextCheckpointAt)
            return;
        migrationNextCheckpointAt = Time.unscaledTime + Mathf.Max(0.25f, migrationCheckpointSeconds);
        HostMigrationCheckpoint checkpoint = CaptureMigrationCheckpoint();
        if (checkpoint != null)
        {
            if (migrationPendingAcks.Count == 0) migrationSendResume = false;
            BroadcastMigration(checkpoint, migrationSendResume ? MigrationResume : MigrationSnapshot, migrationRosterDirty);
            migrationRosterDirty = false;
        }
    }

    private string GetMigrationIdentity(PlayerRef player)
    {
        if (player == runner.LocalPlayer)
            return migrationIdentity;
        byte[] token = runner.GetPlayerConnectionToken(player);
        if (token == null || token.Length != 32)
            return null;
        string identity = Encoding.ASCII.GetString(token);
        return Guid.TryParseExact(identity, "N", out _) ? identity : null;
    }

    private bool MigrationPlayerJoined(PlayerRef player)
    {
        if (!enableHostMigration || !runner.IsServer)
            return IsMigrating;
        string identity = GetMigrationIdentity(player);
        if (identity == null)
        {
            runner.Disconnect(player);
            return true;
        }
        if (migrationInProgress)
        {
            MigrationPlayerState state = FindMigrationPlayer(migrationCheckpoint, identity);
            if (state == null || !state.Connected || identity == migrationCheckpoint.HostIdentity)
            {
                runner.Disconnect(player);
                return true;
            }
            migrationPlayerIdentities[player] = identity;
            migrationRejoined[identity] = player;
            if (migrationRestored)
                RestoreMigrationPlayer(state, player);
            return true;
        }
        if (!migrationMembers.TryGetValue(identity, out MigrationPlayerState member))
        {
            member = new MigrationPlayerState { Identity = identity, JoinOrder = migrationNextJoinOrder++ };
            migrationMembers.Add(identity, member);
        }
        member.Connected = true;
        migrationPlayerIdentities[player] = identity;
        migrationEverHadTwo |= CountConnectedMigrationMembers() >= 2;
        migrationRosterDirty = true;
        migrationNextCheckpointAt = 0f;
        return false;
    }

    private bool MigrationPlayerLeft(PlayerRef player)
    {
        if (migrationClosing)
            return true;
        if (IsMigrating)
        {
            if (runner.IsServer && migrationPlayerIdentities.TryGetValue(player, out string leaving))
            {
                migrationRejoined.Remove(leaving);
                migrationPlayerIdentities.Remove(player);
                if (runner.TryGetPlayerObject(player, out NetworkObject avatar) && avatar != null && avatar.IsValid)
                    runner.Despawn(avatar);
            }
            return true;
        }
        if (!enableHostMigration || !runner.IsServer)
            return false;
        if (migrationPlayerIdentities.TryGetValue(player, out string identity) && migrationMembers.TryGetValue(identity, out MigrationPlayerState member))
            member.Connected = false;
        migrationPlayerIdentities.Remove(player);
        migrationPendingAcks.Remove(player);
        migrationPendingSince.Remove(player);
        migrationRosterDirty = true;
        migrationNextCheckpointAt = 0f;
        if (migrationEverHadTwo && CountConnectedMigrationMembers() <= 1)
        {
            _ = CloseMigrationSessionAsync("La sala se cerro porque solo queda un jugador conectado.", true);
            return true;
        }
        return false;
    }

    private int CountConnectedMigrationMembers()
    {
        int count = 0;
        foreach (MigrationPlayerState member in migrationMembers.Values)
            if (member.Connected)
                count++;
        return count;
    }

    private void ResetMigrationRoundPlayers()
    {
        foreach (MigrationPlayerState member in migrationMembers.Values)
        {
            member.GameId = -1;
            member.HasBall = false;
        }
    }

    private HostMigrationCheckpoint CaptureMigrationCheckpoint()
    {
        if (runner == null || !runner.IsRunning || !runner.IsServer || runner.IsSceneManagerBusy)
            return null;
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        bool lobby = sceneIndex == preGameSceneIndex;
        if (!lobby && !IsMapScene(sceneIndex))
            return null;
        if (lobby && MapVoteController.Instance != null && MapVoteController.Instance.CurrentPhase == VotePhase.Loading)
            return null;
        if (!lobby && (GameManager.Instance == null || GameManager.Instance.Object == null || !GameManager.Instance.Object.IsValid))
            return null;
        List<MigrationPlayerState> players = new List<MigrationPlayerState>();
        foreach (MigrationPlayerState member in migrationMembers.Values)
        {
            if (!member.Connected && (lobby || member.GameId < 0))
                continue;
            MigrationPlayerState copy = JsonUtility.FromJson<MigrationPlayerState>(JsonUtility.ToJson(member));
            if (lobby)
            {
                copy.GameId = -1;
                copy.HasBall = false;
            }
            foreach (KeyValuePair<PlayerRef, string> pair in migrationPlayerIdentities)
            {
                if (pair.Value != member.Identity || !runner.TryGetPlayerObject(pair.Key, out NetworkObject avatar) || avatar == null || !avatar.IsValid)
                    continue;
                Ball ball = avatar.GetComponent<Ball>();
                LobbyPlayer lobbyPlayer = avatar.GetComponent<LobbyPlayer>();
                if (ball != null)
                {
                    ball.CaptureMigration(copy);
                    PlayersChat chat = ball.GetComponent<PlayersChat>();
                    if (chat != null && chat.MigrationChatPrefab != null)
                        migrationChatManagerPrefab = chat.MigrationChatPrefab;
                }
                else if (lobbyPlayer != null)
                    lobbyPlayer.CaptureMigration(copy);
                member.GameId = copy.GameId;
                member.HasBall = copy.HasBall;
                member.Name = copy.Name;
                member.ColorIndex = copy.ColorIndex;
                member.SpriteIndex = copy.SpriteIndex;
                member.Damage = copy.Damage;
                member.DisplayDamage = copy.DisplayDamage;
                member.Lives = copy.Lives;
                member.ShootsLeft = copy.ShootsLeft;
                break;
            }
            if (!lobby && copy.GameId >= 0 && !HasLiveMigrationBall(member.Identity))
                copy.HasBall = false;
            players.Add(copy);
        }
        HostMigrationCheckpoint checkpoint = new HostMigrationCheckpoint
        {
            RoomId = migrationRoomId,
            RoomName = migrationCheckpoint != null ? migrationCheckpoint.RoomName : currentSessionName,
            Region = runner.SessionInfo.Region,
            HostIdentity = migrationIdentity,
            Epoch = migrationEpoch,
            Revision = ++migrationRevision,
            NextJoinOrder = migrationNextJoinOrder,
            SceneIndex = sceneIndex,
            IsLobby = lobby,
            SessionVisible = sessionVisible,
            EverHadTwoPlayers = migrationEverHadTwo,
            Players = players.ToArray(),
            Game = !lobby ? GameManager.Instance.CaptureMigration() : null,
            Vote = lobby && MapVoteController.Instance != null ? MapVoteController.Instance.CaptureMigration() : null,
            Chat = ChatManager.Instance != null && ChatManager.Instance.Object != null && ChatManager.Instance.Object.IsValid ? ChatManager.Instance.conversationRecord.ToString() : ""
        };
        migrationCheckpoint = checkpoint;
        migrationCheckpointReceivedAt = Time.unscaledTime;
        return checkpoint;
    }

    private bool HasLiveMigrationBall(string identity)
    {
        foreach (KeyValuePair<PlayerRef, string> pair in migrationPlayerIdentities)
            if (pair.Value == identity && runner.TryGetPlayerObject(pair.Key, out NetworkObject avatar) && avatar != null && avatar.IsValid && avatar.GetComponent<Ball>() != null)
                return true;
        return false;
    }

    private void BroadcastMigration(HostMigrationCheckpoint checkpoint, int kind, bool force)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(checkpoint));
        if (bytes.Length > MaximumMigrationBytes)
            throw new InvalidOperationException("El checkpoint supera el limite de migracion.");
        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (player == runner.LocalPlayer)
                continue;
            if (!force && migrationPendingAcks.ContainsKey(player) && migrationPendingSince.TryGetValue(player, out float sentAt) && Time.unscaledTime - sentAt < 3f)
                continue;
            migrationPendingAcks[player] = checkpoint.Revision;
            migrationPendingSince[player] = Time.unscaledTime;
            runner.SendReliableDataToPlayer(player, ReliableKey.FromInts(MigrationMagic, kind, checkpoint.Revision, ++migrationTransferSequence), bytes);
        }
    }

    private void ReceiveMigrationData(NetworkRunner source, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
    {
        if (!enableHostMigration || source != runner || migrationClosing)
            return;
        key.GetInts(out int magic, out int kind, out int revision, out int transfer);
        if (magic != MigrationMagic)
            return;
        if (source.IsServer)
        {
            if (kind == MigrationAck && migrationPendingAcks.TryGetValue(player, out int pending) && revision >= pending)
            {
                migrationPendingAcks.Remove(player);
                migrationPendingSince.Remove(player);
            }
            return;
        }
        if (kind == MigrationClose)
        {
            _ = CloseMigrationSessionAsync("La sala se cerro porque solo queda un jugador conectado.", false);
            return;
        }
        if (kind != MigrationSnapshot && kind != MigrationDepart && kind != MigrationResume)
            return;
        if (data.IsEmpty || data.Length > MaximumMigrationBytes)
            return;
        HostMigrationCheckpoint checkpoint;
        try
        {
            checkpoint = JsonUtility.FromJson<HostMigrationCheckpoint>(Encoding.UTF8.GetString(data.ToArray()));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Checkpoint de migracion invalido: " + exception.Message);
            return;
        }
        if (!ValidMigrationCheckpoint(checkpoint))
            return;
        if (migrationCheckpoint != null && migrationCheckpoint.RoomId != checkpoint.RoomId)
            return;
        if (migrationCheckpoint != null && checkpoint.Epoch < migrationCheckpoint.Epoch)
            return;
        if (migrationCheckpoint != null && checkpoint.Epoch == migrationCheckpoint.Epoch && checkpoint.Revision < migrationCheckpoint.Revision)
            return;
        migrationCheckpoint = checkpoint;
        migrationCheckpointReceivedAt = Time.unscaledTime;
        RememberMigrationChatPrefab();
        migrationRoomId = checkpoint.RoomId;
        migrationEpoch = checkpoint.Epoch;
        migrationRevision = checkpoint.Revision;
        migrationNextJoinOrder = checkpoint.NextJoinOrder;
        migrationEverHadTwo = checkpoint.EverHadTwoPlayers;
        source.SendReliableDataToServer(ReliableKey.FromInts(MigrationMagic, MigrationAck, revision, transfer), new byte[] { 1 });
        if (kind == MigrationDepart && !IsMigrating && !leftOnPurpose)
            migrationStartQueued = true;
        else if (kind == MigrationResume && migrationInProgress)
            migrationReadyCheckpoint = checkpoint;
    }

    private bool ValidMigrationCheckpoint(HostMigrationCheckpoint checkpoint)
    {
        if (checkpoint == null || checkpoint.Protocol != 1 || checkpoint.Players == null || checkpoint.Players.Length < 1 || checkpoint.Players.Length > 16 || !Guid.TryParseExact(checkpoint.RoomId, "N", out _))
            return false;
        if (checkpoint.SceneIndex != preGameSceneIndex && !IsMapScene(checkpoint.SceneIndex))
            return false;
        if (string.IsNullOrEmpty(checkpoint.Region) || checkpoint.IsLobby != (checkpoint.SceneIndex == preGameSceneIndex) || (!checkpoint.IsLobby && checkpoint.Game == null))
            return false;
        HashSet<string> identities = new HashSet<string>();
        HashSet<int> orders = new HashSet<int>();
        foreach (MigrationPlayerState state in checkpoint.Players)
            if (state == null || !Guid.TryParseExact(state.Identity, "N", out _) || state.JoinOrder < 1 || !identities.Add(state.Identity) || !orders.Add(state.JoinOrder))
                return false;
        return FindMigrationPlayer(checkpoint, migrationIdentity) != null && FindMigrationPlayer(checkpoint, checkpoint.HostIdentity) != null;
    }

    private static MigrationPlayerState FindMigrationPlayer(HostMigrationCheckpoint checkpoint, string identity)
    {
        if (checkpoint != null && checkpoint.Players != null)
            foreach (MigrationPlayerState player in checkpoint.Players)
                if (player.Identity == identity)
                    return player;
        return null;
    }

    private bool QueueMigrationAfterHostLoss()
    {
        if (IsMigrating)
            return true;
        if (!enableHostMigration || leftOnPurpose || migrationClosing || !isInSession || migrationCheckpoint == null || migrationCheckpoint.HostIdentity == migrationIdentity)
            return false;
        migrationStartQueued = true;
        return true;
    }

    private async Task MigrateOrderedAsync()
    {
        migrationInProgress = true;
        inputEnabled = false;
        migrationSceneReady = false;
        migrationRestoreStarted = false;
        migrationRestored = false;
        migrationReadyCheckpoint = null;
        migrationStartupComplete = false;
        migrationBlockMouseUntilRelease = true;
        migrationStatus = "Cambiando de host...";
        try
        {
            HostMigrationCheckpoint checkpoint = migrationCheckpoint;
            if (checkpoint == null || Time.unscaledTime - migrationCheckpointReceivedAt > migrationMaximumCheckpointAge)
                throw new InvalidOperationException("No hay un checkpoint reciente para recuperar la sala.");
            MigrationPlayerState[] remaining = HostMigrationOrder.Remaining(checkpoint.Players, checkpoint.HostIdentity);
            if (remaining.Length <= 1)
            {
                await CloseMigrationSessionAsync("La sala se cerro porque solo queda un jugador conectado.", false);
                return;
            }
            if (migrationRunnerPrefab == null)
                throw new InvalidOperationException("Falta asignar Migration Runner Prefab.");
            foreach (Ball ball in FindObjectsByType<Ball>(FindObjectsSortMode.None))
                ball.SetMigrationPaused(true);
            Scene holding = SceneManager.CreateScene("HostMigrationHolding_" + checkpoint.RoomId + "_" + (checkpoint.Epoch + 1));
            migrationHoldingScene = holding;
            SceneManager.SetActiveScene(holding);
            NetworkRunner previousRunner = runner;
            if (previousRunner != null)
            {
                previousRunner.RemoveCallbacks(this);
                await MigrationWait(previousRunner.Shutdown(destroyGameObject: false), migrationTimeoutSeconds);
                if (previousRunner != null)
                {
                    if (previousRunner.gameObject == gameObject)
                        Destroy(previousRunner);
                    else
                        Destroy(previousRunner.gameObject);
                }
            }
            runner = null;
            int sceneIndex = checkpoint.SceneIndex;
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene == holding || !scene.isLoaded || (scene.buildIndex != preGameSceneIndex && !IsMapScene(scene.buildIndex)))
                    continue;
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                    while (!unload.isDone)
                        await Task.Yield();
            }
            if (migrationClosing || this == null)
                return;
            migrationRejoined.Clear();
            migrationPlayerIdentities.Clear();
            migrationMembers.Clear();
            foreach (MigrationPlayerState state in checkpoint.Players)
                migrationMembers[state.Identity] = JsonUtility.FromJson<MigrationPlayerState>(JsonUtility.ToJson(state));
            migrationMembers[checkpoint.HostIdentity].Connected = false;
            migrationEpoch = checkpoint.Epoch + 1;
            migrationNextJoinOrder = checkpoint.NextJoinOrder;
            sessionVisible = checkpoint.SessionVisible;
            inPreGame = checkpoint.IsLobby;
            startingGame = false;
            returningToLobby = false;
            gamePlayersSpawned = !checkpoint.IsLobby;
            lastValidInput.Clear();
            lastInputTicks.Clear();
            migrationPendingAcks.Clear();
            migrationPendingSince.Clear();
            bool becomeHost = remaining[0].Identity == migrationIdentity;
            migrationStatus = becomeHost ? "Asumiendo el host..." : "Conectando con " + remaining[0].Name + "...";
            CreateMigrationRunner();
            var photonSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
            photonSettings.FixedRegion = checkpoint.Region;
            currentSessionName = HostMigrationOrder.SessionName(checkpoint.RoomId, migrationEpoch);
            StartGameArgs args = new StartGameArgs
            {
                GameMode = becomeHost ? GameMode.Host : GameMode.Client,
                SessionName = currentSessionName,
                ConnectionToken = Encoding.ASCII.GetBytes(migrationIdentity),
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
                CustomPhotonAppSettings = photonSettings,
                EnableClientSessionCreation = false,
                PlayerCount = maxPlayers,
                IsOpen = true,
                IsVisible = false
            };
            if (becomeHost)
                args.Scene = SceneRef.FromIndex(sceneIndex);
            if (!becomeHost)
                await Task.Delay(800);
            if (migrationClosing || this == null) return;
            float retryDeadline = Time.unscaledTime + migrationTimeoutSeconds;
            var result = await MigrationWait(runner.StartGame(args), migrationTimeoutSeconds);
            while (!result.Ok && !becomeHost && !migrationClosing && Time.unscaledTime < retryDeadline)
            {
                if (runner != null)
                {
                    runner.RemoveCallbacks(this);
                    if (!runner.IsShutdown)
                        await MigrationWait(runner.Shutdown(destroyGameObject: false), 5f);
                    if (runner != null) Destroy(runner.gameObject);
                }
                await Task.Delay(750);
                if (migrationClosing || this == null) return;
                CreateMigrationRunner();
                args.SceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
                result = await MigrationWait(runner.StartGame(args), Mathf.Max(1f, retryDeadline - Time.unscaledTime));
            }
            if (migrationClosing || this == null) return;
            if (!result.Ok)
                throw new InvalidOperationException("No se pudo cambiar de host: " + ReasonToText(result.ShutdownReason));
            migrationStartupComplete = true;
            isInSession = true;
            migrationRestoreDeadline = Time.unscaledTime + Mathf.Max(2f, migrationReconnectSeconds);
            if (becomeHost)
                foreach (PlayerRef player in runner.ActivePlayers)
                    MigrationPlayerJoined(player);
            while (IsMigrating && !migrationClosing && this != null)
            {
                if (Time.unscaledTime > migrationRestoreDeadline + migrationTimeoutSeconds)
                    throw new TimeoutException("Se agoto el tiempo para recuperar los jugadores.");
                await Task.Yield();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Host migration: " + exception.Message);
            await CloseMigrationSessionAsync(exception.Message, false);
        }
    }

    private void RestoreMigrationScene()
    {
        try
        {
            migrationRestoreDeadline = Time.unscaledTime + Mathf.Max(2f, migrationReconnectSeconds);
            foreach (KeyValuePair<string, PlayerRef> pair in migrationRejoined)
            {
                MigrationPlayerState state = FindMigrationPlayer(migrationCheckpoint, pair.Key);
                if (state != null)
                    RestoreMigrationPlayer(state, pair.Value);
            }
            migrationRestored = true;
        }
        catch (Exception exception)
        {
            _ = CloseMigrationSessionAsync("No se pudo restaurar la escena: " + exception.Message, true);
        }
    }

    private void RestoreMigrationPlayer(MigrationPlayerState state, PlayerRef player)
    {
        if (runner.TryGetPlayerObject(player, out NetworkObject existing) && existing != null && existing.IsValid)
            return;
        playerColors[player] = state.ColorIndex;
        playerSprites[player] = state.SpriteIndex;
        playerName[player] = state.Name;
        if (migrationCheckpoint.IsLobby)
        {
            NetworkObject avatar = runner.Spawn(lobbyPlayerPrefab, Vector3.zero, Quaternion.identity, player);
            runner.SetPlayerObject(player, avatar);
            avatar.GetComponent<LobbyPlayer>().RestoreMigration(state);
        }
        else if (state.HasBall)
        {
            NetworkObject avatar = runner.Spawn(ballPrefab, state.Position, state.Rotation, player);
            runner.SetPlayerObject(player, avatar);
            avatar.GetComponent<Ball>().RestoreMigration(state);
        }
    }

    private void TryFinishMigrationHost()
    {
        MigrationPlayerState[] remaining = HostMigrationOrder.Remaining(migrationCheckpoint.Players, migrationCheckpoint.HostIdentity);
        if (migrationRejoined.Count < remaining.Length && Time.unscaledTime < migrationRestoreDeadline)
            return;
        if (migrationRejoined.Count <= 1)
        {
            _ = CloseMigrationSessionAsync("La sala se cerro: no se reconectaron al menos dos jugadores.", true);
            return;
        }
        try
        {
            foreach (MigrationPlayerState state in migrationMembers.Values)
                state.Connected = migrationRejoined.ContainsKey(state.Identity);
            if (migrationCheckpoint.IsLobby)
            {
                if (MapVoteController.Instance != null && migrationCheckpoint.Vote != null)
                    MapVoteController.Instance.RestoreMigration(migrationCheckpoint.Vote);
            }
            else
            {
                if (GameManager.Instance == null)
                    throw new InvalidOperationException("No hay GameManager en el mapa restaurado.");
                GameManager.Instance.RestoreMigration(migrationCheckpoint.Game, migrationCheckpoint.Players, migrationRejoined);
                foreach (KeyValuePair<string, PlayerRef> pair in migrationRejoined)
                {
                    MigrationPlayerState state = FindMigrationPlayer(migrationCheckpoint, pair.Key);
                    if (state != null && state.HasBall && runner.TryGetPlayerObject(pair.Value, out NetworkObject avatar))
                        avatar.GetComponent<Ball>().FinishMigration(state);
                }
            }
            if (!migrationCheckpoint.IsLobby)
            {
                RememberMigrationChatPrefab();
                if (ChatManager.Instance == null && migrationChatManagerPrefab != null)
                    runner.Spawn(migrationChatManagerPrefab);
                if (ChatManager.Instance != null && ChatManager.Instance.Object != null && ChatManager.Instance.Object.IsValid)
                    ChatManager.Instance.RestoreMigration(migrationCheckpoint.Chat);
            }
            migrationInProgress = false;
            migrationEverHadTwo = true;
            inputEnabled = GameManager.Instance == null || !GameManager.Instance.IsGameOver;
            migrationStatus = "";
            migrationNextCheckpointAt = Time.unscaledTime + migrationCheckpointSeconds;
            HostMigrationCheckpoint resumed = CaptureMigrationCheckpoint();
            if (resumed == null)
                throw new InvalidOperationException("La escena restaurada no esta lista.");
            migrationSendResume = true;
            BroadcastMigration(resumed, MigrationResume, true);
            if (!resumed.IsLobby) GameManager.Instance.RefreshMigrationHud(resumed.Players);
            bool reopen = inPreGame && (MapVoteController.Instance == null || !MapVoteController.Instance.IsActive);
            SetSessionOpen(reopen);
            PreGameLobbyUI.Instance?.RefreshLobby();
            if (inPreGame) CheckAllPlayersReady();
        }
        catch (Exception exception)
        {
            _ = CloseMigrationSessionAsync("Error al reanudar: " + exception.Message, true);
        }
    }

    private void TryFinishMigrationClient()
    {
        MigrationPlayerState local = FindMigrationPlayer(migrationReadyCheckpoint, migrationIdentity);
        if (local == null || !local.Connected)
        {
            _ = CloseMigrationSessionAsync("El nuevo host no pudo recuperar tu conexion.", false);
            return;
        }
        if ((migrationReadyCheckpoint.IsLobby || local.HasBall) && !runner.TryGetPlayerObject(runner.LocalPlayer, out _))
            return;
        Ball[] restoredBalls = FindObjectsByType<Ball>(FindObjectsSortMode.None);
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        if (sceneIndex != migrationReadyCheckpoint.SceneIndex)
        {
            if (sceneIndex != preGameSceneIndex && !IsMapScene(sceneIndex)) return;
            if (sceneIndex == preGameSceneIndex && !runner.TryGetPlayerObject(runner.LocalPlayer, out _)) return;
            foreach (Ball ball in restoredBalls)
                if (ball.Object != null && ball.Object.IsValid) ball.ResumeMigrationPhysics();
            inPreGame = sceneIndex == preGameSceneIndex;
            gamePlayersSpawned = !inPreGame;
            migrationInProgress = false;
            migrationStatus = "";
            inputEnabled = GameManager.Instance == null || !GameManager.Instance.IsGameOver;
            PreGameLobbyUI.Instance?.RefreshLobby();
            return;
        }
        foreach (MigrationPlayerState expected in migrationReadyCheckpoint.Players)
        {
            if (!expected.Connected || !expected.HasBall) continue;
            bool found = false;
            foreach (Ball ball in restoredBalls)
                if (ball.Object != null && ball.Object.IsValid && ball.id == expected.GameId) found = true;
            if (!found) return;
        }
        foreach (Ball ball in restoredBalls)
        {
            if (ball.Object == null || !ball.Object.IsValid)
                continue;
            foreach (MigrationPlayerState state in migrationReadyCheckpoint.Players)
                if (state.Connected && state.HasBall && state.GameId == ball.id)
                    ball.FinishMigration(state);
        }
        migrationInProgress = false;
        migrationStatus = "";
        inputEnabled = GameManager.Instance == null || !GameManager.Instance.IsGameOver;
        if (migrationReadyCheckpoint.Game != null && GameManager.Instance != null)
            GameManager.Instance.RefreshMigrationHud(migrationReadyCheckpoint.Players);
        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    private void CreateMigrationRunner()
    {
        runner = Instantiate(migrationRunnerPrefab);
        runner.name = "Migrated NetworkRunner";
        runner.transform.SetParent(transform, false);
        runner.ProvideInput = true;
        runner.AddCallbacks(this);
    }

    private void ReleaseMigrationHoldingScene()
    {
        if (!migrationHoldingScene.IsValid() || !migrationHoldingScene.isLoaded) return;
        Scene restored = SceneManager.GetSceneByBuildIndex(migrationCheckpoint.SceneIndex);
        if (!restored.IsValid() || !restored.isLoaded) return;
        SceneManager.SetActiveScene(restored);
        SceneManager.UnloadSceneAsync(migrationHoldingScene);
        migrationHoldingScene = default;
    }

    private bool MigrationInputBlocked()
    {
        if (!migrationBlockMouseUntilRelease) return false;
        if (Input.GetMouseButton(0)) return true;
        migrationBlockMouseUntilRelease = false;
        return false;
    }

    private void RememberMigrationChatPrefab()
    {
        if (migrationChatManagerPrefab != null) return;
        foreach (PlayersChat chat in FindObjectsByType<PlayersChat>(FindObjectsSortMode.None))
            if (chat.MigrationChatPrefab != null)
            {
                migrationChatManagerPrefab = chat.MigrationChatPrefab;
                return;
            }
    }

    private async Task DepartMigrationAsync()
    {
        migrationPreparing = true;
        inputEnabled = false;
        migrationStatus = "Preparando relevo de host...";
        try
        {
            HostMigrationCheckpoint checkpoint = CaptureMigrationCheckpoint();
            foreach (Ball ball in FindObjectsByType<Ball>(FindObjectsSortMode.None))
                ball.SetMigrationPaused(true);
            if (checkpoint != null)
            {
                MigrationPlayerState[] remaining = HostMigrationOrder.Remaining(checkpoint.Players, migrationIdentity);
                if (remaining.Length >= 2)
                {
                    BroadcastMigration(checkpoint, MigrationDepart, true);
                    float deadline = Time.unscaledTime + 1f;
                    while (migrationPendingAcks.Count > 0 && Time.unscaledTime < deadline)
                        await Task.Yield();
                }
                else
                    BroadcastMigrationClose();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("No se pudo preparar el relevo: " + exception.Message);
        }
        await CloseMigrationSessionAsync("Saliste de la partida.", false);
    }

    private async Task CloseMigrationSessionAsync(string message, bool notify)
    {
        if (migrationClosing)
            return;
        migrationClosing = true;
        leftOnPurpose = true;
        inputEnabled = false;
        migrationStartQueued = false;
        migrationStatus = message;
        if (notify && runner != null && runner.IsRunning && runner.IsServer)
        {
            BroadcastMigrationClose();
            await Task.Delay(300);
        }
        try
        {
            if (runner != null)
                await MigrationWait(runner.Shutdown(destroyGameObject: false), 5f);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Cierre de sala: " + exception.Message);
        }
        migrationInProgress = false;
        migrationPreparing = false;
        if (runner != null && runner.gameObject != gameObject)
            Destroy(runner.gameObject);
        GoToMenu(message, false);
    }

    private void BroadcastMigrationClose()
    {
        foreach (PlayerRef player in runner.ActivePlayers)
            if (player != runner.LocalPlayer)
                runner.SendReliableDataToPlayer(player, ReliableKey.FromInts(MigrationMagic, MigrationClose, migrationRevision, ++migrationTransferSequence), new byte[] { 1 });
    }

    private static async Task MigrationWait(Task task, float seconds)
    {
        if (await Task.WhenAny(task, Task.Delay(Mathf.CeilToInt(seconds * 1000f))) != task)
        {
            _ = task.ContinueWith(completed => { var observed = completed.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            throw new TimeoutException("Se agoto el tiempo de la operacion de red.");
        }
        await task;
    }

    private static async Task<T> MigrationWait<T>(Task<T> task, float seconds)
    {
        await MigrationWait((Task)task, seconds);
        return await task;
    }

    private void OnGUI()
    {
        if (!IsMigrating && !migrationClosing)
            return;
        GUI.Box(new Rect((Screen.width - 440f) * 0.5f, (Screen.height - 80f) * 0.5f, 440f, 80f), migrationStatus);
    }
}
