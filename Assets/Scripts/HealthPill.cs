using UnityEngine;
using Fusion;

public class HealthPill : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if(!Object.HasStateAuthority) return;
        HealthController player = other.GetComponent<HealthController>();
        if(player != null )
        {
            player.Heal(20);
            Runner.Despawn(Object);
        }
    }
}
