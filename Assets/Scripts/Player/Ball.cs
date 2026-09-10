using Fusion;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    [SerializeField] private float force;
    [SerializeField] private float maxDistance;
    [SerializeField] private int maxShoots;
    [SerializeField] private float maxForce;
    [SerializeField] private LineRenderer lr;
    [SerializeField] private LineRenderer trajectoryLr;
    [SerializeField] private int WallLayer;
    [SerializeField] private int trajectoryResolution = 30;
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
    }
    public override void FixedUpdateNetwork()
    {
        Debug.Log("Me llaman cada tick");
        if (GetInput(out NetworkInputData data))
        {
            if (data.Buttons.IsSet((int)InputButton.Fire))
            {
                Debug.Log("Le dieron al boton y yo me entere");
                if (!Object.HasStateAuthority)
                    return;
                if(!wasPressed)
                {
                    Debug.Log("Se le dio al boton y no estaba apretado");
                    ButtonPressed();
                }
                else
                {
                    if(canShoot)
                    {
                        Debug.Log("Puedo disparar");
                        Drag();
                    }
                }
            }
            else
            {
                if(wasPressed)
                {
                    if(canShoot)
                    {
                        Debug.Log("Solte");
                        Throw();
                    }
                    Debug.Log("Solte peron no puedo disparar");
                }
                Debug.Log("Solte y nunca aprete");
            }
        }
    }


    private void ButtonPressed()
    {
        wasPressed = true;
        if (shootsLeft > 0)
        {
            rb.gravityScale = slowGS;
            canShoot = true;
        }
        else
        {
            canShoot = false;
        }
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
        ShowTrajectory();
    }

    private void Throw()
    {
        wasPressed = false;
        trajectoryLr.enabled = false;
        rb.gravityScale = originalGS;
        Vector2 actualPos = transform.position;
        Vector2 throwVector = actualPos - clampedPosition;
        float distance = Vector2.Distance(actualPos, clampedPosition);
        force = Mathf.Clamp(distance / maxDistance, 0, 1) * maxForce;
        rb.AddForce(throwVector * force);
        shootsLeft--;
        spriteRenderer.color = new Color(spriteRenderer.color.r * 0.75f, spriteRenderer.color.g * 0.75f, spriteRenderer.color.b * 0.75f, spriteRenderer.color.a);
        lr.SetPosition(0, Vector2.zero);
        lr.SetPosition(1, Vector2.zero);
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
}
