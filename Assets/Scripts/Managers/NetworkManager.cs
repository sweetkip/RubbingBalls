using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
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

    private Dictionary<PlayerRef, byte> playerColors =
        new Dictionary<PlayerRef, byte>();

    private bool inPreGame;
    private bool startingGame;
    private bool gamePlayersSpawned;

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
            OnJoinFailed?.Invoke();
            return;
        }

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
            Debug.LogError(
                "Couldn't create room: " +
                result.ShutdownReason
            );

            OnJoinFailed?.Invoke();

            return;
        }

        OnJoinSucceeded?.Invoke();
    }

    public async void StartGameClient(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            OnJoinFailed?.Invoke();
            return;
        }

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

            return;
        }

        OnJoinSucceeded?.Invoke();
    }

    public async void QuickPlay()
    {
        runner.ProvideInput = true;

        inPreGame = true;
        startingGame = false;
        gamePlayersSpawned = false;

        var result = await runner.StartGame(
            new StartGameArgs()
            {
                GameMode = GameMode.AutoHostOrClient,

                PlayerCount = maxPlayers,

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
            Debug.LogError(
                "QuickPlay failed: " +
                result.ShutdownReason
            );

            OnJoinFailed?.Invoke();

            return;
        }

        OnJoinSucceeded?.Invoke();
    }

    public async void JoinLobby()
    {
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
        }
    }

    public async void Disconnect()
    {
        if (runner == null)
            return;

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
        if (runner.IsServer && inPreGame && !startingGame)
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
            if (runner.TryGetPlayerObject(
                    player,
                    out NetworkObject playerObject))
            {
                if (playerObject != null)
                {
                    runner.Despawn(playerObject);
                }
            }

            playerColors.Remove(player);

            if (inPreGame && !startingGame)
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

        foreach (PlayerRef player in runner.ActivePlayers)
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

        await runner.LoadScene(
            SceneRef.FromIndex(gameSceneIndex)
        );
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

            objectsToDespawn.Add(playerObject);
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
                ball.SetInitialColor(colorIndex);
            }

            runner.SetPlayerObject(
                player,
                ballObject
            );
        }
    }

    public void OnInput(
        NetworkRunner runner,
        NetworkInput input)
    {
        NetworkInputData data =
            new NetworkInputData();

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
        if (Instance == this)
            Instance = null;

        string sceneToLoad =
            mainMenuSceneName;

        Destroy(gameObject);

        if (SceneManager.GetActiveScene().name !=
            sceneToLoad)
        {
            SceneManager.LoadScene(
                sceneToLoad
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
    }

    public void OnInputMissing(
        NetworkRunner runner,
        PlayerRef player,
        NetworkInput input)
    {
    }

    public void OnConnectedToServer(
        NetworkRunner runner)
    {
        Debug.Log(
            "Nos conectamos al servidor"
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
}