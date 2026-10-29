using UnityEngine;
using Fusion;

public class HealthController : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHealthChanged))]
    public float Health { get; set; }
    [Networked, OnChangedRender(nameof(OnHealthChanged))]
    public int Lives { get; set; }

    [SerializeField] private int initialHealth = 1;
    [SerializeField] private int initialLives = 3;

    private Ball ball;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Health = initialHealth;
            Lives = initialLives;
        }

        ball = GetComponent<Ball>();

        if (ball != null)
            ball.MarkHudDirty();
    }

    public void TakeDamage(float amount)
    {
        if (!Object.HasStateAuthority)
            return;

        Health += amount;
        if (amount > 0f)
            ball?.ReportDamage();
    }

    public void Heal(float amount)
    {
        if (!Object.HasStateAuthority) return;
        Health -= amount;
    }

    private void OnHealthChanged()
    {
        RefreshHud();
    }

    public void RefreshHud()
    {
        if (ball == null || UIManager.Instance == null)
            return;

        UIManager.Instance.SetPlayerInfo(ball.id, ball.ballName, ball.BaseSprite, ball.BaseColor);
        UIManager.Instance.SetPlayerStats(ball.id, Health - initialHealth, Lives);
    }

    public void ResetHealth()
    {
        if (!Object.HasStateAuthority)
            return;
        Health = initialHealth;
    }
}