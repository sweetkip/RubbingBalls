using TreeEditor;
using UnityEngine;

public class Ball : MonoBehaviour
{
    [SerializeField] private float force;
    [SerializeField] private float maxDistance;
    [SerializeField] private int maxShoots;
    [SerializeField] private float maxForce;
    [SerializeField] private LineRenderer lr;
    private int shootsLeft;
    private Rigidbody2D rb;
    private Camera cam;
    private float originalGS;
    private float slowGS;
    private Vector2 clampedPosition;
    private bool canShoot;
    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;
        originalGS = rb.gravityScale;
        slowGS = originalGS / 10;
        shootsLeft = maxShoots;
        canShoot = true;
        lr = GetComponent<LineRenderer>();
    }

    private void OnMouseDown()
    {
        if(shootsLeft > 0)
        {
            rb.gravityScale = slowGS;
            canShoot = true;
        }
        else
        {
            canShoot = false;
        }
    }

    private void OnMouseDrag()
    {
        if(!canShoot)
            return;
        Drag();
    }

    private void Drag()
    {
        Vector2 dragPosition = cam.ScreenToWorldPoint(Input.mousePosition);
        clampedPosition = dragPosition;
        float dragDistance = Vector2.Distance(transform.position, dragPosition);
        Vector2 actualPos = transform.position;
        if (dragDistance > maxDistance)
        {
            clampedPosition = actualPos + (dragPosition - actualPos).normalized * maxDistance;
        }
        lr.SetPosition(0, transform.position);
        lr.SetPosition(1, clampedPosition);
    }

    private void OnMouseUp()
    {
        if(!canShoot)
            return;
        Throw();
    }

    private void Throw()
    {
        rb.gravityScale = originalGS;
        Vector2 actualPos = transform.position;
        Vector2 throwVector = actualPos - clampedPosition;
        float distance = Vector2.Distance(actualPos, clampedPosition);
        force = Mathf.Clamp(distance / maxDistance, 0, 1) * maxForce;
        rb.AddForce(throwVector * force);
        shootsLeft--;
        lr.SetPosition(0, Vector2.zero);
        lr.SetPosition(1, Vector2.zero);
    }
}
