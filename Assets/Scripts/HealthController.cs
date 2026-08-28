using UnityEngine;
using Fusion;

public class HealthController : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHealthChanged))] 
    public int Health { get; set; }
    [SerializeField] private int maxHealth = 100;

    public override void Spawned()
    {
        if(Object.HasStateAuthority)
        {
            Health = maxHealth;
        }
    }

    public void TakeDamage(int amount)
    {
        if (!Object.HasStateAuthority)
            return;

        Health -= amount;
    }

    public void Heal(int amount)
    {
        if (!Object.HasStateAuthority) return;
        Health += amount;
    }
    private void OnHealthChanged()
    {
        Debug.Log("Health cambio: " +  Health);
    }
}
