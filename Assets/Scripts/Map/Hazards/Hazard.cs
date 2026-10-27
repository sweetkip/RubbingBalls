using UnityEngine;

public abstract class Hazard : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out Ball ball))
            return;

        if (ball.Object == null || !ball.Object.IsValid)
            return;

        if (!ball.Object.HasStateAuthority)
            return;

        Apply(ball);
    }

    protected abstract void Apply(Ball ball);
}