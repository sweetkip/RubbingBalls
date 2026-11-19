using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrampolineHazard : Hazard
{
    [SerializeField, Min(0f)] private float launchSpeed = 15f;

    [Header("Animación (se reproduce en todos los clientes)")]
    [SerializeField, Min(0.05f)] private float bounceDuration = 0.25f;
    [SerializeField, Range(0f, 0.8f)] private float squashAmount = 0.3f;

    // 0 = escala normal, 1 = máximo aplastado. Se aplasta rápido y vuelve más lento.
    [SerializeField]
    private AnimationCurve squashCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.3f, 1f),
        new Keyframe(1f, 0f));

    // Todos los trampolines que hay en la escena de esta PC
    private static readonly List<TrampolineHazard> all = new List<TrampolineHazard>();

    private Vector3 baseScale;
    private Coroutine bounceRoutine;

    private void OnEnable()
    {
        baseScale = transform.localScale;
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
        transform.localScale = baseScale;
        bounceRoutine = null;
    }

    // Esto solo corre en el host (Hazard.TryApply ya filtra por StateAuthority)
    protected override void Apply(Ball ball)
    {
        if (!ball.TryGetComponent(out Rigidbody2D rb))
            return;

        var rules = GameManager.CurrentRules;
        rb.linearVelocity = transform.up * (launchSpeed * (rules.version == 1 ? rules.trampoline_scale : 1f));

        // Avisa a todos los clientes para que reproduzcan la animación
        ball.PlayTrampolineBounce(transform.position);
    }

    // Lo llama el RPC de la pelota en TODAS las PCs (host y clientes).
    // Como los trampolines no son NetworkObjects, se identifica el trampolín por su posición.
    public static void PlayNear(Vector2 position)
    {
        TrampolineHazard closest = null;
        float bestDistance = float.MaxValue;

        foreach (TrampolineHazard trampoline in all)
        {
            float distance = ((Vector2)trampoline.transform.position - position).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = trampoline;
            }
        }

        if (closest != null)
            closest.PlayBounce();
    }

    private void PlayBounce()
    {
        if (bounceRoutine != null)
            StopCoroutine(bounceRoutine);

        bounceRoutine = StartCoroutine(BounceRoutine());
    }

    private IEnumerator BounceRoutine()
    {
        float elapsed = 0f;

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;

            float k = squashCurve.Evaluate(Mathf.Clamp01(elapsed / bounceDuration));

            // Se achata en Y (el eje de lanzamiento, transform.up) y se ensancha en X
            transform.localScale = new Vector3(
                baseScale.x * (1f + k * squashAmount),
                baseScale.y * (1f - k * squashAmount),
                baseScale.z);

            yield return null;
        }

        transform.localScale = baseScale;
        bounceRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.up * 2f);
    }
}