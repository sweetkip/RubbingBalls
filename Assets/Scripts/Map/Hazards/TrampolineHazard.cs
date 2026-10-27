using UnityEngine;

public class TrampolineHazard : Hazard
{
    [SerializeField, Min(0f)] private float launchSpeed = 15f;

    protected override void Apply(Ball ball)
    {
        if (!ball.TryGetComponent(out Rigidbody2D rb))
            return;

        rb.linearVelocity = transform.up * launchSpeed;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.up * 2f);
    }
}