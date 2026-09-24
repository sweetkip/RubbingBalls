using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Networked] public int totalPlayers { get; set; }
    [Networked] public int alivePlayers { get; set; }
    public static GameManager Instance;
    [SerializeField] private int maxPlayers;

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
        if (totalPlayers < maxPlayers)   // ARREGLO: antes era "totalPlayers + 1 < maxPlayers" y el 4° jugador no se contaba
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
        int playerLives = player.GetLives();
        if (playerLives - 1 <= 0)
        {
            UIManager.Instance.PlayerLost(id);

            int index = aliveIds.IndexOf(id);

            Runner.Despawn(player.gameObject.GetComponent<NetworkObject>());
            alivePlayers--;
            aliveIds.Remove(id);
            if (index >= 0) alivePlayerRefs.RemoveAt(index);
            CheckWinner();   // CAMBIO: el chequeo de ganador se movió a un método para reusarlo
        }
        else
        {
            player.Respawn();
        }
    }

    // NUEVO: lo llama el NetworkManager (solo en el Host) cuando un jugador se desconecta
    public void PlayerDisconnected(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;

        int index = alivePlayerRefs.IndexOf(playerRef);
        if (index < 0) return;   // ya había perdido antes de irse: no hay nada que actualizar

        int id = aliveIds[index];
        aliveIds.RemoveAt(index);
        alivePlayerRefs.RemoveAt(index);
        alivePlayers--;

        RPC_PlayerDisconnected(id);   // aviso a todos para que lo muestren en pantalla
        CheckWinner();                // si quedó uno solo, gana por abandono
    }

    // NUEVO: RPC del Host a todas las PCs para mostrar "P2 SE DESCONECTÓ"
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayerDisconnected(int id)
    {
        UIManager.Instance.PlayerDisconnected(id);
    }

    // NUEVO: si queda un solo jugador vivo, es el ganador (lo usan PlayerOut y PlayerDisconnected)
    private void CheckWinner()
    {
        if (alivePlayers > 1) return;                   // todavía hay partida
        if (WinnerPlayer != PlayerRef.None) return;     // ya había ganador

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