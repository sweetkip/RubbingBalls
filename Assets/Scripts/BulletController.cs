using UnityEngine;
using Fusion;

public class BulletController : NetworkBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private int damage = 1;
    public override void FixedUpdateNetwork()
    {
        if(Object.HasStateAuthority)
        {
            transform.Translate(Vector3.right * speed * Runner.DeltaTime);
            if(transform.position.x >= 50)
            {
                Runner.Despawn(Object);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(!Object.HasStateAuthority)
            return;
        HealthController health = other.GetComponent<HealthController>();
        if(health != null)
        {
            health.TakeDamage(damage);
        }
    }
}
