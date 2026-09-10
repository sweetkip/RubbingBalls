using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

//{}

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runner;
    [SerializeField] private NetworkPrefabRef playerPrefab;
    private int shootsLeft;

    private void Awake()
    {
        runner.AddCallbacks(this);
    }

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
        SceneManager.LoadScene("Lobby");
        Debug.Log("Se unio el crack");
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
        SceneManager.LoadScene("Lobby");
        Debug.Log("Se unio un wachin");
    }



    public async void QuickPlay()
    {
        Debug.Log("RIBOMBEEEE");
        runner.ProvideInput = true;

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient
        });
    }

    public async void JoinLobby()
    {
        var result = await runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (!result.Ok)
        {
            Debug.LogError(result.ShutdownReason);
        }
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        //throw new NotImplementedException();
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        //throw new NotImplementedException();
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            int playerCount = runner.ActivePlayers.Count();

            if (playerCount >= 2)
            {
                runner.LoadScene(SceneRef.FromIndex(1), LoadSceneMode.Single);
            }
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
        throw new NotImplementedException();
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        //throw new NotImplementedException();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        //throw new NotImplementedException();
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
    {
        //throw new NotImplementedException();
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        //throw new NotImplementedException();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData data = new NetworkInputData();
        data.Buttons.Set((int)InputButton.Fire, Input.GetMouseButton(0));
        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        throw new NotImplementedException();
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        throw new NotImplementedException();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        foreach (SessionInfo session in sessionList)
        {
            Debug.Log(session.Name + " - " + session.PlayerCount + "/" + session.MaxPlayers);
        }
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        //throw new NotImplementedException();
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        //throw new NotImplementedException();
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!runner.IsServer)
            return;

        foreach(PlayerRef player in runner.ActivePlayers)
        {
            NetworkObject newPlayer = runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
            runner.SetPlayerObject(player, newPlayer);
        }
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        //throw new NotImplementedException();
    }

    public async void Disconect()
    {
        await runner.Shutdown();
    }
}