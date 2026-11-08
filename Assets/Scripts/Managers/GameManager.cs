using Fusion;
using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum StartPhase : byte { None = 0, Ready = 1, Set = 2, Go = 3 }

public class GameManager : NetworkBehaviour
{
    [Networked] public int totalPlayers { get; set; }
    [Networked] public int alivePlayers { get; set; }
    public static GameManager Instance;
    [SerializeField] private int maxPlayers;
    [SerializeField] private NetworkPrefabRef explosionPrefab;

    [Header("Feedback")]
    [SerializeField] private AudioClip fallSound;
    [SerializeField, Range(0f, 1f)] private float fallSoundVolume = 0.65f;
    [SerializeField] private AudioMixerGroup feedbackMixer;

    private List<int> aliveIds = new List<int>();
    private List<byte> aliveTeams = new List<byte>();
    private List<PlayerRef> alivePlayerRefs = new List<PlayerRef>();

    [Networked, OnChangedRender(nameof(OnWinnerChanged))]
    public int WinnerId { get; set; } = -1;

    [Networked] public NetworkBool TeamsMatch { get; private set; }

    [Networked, OnChangedRender(nameof(OnWinnerChanged))]
    public byte WinnerTeam { get; set; } = LobbyPlayer.NoTeam;

    [Networked, OnChangedRender(nameof(OnWinnerChanged))]
    public PlayerRef WinnerPlayer { get; set; }
    [SerializeField] private float returnToLobbySeconds = 8f;
    [Networked] public TickTimer ReturnTimer { get; private set; }

    [Header("Start countdown")]
    [SerializeField, Min(0.1f)] private float readySeconds = 1f;
    [SerializeField, Min(0.1f)] private float setSeconds = 1f;
    [SerializeField, Min(0.1f)] private float goSeconds = 0.75f;
    [SerializeField] private bool allowPlayDuringGo = true;
    [Networked] public TickTimer StartTimer { get; private set; }

    public override void Spawned()
    {
        aliveIds.Clear();
        alivePlayerRefs.Clear();
        aliveTeams.Clear();

        if (!HasStateAuthority)
            return;

        totalPlayers = 0;
        alivePlayers = 0;
        WinnerId = -1;
        WinnerPlayer = PlayerRef.None;
        WinnerTeam = LobbyPlayer.NoTeam;
        TeamsMatch = false;
        ReturnTimer = default;
        StartTimer = default;
    }

    public void SetTeamsMatch(bool value)
    {
        if (!HasStateAuthority)
            return;

        TeamsMatch = value;
    }

