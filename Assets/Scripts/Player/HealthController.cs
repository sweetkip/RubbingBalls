using UnityEngine;
using Fusion;

public class HealthController : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHealthChanged))] 
    public float Health { get; set; }
    [SerializeField] private int initialHealth = 1;
    private int id;
    private Ball ball;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Health = initialHealth;
        }
        ball = GetComponent<Ball>();
        if (ball != null)
        {
            id = ball.id;
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
    }

    public void ResetHealth()
    {
        if (!Object.HasStateAuthority)
            return;
        Health = initialHealth;
    }
}
