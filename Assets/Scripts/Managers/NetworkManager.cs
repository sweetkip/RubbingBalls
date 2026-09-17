using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

//{}

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runner;
    [SerializeField] private NetworkPrefabRef playerPrefab;
    private int shootsLeft;

    //<3
    [SerializeField] private string[] presetRooms = new string[] { "Red Room", "Chuck Room", "Bomb Room" };
    
    public static NetworkManager Instance { get; private set; }
    public event Action<List<SessionInfo>> OnSessionListChanged;
    private List<SessionInfo> lastSessionList = new List<SessionInfo>();
    public string[] PresetRooms => presetRooms;

    //<3


    private void Awake()
    {
        Instance = this;    //<3
        runner.AddCallbacks(this);
    }

    //<3
    private async void Start()
    {
        //Se tiene que unir al lobby ni bien comienza para poder recibir OnSessionListUpdated
        await JoinLobby();
    }
    /*
    //Tiene sentido este comentado más adelante :P
    public async void StartGameHost(string sessionName)
    {
        runner.ProvideInput = true;

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = sessionName,
            PlayerCount = 4,
            IsOpen = true,      //No se puede unir == falso     int playerCount = runner.ActivePlayers.Count();     if (playerCount >= 4)    Runner.SessionInfo.IsOpen = false;
            IsVisible = true,    //Partida privada == falso      if PlayerCount == 4     Runner.SessionInfo.IsVisible = false;
            MatchmakingMode = Photon.Realtime.MatchmakingMode.FillRoom,      //El mejor modo, llena una sala después pasa a la siguiente. Random es random y la serial une por orden de sala de a un jugador.
            SceneManager = GetComponent<NetworkSceneManagerDefault>()
        });
        await runner.LoadScene("Lobby");
        
    }

    public async void StartGameClient(string sessionName)
    {
        runner.ProvideInput = true;

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = sessionName,
            SceneManager = GetComponent<NetworkSceneManagerDefault>()
        });
    }
    */
    //<3

    public async System.Threading.Tasks.Task JoinLobby()
    {
        var result = await runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (!result.Ok)
        {
            Debug.LogError("No se pudo unir al lobby: " + result.ShutdownReason);
        }
    }

    //<3
    //Acá voy a tratar de unir el crear salas como host y unirse a ellas como clientes
    //Quizás Ale me quiera matar jsksj, pero es para poder usar estos métodos para el quick play y la lista :P
    public async void JoinOrCreateSession(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName)) return;

        runner.ProvideInput = true;

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = sessionName,
            PlayerCount = 4,
            IsOpen = true,
            IsVisible = true,
            MatchmakingMode = Photon.Realtime.MatchmakingMode.FillRoom,
            SceneManager = GetComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
        {
            await runner.LoadScene("Lobby");
        }
        else
        {
            Debug.LogError("Error al unirse / crear sala: " + result.ShutdownReason);
        }
    }
    //<3

    //<3
    //Acá está el porque las comenté antes.
    //El creado es básicamente el mismo de estas dos, solo cambiando si son host o client
    //Para no repetir código entonces utilizan el método de arriba y listo
    public void StartGameHost(string sessionName) => JoinOrCreateSession(sessionName);
    public void StartGameClient(string sessionName) => JoinOrCreateSession(sessionName);
    //<3

    //<3
    public async void QuickPlay()
    {
        SessionInfo best = lastSessionList
            .Where(s => s.IsValid && s.IsOpen && s.PlayerCount < s.MaxPlayers)
            .OrderByDescending(s => s.PlayerCount)
            .FirstOrDefault();

        if (best != null)
        {
            JoinOrCreateSession(best.Name);
        }
        else
        {
            string randomName = "Sala " + GenerateRandomCode(6);
            JoinOrCreateSession(randomName);
        }
    }
    //<3

    //<3
    private string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var result = new char[length];
        for ( int i = 0; i < length; i++ )
            result[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        return new string(result);
    }
    //<3

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            NetworkObject playerObject = runner.GetPlayerObject(player);

            if (playerObject != null)
            {
                runner.Despawn(playerObject);
            }
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        SceneManager.LoadScene("Lobby");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning("Desconectado : " + reason);
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

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

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        foreach (SessionInfo session in sessionList)
        {
            Debug.Log(session.Name + " - " + session.PlayerCount + "/" + session.MaxPlayers);
        }
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!runner.IsServer)
            return;
        Debug.Log("Cargamos una escena");
    }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    public async void Disconect()
    {
        await runner.Shutdown();
    }
}