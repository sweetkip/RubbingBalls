using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class BallFeedback : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField] private bool squashAndStretch = true;
    [SerializeField, Range(0f, 0.4f)] private float movementStretch = 0.13f;
    [SerializeField, Range(0f, 0.4f)] private float aimSquash = 0.1f;
    [SerializeField, Range(0f, 0.4f)] private float launchStretch = 0.24f;
    [SerializeField, Range(0f, 0.4f)] private float impactSquash = 0.28f;
    [SerializeField, Min(0.1f)] private float fullStretchSpeed = 18f;
    [SerializeField, Min(1f)] private float recoverySpeed = 18f;
    [SerializeField, Min(0.1f)] private float wobbleFrequency = 22f;
    [SerializeField, Min(0.1f)] private float wobbleDamping = 11f;
    [SerializeField, Min(0.1f)] private float teleportDistance = 4f;

    [Header("Damage Flash")]
    [SerializeField, Min(0.01f)] private float damageFlashDuration = 1f;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.05f, 0.03f, 1f);
    [SerializeField, Range(0f, 1f)] private float damageFlashOpacity = 0.85f;

    [Header("Audio")]
    [SerializeField] private bool playSounds = true;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.65f;
    [SerializeField, Range(0f, 0.2f)] private float pitchVariation = 0.06f;
    [SerializeField] private AudioMixerGroup outputMixer;
    [SerializeField] private AudioClip launchSound;
    [SerializeField] private AudioClip marbleImpactSound;
    [SerializeField] private AudioClip wallImpactSound;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip respawnSound;
    private static Shader cachedFlashShader;

    public SpriteRenderer VisualRenderer { get; private set; }

    private Transform visualRoot;
    private SpriteRenderer flashRenderer;
    private Material flashMaterial;
    private AudioSource audioSource;
    private Color baseColor = Color.white;
    private Vector3 previousPosition;
    private Vector2 smoothVelocity;
    private Vector2 aimDirection = Vector2.right;
    private Vector2 kickDirection = Vector2.right;
    private float aimPower;
    private float kickAmount;
    private float kickStarted = -100f;
    private float flashUntil = -100f;
    private float lastDamageSoundTime = -100f;
    private float currentStretch = 1f;
    private float currentAngle;
    private bool isAiming;
    private bool sourceWasEnabled;
    private bool initialized;

    public void Initialize(SpriteRenderer defaultRenderer)
    {
        if (initialized)
            return;

        sourceRenderer = sourceRenderer != null ? sourceRenderer : defaultRenderer;
        if (sourceRenderer == null)
            return;

        initialized = true;
        sourceWasEnabled = sourceRenderer.enabled;
        baseColor = sourceRenderer.color;
        visualRoot = new GameObject("Ball Feedback Visual").transform;
        visualRoot.SetParent(sourceRenderer.transform, false);
        visualRoot.gameObject.layer = sourceRenderer.gameObject.layer;

        GameObject spriteObject = new GameObject("Sprite");
        spriteObject.transform.SetParent(visualRoot, false);
        spriteObject.layer = sourceRenderer.gameObject.layer;
        VisualRenderer = spriteObject.AddComponent<SpriteRenderer>();
        CopyRenderer(sourceRenderer, VisualRenderer);
        sourceRenderer.enabled = false;

        Shader shader = GetFlashShader();
        if (shader != null && shader.isSupported)
        {
            flashMaterial = new Material(shader);
            GameObject flashObject = new GameObject("Damage Flash");
            flashObject.transform.SetParent(spriteObject.transform, false);
            flashObject.layer = spriteObject.layer;
            flashRenderer = flashObject.AddComponent<SpriteRenderer>();
            CopyRenderer(VisualRenderer, flashRenderer);
            flashRenderer.sharedMaterial = flashMaterial;
            flashRenderer.sortingOrder = VisualRenderer.sortingOrder + 1;
            flashRenderer.enabled = false;
        }
        else
            Debug.LogWarning("BallFeedback: no se pudo cargar BallFeedback/BallFlash. Se usara un tinte rojo de respaldo.", this);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.loop = false;
        ResetFeedback();
    }

    public static Shader GetFlashShader()
    {
        if (cachedFlashShader == null)
            cachedFlashShader = Resources.Load<Shader>("BallFeedback/BallFlash");

        return cachedFlashShader;
    }

    public static void PreloadResources()
    {
        GetFlashShader();
    }

    private static void CopyRenderer(SpriteRenderer source, SpriteRenderer target)
    {
        target.sprite = source.sprite;
        target.color = source.color;
        target.sharedMaterials = source.sharedMaterials;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.drawMode = source.drawMode;
        target.size = source.size;
        target.tileMode = source.tileMode;
        target.adaptiveModeThreshold = source.adaptiveModeThreshold;
        target.maskInteraction = source.maskInteraction;
        target.spriteSortPoint = source.spriteSortPoint;
        target.enabled = source.enabled;
    }

    public void SetBaseColor(Color color)
    {
        baseColor = color;
        if (VisualRenderer != null)
            VisualRenderer.color = color;
    }

    public void SetAim(bool aiming, Vector2 direction, float power)
    {
        isAiming = aiming;
        aimPower = Mathf.Clamp01(power);
        if (direction.sqrMagnitude > 0.0001f)
            aimDirection = direction.normalized;
    }

    public void PlayLaunch(Vector2 direction, float power)
    {
        isAiming = false;
        Kick(direction, launchStretch * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(power)));
        PlaySound(launchSound, BallFeedbackSound.Launch, Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(power)));
    }

    public void PlayImpact(Vector2 normal, float speed, bool marble, bool withSound)
    {
        float strength = Mathf.InverseLerp(0.5f, fullStretchSpeed, speed);
        Kick(normal, -impactSquash * Mathf.Lerp(0.3f, 1f, strength));
        if (withSound)
            PlaySound(marble ? marbleImpactSound : wallImpactSound, marble ? BallFeedbackSound.MarbleImpact : BallFeedbackSound.WallImpact, Mathf.Lerp(0.3f, 1f, strength) * (marble ? 1f : 0.3f));
    }

    public void PlayDamage()
    {
        flashUntil = Time.time + Mathf.Max(0.01f, damageFlashDuration);
        if (Time.time - lastDamageSoundTime >= 0.05f)
        {
            lastDamageSoundTime = Time.time;
            PlaySound(damageSound, BallFeedbackSound.Damage, 0.75f);
        }
    }

    public void PlayRespawn()
    {
        ResetFeedback();
        Kick(Vector2.up, -impactSquash * 0.75f);
        PlaySound(respawnSound, BallFeedbackSound.Respawn, 0.8f);
    }

    private void Kick(Vector2 direction, float amount)
    {
        kickDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        kickAmount = amount;
        kickStarted = Time.time;
    }

    private void PlaySound(AudioClip customClip, BallFeedbackSound sound, float volume)
    {
        if (!playSounds || Application.isBatchMode || audioSource == null || soundVolume <= 0f)
            return;

        AudioClip clip = customClip != null ? customClip : BallFeedbackAudio.GetClip(sound);
        if (clip == null)
            return;

        audioSource.outputAudioMixerGroup = outputMixer;
        audioSource.pitch = Random.Range(1f - pitchVariation, 1f + pitchVariation);
        audioSource.PlayOneShot(clip, soundVolume * Mathf.Clamp01(volume));
    }

    private void LateUpdate()
    {
        if (!initialized || VisualRenderer == null || Time.deltaTime <= 0f)
            return;

        float dt = Time.deltaTime;
        Vector2 displacement = transform.position - previousPosition;
        previousPosition = transform.position;
        if (displacement.sqrMagnitude > teleportDistance * teleportDistance)
            smoothVelocity = Vector2.zero;
        else
            smoothVelocity = Vector2.Lerp(smoothVelocity, displacement / dt, 1f - Mathf.Exp(-20f * dt));

        UpdateDeformation(dt);
        UpdateFlash();
    }

    private void UpdateDeformation(float dt)
    {
        float speed = smoothVelocity.magnitude;
        float stretch = movementStretch * Mathf.Clamp01(speed / Mathf.Max(0.1f, fullStretchSpeed));
        Vector2 direction = speed > 0.1f ? smoothVelocity.normalized : kickDirection;
        if (isAiming)
        {
            stretch = -aimSquash * aimPower;
            direction = aimDirection;
        }

        float age = Mathf.Max(0f, Time.time - kickStarted);
        float envelope = Mathf.Exp(-Mathf.Max(0.1f, wobbleDamping) * age);
        if (envelope > 0.005f)
        {
            stretch += kickAmount * envelope * Mathf.Cos(age * wobbleFrequency);
            direction = kickDirection;
        }

        float blend = 1f - Mathf.Exp(-Mathf.Max(1f, recoverySpeed) * dt);
        currentStretch = Mathf.Lerp(currentStretch, squashAndStretch ? Mathf.Clamp(1f + stretch, 0.55f, 1.55f) : 1f, blend);
        Vector3 localDirection = visualRoot.parent.InverseTransformDirection(direction);
        float angle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
        currentAngle = Mathf.LerpAngle(currentAngle, angle, blend);
        visualRoot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        visualRoot.localScale = new Vector3(currentStretch, 1f / currentStretch, 1f);
        VisualRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -currentAngle);
    }

    private void UpdateFlash()
    {
        float remaining = flashUntil - Time.time;
        float fadeDuration = Mathf.Min(0.15f, Mathf.Max(0.01f, damageFlashDuration));
        float opacity = damageFlashOpacity * Mathf.Clamp01(remaining / fadeDuration);
        VisualRenderer.color = baseColor;
        if (flashRenderer == null)
        {
            if (remaining > 0f)
                VisualRenderer.color = Color.Lerp(baseColor, damageFlashColor, opacity);
            return;
        }

        flashRenderer.enabled = remaining > 0f && VisualRenderer.enabled;
        if (!flashRenderer.enabled)
            return;

        flashRenderer.sprite = VisualRenderer.sprite;
        flashRenderer.flipX = VisualRenderer.flipX;
        flashRenderer.flipY = VisualRenderer.flipY;
        flashRenderer.size = VisualRenderer.size;
        flashRenderer.sortingLayerID = VisualRenderer.sortingLayerID;
        flashRenderer.sortingOrder = VisualRenderer.sortingOrder + 1;
        Color color = damageFlashColor;
        color.a *= opacity * baseColor.a;
        flashRenderer.color = color;
    }

    public void ResetFeedback()
    {
        previousPosition = transform.position;
        smoothVelocity = Vector2.zero;
        isAiming = false;
        kickStarted = -100f;
        flashUntil = -100f;
        currentStretch = 1f;
        currentAngle = 0f;
        if (visualRoot != null)
        {
            visualRoot.localScale = Vector3.one;
            visualRoot.localRotation = Quaternion.identity;
        }
        if (VisualRenderer != null)
        {
            VisualRenderer.transform.localRotation = Quaternion.identity;
            VisualRenderer.color = baseColor;
        }
        if (flashRenderer != null)
            flashRenderer.enabled = false;
    }

    private void OnDisable()
    {
        ResetFeedback();
        if (audioSource != null)
            audioSource.Stop();
    }

    private void OnDestroy()
    {
        if (initialized && sourceRenderer != null)
            sourceRenderer.enabled = sourceWasEnabled;
        if (flashMaterial != null)
            Destroy(flashMaterial);
        if (visualRoot != null)
            Destroy(visualRoot.gameObject);
        if (audioSource != null)
            Destroy(audioSource);
    }
}
