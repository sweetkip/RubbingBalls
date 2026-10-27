using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class BallCollition : NetworkBehaviour
{
    public Vector2 speed { get; set; }
    [SerializeField, Min(0f)] private float damage = 0.01f;
    [SerializeField, Min(0f)] private float minimumDamageSpeed = 5f;
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
        if (Object.HasStateAuthority)
            speed = rb.linearVelocity;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority || ball == null)
            return;
        BallCollition other = collision.rigidbody != null ? collision.rigidbody.GetComponent<BallCollition>() : collision.gameObject.GetComponentInParent<BallCollition>();
        Vector2 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector2.up;
        if (other == null)
        {
            ball.ReportImpact(normal, collision.relativeVelocity.magnitude);
            return;
        }
        if (other == this || other.ball == null || other.Object == null || !other.Object.IsValid || ball.id > other.ball.id || ball.IsPushImmune || other.ball.IsPushImmune)
            return;
        int tick = Runner.Tick.Raw;
        if (processedCollisionTicks.TryGetValue(other.ball.id, out int previousTick) && previousTick == tick)
            return;
        processedCollisionTicks[other.ball.id] = tick;
        Vector2 mySpeed = speed;
        Vector2 otherSpeed = other.speed;
        ball.ReportImpact(normal, Mathf.Max(collision.relativeVelocity.magnitude, (mySpeed - otherSpeed).magnitude), other.ball);
        if (mySpeed.magnitude > minimumDamageSpeed)
        {
            other.myHealth.TakeDamage(damage * mySpeed.magnitude);
            other.EnemyPush(mySpeed.normalized);
        }
        if (otherSpeed.magnitude > other.minimumDamageSpeed)
        {
            myHealth.TakeDamage(other.damage * otherSpeed.magnitude);
            EnemyPush(otherSpeed.normalized);
        }
    }

    public void EnemyPush(Vector2 dir)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority || ball.IsPushImmune)
            return;
        rb.AddForce(dir.normalized * dir.magnitude * myHealth.Health, ForceMode2D.Impulse);
    }
}
