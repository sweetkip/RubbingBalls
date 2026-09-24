using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Networked] public int totalPlayers { get; set; }
    [Networked] public int alivePlayers { get; set; }
    public static GameManager Instance;
    [SerializeField] private int maxPlayers;
    [SerializeField] private NetworkPrefabRef explosionPrefab;

    private List<int> aliveIds = new List<int>();
    private List<PlayerRef> alivePlayerRefs = new List<PlayerRef>();

    [Networked, OnChangedRender(nameof(OnWinnerChanged))]
    public int WinnerId { get; set; } = -1;

    [Networked, OnChangedRender(nameof(OnWinnerChanged))]
    public PlayerRef WinnerPlayer { get; set; }

    public override void Spawned()
    {
        totalPlayers = 0;
        alivePlayers = 0;
        WinnerId = -1;
        WinnerPlayer = PlayerRef.None;
        aliveIds.Clear();
        alivePlayerRefs.Clear();
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
        Vector3 deathPosition = player.transform.position;
        RPC_SpawnExplosion(deathPosition);
        int playerLives = player.GetLives();
        if (playerLives - 1 <= 0)
        {
            UIManager.Instance.PlayerLost(id);

            int index = aliveIds.IndexOf(id);

            Runner.Despawn(player.gameObject.GetComponent<NetworkObject>());
            alivePlayers--;
            aliveIds.Remove(id);
            if (index >= 0) alivePlayerRefs.RemoveAt(index);
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
        alivePlayers--;

        RPC_PlayerDisconnected(id);   
        CheckWinner();                
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayerDisconnected(int id)
    {
        UIManager.Instance.PlayerDisconnected(id);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnExplosion(Vector3 position)
    {
        if (!Object.HasStateAuthority)
            return;

        Runner.Spawn(explosionPrefab, position, Quaternion.identity);
    }


    private void CheckWinner()
    {
        if (alivePlayers > 1) return;                 
        if (WinnerPlayer != PlayerRef.None) return;  

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
    }

    public void ShowResults()
    {
        UIManager.Instance.GameIsOver(true);
    }

    private void OnWinnerChanged()
    {
        if (WinnerPlayer == PlayerRef.None)
            return;

        bool won = WinnerPlayer == Runner.LocalPlayer;
        UIManager.Instance.GameIsOver(won);
    }
}