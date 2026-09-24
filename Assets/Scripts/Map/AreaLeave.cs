using UnityEngine;
using Fusion;

public class AreaLeave : NetworkBehaviour
{
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        Ball ball = collision.gameObject.GetComponent<Ball>();
        if (ball == null)
            return;

        if (ball.Object == null || !ball.Object.IsValid)
            return;

        GameManager.Instance.PlayerOut(ball.id, ball);
    }
}