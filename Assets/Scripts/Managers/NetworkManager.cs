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

    // NUEVO: avisa a la UI cada vez que cambia el estado de la conexión (mensaje, ¿es un error?)
    public event Action<string, bool> OnStatusChanged;

    // NUEVO: datos para saber qué pasó cuando se corta la conexión
    private bool isInSession;        // true cuando StartGame salió bien (ya estoy en una partida)
    private bool leftOnPurpose;      // true cuando el jugador tocó "Salir"
    private bool goingToMenu;        // evita volver al menú dos veces
    private string currentSessionName = "";

    private const string RoomCodeChars =
        "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private const int RoomCodeLength = 5;

    private TaskCompletionSource<List<SessionInfo>> quickSessionTcs;

    private Dictionary<PlayerRef, byte> playerColors =
        new Dictionary<PlayerRef, byte>();

    private bool inPreGame;
    private bool startingGame;
    private bool gamePlayersSpawned;

    private bool inputEnabled = true;   //<3

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
            SetStatus("Escribí un nombre para la partida.", true);   // NUEVO
            OnJoinFailed?.Invoke();
            return;
        }

        SetStatus("Creando la partida \"" + sessionName + "\"...");   // NUEVO
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
            GoToMenu(ReasonToText(result.ShutdownReason), true);   // NUEVO: muestro el motivo real

            return;
        }

        // NUEVO: ya estoy en una partida
        isInSession = true;
        currentSessionName = sessionName;
        SetStatus("Partida \"" + sessionName + "\" creada. Entrando a la sala...");

        OnJoinSucceeded?.Invoke();
    }

    public async void StartGameClient(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            SetStatus("Escribí el nombre de la partida a la que querés entrar.", true);   // NUEVO
            OnJoinFailed?.Invoke();
            return;
        }

        SetStatus("Uniéndote a \"" + sessionName + "\"...");   // NUEVO
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
            GoToMenu(ReasonToText(result.ShutdownReason), true);   // NUEVO: muestro el motivo real

            return;
        }

        // NUEVO: ya estoy en una partida
        isInSession = true;
        currentSessionName = sessionName;
        SetStatus("¡Conectado a \"" + sessionName + "\"! Entrando a la sala...");

        OnJoinSucceeded?.Invoke();
    }

    public async void JoinLobby()
    {
        SetStatus("Buscando partidas...");   // NUEVO

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

            GoToMenu(ReasonToText(result.ShutdownReason), true);   // NUEVO
        }
    }

    public async void QuickPlay()
    {
        runner.ProvideInput = true;

        inPreGame = true;
        startingGame = false;
        gamePlayersSpawned = false;

        SetStatus("Buscando una partida libre...");   // NUEVO

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
            GoToMenu(ReasonToText(lobbyResult.ShutdownReason), true);   // NUEVO

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
            StartGameClient(best.Name);
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

        for (int i = 0; i < RoomCodeLength; i++)
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

        leftOnPurpose = true;   // NUEVO: me voy yo, no es un error

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
            // NUEVO: si se fue en plena partida, aviso al GameManager
            // para que actualice los vivos y chequee si alguien ganó por abandono
            if (!inPreGame && GameManager.Instance != null)
            {
                GameManager.Instance.PlayerDisconnected(player);
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

        runner.SetPlayerObject(player,lobbyPlayer);
    }

    private void EnsureLobbyPlayersExist()
    {
        if (!runner.IsServer)
            return;

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            SpawnLobbyPlayerIfNeeded(runner,player);
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

        foreach (PlayerRef player in runner.ActivePlayers)
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

        if (playerCount == 0)
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
            runner.SessionInfo.IsOpen = false;
            runner.SessionInfo.IsVisible = false;
        }

        SaveLobbyPlayerData();

        DespawnLobbyPlayers();

        gamePlayersSpawned = false;

        await runner.LoadScene(SceneRef.FromIndex(gameSceneIndex));
    }

    private void SaveLobbyPlayerData()
    {
        playerColors.Clear();

        foreach (PlayerRef player in runner.ActivePlayers)
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

        foreach (PlayerRef player in runner.ActivePlayers)
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

        foreach (NetworkObject obj in objectsToDespawn)
        {
            runner.Despawn(obj);
        }
    }

    private void SpawnGamePlayers()
    {
        if (!runner.IsServer)
            return;

        if (gamePlayersSpawned)
            return;

        gamePlayersSpawned = true;

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            NetworkObject ballObject =
                runner.Spawn(
                    ballPrefab,
                    Vector3.zero,
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
                colorIndex = savedColor;
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
        }
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData data = new NetworkInputData();

        //<3 Hice esto para que el player pueda interactuar con los btns de victoria/derrota <3
        
        /*data.Buttons.Set((int)InputButton.Fire,Input.GetMouseButton(0));
        if (Camera.main != null)
        {
            data.AimWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        }*/

        if (inputEnabled)
        {
            data.Buttons.Set((int)InputButton.Fire, Input.GetMouseButton(0));
            if (Camera.main != null)
            {
                data.AimWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            }
        }

        input.Set(data);
    }

    public void OnSceneLoadDone(
        NetworkRunner runner)
    {
        int sceneIndex =
            SceneManager.GetActiveScene().buildIndex;

        if (sceneIndex == preGameSceneIndex)
        {
            inPreGame = true;
            startingGame = false;
            gamePlayersSpawned = false;

            if (runner.IsServer)
            {
                EnsureLobbyPlayersExist();
            }

            PreGameLobbyUI.Instance?.RefreshLobby();
        }

        if (sceneIndex == gameSceneIndex)
        {
            inPreGame = false;

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
        // CAMBIO: siempre vuelvo al menú, pero ahora explicando qué pasó
        if (leftOnPurpose)
        {
            GoToMenu("Saliste de la partida.", false);
        }
        else if (isInSession)
        {
            // Estaba en una partida y se cortó: guardo el nombre para poder reintentar
            ConnectionMessage.RejoinSession = currentSessionName;
            GoToMenu("Se cortó la conexión con la partida (el host se fue o falló internet).", true);
        }
        else
        {
            // Nunca llegué a entrar: fue un error al crear o al unirse
            GoToMenu(ReasonToText(shutdownReason), true);
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

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning("Failed connection: " + reason);
        OnJoinFailed?.Invoke();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        OnSessionListChanged?.Invoke(sessionList);

        quickSessionTcs?.TrySetResult(sessionList);

        // NUEVO: le cuento al jugador cuántas partidas hay
        if (!isInSession)
        {
            int visibles = 0;
            foreach (SessionInfo session in sessionList)
            {
                if (session.IsVisible) visibles++;
            }

            if (visibles == 0) SetStatus("No hay partidas abiertas. ¡Creá una!");
            else SetStatus("Partidas encontradas: " + visibles);
        }
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        Debug.Log("Input missing, runner: " + runner + " player: " + player + ". Input is: " + input);
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Nos conectamos al servidor");
        SetStatus("Conectado al host.");   // NUEVO
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }

    // ===================== NUEVO: estado de conexión y errores =====================

    // Manda un mensaje a la UI (ConnectionStatusUI lo muestra en pantalla)
    private void SetStatus(string message, bool isError = false)
    {
        Debug.Log("[Estado de conexión] " + message);
        OnStatusChanged?.Invoke(message, isError);
    }

    // Vuelve al menú mostrando un mensaje. Se usa para TODOS los cortes y errores.
    // Siempre recarga el MainMenu: un NetworkRunner apagado no se puede reusar,
    // y el menú recargado trae un NetworkManager nuevo con su Runner.
    private void GoToMenu(string message, bool isError)
    {
        if (goingToMenu) return;   // si ya estoy volviendo, no lo hago dos veces
        goingToMenu = true;

        ConnectionMessage.Set(message, isError);   // lo guardo: sobrevive al cambio de escena

        if (Instance == this)
        {
            Instance = null;
        }

        if (this != null)   // por si Fusion ya destruyó este objeto
        {
            Destroy(gameObject);
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Traduce el motivo técnico de Fusion a un mensaje que entienda el jugador
    private string ReasonToText(ShutdownReason reason)
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
}