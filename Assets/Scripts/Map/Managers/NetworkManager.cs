using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runner;

    [SerializeField] private NetworkPrefabRef lobbyPlayerPrefab;
    [SerializeField] private NetworkPrefabRef ballPrefab;

    [SerializeField] private int preGameSceneIndex = 1;
    [SerializeField] private int[] mapSceneIndices = { 2,3 };
    private bool sessionVisible = true;

    public const int MinPlayersToStart = 2;
    public int MapCount => mapSceneIndices.Length;
    //private int selectedMapSceneIndex;

    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [SerializeField] private int maxPlayers = 4;

    public static NetworkManager Instance { get; private set; }

    public NetworkRunner Runner => runner;

    public event Action<List<SessionInfo>> OnSessionListChanged;
    public event Action OnJoinFailed;
    public event Action OnJoinSucceeded;
    public event Action<bool> OnLobbySearching;
    public event Action<string, bool> OnStatusChanged;

    private bool isInSession;
    private bool leftOnPurpose;
    private bool goingToMenu;
    private string currentSessionName = "";

    private const string RoomCodeChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int RoomCodeLength = 5;

    private bool inLobby;
    private List<SessionInfo> lastSessionList = new List<SessionInfo>();

    private TaskCompletionSource<List<SessionInfo>> quickSessionTcs;

    private Dictionary<PlayerRef, byte> playerColors = new Dictionary<PlayerRef, byte>();
    private Dictionary<PlayerRef, byte> playerSprites = new Dictionary<PlayerRef, byte>();
    private Dictionary<PlayerRef, string> playerName = new Dictionary<PlayerRef, string>();
    private readonly Dictionary<PlayerRef, NetworkInputData> lastValidInput = new Dictionary<PlayerRef, NetworkInputData>();

    private bool inPreGame;
    private bool startingGame;
    private bool gamePlayersSpawned;

    private bool inputEnabled = true;
    [SerializeField, Min(0f)] private float missingInputGrace = 0.15f;
    private readonly Dictionary<PlayerRef, int> lastInputTicks = new Dictionary<PlayerRef, int>();
    private bool returningToLobby;

    public static NetworkManager Find()
    {
        return Instance != null ? Instance : FindAnyObjectByType<NetworkManager>();
    }

    public static bool TryGetRunner(out NetworkRunner runner)
    {
        if (Instance != null && Instance.runner != null && Instance.runner.IsRunning)
        {
            runner = Instance.runner;
            return true;
        }
        runner = null;
        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;

        if (runner == null)
            runner = GetComponent<NetworkRunner>();

        runner.AddCallbacks(this);
    }

    public async void StartGameHost(string sessionName, bool visible)
    {
        if (IsMigrating || migrationClosing || isInSession || !ValidateMigrationSetup()) return;
        ResetMigrationSession(true);
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            SetStatus("Escribí un nombre para la partida.", true);
            OnJoinFailed?.Invoke();
            return;
        }

        SetStatus("Creando la partida \"" + sessionName + "\"...");
        sessionVisible = visible;
        runner.ProvideInput = true;
        inPreGame = true;
        startingGame = false;
        gamePlayersSpawned = false;

        var result = await runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = sessionName,
                ConnectionToken = System.Text.Encoding.ASCII.GetBytes(migrationIdentity),
                PlayerCount = maxPlayers,

                IsOpen = true,
                IsVisible = visible,

                MatchmakingMode = Photon.Realtime.MatchmakingMode.FillRoom,
                Scene = SceneRef.FromIndex(preGameSceneIndex),
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
            });

        if (!result.Ok)
        {
            Debug.LogWarning("Couldn't create room: " + result.ShutdownReason);
            OnJoinFailed?.Invoke();
            GoToMenu(ReasonToText(result.ShutdownReason), true);
            return;
        }

        isInSession = true;
        currentSessionName = sessionName;
        if (enableHostMigration)
            foreach (PlayerRef player in runner.ActivePlayers)
                if (!migrationPlayerIdentities.ContainsKey(player)) MigrationPlayerJoined(player);

        SetStatus("Partida \"" + sessionName + "\" creada. Entrando a la sala...");

        OnJoinSucceeded?.Invoke();
    }

    public async void StartGameClient(string sessionName)
    {
        if (IsMigrating || migrationClosing || isInSession || !ValidateMigrationSetup()) return;
        ResetMigrationSession(false);
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            SetStatus("Escribí el nombre de la partida a la que querés entrar.", true);
            OnJoinFailed?.Invoke();
            return;
        }

        SetStatus("Uniéndote a \"" + sessionName + "\"...");

        runner.ProvideInput = true;
        inPreGame = true;
        startingGame = false;
        gamePlayersSpawned = false;

        var result = await runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionName,
                ConnectionToken = System.Text.Encoding.ASCII.GetBytes(migrationIdentity),
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
            });

        if (!result.Ok)
        {
            Debug.LogWarning($"No se pudo unir a '{sessionName}': " + result.ShutdownReason);
            OnJoinFailed?.Invoke();
            GoToMenu(ReasonToText(result.ShutdownReason), true);

            return;
        }

        isInSession = true;
        currentSessionName = sessionName;

        SetStatus("¡Conectado a \"" + sessionName + "\"! Entrando a la sala...");
        OnJoinSucceeded?.Invoke();
    }

    public async void JoinLobby()
    {
        if (inLobby)
        {
            OnSessionListChanged?.Invoke(lastSessionList);
            SetStatus(StatusForSessionList(lastSessionList));
            return;
        }

        SetStatus("Buscando partidas...");
        OnLobbySearching?.Invoke(true);
        var result = await runner.JoinSessionLobby(SessionLobby.ClientServer);
        OnLobbySearching?.Invoke(false);

        if (!result.Ok)
        {
            Debug.LogError("Couldn't join the lobby: " + result.ShutdownReason);
            GoToMenu(ReasonToText(result.ShutdownReason), true);
            return;
        }

        inLobby = true;
    }

    public async void QuickPlay()
    {
        if (IsMigrating || migrationClosing || isInSession || !ValidateMigrationSetup()) return;
        runner.ProvideInput = true;
        inPreGame = true;
        startingGame = false;
        gamePlayersSpawned = false;

        List<SessionInfo> sessions;

        if (inLobby)
            sessions = lastSessionList;
        else
        {
            SetStatus("Searching free lobby...");
            OnLobbySearching?.Invoke(true);

            quickSessionTcs = new TaskCompletionSource<List<SessionInfo>>();
            var lobbyResult = await runner.JoinSessionLobby(SessionLobby.ClientServer);

            if (!lobbyResult.Ok)
            {
                Debug.LogError("QuickPlay: couldn't join the lobby - " + lobbyResult.ShutdownReason);
                quickSessionTcs = null;
                OnJoinFailed?.Invoke();
                GoToMenu(ReasonToText(lobbyResult.ShutdownReason), true);

                return;
            }

            inLobby = true;
            var listTask = quickSessionTcs.Task;
            var timeoutTask = Task.Delay(5000);
            var finishedTask = await Task.WhenAny(listTask, timeoutTask);
            sessions = finishedTask == listTask ? listTask.Result : new List<SessionInfo>();
            quickSessionTcs = null;
            OnLobbySearching?.Invoke(false);
        }

        SessionInfo best = null;
        foreach (SessionInfo session in sessions)
        {
            if (!session.IsOpen)
                continue;

            if (!session.IsVisible)
                continue;

            if (session.PlayerCount >= session.MaxPlayers)
                continue;

            if (best == null || session.PlayerCount > best.PlayerCount)
                best = session;
        }

        if (best != null)
            StartGameClient(best.Name);
        else
            StartGameHost(GenerateRoomCode(), true);
    }

    private string GenerateRoomCode()
    {
        char[] chars = new char[RoomCodeLength];

        for (int i = 0; i < RoomCodeLength; i++)
        {
            chars[i] = RoomCodeChars[UnityEngine.Random.Range(0, RoomCodeChars.Length)];
        }

        return new string(chars);
    }

    public async void Disconnect()
    {
        if (runner == null) return;

        if (migrationClosing || leftOnPurpose) return;
        leftOnPurpose = true;
        if (enableHostMigration && runner.IsServer && !IsMigrating)
            await DepartMigrationAsync();
        else
            await CloseMigrationSessionAsync("Saliste de la partida.", false);
    }

    public void Disconect()
    {
        Disconnect();
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner != this.runner || MigrationPlayerJoined(player)) return;
        if (runner.IsServer && inPreGame && !startingGame)
            SpawnLobbyPlayerIfNeeded(runner, player);

        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    public void OnPlayerLeft(NetworkRunner runner,PlayerRef player)
    {
        if (runner != this.runner || MigrationPlayerLeft(player)) return;
        if (runner.IsServer)
        {
            if (!inPreGame && GameManager.Instance != null)
                GameManager.Instance.PlayerDisconnected(player);

            if (runner.TryGetPlayerObject(player, out NetworkObject playerObject))
            {
                if (playerObject != null)
                    runner.Despawn(playerObject);
            }

            playerColors.Remove(player);
            playerSprites.Remove(player);
            playerName.Remove(player);
            lastValidInput.Remove(player);
            lastInputTicks.Remove(player);

            if (inPreGame && !startingGame)
                CheckAllPlayersReady();
        }

        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    private void SpawnLobbyPlayerIfNeeded(NetworkRunner runner, PlayerRef player)
    {
        if (runner.TryGetPlayerObject(player, out NetworkObject existingObject))
        {
            if (existingObject != null && existingObject.GetComponent<LobbyPlayer>() != null)
                return;
        }

        NetworkObject lobbyPlayer = runner.Spawn(lobbyPlayerPrefab, Vector3.zero, Quaternion.identity, player);
        runner.SetPlayerObject(player, lobbyPlayer);
    }

    private void EnsureLobbyPlayersExist()
    {
        if (!runner.IsServer)
            return;

        foreach (PlayerRef player in runner.ActivePlayers)
            SpawnLobbyPlayerIfNeeded(runner, player);
    }

    public void CheckAllPlayersReady()
    {
        if (IsMigrating || migrationClosing) return;
        if (!runner.IsServer)
            return;

        if (!inPreGame)
            return;

        if (startingGame)
            return;

        // NUEVO: si la votación ya está en marcha, no hacer nada
        if (MapVoteController.Instance != null && MapVoteController.Instance.IsActive)
            return;

        int playerCount = 0;

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            playerCount++;

            if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject))
                return;

            LobbyPlayer lobbyPlayer = playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer == null)
                return;

            if (!lobbyPlayer.IsReady)
                return;
        }

        if (playerCount < MinPlayersToStart)
            return;

        BeginMapVote();
    }

    private void BeginMapVote()
    {
        if (MapVoteController.Instance == null)
        {
            Debug.LogWarning("No hay MapVoteController en la escena Lobby: se carga el primer mapa.");
            StartMatch(0);
            return;
        }

        SetSessionOpen(false);
        MapVoteController.Instance.BeginVoting();
    }

    public void CancelMapVote()
    {
        SetSessionOpen(true);
    }

    private void SetSessionOpen(bool open)
    {
        if (!runner.SessionInfo.IsValid)
            return;

        runner.SessionInfo.IsOpen = open;
        runner.SessionInfo.IsVisible = open && sessionVisible;
    }

    public async void StartMatch(int mapIndex)
    {
        if (IsMigrating || migrationClosing) return;
        if (!runner.IsServer)
            return;

        if (startingGame)
            return;

        if (mapIndex < 0 || mapIndex >= mapSceneIndices.Length)
            mapIndex = 0;

        startingGame = true;
        inPreGame = false;

        SetSessionOpen(false);

        SaveLobbyPlayerData();
        ResetMigrationRoundPlayers();
        DespawnLobbyPlayers();

        gamePlayersSpawned = false;

        await runner.LoadScene(SceneRef.FromIndex(mapSceneIndices[mapIndex]));
    }

    //private async void StartMatch()
    //{
    //    if (!runner.IsServer)
    //        return;

    //    if (startingGame)
    //        return;

    //    startingGame = true;
    //    inPreGame = false;

    //    if (runner.SessionInfo.IsValid)
    //    {
    //        runner.SessionInfo.IsOpen = false;
    //        runner.SessionInfo.IsVisible = false;
    //    }

    //    selectedMapSceneIndex = ChooseMapSceneIndex();

    //    SaveLobbyPlayerData();
    //    DespawnLobbyPlayers();

    //    gamePlayersSpawned = false;

    //    await runner.LoadScene(SceneRef.FromIndex(selectedMapSceneIndex));
    //}

    private void SaveLobbyPlayerData()
    {
        playerColors.Clear();
        playerSprites.Clear();
        playerName.Clear();

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject))
                continue;

            LobbyPlayer lobbyPlayer = playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer == null)
                continue;

            playerColors[player] = lobbyPlayer.ColorIndex;
            playerSprites[player] = lobbyPlayer.SpriteIndex;
            playerName[player] = lobbyPlayer.Nickname.ToString();
        }
    }

    public async void ReturnToLobby()
    {
        if (IsMigrating || migrationClosing) return;
        if (!runner.IsServer)
            return;

        if (returningToLobby || inPreGame)
            return;

        returningToLobby = true;

        DespawnGameBalls();
        lastValidInput.Clear();
        lastInputTicks.Clear();

        await runner.LoadScene(SceneRef.FromIndex(preGameSceneIndex));
    }

    private void DespawnGameBalls()
    {
        List<NetworkObject> objectsToDespawn = new List<NetworkObject>();

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject))
                continue;

            if (playerObject == null || !playerObject.IsValid)
                continue;

            if (playerObject.GetComponent<Ball>() == null)
                continue;

            objectsToDespawn.Add(playerObject);
        }

        foreach (NetworkObject obj in objectsToDespawn)
            runner.Despawn(obj);
    }

    private void DespawnLobbyPlayers()
    {
        List<NetworkObject> objectsToDespawn = new List<NetworkObject>();

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject))
                continue;

            if (playerObject == null)
                continue;

            if (playerObject.GetComponent<LobbyPlayer>() == null)
                continue;

            objectsToDespawn.Add(playerObject);
        }

        foreach (NetworkObject obj in objectsToDespawn)
            runner.Despawn(obj);
    }

    private void SpawnGamePlayers()
    {
        if (IsMigrating || migrationClosing) return;
        if (!runner.IsServer)
            return;

        if (gamePlayersSpawned)
            return;

        PlayerSpawnPoints spawnPointManager = FindFirstObjectByType<PlayerSpawnPoints>();

        if (spawnPointManager == null)
        {
            Debug.LogError("No se encontró ningún PlayerSpawnPoints en la escena Game.");
            return;
        }

        List<PlayerRef> players = new List<PlayerRef>();

        foreach (PlayerRef player in runner.ActivePlayers)
            players.Add(player);

        players.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));

        int playerCount = players.Count;

        Transform[] spawnPoints = spawnPointManager.GetSpawnPoints(playerCount);

        if (spawnPoints == null)
        {
            Debug.LogError("No hay una distribución de spawn configurada para " + playerCount + " jugadores.");
            return;
        }

        if (spawnPoints.Length < playerCount)
        {
            Debug.LogError("La distribución para " + playerCount + " jugadores tiene solamente " + spawnPoints.Length + " puntos de spawn.");
            return;
        }

        for (int i = 0; i < playerCount; i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogError("El Spawn " + i + " para " + playerCount + " jugadores no está asignado.");
                return;
            }
        }

        gamePlayersSpawned = true;

        for (int i = 0; i < playerCount; i++)
        {
            PlayerRef player = players[i];
            Transform spawnPoint = spawnPoints[i];
            NetworkObject ballObject = runner.Spawn(ballPrefab, spawnPoint.position, Quaternion.identity, player);
            Ball ball = ballObject.GetComponent<Ball>();

            byte colorIndex = 0;
            byte spriteIndex = 0;
            string name = "";

            if (playerColors.TryGetValue(player, out byte savedColor))
                colorIndex = savedColor;

            if (playerSprites.TryGetValue(player, out byte savedSprite))
                spriteIndex = savedSprite;

            if (playerName.TryGetValue(player, out string savedName))
                name = savedName;

            if (ball != null)
            {
                ball.SetInitialColor(colorIndex);
                ball.SetInitialSprite(spriteIndex);
                ball.SetName(name);
            }

            runner.SetPlayerObject(player, ballObject);

            Debug.Log("Player " + player.PlayerId + " spawneado en " + spawnPoint.name + " - Posición: " + spawnPoint.position);
        }

        if (GameManager.Instance != null)
            GameManager.Instance.BeginStartCountdown();
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData data = new NetworkInputData();

        if (inputEnabled && !IsMigrating && !migrationClosing && !MigrationInputBlocked())
        {
            data.Buttons.Set((int)InputButton.Fire,Input.GetMouseButton(0));

            if (Camera.main != null)
            {
                Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                data.AimWorldPosition = mouseWorld;

                if (runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject localObject))
                {
                    if (localObject != null)
                        data.AimOffset = mouseWorld - (Vector2)localObject.transform.position;
                }
            }
        }

        input.Set(data);
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner != this.runner) return;
        if (IsMigrating)
        {
            migrationSceneReady = true;
            return;
        }
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;

        if (sceneIndex == preGameSceneIndex)
        {
            inPreGame = true;
            startingGame = false;
            gamePlayersSpawned = false;
            returningToLobby = false;

            if (runner.IsServer)
            {
                SetSessionOpen(true);
                EnsureLobbyPlayersExist();
            }

            PreGameLobbyUI.Instance?.RefreshLobby();
        }

        if (IsMapScene(sceneIndex))
        {
            inPreGame = false;
            int playerCount = 0;

            foreach (PlayerRef player in runner.ActivePlayers)
            {
                playerCount++;
            }


            UIManager.Instance?.PlayerJoined(playerCount);

            if (runner.IsServer)
                SpawnGamePlayers();
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (runner != this.runner || IsMigrating || migrationClosing) return;
        if (QueueMigrationAfterHostLoss()) return;
        if (leftOnPurpose)
        {
            GoToMenu("Saliste de la partida.", false);
        }
        else if (isInSession)
        {
            ConnectionMessage.RejoinSession = currentSessionName;
            GoToMenu("Se cortó la conexión con la partida (el host se fue o falló internet).", true);
        }
        else
        {
            GoToMenu(ReasonToText(shutdownReason),true);
        }
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        if (runner != this.runner || IsMigrating || migrationClosing || QueueMigrationAfterHostLoss()) return;
        Debug.LogWarning("Disconnected from server: " + reason);
        OnJoinFailed?.Invoke();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        if (runner != this.runner || IsMigrating || migrationClosing) return;
        Debug.LogWarning("Failed connection: " + reason);
        OnJoinFailed?.Invoke();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        lastSessionList = new List<SessionInfo>(sessionList);
        OnSessionListChanged?.Invoke(lastSessionList);
        quickSessionTcs?.TrySetResult(sessionList);

        if (!isInSession)
        {
            int visibles = 0;

            foreach (SessionInfo session in sessionList)
            {
                if (session.IsVisible)
                    visibles++;
            }

            if (visibles == 0)
                SetStatus("No hay partidas abiertas. ¡Creá una!");

            else
                SetStatus("Partidas encontradas: " + visibles);
        }
    }

    private string StatusForSessionList(List<SessionInfo> sessionList)
    {
        int visibles = 0;

        foreach (SessionInfo session in sessionList)
        {
            if (session.IsVisible)
                visibles++;
        }

        return visibles == 0 ? "No avalible lobbies. Create one!" : "Lobbies found: " + visibles;
    }

    public void RegisterLastInput(PlayerRef player, NetworkInputData data)
    {
        lastValidInput[player] = data;
        lastInputTicks[player] = runner.Tick.Raw;
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player,NetworkInput input)
    {

        NetworkInputData fallback = default;

        if (lastValidInput.TryGetValue(player, out NetworkInputData cached))
        {
            fallback = cached;
            if (!lastInputTicks.TryGetValue(player, out int tick) || runner.Tick.Raw - tick > Mathf.CeilToInt(missingInputGrace / runner.DeltaTime))
            {
                fallback.Buttons.Set((int)InputButton.Fire, false);
                fallback.Buttons.Set(1, true);
            }
        }

        fallback.Buttons.Set(2, true);
        input.Set(fallback);
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Nos conectamos al servidor");
        SetStatus("Conectado al host.");
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
    {
        ReceiveMigrationData(runner, player, key, data);
    }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        if (runner == this.runner) QueueMigrationAfterHostLoss();
    }
    public void OnSceneLoadStart(NetworkRunner runner) { }

    private void SetStatus(string message, bool isError = false)
    {
        Debug.Log("[Estado de conexión] " + message);
        OnStatusChanged?.Invoke(message, isError);
    }

    private void GoToMenu(string message, bool isError)
    {
        if (goingToMenu) return;

        goingToMenu = true;

        ConnectionMessage.Set(message,isError);

        if (Instance == this)
            Instance = null;

        if (this != null)
            Destroy(gameObject);

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private string ReasonToText(
        ShutdownReason reason)
    {
        switch (reason)
        {
            case ShutdownReason.GameNotFound:
                return "No existe una partida con ese nombre.";

            case ShutdownReason.GameIsFull:
                return "La partida está llena.";

            case ShutdownReason.GameClosed:
                return "La partida está cerrada (ya empezó).";

            case ShutdownReason.GameIdAlreadyExists:
            case ShutdownReason.ServerInRoom:
                return "Ya existe una partida con ese nombre. Probá con otro.";

            case ShutdownReason.MaxCcuReached:
                return "Photon llegó al límite de jugadores conectados. Probá más tarde.";

            case ShutdownReason.PhotonCloudTimeout:
            case ShutdownReason.ConnectionTimeout:
            case ShutdownReason.ConnectionRefused:
            case ShutdownReason.OperationTimeout:
                return "No se pudo conectar. Revisá tu conexión a internet.";

            case ShutdownReason.InvalidRegion:
            case ShutdownReason.InvalidAuthentication:
                return "Error de configuración de Photon (AppId o región).";

            default:
                return "Error de conexión: " + reason;
        }
    }

    public string SendName(PlayerRef player)
    {
        if (playerName.TryGetValue(player, out string savedName))
            return savedName;

        return "";
    }

    //private int ChooseMapSceneIndex()
    //{
    //    int mapCount = mapSceneIndices.Length;
    //    int[] votes = new int[mapCount];


    //    foreach (PlayerRef player in runner.ActivePlayers)
    //    {
    //        if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject))
    //            continue;

    //        LobbyPlayer lobbyPlayer = playerObject.GetComponent<LobbyPlayer>();

    //        if (lobbyPlayer == null)
    //            continue;

    //        byte vote = lobbyPlayer.MapVote;

    //        if (vote < mapCount)
    //            votes[vote]++;
    //    }


    //    int maxVotes = 0;

    //    for (int i = 0; i < mapCount; i++)
    //    {
    //        if (votes[i] > maxVotes)
    //            maxVotes = votes[i];
    //    }


    //    List<int> winners = new List<int>();

    //    for (int i = 0; i < mapCount; i++)
    //    {
    //        if (maxVotes == 0 || votes[i] == maxVotes)
    //            winners.Add(i);
    //    }


    //    int winner = winners[UnityEngine.Random.Range(0, winners.Count)];

    //    int chosenScene = mapSceneIndices[winner];
    //    Debug.Log("Votos: " + string.Join(", ", votes) + " -> escena elegida: " + chosenScene);
    //    return chosenScene;
    //}

    private bool IsMapScene(int sceneIndex)
    {
        foreach (int mapIndex in mapSceneIndices)
        {
            if (mapIndex == sceneIndex)
                return true;
        }

        return false;
    }


}