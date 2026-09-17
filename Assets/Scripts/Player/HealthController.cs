using UnityEngine;
using Fusion;

public class HealthController : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHealthChanged))] 
    public int Health { get; set; }
    [SerializeField] private int maxHealth = 10;
    private int id;

    public override void Spawned()
    {
        if(Object.HasStateAuthority)
        {
            Health = maxHealth;
            id = UIManager.Instance.IJoined();
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
        UIManager.Instance.ChangeHealth(Health, id);
        Debug.Log("Health cambio: " +  Health);
    }
}
