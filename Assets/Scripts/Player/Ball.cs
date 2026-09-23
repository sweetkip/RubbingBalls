using Fusion;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    [Networked] public int id { get; private set; }
    [SerializeField] private float force;
    [SerializeField] private float maxDistance;
    [SerializeField] private int maxShoots;
    [SerializeField] private float maxForce;
    [SerializeField] private LineRenderer lr;
    [SerializeField] private LineRenderer trajectoryLr;
    [SerializeField] private int WallLayer;
    [SerializeField] private int trajectoryResolution = 30;

    NetworkTransform netTransform;
    private SpriteRenderer spriteRenderer;
    private int shootsLeft;
    private Rigidbody2D rb;
    private Camera cam;
    private float originalGS;
    private float slowGS;
    private Vector2 clampedPosition;
    private bool canShoot;
    private Color originalColor;
    private bool wasPressed;
    private HealthController health;
    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;
        originalGS = rb.gravityScale;
        slowGS = originalGS / 10;
        shootsLeft = maxShoots;
        canShoot = true;
        lr = GetComponent<LineRenderer>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
        wasPressed = false;
        health = GetComponent<HealthController>();
        netTransform = this.GetComponent<NetworkTransform>();
    }

    public override void Spawned()
    {
        id = GameManager.Instance.IJoined();
    }
    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            if (data.Buttons.IsSet((int)InputButton.Fire))
            {
                if (!wasPressed)
                {
                    ButtonPressed();
                }
                else
                {
                    if (canShoot)
                    {
                        Drag(data.AimWorldPosition);
                    }
                }
            }
            else
            {
                if (wasPressed && canShoot)
                {
                    Throw();
                }
                wasPressed = false;
                trajectoryLr.enabled = false;
            }
        }
    }

    private void ButtonPressed()
    {
        wasPressed = true;
        if (shootsLeft > 0)
        {
            canShoot = true;
            if (Object.HasStateAuthority)
            {
                rb.gravityScale = slowGS;
            }
        }
        else
        {
            canShoot = false;
        }
    }

    private void Drag(Vector2 aimWorldPosition)
    {
        Vector2 dragPosition = aimWorldPosition;
        clampedPosition = dragPosition;
        float dragDistance = Vector2.Distance(transform.position, dragPosition);
        Vector2 actualPos = transform.position;
        if (dragDistance > maxDistance)
        {
            clampedPosition = actualPos + (dragPosition - actualPos).normalized * maxDistance;
        }
        lr.SetPosition(0, transform.position);
        lr.SetPosition(1, clampedPosition);
        ShowTrajectory();
    }

    private void Throw()
    {
        wasPressed = false;
        trajectoryLr.enabled = false;
        lr.SetPosition(0, Vector2.zero);
        lr.SetPosition(1, Vector2.zero);

        if (!Object.HasStateAuthority)
            return; // solo el host aplica la física real

        rb.gravityScale = originalGS;
        Vector2 actualPos = transform.position;
        Vector2 throwVector = actualPos - clampedPosition;
        float distance = Vector2.Distance(actualPos, clampedPosition);
        force = Mathf.Clamp(distance / maxDistance, 0, 1) * maxForce;
        rb.AddForce(throwVector * force);
        shootsLeft--;
        spriteRenderer.color = new Color(spriteRenderer.color.r * 0.75f, spriteRenderer.color.g * 0.75f, spriteRenderer.color.b * 0.75f, spriteRenderer.color.a);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision != null && collision.gameObject.layer == WallLayer)
        {
            spriteRenderer.color = originalColor;
            shootsLeft = maxShoots;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision != null && collision.gameObject.layer == WallLayer)
        {
            spriteRenderer.color = originalColor;
            shootsLeft = maxShoots;
        }
    }

    private void ShowTrajectory()
    {
        trajectoryLr.enabled = true;
        trajectoryLr.positionCount = trajectoryResolution;

        Vector3[] points = new Vector3[trajectoryResolution];

        Vector2 actualPos = transform.position;
        Vector2 throwVector = actualPos - clampedPosition;
        float distance = Vector2.Distance(actualPos, clampedPosition);
        force = Mathf.Clamp(distance / maxDistance, 0, 1) * maxForce;
        Vector2 velocity = (throwVector * force) / 50;

        Vector2 startPos = transform.position;
        for (int i = 0; i < trajectoryResolution; i++)
        {
            float t = i * Time.fixedDeltaTime;
            Vector2 pos = startPos + velocity * t + 0.5f * Physics2D.gravity * t * t;
            points[i] = pos;
        }
        trajectoryLr.SetPositions(points);
    }

    public void Respawn()
    {
        if (!Object.HasStateAuthority)
            return;
        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;
        netTransform.Teleport(Vector3.zero);
        shootsLeft = maxShoots;
        spriteRenderer.color = originalColor;
        health.ResetHealth();
        rb.simulated = true;
    }
}
