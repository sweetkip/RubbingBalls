using Fusion;
using UnityEngine;

public class BallCollition : NetworkBehaviour
{
    [Networked] public Vector2 speed {get; set;}
    private float damage = 0.01f;
    private Rigidbody2D rb;
    private HealthController myHealth;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        myHealth = this.gameObject.GetComponent<HealthController>();
        
    }

    public override void FixedUpdateNetwork()
    {
        if(rb != null)
        {
            speed = rb.linearVelocity;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        HealthController otherHealth = collision.gameObject.GetComponent<HealthController>();
        BallCollition otherCollition = collision.gameObject.GetComponent<BallCollition>();
        if (otherHealth != null && otherCollition != null)
        {
            Vector2 otherSpeed = otherCollition.speed;
            if (speed.magnitude > 5f)
            {
                otherHealth.TakeDamage(damage * speed.magnitude);
                otherCollition.EnemyPush(speed.normalized);
            }
            if (otherSpeed.magnitude > 5f)
            {
                myHealth.TakeDamage(damage * otherSpeed.magnitude);
                EnemyPush(otherSpeed.normalized);
            }
        }
    }

    public void EnemyPush(Vector2 dir)
    {
        if (!Object.HasStateAuthority)
            return;
        rb.AddForce(dir.normalized * dir.magnitude * myHealth.Health, ForceMode2D.Impulse);
    }
}
