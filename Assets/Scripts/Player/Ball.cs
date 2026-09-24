using Fusion;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    [Networked]
    public int id { get; private set; }

    [Networked, OnChangedRender(nameof(UpdateBallColor))]
    public byte ColorIndex { get; private set; }

    [Networked, OnChangedRender(nameof(UpdateBallColor))]
    public int ShootsLeft { get; private set; }

    [SerializeField] private PlayerColorPalette palette;

    [SerializeField] private float force;
    [SerializeField] private float maxDistance;
    [SerializeField] private int maxShoots;
    [SerializeField] private float maxForce;

    [SerializeField] private LineRenderer lr;
    [SerializeField] private LineRenderer trajectoryLr;

    [SerializeField] private int WallLayer;
    [SerializeField] private int trajectoryResolution = 30;

    private NetworkTransform netTransform;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;

    private float originalGS;
    private float slowGS;

    private Vector2 clampedPosition;

    private bool canShoot;
    private bool wasPressed;

    private HealthController health;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        spriteRenderer =
            GetComponent<SpriteRenderer>();

        health =
            GetComponent<HealthController>();

        netTransform =
            GetComponent<NetworkTransform>();

        if (lr == null)
        {
            lr =
                GetComponent<LineRenderer>();
        }

        originalGS =
            rb.gravityScale;

        slowGS =
            originalGS / 10f;
    }

    private void Start()
    {
        canShoot = true;
        wasPressed = false;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            id =
                GameManager.Instance.IJoined();

            ShootsLeft =
                maxShoots;
        }

        UpdateBallColor();
    }

    public void SetInitialColor(
        byte colorIndex)
    {
        if (!Object.HasStateAuthority)
            return;

        if (palette == null ||
            palette.Count == 0)
            return;

        if (colorIndex >= palette.Count)
            colorIndex = 0;

        ColorIndex = colorIndex;

        UpdateBallColor();
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(
                out NetworkInputData data))
        {
            if (data.Buttons.IsSet(
                    (int)InputButton.Fire))
            {
                if (!wasPressed)
                {
                    ButtonPressed();
                }
                else
                {
                    if (canShoot)
                    {
                        Drag(
                            data.AimWorldPosition
                        );
                    }
                }
            }
            else
            {
                if (wasPressed &&
                    canShoot)
                {
                    Throw();
                }

                wasPressed = false;

                if (trajectoryLr != null)
                {
                    trajectoryLr.enabled =
                        false;
                }
            }
        }
    }

    private void ButtonPressed()
    {
        wasPressed = true;

        if (ShootsLeft > 0)
        {
            canShoot = true;

            if (Object.HasStateAuthority)
            {
                rb.gravityScale =
                    slowGS;
            }
        }
        else
        {
            canShoot = false;
        }
    }

    private void Drag(
        Vector2 aimWorldPosition)
    {
        Vector2 dragPosition =
            aimWorldPosition;

        clampedPosition =
            dragPosition;

        float dragDistance =
            Vector2.Distance(
                transform.position,
                dragPosition
            );

        Vector2 actualPos =
            transform.position;

        if (dragDistance > maxDistance)
        {
            clampedPosition =
                actualPos +
                (
                    dragPosition -
                    actualPos
                ).normalized *
                maxDistance;
        }

        if (lr != null)
        {
            lr.SetPosition(
                0,
                transform.position
            );

            lr.SetPosition(
                1,
                clampedPosition
            );
        }

        ShowTrajectory();
    }

    private void Throw()
    {
        wasPressed = false;

        if (trajectoryLr != null)
        {
            trajectoryLr.enabled =
                false;
        }

        if (lr != null)
        {
            lr.SetPosition(
                0,
                Vector2.zero
            );

            lr.SetPosition(
                1,
                Vector2.zero
            );
        }

        if (!Object.HasStateAuthority)
            return;

        rb.gravityScale =
            originalGS;

        Vector2 actualPos =
            transform.position;

        Vector2 throwVector =
            actualPos -
            clampedPosition;

        float distance =
            Vector2.Distance(
                actualPos,
                clampedPosition
            );

        force =
            Mathf.Clamp(
                distance / maxDistance,
                0f,
                1f
            ) *
            maxForce;

        rb.AddForce(
            throwVector * force
        );

        ShootsLeft =
            Mathf.Max(
                0,
                ShootsLeft - 1
            );

        UpdateBallColor();
    }

    private void OnCollisionEnter2D(
        Collision2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        if (collision.gameObject.layer ==
            WallLayer)
        {
            ShootsLeft =
                maxShoots;

            UpdateBallColor();
        }
    }

    private void OnCollisionStay2D(
        Collision2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        if (collision.gameObject.layer ==
            WallLayer)
        {
            if (ShootsLeft != maxShoots)
            {
                ShootsLeft =
                    maxShoots;

                UpdateBallColor();
            }
        }
    }

    private void ShowTrajectory()
    {
        if (trajectoryLr == null)
            return;

        trajectoryLr.enabled =
            true;

        trajectoryLr.positionCount =
            trajectoryResolution;

        Vector3[] points =
            new Vector3[
                trajectoryResolution
            ];

        Vector2 actualPos =
            transform.position;

        Vector2 throwVector =
            actualPos -
            clampedPosition;

        float distance =
            Vector2.Distance(
                actualPos,
                clampedPosition
            );

        force =
            Mathf.Clamp(
                distance / maxDistance,
                0f,
                1f
            ) *
            maxForce;

        Vector2 velocity =
            (
                throwVector *
                force
            ) /
            50f;

        Vector2 startPos =
            transform.position;

        for (
            int i = 0;
            i < trajectoryResolution;
            i++)
        {
            float t =
                i *
                Time.fixedDeltaTime;

            Vector2 pos =
                startPos +
                velocity * t +
                0.5f *
                Physics2D.gravity *
                t *
                t;

            points[i] = pos;
        }

        trajectoryLr.SetPositions(
            points
        );
    }

    private void UpdateBallColor()
    {
        if (spriteRenderer == null)
            return;

        if (palette == null ||
            palette.Count == 0)
            return;

        Color baseColor =
            palette.GetColor(
                ColorIndex
            );

        int usedShoots =
            Mathf.Clamp(
                maxShoots -
                ShootsLeft,
                0,
                maxShoots
            );

        float darkness =
            Mathf.Pow(
                0.75f,
                usedShoots
            );

        spriteRenderer.color =
            new Color(
                baseColor.r *
                darkness,

                baseColor.g *
                darkness,

                baseColor.b *
                darkness,

                baseColor.a
            );
    }

    public void Respawn()
    {
        if (!Object.HasStateAuthority)
            return;

        rb.simulated =
            false;

        rb.linearVelocity =
            Vector2.zero;

        rb.gravityScale =
            originalGS;

        netTransform.Teleport(
            Vector3.zero
        );

        ShootsLeft =
            maxShoots;

        health.ResetHealth();

        rb.simulated =
            true;

        UpdateBallColor();
    }
}