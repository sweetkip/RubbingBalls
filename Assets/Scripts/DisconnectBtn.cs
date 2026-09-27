using UnityEngine;

public class DisconnectBtn : MonoBehaviour
{
    NetworkManager networkManager;
    public void DisconnectFromGame()
    {
        networkManager = NetworkManager.Find();
        networkManager.Disconect();
    }
}