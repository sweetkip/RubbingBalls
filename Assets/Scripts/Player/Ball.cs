using Fusion;
using System.Threading;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    [Networked] public int id { get; private set; }

    [Networked, OnChangedRender(nameof(UpdateBallColor))]
    public byte ColorIndex { get; private set; }

    [Networked, OnChangedRender(nameof(UpdateBallSprite))]
    public byte SpriteIndex { get; private set; }

    [Networked, OnChangedRender(nameof(UpdateBallColor))]
    public int ShootsLeft { get; private set; }

    [Networked] public string ballName { get; private set; }
    [Networked] private NetworkBool IsAiming { get; set; }
    [Networked] private Vector2 AimOffset { get; set; }

    [SerializeField] private PlayerColorPalette palette;

    [SerializeField] private float force;
    [SerializeField] private float maxDistance;
    [SerializeField] private int maxShoots;
    [SerializeField] private float maxForce;

    [SerializeField] private LineRenderer lr;
    [SerializeField] private LineRenderer trajectoryLr;

    [SerializeField] private int WallLayer;
    [SerializeField] private int trajectoryResolution = 30;

    [SerializeField] private GameObject localIndicator;
    [SerializeField] private KeyCode toggleIndicatorKey = KeyCode.Tab;

    private bool localIndicatorEnabled = true;

    private NetworkTransform netTransform;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;

    private float originalGS;
    private float slowGS;

    private Vector2 clampedPosition;

    private bool canShoot;
    private bool wasPressed;

    [SerializeField, Min(0f)] private float respawnPushImmunity = 2f;
    [Networked] public TickTimer PushProtection { get; private set; }
    public bool IsPushImmune => Object != null && Object.IsValid && !PushProtection.ExpiredOrNotRunning(Runner);
    public Collider2D[] PhysicsColliders { get; private set; }
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool previousProtection;
    private Vector3[] trajectoryPoints;
    private HealthController health;
    private BallFeedback feedback;
    private float lastImpactFeedbackTime = -100f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        spriteRenderer = GetComponent<SpriteRenderer>();

        health = GetComponent<HealthController>();

        netTransform = GetComponent<NetworkTransform>();
        feedback = GetComponent<BallFeedback>();
        if (feedback == null)
            feedback = gameObject.AddComponent<BallFeedback>();
        feedback.Initialize(spriteRenderer);
        if (feedback.VisualRenderer != null)
            spriteRenderer = feedback.VisualRenderer;

        if (lr == null)
            lr = GetComponent<LineRenderer>();

        PhysicsColliders = GetComponents<Collider2D>();
        originalGS = rb.gravityScale;

        slowGS = originalGS / 10f;
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
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            BallCollisionProtection.Register(this);
            id = GameManager.Instance.IJoined(Object.InputAuthority);
            ShootsLeft = maxShoots;
        }

        UpdateBallColor();
        UpdateBallSprite();
        UpdateLocalIndicator();
        feedback.ResetFeedback();
    }

    public override void Render()
    {
        if (!Object.HasInputAuthority)
            feedback.SetAim(IsAiming, AimOffset, AimOffset.magnitude / Mathf.Max(0.01f, maxDistance));
    }

    private void Update()
    {
        if (Object == null)
            return;

        if (!Object.HasInputAuthority)
            return;

        if (Input.GetKeyDown(toggleIndicatorKey))
        {
            localIndicatorEnabled = !localIndicatorEnabled;
            UpdateLocalIndicator();
        }
    }

    private void UpdateLocalIndicator()
    {
        if (localIndicator == null)
            return;

        localIndicator.SetActive(Object.HasInputAuthority && localIndicatorEnabled);
    }


    public void SetInitialColor(byte colorIndex)
    {
        if (!Object.HasStateAuthority)
            return;

        if (palette == null || palette.CountColors == 0)
            return;

        if (colorIndex >= palette.CountColors)
            colorIndex = 0;

        ColorIndex = colorIndex;
        UpdateBallColor();
    }

    public void SetInitialSprite(byte spriteIndex)
    {
        if (!Object.HasStateAuthority)
            return;

        if (palette == null || palette.CountSprites == 0)
            return;

        if (spriteIndex >= palette.CountSprites)
            spriteIndex = 0;

        SpriteIndex = spriteIndex;
        UpdateBallSprite();
    }

    public void SetName(string name)
    {
        if (!Object.HasStateAuthority)
            return;
        this.ballName = name;
        if(string.IsNullOrWhiteSpace(this.ballName))
        {
            this.ballName = "NoName";
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && previousProtection != IsPushImmune)
        {
            previousProtection = IsPushImmune;
            BallCollisionProtection.Refresh(this);
        }
        if (GetInput(out NetworkInputData data))
        {
            GameManager manager = GameManager.Instance;

            if (manager != null && !manager.CanPlay)
            {
                CancelAim();
                return;
            }

            if (data.Buttons.IsSet(1))
            {
                wasPressed = false;
                canShoot = false;
                if (Object.HasStateAuthority) { rb.gravityScale = originalGS; IsAiming = false; }
                HideAimFeedback();
                return;
            }
            if (data.Buttons.IsSet((int)InputButton.Fire))
            {
                if (!data.Buttons.IsSet(2)) NetworkManager.Instance?.RegisterLastInput(Object.InputAuthority, data);
                if (!wasPressed)
                    ButtonPressed();
                else
                {
                    if (canShoot)
                    {
                        Vector2 ballPosition = transform.position;
                        Drag(ballPosition + data.AimOffset);
                    }
                }
            }
            else
            {
                if (wasPressed && canShoot)
                    Throw();

                wasPressed = false;
                if (Object.HasStateAuthority)
                    IsAiming = false;
                feedback.SetAim(false, Vector2.zero, 0f);

                if (trajectoryLr != null)
                    trajectoryLr.enabled = false;
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
                rb.gravityScale = slowGS;
        }
        else
            canShoot = false;

        if (Object.HasStateAuthority)
            IsAiming = canShoot;
    }

    private void Drag(Vector2 aimWorldPosition)
    {
        Vector2 dragPosition = aimWorldPosition;

        clampedPosition = dragPosition;

        float dragDistance = Vector2.Distance(transform.position, dragPosition);

        Vector2 actualPos = transform.position;

        if (dragDistance > maxDistance)
            clampedPosition = actualPos + (dragPosition - actualPos).normalized * maxDistance;

        Vector2 feedbackOffset = actualPos - clampedPosition;
        if (Object.HasStateAuthority)
            AimOffset = feedbackOffset;
        feedback.SetAim(true, feedbackOffset, feedbackOffset.magnitude / Mathf.Max(0.01f, maxDistance));

        if (lr != null)
        {
            lr.SetPosition(0, transform.position);
            lr.SetPosition(1, clampedPosition);
        }

        ShowTrajectory();
    }

    private void Throw()
    {
        wasPressed = false;
        feedback.SetAim(false, Vector2.zero, 0f);

        if (trajectoryLr != null)
            trajectoryLr.enabled = false;

        if (lr != null)
        {
            lr.SetPosition(0, Vector2.zero);
            lr.SetPosition(1, Vector2.zero);
        }

        if (!Object.HasStateAuthority)
            return;

        IsAiming = false;
        AimOffset = Vector2.zero;
        rb.gravityScale = originalGS;
        rb.linearVelocity = Vector2.zero;

        Vector2 actualPos = transform.position;
        Vector2 throwVector = actualPos - clampedPosition;
        float distance = Vector2.Distance(actualPos, clampedPosition);

        force = Mathf.Clamp(distance / maxDistance, 0f, 1f) * maxForce;
        rb.AddForce(throwVector * force);
        ShootsLeft = Mathf.Max(0, ShootsLeft - 1);
        RPC_PlayLaunchFeedback(throwVector, Mathf.Clamp01(distance / Mathf.Max(0.01f, maxDistance)));

        UpdateBallColor();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        if (collision.gameObject.layer == WallLayer)
        {
            ShootsLeft = maxShoots;
            UpdateBallColor();
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        if (collision.gameObject.layer == WallLayer)
        {
            if (ShootsLeft != maxShoots)
            {
                ShootsLeft = maxShoots;
                UpdateBallColor();
            }
        }
    }

    private void ShowTrajectory()
    {
        if (trajectoryLr == null)
            return;

        trajectoryLr.enabled = true;

        trajectoryLr.positionCount = trajectoryResolution;

        if (trajectoryPoints == null || trajectoryPoints.Length != trajectoryResolution)
            trajectoryPoints = new Vector3[trajectoryResolution];
        Vector3[] points = trajectoryPoints;
        Vector2 actualPos = transform.position;
        Vector2 throwVector = actualPos - clampedPosition;

        float distance = Vector2.Distance(actualPos, clampedPosition);

        force = Mathf.Clamp(distance / maxDistance, 0f, 1f) * maxForce;

        Vector2 velocity = (throwVector * force) / 50f;
        Vector2 startPos = transform.position;

        for (int i = 0; i < trajectoryResolution; i++)
        {
            float t = i * Time.fixedDeltaTime;
            Vector2 pos = startPos + velocity * t + 0.5f * Physics2D.gravity * t * t;
            points[i] = pos;
        }

        trajectoryLr.SetPositions(points);
    }

    private void UpdateBallColor()
    {
        if (spriteRenderer == null)
            return;

        if (palette == null || palette.CountColors == 0)
            return;

        Color baseColor = palette.GetColor(ColorIndex);
        int usedShoots = Mathf.Clamp(maxShoots - ShootsLeft, 0, maxShoots);
        float darkness = Mathf.Pow(0.75f, usedShoots);

        feedback.SetBaseColor(new Color(baseColor.r * darkness, baseColor.g * darkness, baseColor.b * darkness, baseColor.a));
    }

    private void UpdateBallSprite()
    {
        if (spriteRenderer == null)
            return;

        if (palette == null || palette.CountSprites == 0)
            return;
        spriteRenderer.sprite = palette.GetSprite(SpriteIndex);
    }

    public int GetLives()
    {
        return health.Lives;
    }

    public void Respawn()
    {
        if (!Object.HasStateAuthority)
            return;

        health.Lives -= 1;
        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = originalGS;
        rb.position = spawnPosition;
        rb.angularVelocity = 0f;
        netTransform.Teleport(spawnPosition, spawnRotation);
        ShootsLeft = maxShoots;
        health.ResetHealth();
        rb.simulated = true;
        PushProtection = TickTimer.CreateFromSeconds(Runner, respawnPushImmunity);
        previousProtection = IsPushImmune;
        BallCollisionProtection.Refresh(this);
        UpdateBallColor();
        RPC_PlayRespawnFeedback();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        BallCollisionProtection.Unregister(this);
    }

    private void OnDestroy()
    {
        BallCollisionProtection.Unregister(this);
    }

    private void HideAimFeedback()
    {
        if (lr != null) { lr.SetPosition(0, Vector3.zero); lr.SetPosition(1, Vector3.zero); }
        if (trajectoryLr != null) trajectoryLr.enabled = false;
        feedback.SetAim(false, Vector2.zero, 0f);
    }

    public void ReportImpact(Vector2 normal, float speed, Ball other = null)
    {
        if (!Object.HasStateAuthority || speed < 0.75f) return;
        RPC_PlayImpactFeedback(normal, speed, other != null, true);
        if (other != null) other.RPC_PlayImpactFeedback(-normal, speed, true, false);
    }

    public void ReportDamage()
    {
        if (Object != null && Object.IsValid && Object.HasStateAuthority)
            RPC_PlayDamageFeedback();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Unreliable)]
    private void RPC_PlayLaunchFeedback(Vector2 direction, float power)
    {
        feedback.PlayLaunch(direction, power);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Unreliable)]
    private void RPC_PlayImpactFeedback(Vector2 normal, float impactSpeed, bool marble, bool withSound)
    {
        feedback.PlayImpact(normal, impactSpeed, marble, withSound);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayDamageFeedback()
    {
        feedback.PlayDamage();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayRespawnFeedback()
    {
        feedback.PlayRespawn();
    }

    private void CancelAim()
    {
        wasPressed = false;

        if (Object.HasStateAuthority)
        {
            rb.gravityScale = originalGS;
            IsAiming = false;
        }

        HideAimFeedback();
    }
}