using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runner;

    [SerializeField] private NetworkPrefabRef lobbyPlayerPrefab;
    [SerializeField] private NetworkPrefabRef ballPrefab;

    [SerializeField] private int preGameSceneIndex = 1;
    [SerializeField] private int gameSceneIndex = 2;

    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [SerializeField] private int maxPlayers = 4;

    public static NetworkManager Instance { get; private set; }

    public NetworkRunner Runner => runner;

    public event Action<List<SessionInfo>> OnSessionListChanged;
    public event Action OnJoinFailed;
    public event Action OnJoinSucceeded;

    public event Action<string, bool> OnStatusChanged;

    private bool isInSession;
    private bool leftOnPurpose;
    private bool goingToMenu;
    private string currentSessionName = "";

    private const string RoomCodeChars =
        "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private const int RoomCodeLength = 5;

    private TaskCompletionSource<List<SessionInfo>> quickSessionTcs;

    private Dictionary<PlayerRef, byte> playerColors =
        new Dictionary<PlayerRef, byte>();
    private readonly Dictionary<PlayerRef, NetworkInputData> lastValidInput = new Dictionary<PlayerRef, NetworkInputData>();

    private bool inPreGame;
    private bool startingGame;
    private bool gamePlayersSpawned;

    private bool inputEnabled = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        if (runner == null)
        {
            runner = GetComponent<NetworkRunner>();
        }

        runner.AddCallbacks(this);
    }

    public async void StartGameHost(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            SetStatus(
                "Escribí un nombre para la partida.",
                true
            );

            OnJoinFailed?.Invoke();

            return;
        }

        SetStatus(
            "Creando la partida \"" +
            sessionName +
            "\"..."
        );

        runner.ProvideInput = true;

        inPreGame = true;
        startingGame = false;
        gamePlayersSpawned = false;

        var result = await runner.StartGame(
            new StartGameArgs()
            {
                GameMode = GameMode.Host,

                SessionName = sessionName,

                PlayerCount = maxPlayers,

                IsOpen = true,
                IsVisible = true,

                MatchmakingMode =
                    Photon.Realtime.MatchmakingMode.FillRoom,

                Scene =
                    SceneRef.FromIndex(preGameSceneIndex),

                SceneManager =
                    GetComponent<NetworkSceneManagerDefault>()
            }
        );

        if (!result.Ok)
        {
            Debug.LogWarning(
                "Couldn't create room: " +
                result.ShutdownReason
            );

            OnJoinFailed?.Invoke();

            GoToMenu(
                ReasonToText(result.ShutdownReason),
                true
            );

            return;
        }

        isInSession = true;

        currentSessionName =
            sessionName;

        SetStatus(
            "Partida \"" +
            sessionName +
            "\" creada. Entrando a la sala..."
        );

        OnJoinSucceeded?.Invoke();
    }

    public async void StartGameClient(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            SetStatus(
                "Escribí el nombre de la partida a la que querés entrar.",
                true
            );

            OnJoinFailed?.Invoke();

            return;
        }

        SetStatus(
            "Uniéndote a \"" +
            sessionName +
            "\"..."
        );

        runner.ProvideInput = true;

        inPreGame = true;
        startingGame = false;
        gamePlayersSpawned = false;

        var result = await runner.StartGame(
            new StartGameArgs()
            {
                GameMode = GameMode.Client,

                SessionName = sessionName,

                SceneManager =
                    GetComponent<NetworkSceneManagerDefault>()
            }
        );

        if (!result.Ok)
        {
            Debug.LogWarning(
                $"No se pudo unir a '{sessionName}': " +
                result.ShutdownReason
            );

            OnJoinFailed?.Invoke();

            GoToMenu(
                ReasonToText(result.ShutdownReason),
                true
            );

            return;
        }

        isInSession = true;

        currentSessionName =
            sessionName;

        SetStatus(
            "¡Conectado a \"" +
            sessionName +
            "\"! Entrando a la sala..."
        );

        OnJoinSucceeded?.Invoke();
    }

    public async void JoinLobby()
    {
        SetStatus(
            "Buscando partidas..."
        );

        var result =
            await runner.JoinSessionLobby(
                SessionLobby.ClientServer
            );

        if (!result.Ok)
        {
            Debug.LogError(
                "Couldn't join the lobby: " +
                result.ShutdownReason
            );

            GoToMenu(
                ReasonToText(result.ShutdownReason),
                true
            );
        }
    }

    public async void QuickPlay()
    {
        runner.ProvideInput = true;

        inPreGame = true;
        startingGame = false;
        gamePlayersSpawned = false;

        SetStatus(
            "Buscando una partida libre..."
        );

        quickSessionTcs =
            new TaskCompletionSource<List<SessionInfo>>();

        var lobbyResult =
            await runner.JoinSessionLobby(
                SessionLobby.ClientServer
            );

        if (!lobbyResult.Ok)
        {
            Debug.LogError(
                "QuickPlay: couldn't join the lobby - " +
                lobbyResult.ShutdownReason
            );

            quickSessionTcs = null;

            OnJoinFailed?.Invoke();

            GoToMenu(
                ReasonToText(lobbyResult.ShutdownReason),
                true
            );

            return;
        }

        var listTask =
            quickSessionTcs.Task;

        var timeoutTask =
            Task.Delay(5000);

        var finishedTask =
            await Task.WhenAny(
                listTask,
                timeoutTask
            );

        List<SessionInfo> sessions =
            finishedTask == listTask
                ? listTask.Result
                : new List<SessionInfo>();

        quickSessionTcs = null;

        SessionInfo best = null;

        foreach (SessionInfo session in sessions)
        {
            if (!session.IsOpen)
                continue;

            if (!session.IsVisible)
                continue;

            if (session.PlayerCount >= session.MaxPlayers)
                continue;

            if (best == null ||
                session.PlayerCount > best.PlayerCount)
            {
                best = session;
            }
        }

        if (best != null)
        {
            StartGameClient(
                best.Name
            );
        }
        else
        {
            StartGameHost(
                GenerateRoomCode()
            );
        }
    }

    private string GenerateRoomCode()
    {
        char[] chars =
            new char[RoomCodeLength];

        for (
            int i = 0;
            i < RoomCodeLength;
            i++)
        {
            chars[i] =
                RoomCodeChars[
                    UnityEngine.Random.Range(
                        0,
                        RoomCodeChars.Length
                    )
                ];
        }

        return new string(chars);
    }

    public async void Disconnect()
    {
        if (runner == null)
            return;

        leftOnPurpose = true;

        await runner.Shutdown();
    }

    public void Disconect()
    {
        Disconnect();
    }

    public void OnPlayerJoined(
        NetworkRunner runner,
        PlayerRef player)
    {
        if (runner.IsServer &&
            inPreGame &&
            !startingGame)
        {
            SpawnLobbyPlayerIfNeeded(
                runner,
                player
            );
        }

        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    public void OnPlayerLeft(
        NetworkRunner runner,
        PlayerRef player)
    {
        if (runner.IsServer)
        {
            if (!inPreGame &&
                GameManager.Instance != null)
            {
                GameManager.Instance.PlayerDisconnected(
                    player
                );
            }

            if (runner.TryGetPlayerObject(
                    player,
                    out NetworkObject playerObject))
            {
                if (playerObject != null)
                {
                    runner.Despawn(
                        playerObject
                    );
                }
            }

            playerColors.Remove(player);
            lastValidInput.Remove(player);

            if (inPreGame &&
                !startingGame)
            {
                CheckAllPlayersReady();
            }
        }

        PreGameLobbyUI.Instance?.RefreshLobby();
    }

    private void SpawnLobbyPlayerIfNeeded(
        NetworkRunner runner,
        PlayerRef player)
    {
        if (runner.TryGetPlayerObject(
                player,
                out NetworkObject existingObject))
        {
            if (existingObject != null &&
                existingObject.GetComponent<LobbyPlayer>() != null)
            {
                return;
            }
        }

        NetworkObject lobbyPlayer =
            runner.Spawn(
                lobbyPlayerPrefab,
                Vector3.zero,
                Quaternion.identity,
                player
            );

        runner.SetPlayerObject(
            player,
            lobbyPlayer
        );
    }

    private void EnsureLobbyPlayersExist()
    {
        if (!runner.IsServer)
            return;

        foreach (
            PlayerRef player
            in runner.ActivePlayers)
        {
            SpawnLobbyPlayerIfNeeded(
                runner,
                player
            );
        }
    }

    public void CheckAllPlayersReady()
    {
        if (!runner.IsServer)
            return;

        if (!inPreGame)
            return;

        if (startingGame)
            return;

        int playerCount = 0;

        foreach (
            PlayerRef player
            in runner.ActivePlayers)
        {
            playerCount++;

            if (!runner.TryGetPlayerObject(
                    player,
                    out NetworkObject playerObject))
            {
                return;
            }

            LobbyPlayer lobbyPlayer =
                playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer == null)
                return;

            if (!lobbyPlayer.IsReady)
                return;
        }

        if (playerCount < 2)
            return;

        StartMatch();
    }

    private async void StartMatch()
    {
        if (!runner.IsServer)
            return;

        if (startingGame)
            return;

        startingGame = true;
        inPreGame = false;

        if (runner.SessionInfo.IsValid)
        {
            runner.SessionInfo.IsOpen =
                false;

            runner.SessionInfo.IsVisible =
                false;
        }

        SaveLobbyPlayerData();

        DespawnLobbyPlayers();

        gamePlayersSpawned =
            false;

        await runner.LoadScene(
            SceneRef.FromIndex(
                gameSceneIndex
            )
        );
    }

    private void SaveLobbyPlayerData()
    {
        playerColors.Clear();

        foreach (
            PlayerRef player
            in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(
                    player,
                    out NetworkObject playerObject))
            {
                continue;
            }

            LobbyPlayer lobbyPlayer =
                playerObject.GetComponent<LobbyPlayer>();

            if (lobbyPlayer == null)
                continue;

            playerColors[player] =
                lobbyPlayer.ColorIndex;
        }
    }

    private void DespawnLobbyPlayers()
    {
        List<NetworkObject> objectsToDespawn =
            new List<NetworkObject>();

        foreach (
            PlayerRef player
            in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(
                    player,
                    out NetworkObject playerObject))
            {
                continue;
            }

            if (playerObject == null)
                continue;

            if (playerObject.GetComponent<LobbyPlayer>() == null)
                continue;

            objectsToDespawn.Add(
                playerObject
            );
        }

        foreach (
            NetworkObject obj
            in objectsToDespawn)
        {
            runner.Despawn(
                obj
            );
        }
    }

    private void SpawnGamePlayers()
    {
        if (!runner.IsServer)
            return;

        if (gamePlayersSpawned)
            return;

        PlayerSpawnPoints spawnPointManager =
            FindFirstObjectByType<PlayerSpawnPoints>();

        if (spawnPointManager == null)
        {
            Debug.LogError(
                "No se encontró ningún PlayerSpawnPoints en la escena Game."
            );

            return;
        }

        List<PlayerRef> players =
            new List<PlayerRef>();

        foreach (
            PlayerRef player
            in runner.ActivePlayers)
        {
            players.Add(
                player
            );
        }

        players.Sort(
            (a, b) =>
                a.PlayerId.CompareTo(
                    b.PlayerId
                )
        );

        int playerCount =
            players.Count;

        Transform[] spawnPoints =
            spawnPointManager.GetSpawnPoints(
                playerCount
            );

        if (spawnPoints == null)
        {
            Debug.LogError(
                "No hay una distribución de spawn configurada para " +
                playerCount +
                " jugadores."
            );

            return;
        }

        if (spawnPoints.Length < playerCount)
        {
            Debug.LogError(
                "La distribución para " +
                playerCount +
                " jugadores tiene solamente " +
                spawnPoints.Length +
                " puntos de spawn."
            );

            return;
        }

        for (
            int i = 0;
            i < playerCount;
            i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogError(
                    "El Spawn " +
                    i +
                    " para " +
                    playerCount +
                    " jugadores no está asignado."
                );

                return;
            }
        }

        gamePlayersSpawned =
            true;

        for (
            int i = 0;
            i < playerCount;
            i++)
        {
            PlayerRef player =
                players[i];

            Transform spawnPoint =
                spawnPoints[i];

            NetworkObject ballObject =
                runner.Spawn(
                    ballPrefab,
                    spawnPoint.position,
                    Quaternion.identity,
                    player
                );

            Ball ball =
                ballObject.GetComponent<Ball>();

            byte colorIndex = 0;

            if (playerColors.TryGetValue(
                    player,
                    out byte savedColor))
            {
                colorIndex =
                    savedColor;
            }

            if (ball != null)
            {
                ball.SetInitialColor(
                    colorIndex
                );
            }

            runner.SetPlayerObject(
                player,
                ballObject
            );

            Debug.Log(
                "Player " +
                player.PlayerId +
                " spawneado en " +
                spawnPoint.name +
                " - Posición: " +
                spawnPoint.position
            );
        }
    }

    public void SetInputEnabled(
        bool enabled)
    {
        inputEnabled =
            enabled;
    }

    public void OnInput(
        NetworkRunner runner,
        NetworkInput input)
    {
        NetworkInputData data =
            new NetworkInputData();

        if (inputEnabled)
        {
            data.Buttons.Set(
                (int)InputButton.Fire,
                Input.GetMouseButton(0)
            );

            if (Camera.main != null)
            {
                data.AimWorldPosition =
                    Camera.main.ScreenToWorldPoint(
                        Input.mousePosition
                    );
            }
        }

        input.Set(
            data
        );
    }

    public void OnSceneLoadDone(
        NetworkRunner runner)
    {
        int sceneIndex =
            SceneManager.GetActiveScene().buildIndex;

        if (sceneIndex ==
            preGameSceneIndex)
        {
            inPreGame = true;

            startingGame = false;

            gamePlayersSpawned =
                false;

            if (runner.IsServer)
            {
                EnsureLobbyPlayersExist();
            }

            PreGameLobbyUI.Instance?.RefreshLobby();
        }

        if (sceneIndex == gameSceneIndex)
        {
            inPreGame = false;

            int playerCount = 0;

            foreach (PlayerRef player in runner.ActivePlayers)
            {
                playerCount++;
            }

            UIManager.Instance?.PlayerJoined(playerCount);

            if (runner.IsServer)
            {
                SpawnGamePlayers();
            }
        }
    }

    public void OnShutdown(
        NetworkRunner runner,
        ShutdownReason shutdownReason)
    {
        if (leftOnPurpose)
        {
            GoToMenu(
                "Saliste de la partida.",
                false
            );
        }
        else if (isInSession)
        {
            ConnectionMessage.RejoinSession =
                currentSessionName;

            GoToMenu(
                "Se cortó la conexión con la partida (el host se fue o falló internet).",
                true
            );
        }
        else
        {
            GoToMenu(
                ReasonToText(
                    shutdownReason
                ),
                true
            );
        }
    }

    public void OnDisconnectedFromServer(
        NetworkRunner runner,
        NetDisconnectReason reason)
    {
        Debug.LogWarning(
            "Disconnected from server: " +
            reason
        );

        OnJoinFailed?.Invoke();
    }

    public void OnConnectFailed(
        NetworkRunner runner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason)
    {
        Debug.LogWarning(
            "Failed connection: " +
            reason
        );

        OnJoinFailed?.Invoke();
    }

    public void OnSessionListUpdated(
        NetworkRunner runner,
        List<SessionInfo> sessionList)
    {
        OnSessionListChanged?.Invoke(
            sessionList
        );

        quickSessionTcs?.TrySetResult(
            sessionList
        );

        if (!isInSession)
        {
            int visibles = 0;

            foreach (
                SessionInfo session
                in sessionList)
            {
                if (session.IsVisible)
                {
                    visibles++;
                }
            }

            if (visibles == 0)
            {
                SetStatus(
                    "No hay partidas abiertas. ¡Creá una!"
                );
            }
            else
            {
                SetStatus(
                    "Partidas encontradas: " +
                    visibles
                );
            }
        }
    }

    public void RegisterLastInput(PlayerRef player, NetworkInputData data)
    {
        lastValidInput[player] = data;
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player,NetworkInput input)
    {
        Debug.Log("Input missing, runner: " + runner + " player: " + player + ". Input is: " + input);
        NetworkInputData fallback = default;
        if (lastValidInput.TryGetValue(player, out NetworkInputData cached))
        {
            fallback = cached;
        }
        input.Set(fallback);
    }

    public void OnConnectedToServer(
        NetworkRunner runner)
    {
        Debug.Log(
            "Nos conectamos al servidor"
        );

        SetStatus(
            "Conectado al host."
        );
    }

    public void OnObjectExitAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    public void OnObjectEnterAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    public void OnConnectRequest(
        NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
    }

    public void OnReliableDataReceived(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        ReadOnlySpan<byte> data)
    {
    }

    public void OnReliableDataProgress(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        float progress)
    {
    }

    public void OnCustomAuthenticationResponse(
        NetworkRunner runner,
        Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(
        NetworkRunner runner,
        HostMigrationToken hostMigrationToken)
    {
    }

    public void OnSceneLoadStart(
        NetworkRunner runner)
    {
    }

    private void SetStatus(
        string message,
        bool isError = false)
    {
        Debug.Log(
            "[Estado de conexión] " +
            message
        );

        OnStatusChanged?.Invoke(
            message,
            isError
        );
    }

    private void GoToMenu(
        string message,
        bool isError)
    {
        if (goingToMenu)
            return;

        goingToMenu =
            true;

        ConnectionMessage.Set(
            message,
            isError
        );

        if (Instance == this)
        {
            Instance =
                null;
        }

        if (this != null)
        {
            Destroy(
                gameObject
            );
        }

        SceneManager.LoadScene(
            mainMenuSceneName
        );
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
                return "Error de conexión: " +
                       reason;
        }
    }
}