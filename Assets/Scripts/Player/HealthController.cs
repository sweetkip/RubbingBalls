using UnityEngine;
using Fusion;

public class HealthController : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHealthChanged))] 
    public float Health { get; set; }
    [SerializeField] private int initialHealth = 1;
    private int id;

    public override void Spawned()
    {
        if(Object.HasStateAuthority)
        {
            Health = 1;
            id = UIManager.Instance.IJoined();
        }
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
        Debug.Log("Health cambio: " +  Health);
    }
}
