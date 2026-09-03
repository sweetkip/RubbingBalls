using UnityEngine;

public class DisconnectBtn : MonoBehaviour
{
    NetworkManager networkManager;
    public void DisconnectFromGame()
    {
        networkManager = FindAnyObjectByType<NetworkManager>();
        networkManager.Disconect();
    }
}
