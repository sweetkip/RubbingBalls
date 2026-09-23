using UnityEngine;
using Fusion;

public class HealthController : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHealthChanged))] 
    public float Health { get; set; }
    [Networked] private int id {  get; set; }
    [SerializeField] private int initialHealth = 1;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Health = initialHealth;
        }
        id = UIManager.Instance.IJoined();
    }

    public void TakeDamage(float amount)
    {
        if (!Object.HasStateAuthority)
            return;

        Health += amount;
    }

    public void Heal(float amount)
    {
        if (!Object.HasStateAuthority) return;
        Health -= amount;
    }
    private void OnHealthChanged()
    {
        UIManager.Instance.ChangeHealth(Health, id);
    }

    public void ResetHealth()
    {
        if (!Object.HasStateAuthority)
            return;
        Health = initialHealth;
    }
}
