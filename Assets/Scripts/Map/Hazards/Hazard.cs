using UnityEngine;

public abstract class Hazard : MonoBehaviour
{
    // Los hazards que siguen actuando mientras la bola está encima lo ponen en true
    protected virtual bool ActsWhileInside => false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApply(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (ActsWhileInside)
            TryApply(other);
    }

    private void TryApply(Collider2D other)
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