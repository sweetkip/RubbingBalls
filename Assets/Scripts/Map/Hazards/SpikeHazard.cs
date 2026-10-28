using System.Collections.Generic;
using UnityEngine;

public class SpikeHazard : Hazard
{
    [SerializeField, Min(0f)] private float damage = 0.1f;
    [SerializeField, Min(0.05f)] private float secondsBetweenHits = 1f;

    private readonly Dictionary<Ball, float> nextHitTime = new Dictionary<Ball, float>();

    protected override bool ActsWhileInside => true;

    protected override void Apply(Ball ball)
    {
        if (ball.IsPushImmune)
            return;

        if (nextHitTime.TryGetValue(ball, out float allowedAt) && Time.time < allowedAt)
            return;

        if (!ball.TryGetComponent(out HealthController health))
            return;

        health.TakeDamage(damage);
        nextHitTime[ball] = Time.time + secondsBetweenHits;
    }
}