using Fusion;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    [SerializeField] private NetworkRunner runner;

    public async void StartGameHost()
    {
        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = "Partida01"
        });
    }

    public async void StartGameClient()
    {
        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = "Partida01"
        });
    }
}
