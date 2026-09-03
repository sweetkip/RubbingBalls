using UnityEngine;

public class DisconnectBtn : MonoBehaviour
{
    NetworkManager networkManager;
    public void Disconnect()
    {
        networkManager = FindAnyObjectByType<NetworkManager>();
        networkManager.Disconnect();
    }
}
