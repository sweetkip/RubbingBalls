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
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private int sceneIndex = 1;

    private int shootsLeft;

    public static NetworkManager Instance { get; private set; }
    
    public event Action<List<SessionInfo>> OnSessionListChanged;
    public event Action OnJoinFailed;
    public event Action OnJoinSucceeded;

    //<3
    private const string RoomCodeChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int RoomCodeLenght = 5;
    private TaskCompletionSource<List<SessionInfo>> quickSessionTcs;
    //<3


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        

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

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = sessionName,
            PlayerCount = 4,
            IsOpen = true,      //No se puede unir == falso     int playerCount = runner.ActivePlayers.Count();     if (playerCount >= 4)    Runner.SessionInfo.IsOpen = false;
            IsVisible = true,    //Partida privada == falso      if PlayerCount == 4     Runner.SessionInfo.IsVisible = false;
            MatchmakingMode = Photon.Realtime.MatchmakingMode.FillRoom,      //El mejor modo, llena una sala después pasa a la siguiente. Random es random y la serial une por orden de sala de a un jugador.
            SceneManager = GetComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
        {
            Debug.LogError("Couldn't create room: " + result.ShutdownReason);
            OnJoinFailed?.Invoke();
            return;
        }

        OnJoinSucceeded?.Invoke();
        await runner.LoadScene("Lobby");
        
    }

    public async void StartGameClient(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            OnJoinFailed?.Invoke();
            return;
        }

        runner.ProvideInput = true;

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = sessionName,
            SceneManager = GetComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
        {
            Debug.LogWarning($"No se pudo unir a '{sessionName}': {result.ShutdownReason}");
            OnJoinFailed?.Invoke();
            return;
        }
    }

    public async void JoinLobby()
    {
        var result = await runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (!result.Ok)
        {
            Debug.LogError("Couldn't join the lobby: " + result.ShutdownReason);
        }
    }


    public async void QuickPlay()
    {
        /*
        //ESTE CÓDIGO ES EL VIEJO, EL QUE FUNCIONA COMO UN QUICK DE FOTON REAL
        //LO TUVE QUE CAMBIAR PORQUE SINO GENERABA UNA LISTA LAARGA DE CARACTERES COMO NOMBRE DE LA ROOM
        runner.ProvideInput = true;

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            PlayerCount = 4,
            MatchmakingMode = Photon.Realtime.MatchmakingMode.FillRoom,
            Scene = SceneRef.FromIndex(sceneIndex),
            SceneManager = GetComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
        {
            Debug.LogError("QuickPlay failed: " + result.ShutdownReason);
            OnJoinFailed?.Invoke();
            return;
        }

        OnJoinSucceeded?.Invoke();
        */

        quickSessionTcs = new TaskCompletionSource<List<SessionInfo>>();

        var lobbyResult = await runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (!lobbyResult.Ok)
        {
            Debug.LogError("QuickPlay: couldn't join the lobby - " + lobbyResult.ShutdownReason);
            quickSessionTcs = null;
            OnJoinFailed?.Invoke();
            return;
        }

        var listTask = quickSessionTcs.Task;
        var timeoutTask = Task.Delay(5000);
        var finishedTask = await Task.WhenAny(listTask, timeoutTask);
        quickSessionTcs = null;

        List<SessionInfo> sessions = finishedTask == listTask ? listTask.Result : new List<SessionInfo>();

        SessionInfo best = null;
        foreach (SessionInfo session in sessions)
        {
            if (!session.IsOpen || !session.IsVisible) continue;
            if (session.PlayerCount >= session.MaxPlayers) continue;
            if (best == null || session.PlayerCount > best.PlayerCount)
                best = session;
        }

        if (best != null)
        {
            StartGameClient(best.Name);
        }
        else
        {
            StartGameHost(GenerateRoomCode());
        }
    }

    private string GenerateRoomCode()
    {
        var chars = new char[RoomCodeLenght];
        for (int i = 0; i < RoomCodeLenght; i++)
            chars[i] = RoomCodeChars[UnityEngine.Random.Range(0, RoomCodeChars.Length)];
        
        return new string(chars);
    }

    public async void Disconnect()
    {
        await runner.Shutdown();
    }



    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        /*
        if (runner.IsServer)
        {
            runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
        }*/

        if (!runner.IsServer) return;

        NetworkObject obj = runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
        runner.SetPlayerObject(player, obj);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        /*
        if (runner.IsServer)
        {
            NetworkObject playerObject = runner.GetPlayerObject(player);

            if (playerObject != null)
            {
                runner.Despawn(playerObject);
            }
        }*/

        if (!runner.IsServer) return;

        NetworkObject playerObject = runner.GetPlayerObject(player);
        if (playerObject != null)
        {
            runner.Despawn(playerObject);
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        SceneManager.LoadScene("Lobby");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        //throw new NotImplementedException();
        Debug.LogWarning("Disconnected from server: " + reason);
        OnJoinFailed?.Invoke();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning("Failed connection: " + reason);
        OnJoinFailed?.Invoke();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        /*
        foreach (SessionInfo session in sessionList)
        {
            Debug.Log(session.Name + " - " + session.PlayerCount + "/" + session.MaxPlayers);
        }*/
        OnSessionListChanged?.Invoke(sessionList);
        quickSessionTcs?.TrySetResult(sessionList);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData data = new NetworkInputData();
        data.Buttons.Set((int)InputButton.Fire, Input.GetMouseButton(0));
        data.AimWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        Debug.Log("Falta input, el runner es: " + runner + " el player es: " + player + " El input es: " + input);
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Nos conectamos al sever");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!runner.IsServer) return;
        Debug.Log("Cargamos una escena");
    }


    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }


    public async void Disconect()
    {
        await runner.Shutdown();
    }
}