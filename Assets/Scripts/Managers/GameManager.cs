using UnityEngine;
using Fusion;

public class GameManager : NetworkBehaviour
{
    [Networked, Capacity(4)]
    public NetworkArray<int> playersLives { get; }
    [Networked] public int totalPlayers { get; set; }
    public static GameManager Instance;
    [SerializeField] private int maxLives;
    [SerializeField] private int maxPlayers;

    public override void Spawned()
    {
        totalPlayers = 0;
    }
    public int IJoined()
    {
        int id = totalPlayers;
        Debug.Log("Someone joinded: " + id);
        if (totalPlayers + 1 < maxPlayers)
        {
            totalPlayers++;
        }
        else
        {
            Debug.Log("No more players can Join");
        }
        playersLives.Set(id, maxLives);
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
        int currentLives = playersLives.Get(id);
        playersLives.Set(id, currentLives - 1);
        if (playersLives.Get(id) <= 0)
        {
            UIManager.Instance.PlayerLost(id);
            Runner.Despawn(player.gameObject.GetComponent<NetworkObject>());
        }
        else
        {
            player.Respawn();
            UIManager.Instance.ChangeHealth(player.GetComponent<HealthController>().Health, id);
        }
    }

    public int GetPlayerLives(int id)
    {
        return playersLives.Get(id);
    }
}
