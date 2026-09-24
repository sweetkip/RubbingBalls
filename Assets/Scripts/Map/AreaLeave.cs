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

        // ARREGLO: cuando se borra una bola (su jugador se desconectó), Unity también
        // dispara OnTriggerExit2D. Esa bola ya no existe en la red, así que la ignoro.
        // Sin esto, tiraba error al leer ball.id.
        if (ball.Object == null || !ball.Object.IsValid)
            return;

        GameManager.Instance.PlayerOut(ball.id, ball);
    }
}