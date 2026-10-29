using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class BallCollition : NetworkBehaviour
{
    public Vector2 speed { get; set; }
    [SerializeField, Min(0f)] private float damage = 0.01f;
    [SerializeField, Min(0f)] private float minimumDamageSpeed = 5f;
    [SerializeField, Min(0f)] private float collisionCooldown = 0.15f;
    private readonly Dictionary<int, int> processedCollisionTicks = new Dictionary<int, int>();
    private Rigidbody2D rb;
    private HealthController myHealth;
    private Ball ball;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        myHealth = GetComponent<HealthController>();
        ball = GetComponent<Ball>();
    }

    public override void FixedUpdateNetwork()
    {
        CaptureSpeed();
    }

    private void FixedUpdate()
    {
        CaptureSpeed();
    }

    private void CaptureSpeed()
    {
        if (Object != null && Object.IsValid && Object.HasStateAuthority && rb.simulated)
            speed = rb.linearVelocity;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority || ball == null)
            return;
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMigrating)
            return;
        BallCollition other = collision.rigidbody != null ? collision.rigidbody.GetComponent<BallCollition>() : collision.gameObject.GetComponentInParent<BallCollition>();
        Vector2 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector2.up;
        if (other == null)
        {
            ball.ReportImpact(normal, collision.relativeVelocity.magnitude);
            return;
        }
        if (other == this || other.ball == null || other.Object == null || !other.Object.IsValid || other.Runner != Runner || ball.id > other.ball.id || ball.IsPushImmune || other.ball.IsPushImmune)
            return;
        Vector2 separation = rb.worldCenterOfMass - other.rb.worldCenterOfMass;
        if (collision.contactCount == 0) normal = separation.normalized;
        if (normal.sqrMagnitude < 0.0001f) return;
        normal.Normalize();
        if (Vector2.Dot(normal, separation) < 0f) normal = -normal;
        float impactSpeed = Mathf.Abs(Vector2.Dot(collision.relativeVelocity, normal));
        if (impactSpeed < 0.1f) return;
        int tick = Runner.Tick.Raw;
        int cooldownTicks = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(collisionCooldown, other.collisionCooldown) / Runner.DeltaTime));
        if (processedCollisionTicks.TryGetValue(other.ball.id, out int previousTick) && unchecked(tick - previousTick) < cooldownTicks)
            return;
        processedCollisionTicks[other.ball.id] = tick;
        Vector2 mySpeed = speed;
        Vector2 otherSpeed = other.speed;
        ball.ReportImpact(normal, collision.relativeVelocity.magnitude, other.ball);
        if (mySpeed.magnitude > minimumDamageSpeed)
        {
            other.myHealth.TakeDamage(damage * mySpeed.magnitude);
            other.EnemyPush(-normal);
        }
        if (otherSpeed.magnitude > other.minimumDamageSpeed)
        {
            myHealth.TakeDamage(other.damage * otherSpeed.magnitude);
            EnemyPush(normal);
        }
    }

    public void EnemyPush(Vector2 dir)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority || ball.IsPushImmune)
            return;
        rb.AddForce(dir.normalized * dir.magnitude * myHealth.Health, ForceMode2D.Impulse);
    }
}
