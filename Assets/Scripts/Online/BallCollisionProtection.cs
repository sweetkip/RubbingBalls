using System.Collections.Generic;
using UnityEngine;

public static class BallCollisionProtection
{
    private sealed class Pair
    {
        public Ball A;
        public Ball B;
        public Collider2D First;
        public Collider2D Second;
        public bool OriginallyIgnored;
    }

    private static readonly List<Ball> balls = new List<Ball>(4);
    private static readonly List<Pair> pairs = new List<Pair>(6);

    public static void Register(Ball ball)
    {
        Unregister(ball);
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            Ball other = balls[i];
            if (other == null)
            {
                balls.RemoveAt(i);
                continue;
            }
            if (other.Runner != ball.Runner)
                continue;
            foreach (Collider2D first in ball.PhysicsColliders)
            foreach (Collider2D second in other.PhysicsColliders)
            {
                if (!Usable(first) || !Usable(second))
                    continue;
                pairs.Add(new Pair { A = ball, B = other, First = first, Second = second, OriginallyIgnored = Physics2D.GetIgnoreCollision(first, second) });
            }
        }
        balls.Add(ball);
        Refresh(ball);
    }

    public static void Refresh(Ball ball)
    {
        for (int i = pairs.Count - 1; i >= 0; i--)
        {
            Pair pair = pairs[i];
            if (pair.A == null || pair.B == null || pair.First == null || pair.Second == null)
            {
                pairs.RemoveAt(i);
                continue;
            }
            if (pair.A != ball && pair.B != ball)
                continue;
            if (!Usable(pair.First) || !Usable(pair.Second))
                continue;
            bool ignore = pair.OriginallyIgnored || pair.A.IsPushImmune || pair.B.IsPushImmune;
            if (Physics2D.GetIgnoreCollision(pair.First, pair.Second) != ignore)
                Physics2D.IgnoreCollision(pair.First, pair.Second, ignore);
        }
    }

    public static void Unregister(Ball ball)
    {
        for (int i = pairs.Count - 1; i >= 0; i--)
        {
            Pair pair = pairs[i];
            if (pair.A != ball && pair.B != ball && pair.A != null && pair.B != null)
                continue;
            if (Usable(pair.First) && Usable(pair.Second))
                Physics2D.IgnoreCollision(pair.First, pair.Second, pair.OriginallyIgnored);
            pairs.RemoveAt(i);
        }
        balls.Remove(ball);
    }

    private static bool Usable(Collider2D collider)
    {
        return collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger;
    }
}
