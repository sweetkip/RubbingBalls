using Fusion;
using UnityEngine;

public class BallCollition : NetworkBehaviour
{
    private int damage = 1;
    private Rigidbody2D rb;
    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Object.HasStateAuthority)
            return;
        HealthController otherHealth = collision.gameObject.GetComponent<HealthController>();
        Rigidbody2D otherRb2D = collision.gameObject.GetComponent<Rigidbody2D>();
        if (otherHealth != null && otherRb2D != null)
        {
            float mySpeed = rb.linearVelocity.magnitude;
            float otherSpeed = otherRb2D.linearVelocity.magnitude;
            if (mySpeed <= otherSpeed)
            {
                Debug.Log("Mi velocidad era de: " + mySpeed + " Su velocidad era de: " + otherSpeed);
                otherHealth.TakeDamage(damage);
            }
        }
    }
}
