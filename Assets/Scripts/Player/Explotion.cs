using UnityEngine;
using Fusion;
public class Explotion : NetworkBehaviour
{
    private NetworkObject networkObject;
    private void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
    }
    public void Explode()
    {
        Runner.Despawn(networkObject);
    }
}