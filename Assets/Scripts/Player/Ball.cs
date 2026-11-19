using Fusion;
using System.Threading;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    [Networked] public int id { get; private set; }

    [Networked, OnChangedRender(nameof(UpdateBallColor))]
    public byte ColorIndex { get; private set; }

    [Networked, OnChangedRender(nameof(OnTeamIdChanged))]
    public byte TeamId { get; private set; }

    [Networked, OnChangedRender(nameof(UpdateBallSprite))]
    public byte SpriteIndex { get; private set; }

    [Networked, OnChangedRender(nameof(UpdateBallColor))]
    public int ShootsLeft { get; private set; }

    [Networked] public string ballName { get; private set; }
    [Networked] private NetworkBool IsAiming { get; set; }
    [Networked] private Vector2 AimOffset { get; set; }
    private byte lastCollisionTeam = 254;
    private bool lastCollisionImmune;
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
    private RoundRules rules;
    private PhysicsMaterial2D prefabMaterial, liveMaterial;
    private int DashLimit => rules.version == 1 ? rules.dashes : maxShoots;
    private float LaunchForce => rules.version == 1 ? rules.max_force : maxForce;
    private float DragDistance => rules.version == 1 ? rules.drag_distance : maxDistance;
    private float SpawnImmunity => rules.version == 1 ? rules.spawn_immunity : respawnPushImmunity;
    private float Gravity => rules.version == 1 ? rules.gravity : originalGS;
    private float AimGravity => Gravity * (rules.version == 1 ? rules.aim_gravity_ratio : 0.1f);

    private void ApplyRules()
    {
        if (rules.version == 1) return;
        rules = GameManager.CurrentRules;
        if (rules.version != 1) return;
        rb.gravityScale = Gravity;
        if (!liveMaterial)
            liveMaterial = prefabMaterial ? Instantiate(prefabMaterial) : new PhysicsMaterial2D("Round ball");
        liveMaterial.bounciness = rules.bounce;
        rb.sharedMaterial = liveMaterial;
    }

    private Vector2 clampedPosition;

    private bool canShoot;
    private bool wasPressed;

    [SerializeField, Min(0f)] private float respawnPushImmunity = 2f;
    [Networked] public TickTimer PushProtection { get; private set; }
    public bool IsPushImmune => Object != null && Object.IsValid && !PushProtection.ExpiredOrNotRunning(Runner);

    public static byte LocalTeam { get; private set; } = LobbyPlayer.NoTeam;

    public Collider2D[] PhysicsColliders { get; private set; }
    public PlayerColorPalette MigrationPalette => palette;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool previousProtection;
    private Vector3[] trajectoryPoints;
    private HealthController health;
    private BallFeedback feedback;
    private float lastImpactFeedbackTime = -100f;
    private bool hudDirty;

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

        prefabMaterial = rb.sharedMaterial;
    }

    private void Start()
    {
        canShoot = true;
        wasPressed = false;
    }

    public override void Spawned()
    {
        rules = default;
        rb.sharedMaterial = prefabMaterial;
        rb.gravityScale = Gravity;
        ApplyRules();
        if (Object.HasStateAuthority && (NetworkManager.Instance == null || !NetworkManager.Instance.IsMigrating))
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            TeamId = LobbyPlayer.NoTeam;
            id = GameManager.Instance.IJoined(Object.InputAuthority);
            ShootsLeft = DashLimit;
        }

        BallCollisionProtection.Register(this);

        UpdateBallColor();
        UpdateBallSprite();
        UpdateLocalIndicator();
        RememberLocalTeam();
        feedback.ResetFeedback();
        hudDirty = true;
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMigrating)
            SetMigrationPaused(true);
    }

    public override void Render()
    {
        ApplyRules();
        if (hudDirty)
        {
            hudDirty = false;

            if (health != null)
                health.RefreshHud();
        }
        if (!Object.HasInputAuthority)
            feedback.SetAim(IsAiming, AimOffset, AimOffset.magnitude / Mathf.Max(0.01f, DragDistance));

        RefreshCollisionRules();
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
        hudDirty = true;
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
        hudDirty = true;
    }

    public void SetTeam(byte team)
    {
        if (!Object.HasStateAuthority)
            return;

        TeamId = team;
        UpdateBallColor();
        hudDirty = true;
        BallCollisionProtection.Refresh(this);
        RememberLocalTeam();
    }



    private void RefreshCollisionRules()
    {
        bool immune = IsPushImmune;

        if (TeamId == lastCollisionTeam && immune == lastCollisionImmune)
            return;

        lastCollisionTeam = TeamId;
        lastCollisionImmune = immune;
        BallCollisionProtection.Refresh(this);
    }

    private void RememberLocalTeam()
    {
        if (Object != null && Object.IsValid && Object.HasInputAuthority)
            LocalTeam = TeamId;
    }

    public bool IsSameTeam(Ball other)
    {
        if (other == null || other == this)
            return false;

        if (Object == null || !Object.IsValid || other.Object == null || !other.Object.IsValid)
            return false;

        return TeamId <= LobbyPlayer.TeamBlue && TeamId == other.TeamId;
    }

    private void OnTeamIdChanged()
    {
        UpdateBallColor();
        hudDirty = true;
        RememberLocalTeam();
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
        hudDirty = true;
    }

    public override void FixedUpdateNetwork()
    {
        ApplyRules();
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMigrating) return;
        if (Object.HasStateAuthority && previousProtection != IsPushImmune)
        {
            previousProtection = IsPushImmune;
            BallCollisionProtection.Refresh(this);
        }
        if (GetInput(out NetworkInputData data))
        {
            if (!data.Buttons.IsSet(2)) NetworkManager.Instance?.RegisterLastInput(Object.InputAuthority, data);
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
                if (Object.HasStateAuthority) { rb.gravityScale = Gravity; IsAiming = false; }
                HideAimFeedback();
                return;
            }
            if (data.Buttons.IsSet((int)InputButton.Fire))
            {
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
                {
                    rb.gravityScale = Gravity;
                    IsAiming = false;
                    AimOffset = Vector2.zero;
                }
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
                rb.gravityScale = AimGravity;
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

        if (dragDistance > DragDistance)
            clampedPosition = actualPos + (dragPosition - actualPos).normalized * DragDistance;

        Vector2 feedbackOffset = actualPos - clampedPosition;
        if (Object.HasStateAuthority)
            AimOffset = feedbackOffset;
        feedback.SetAim(true, feedbackOffset, feedbackOffset.magnitude / Mathf.Max(0.01f, DragDistance));

        if (Object.HasInputAuthority)
        {
            if (lr != null)
            {
                lr.SetPosition(0, transform.position);
                lr.SetPosition(1, clampedPosition);
            }

            ShowTrajectory();
        }
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
        rb.gravityScale = Gravity;
        rb.linearVelocity = Vector2.zero;

        Vector2 actualPos = transform.position;
        Vector2 throwVector = actualPos - clampedPosition;
        float distance = Vector2.Distance(actualPos, clampedPosition);

        force = Mathf.Clamp(distance / DragDistance, 0f, 1f) * LaunchForce;
        rb.AddForce(throwVector * force);
        ShootsLeft = Mathf.Max(0, ShootsLeft - 1);
        RPC_PlayLaunchFeedback(throwVector, Mathf.Clamp01(distance / Mathf.Max(0.01f, DragDistance)));

        UpdateBallColor();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        if (collision.gameObject.layer == WallLayer)
        {
            ShootsLeft = DashLimit;
            UpdateBallColor();
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        if (collision.gameObject.layer == WallLayer)
        {
            if (ShootsLeft != DashLimit)
            {
                ShootsLeft = DashLimit;
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

        force = Mathf.Clamp(distance / DragDistance, 0f, 1f) * LaunchForce;

        Vector2 velocity = (throwVector * force) / 50f;
        Vector2 startPos = transform.position;

        for (int i = 0; i < trajectoryResolution; i++)
        {
            float t = i * Time.fixedDeltaTime;
            Vector2 pos = startPos + velocity * t + 0.5f * Physics2D.gravity * Gravity * t * t;
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

        Color baseColor = palette.GetPlayerColor(ColorIndex, TeamId);
        int usedShoots = Mathf.Clamp(DashLimit - ShootsLeft, 0, DashLimit);
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
        rb.gravityScale = Gravity;
        rb.position = spawnPosition;
        rb.angularVelocity = 0f;
        netTransform.Teleport(spawnPosition, spawnRotation);
        ShootsLeft = DashLimit;
        health.ResetHealth();
        rb.simulated = true;
        PushProtection = TickTimer.CreateFromSeconds(Runner, SpawnImmunity);
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
        if (liveMaterial) Destroy(liveMaterial);
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

    public void PlayTrampolineBounce(Vector2 trampolinePosition)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
            return;

        RPC_PlayTrampolineBounce(trampolinePosition);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Unreliable)]
    private void RPC_PlayTrampolineBounce(Vector2 trampolinePosition)
    {
        TrampolineHazard.PlayNear(trampolinePosition);
    }


    private void CancelAim()
    {
        wasPressed = false;

        if (Object.HasStateAuthority)
        {
            rb.gravityScale = Gravity;
            IsAiming = false;
        }

        HideAimFeedback();
    }

    public Sprite BaseSprite
    {
        get
        {
            if (palette == null)
                return null;

            return palette.GetSprite(SpriteIndex);
        }
    }

    public Color BaseColor
    {
        get
        {
            if (palette == null)
                return Color.white;

            return palette.GetPlayerColor(ColorIndex, TeamId);
        }
    }

    public void CaptureMigration(MigrationPlayerState state)
    {
        state.GameId = id;
        state.HasBall = true;
        state.Name = ballName;
        state.ColorIndex = ColorIndex;
        state.SpriteIndex = SpriteIndex;
        state.Team = TeamId;
        state.ShootsLeft = ShootsLeft;
        state.Position = rb.position;
        state.Position.z = transform.position.z;
        state.Rotation = Quaternion.Euler(0f, 0f, rb.rotation);
        state.Velocity = rb.linearVelocity;
        state.AngularVelocity = rb.angularVelocity;
        state.SpawnPosition = spawnPosition;
        state.SpawnRotation = spawnRotation;
        state.ProtectionRemaining = PushProtection.RemainingTime(Runner) ?? 0f;
        health.CaptureMigration(state);
    }

    public void RestoreMigration(MigrationPlayerState state)
    {
        if (!Object.HasStateAuthority) return;
        id = state.GameId;
        ballName = state.Name;
        ColorIndex = state.ColorIndex;
        SpriteIndex = state.SpriteIndex;
        TeamId = state.Team;
        RememberLocalTeam();
        ShootsLeft = state.ShootsLeft;
        spawnPosition = state.SpawnPosition;
        spawnRotation = state.SpawnRotation;
        health.RestoreMigration(state);
        rb.position = state.Position;
        rb.rotation = state.Rotation.eulerAngles.z;
        netTransform.Teleport(state.Position, state.Rotation);
        SetMigrationPaused(true);
        UpdateBallColor();
        UpdateBallSprite();
        hudDirty = true;
    }

    public void SetMigrationPaused(bool paused)
    {
        if (!paused) return;
        if (Object != null && Object.IsValid) CancelAim();
        else HideAimFeedback();
        rb.simulated = false;
    }

    public void ResumeMigrationPhysics()
    {
        ApplyRules();
        rb.gravityScale = Gravity;
        wasPressed = false;
        canShoot = false;
        HideAimFeedback();
        rb.simulated = true;
        hudDirty = true;
        health.RefreshHud();
    }

    public void FinishMigration(MigrationPlayerState state)
    {
        ApplyRules();
        rb.gravityScale = Gravity;
        wasPressed = false;
        canShoot = false;
        HideAimFeedback();
        if (Object.HasStateAuthority)
        {
            PushProtection = state.ProtectionRemaining > 0f ? TickTimer.CreateFromSeconds(Runner, state.ProtectionRemaining) : TickTimer.None;
            IsAiming = false;
            AimOffset = Vector2.zero;
        }
        rb.simulated = true;
        if (Object.HasStateAuthority) BallCollisionProtection.Register(this);
        rb.linearVelocity = state.Velocity;
        rb.angularVelocity = state.AngularVelocity;
        previousProtection = IsPushImmune;
        if (Object.HasStateAuthority) BallCollisionProtection.Refresh(this);
        hudDirty = true;
        health.RefreshHud();
    }

    public void MarkHudDirty()
    {
        hudDirty = true;
    }
}