    public int IJoined(PlayerRef playerRef)
    {
        int id = totalPlayers;
        if (totalPlayers < maxPlayers)
        {
            totalPlayers++;
            alivePlayers++;
            aliveIds.Add(id);
            alivePlayerRefs.Add(playerRef);
            aliveTeams.Add(NetworkManager.Instance != null ? NetworkManager.Instance.GetMatchTeam(playerRef) : LobbyPlayer.NoTeam);
        }
        else
        {
            Debug.Log("No more players can Join");
        }
        UIManager.Instance.PlayerJoined(totalPlayers);
        return id;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void PlayerOut(int id, Ball player)
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMigrating) return;
        Vector3 deathPosition = player.transform.position;
        RPC_SpawnExplosion(deathPosition);
        int playerLives = player.GetLives();
        if (playerLives - 1 <= 0)
        {
            RPC_PlayerLost(id, player.ballName);

            int index = aliveIds.IndexOf(id);

            Runner.Despawn(player.gameObject.GetComponent<NetworkObject>());
            alivePlayers--;
            aliveIds.Remove(id);
            if (index >= 0)
            {
                alivePlayerRefs.RemoveAt(index);
                aliveTeams.RemoveAt(index);
            }
            CheckWinner();
        }
        else
        {
            player.Respawn();
        }
    }
    public void PlayerDisconnected(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;

        int index = alivePlayerRefs.IndexOf(playerRef);
        if (index < 0) return;

        int id = aliveIds[index];
        aliveIds.RemoveAt(index);
        alivePlayerRefs.RemoveAt(index);
        aliveTeams.RemoveAt(index);
        alivePlayers--;
        string name = NetworkManager.Instance.SendName(playerRef);
        if (string.IsNullOrWhiteSpace(name))
            name = $"Player {playerRef.PlayerId}";
        RPC_PlayerDisconnected(id, name);
        CheckWinner();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayerDisconnected(int id, string name)
    {
        UIManager.Instance.PlayerDisconnected(id, name);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayerLost(int id, string name)
    {
        UIManager.Instance.PlayerLost(id, name);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnExplosion(Vector3 position)
    {
        BallFeedbackAudio.PlayDetached(fallSound, BallFeedbackSound.Fall, position, fallSoundVolume, feedbackMixer);
        if (!Object.HasStateAuthority)
            return;

        Runner.Spawn(explosionPrefab, position, Quaternion.identity);
    }


    private void CheckWinner()
    {
        if (!HasStateAuthority) return;
        if (ReturnTimer.IsRunning) return;

        if (TeamsMatch)
        {
            int red = CountAliveInTeam(LobbyPlayer.TeamRed);
            int blue = CountAliveInTeam(LobbyPlayer.TeamBlue);

            if (red > 0 && blue > 0)
                return;

            WinnerId = -1;
            WinnerPlayer = PlayerRef.None;

            if (red > 0)
                WinnerTeam = LobbyPlayer.TeamRed;
            else if (blue > 0)
                WinnerTeam = LobbyPlayer.TeamBlue;
            else
                WinnerTeam = LobbyPlayer.NoTeam;

            ReturnTimer = TickTimer.CreateFromSeconds(Runner, returnToLobbySeconds);
            return;
        }

        if (alivePlayers > 1) return;

        if (aliveIds.Count == 1)
        {
            WinnerId = aliveIds[0];
            WinnerPlayer = alivePlayerRefs[0];
        }
        else
        {
            WinnerId = -1;
            WinnerPlayer = PlayerRef.None;
        }

        ReturnTimer = TickTimer.CreateFromSeconds(Runner, returnToLobbySeconds);
    }

    private int CountAliveInTeam(byte team)
    {
        int count = 0;

        foreach (byte aliveTeam in aliveTeams)
        {
            if (aliveTeam == team)
                count++;
        }

        return count;
    }

    public void ShowResults()
    {
        UIManager.Instance.GameIsOver(true);
    }

    private void OnWinnerChanged()
    {
        if (TeamsMatch)
        {
            if (WinnerTeam > LobbyPlayer.TeamBlue)
                return;
        }
        else if (WinnerPlayer == PlayerRef.None)
            return;

        UIManager.Instance.GameIsOver(DidLocalPlayerWin());
    }

    public bool DidLocalPlayerWin()
    {
        if (TeamsMatch)
            return WinnerTeam <= LobbyPlayer.TeamBlue && Ball.LocalTeam == WinnerTeam;

        return WinnerPlayer == Runner.LocalPlayer;
    }

    public override void FixedUpdateNetwork()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMigrating) return;
        if (!HasStateAuthority)
            return;

        if (!ReturnTimer.Expired(Runner))
            return;

        if (NetworkManager.Instance == null)
            return;

        NetworkManager.Instance.ReturnToLobby();
    }

    public bool IsGameOver
    {
        get
        {
            if (Object == null || !Object.IsValid)
                return false;

            return ReturnTimer.IsRunning;
        }
    }

    public float ReturnRemainingSeconds
    {
        get
        {
            if (!IsGameOver)
                return 0f;

            float? remaining = ReturnTimer.RemainingTime(Runner);

            if (remaining == null)
                return 0f;

            return remaining.Value;
        }
    }

    public MigrationGameState CaptureMigration()
    {
        return new MigrationGameState
        {
            TotalPlayers = totalPlayers,
            WinnerId = WinnerId,
            TeamsMatch = TeamsMatch,
            WinnerTeam = WinnerTeam,
            ReturnRunning = ReturnTimer.IsRunning,
            ReturnRemaining = ReturnTimer.RemainingTime(Runner) ?? 0f,
            StartRunning = !StartTimer.ExpiredOrNotRunning(Runner),
            StartRemaining = StartTimer.RemainingTime(Runner) ?? 0f

        };
    }

    public void RestoreMigration(MigrationGameState state, MigrationPlayerState[] players, Dictionary<string, PlayerRef> rejoined)
    {
        if (!HasStateAuthority || state == null) return;
        totalPlayers = state.TotalPlayers;
        TeamsMatch = state.TeamsMatch;
        WinnerTeam = state.WinnerTeam;
        aliveIds.Clear();
        alivePlayerRefs.Clear();
        aliveTeams.Clear();
        foreach (MigrationPlayerState player in players)
        {
            if (!player.HasBall || !rejoined.TryGetValue(player.Identity, out PlayerRef reference)) continue;
            aliveIds.Add(player.GameId);
            alivePlayerRefs.Add(reference);
            aliveTeams.Add(player.Team);
        }
        alivePlayers = aliveIds.Count;
        WinnerId = state.WinnerId;
        WinnerPlayer = PlayerRef.None;
        foreach (MigrationPlayerState player in players)
            if (player.GameId == WinnerId && rejoined.TryGetValue(player.Identity, out PlayerRef reference))
                WinnerPlayer = reference;
        ReturnTimer = state.ReturnRunning ? TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.01f, state.ReturnRemaining)) : TickTimer.None;
        StartTimer = state.StartRunning ? TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.01f, state.StartRemaining)) : TickTimer.None;
        CheckWinner();
        RefreshMigrationHud(players);
    }

    public void RefreshMigrationHud(MigrationPlayerState[] players)
    {
        if (UIManager.Instance == null || players == null) return;
        UIManager.Instance.PlayerJoined(totalPlayers);
        Ball referenceBall = FindFirstObjectByType<Ball>();
        PlayerColorPalette palette = referenceBall != null ? referenceBall.MigrationPalette : null;
        foreach (MigrationPlayerState player in players)
        {
            if (player.GameId < 0) continue;
            UIManager.Instance.SetPlayerInfo(player.GameId, player.Name, palette != null ? palette.GetSprite(player.SpriteIndex) : null, palette != null ? palette.GetPlayerColor(player.ColorIndex, player.Team) : Color.white); UIManager.Instance.SetPlayerStats(player.GameId, player.DisplayDamage, player.Lives);
            if (!player.Connected) UIManager.Instance.PlayerDisconnected(player.GameId, player.Name);
            else if (!player.HasBall) UIManager.Instance.PlayerLost(player.GameId, player.Name);
        }
        if (IsGameOver) UIManager.Instance.GameIsOver(DidLocalPlayerWin());
    }

    public void BeginStartCountdown()
    {
        if (!HasStateAuthority)
            return;

        StartTimer = TickTimer.CreateFromSeconds(Runner, readySeconds + setSeconds + goSeconds);
    }

    public StartPhase CurrentStartPhase
    {
        get
        {
            if (Object == null || !Object.IsValid)
                return StartPhase.None;

            float? remaining = StartTimer.RemainingTime(Runner);

            if (remaining == null)
                return StartPhase.None;

            float total = readySeconds + setSeconds + goSeconds;
            float elapsed = Mathf.Clamp(total - remaining.Value, 0f, total);

            if (elapsed < readySeconds)
                return StartPhase.Ready;

            if (elapsed < readySeconds + setSeconds)
                return StartPhase.Set;

            if (elapsed < total)
                return StartPhase.Go;

            return StartPhase.None;
        }
    }

    public bool CanPlay
    {
        get
        {
            StartPhase phase = CurrentStartPhase;

            if (phase == StartPhase.Ready || phase == StartPhase.Set)
                return false;

            if (phase == StartPhase.Go)
                return allowPlayDuringGo;

            return true;
        }
    }
}