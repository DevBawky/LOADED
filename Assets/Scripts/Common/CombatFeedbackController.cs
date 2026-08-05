using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatFeedbackController : MonoBehaviour
{
    private const int MaxFullscreenImpacts = 4;
    private const string FeedbackPanelName = "Panel | Feedback";
    private const string ComboTextName = "Text | Combo";
    private const string ComboTimerName = "Image | Combo Timer";
    private const string CurrentDamageTextName = "Text | Current Damage";
    private static readonly int FullscreenCentersId =
        Shader.PropertyToID("_KillImpactCenters");
    private static readonly int FullscreenDirectionsId =
        Shader.PropertyToID("_KillImpactDirections");
    private static readonly int FullscreenParamsId =
        Shader.PropertyToID("_KillImpactParams");
    private static readonly int FullscreenColorId =
        Shader.PropertyToID("_KillImpactColor");
    private static readonly int FullscreenIntensityId =
        Shader.PropertyToID("_KillImpactIntensity");
    private static readonly int FullscreenAspectId =
        Shader.PropertyToID("_KillImpactAspect");
    private static readonly int FullscreenShockwaveId =
        Shader.PropertyToID("_KillImpactShockwave");
    private static readonly int FullscreenRgbSplitId =
        Shader.PropertyToID("_KillImpactRgbSplit");
    private static readonly int FullscreenRadialZoomId =
        Shader.PropertyToID("_KillImpactRadialZoom");
    private static readonly int FullscreenTearId =
        Shader.PropertyToID("_KillImpactTear");

    private struct FullscreenImpactState
    {
        public bool Active;
        public Vector2 Center;
        public Vector2 Direction;
        public float Elapsed;
        public float Duration;
        public float Intensity;
        public bool Critical;
        public bool FinalKill;
    }

    [Header("Combo")]
    [Min(0.25f)]
    [SerializeField] private float comboDuration = 3.5f;
    [SerializeField] private Color comboLowColor = Color.white;
    [SerializeField] private Color comboHighColor =
        new Color(1f, 0.42f, 0.12f, 1f);
    [SerializeField] private Color timerDangerColor =
        new Color(1f, 0.18f, 0.08f, 1f);

    [Header("Kill Motion")]
    [Range(0.05f, 1f)]
    [SerializeField] private float killSlowMotionScale = 0.32f;
    [Min(0f)]
    [SerializeField] private float killSlowMotionHold = 0.09f;
    [Min(0.01f)]
    [SerializeField] private float killSlowMotionRecovery = 0.18f;
    [Min(0f)]
    [SerializeField] private float killCameraShake = 0.055f;

    [Header("Volume Pulse")]
    [Min(0.01f)]
    [SerializeField] private float volumePulseDuration = 0.34f;
    [Range(0f, 1f)]
    [SerializeField] private float chromaticBoost = 0.18f;
    [Min(0f)]
    [SerializeField] private float bloomBoost = 1.35f;
    [Range(0f, 1f)]
    [SerializeField] private float vignetteBoost = 0.09f;
    [Range(-1f, 1f)]
    [SerializeField] private float lensDistortionBoost = -0.075f;
    [Range(0f, 50f)]
    [SerializeField] private float contrastBoost = 11f;

    [Header("Fullscreen Impact")]
    [SerializeField] private bool fullscreenImpactEnabled = true;
    [Min(0.05f)]
    [SerializeField] private float fullscreenImpactDuration = 0.42f;
    [Range(0f, 2f)]
    [SerializeField] private float shockwaveStrength = 1f;
    [Range(0f, 2f)]
    [SerializeField] private float rgbSplitStrength = 1f;
    [Range(0f, 2f)]
    [SerializeField] private float radialZoomStrength = 0.85f;
    [Range(0f, 2f)]
    [SerializeField] private float directionalTearStrength = 0.72f;
    [SerializeField] private Color fullscreenImpactColor =
        new Color(1f, 0.3f, 0.06f, 1f);

    [Header("Hit Feedback")]
    [Min(0.05f)]
    [SerializeField] private float hitFullscreenDuration = 0.14f;
    [Range(0f, 1f)]
    [SerializeField] private float minimumHitIntensity = 0.18f;
    [Min(0f)]
    [SerializeField] private float hitCameraShake = 0.018f;

    [Header("Audio")]
    [SerializeField] private AudioClip hitAccentClip;
    [SerializeField] private AudioClip criticalAccentClip;
    [SerializeField] private AudioClip killAccentClip;
    [Range(0f, 1f)]
    [SerializeField] private float hitAccentVolume = 0.72f;
    [Range(0f, 1f)]
    [SerializeField] private float criticalAccentVolume = 0.9f;
    [Range(0f, 1f)]
    [SerializeField] private float killAccentVolume = 0.95f;

    private TMP_Text comboText;
    private TMP_Text currentDamageText;
    private Image comboTimer;
    private CanvasGroup comboCanvasGroup;
    private CanvasGroup comboTimerCanvasGroup;
    private CanvasGroup damageCanvasGroup;
    private RectTransform comboRect;
    private RectTransform damageRect;
    private Vector3 comboBaseScale = Vector3.one;
    private Vector3 damageBaseScale = Vector3.one;
    private Quaternion comboBaseRotation = Quaternion.identity;
    private Quaternion damageBaseRotation = Quaternion.identity;
    private Color damageBaseColor = Color.white;
    private Color timerBaseColor = Color.white;

    private int comboCount;
    private int cylinderDamage;
    private float displayedCylinderDamage;
    private float comboRemaining;
    private float damageHoldRemaining;
    private float comboPunchRemaining;
    private float damagePunchRemaining;
    private float overkillFlashRemaining;
    private bool cylinderActive;
    private bool uiBound;

    private Volume cameraVolume;
    private ChromaticAberration chromaticAberration;
    private Bloom bloom;
    private Vignette vignette;
    private LensDistortion lensDistortion;
    private ColorAdjustments colorAdjustments;
    private bool chromaticBaseActive;
    private bool bloomBaseActive;
    private bool vignetteBaseActive;
    private bool lensBaseActive;
    private bool colorBaseActive;
    private float chromaticBase;
    private float bloomBase;
    private float vignetteBase;
    private float lensBase;
    private float contrastBase;
    private bool chromaticBaseOverride;
    private bool bloomBaseOverride;
    private bool vignetteBaseOverride;
    private bool lensBaseOverride;
    private bool contrastBaseOverride;
    private Coroutine volumePulseCoroutine;
    private Coroutine slowMotionCoroutine;
    private Coroutine audioFilterCoroutine;
    private readonly FullscreenImpactState[] fullscreenImpacts =
        new FullscreenImpactState[MaxFullscreenImpacts];
    private readonly Vector4[] fullscreenCenters =
        new Vector4[MaxFullscreenImpacts];
    private readonly Vector4[] fullscreenDirections =
        new Vector4[MaxFullscreenImpacts];
    private readonly Vector4[] fullscreenParams =
        new Vector4[MaxFullscreenImpacts];

    private AudioSource audioSource;
    private AudioSource accentAudioSource;
    private AudioLowPassFilter lowPassFilter;
    private bool createdLowPassFilter;
    private bool lowPassBaseEnabled;
    private float lowPassBaseCutoff = 22000f;
    private float slowMotionBaseScale = 1f;
    private bool ownsTimeScale;

    public int ComboCount => comboCount;
    public int CylinderDamage => cylinderDamage;

    private void Awake()
    {
        BindUi();
        BindVolume();
        InitializeAudio();
    }

    private void Start()
    {
        BindUi();
        RefreshUiImmediate();
    }

    private void Update()
    {
        if (!uiBound)
        {
            BindUi();
        }

        float deltaTime = Time.unscaledDeltaTime;
        UpdateCombo(deltaTime);
        UpdateDamage(deltaTime);
        UpdateFullscreenImpacts(deltaTime);
        AnimateUi(deltaTime);
    }

    private void OnDisable()
    {
        CancelSlowMotionAndRestore();
        RestoreVolume();
        RestoreAudioFilter();
        ResetFullscreenImpact();
        ResetUiTransforms();
    }

    private void OnDestroy()
    {
        CancelSlowMotionAndRestore();
        DestroyRuntimeClip(hitAccentClip, "Runtime Hit Accent");
        DestroyRuntimeClip(criticalAccentClip, "Runtime Critical Accent");
        DestroyRuntimeClip(killAccentClip, "Runtime Kill Accent");

        if (createdLowPassFilter && lowPassFilter != null)
        {
            Destroy(lowPassFilter);
        }
    }

    public void BeginCylinder()
    {
        cylinderActive = true;

        if (comboCount <= 0)
        {
            cylinderDamage = 0;
            displayedCylinderDamage = 0f;
            damageHoldRemaining = 0f;
            overkillFlashRemaining = 0f;
            UpdateDamageText(false);
        }

        if (damageCanvasGroup != null)
        {
            damageCanvasGroup.alpha = 1f;
        }
    }

    public void EndCylinder()
    {
        cylinderActive = false;
        damageHoldRemaining = cylinderDamage > 0 ? 1.35f : 0.3f;
    }

    public void ResetCombo()
    {
        comboCount = 0;
        comboRemaining = 0f;
        cylinderDamage = 0;
        displayedCylinderDamage = 0f;
        damageHoldRemaining = 0f;
        comboPunchRemaining = 0f;
        damagePunchRemaining = 0f;
        overkillFlashRemaining = 0f;
        cylinderActive = false;
        UpdateComboText();
        UpdateDamageText(false);

        if (comboTimer != null)
        {
            comboTimer.fillAmount = 0f;
        }

        if (comboCanvasGroup != null)
        {
            comboCanvasGroup.alpha = 0f;
        }

        if (comboTimerCanvasGroup != null)
        {
            comboTimerCanvasGroup.alpha = 0f;
        }

        if (damageCanvasGroup != null)
        {
            damageCanvasGroup.alpha = 0f;
        }
    }

    public void RecordDamage(int appliedDamage, bool wasOverkill = false)
    {
        if (appliedDamage <= 0)
        {
            return;
        }

        long combined = (long)cylinderDamage + appliedDamage;
        int previousDamage = cylinderDamage;
        cylinderDamage = combined >= int.MaxValue
            ? int.MaxValue
            : (int)combined;
        displayedCylinderDamage = Mathf.Max(
            displayedCylinderDamage,
            previousDamage + appliedDamage * 0.72f);
        damagePunchRemaining = 0.24f;
        damageHoldRemaining = Mathf.Max(damageHoldRemaining, 0.8f);
        overkillFlashRemaining = wasOverkill ? 0.3f : overkillFlashRemaining;
        UpdateDamageText(wasOverkill);
    }

    public void RecordHit(
        Vector3 worldPosition,
        int horizontalDirection,
        int appliedDamage,
        int targetMaxHealth,
        bool wasCritical,
        float cylinderBuild)
    {
        float damageRatio = targetMaxHealth <= 0
            ? 0f
            : Mathf.Clamp01((float)appliedDamage / targetMaxHealth);
        float intensity = Mathf.Clamp01(
            minimumHitIntensity
            + Mathf.Sqrt(damageRatio) * 0.58f
            + (wasCritical ? 0.2f : 0f));
        intensity *= Mathf.Lerp(0.9f, 1.16f, Mathf.Clamp01(cylinderBuild));
        CombatCameraShake.Play(
            hitCameraShake * Mathf.Lerp(0.75f, 1.75f, intensity));
        PlayHitAccent(
            worldPosition,
            intensity,
            wasCritical,
            cylinderBuild);
        QueueFullscreenImpact(
            worldPosition,
            horizontalDirection,
            intensity,
            hitFullscreenDuration * (wasCritical ? 1.25f : 1f),
            wasCritical,
            false);
    }

    public void RecordDefeat(
        Vector3 worldPosition,
        int horizontalDirection,
        int appliedDamage,
        int targetMaxHealth,
        bool wasCritical,
        bool wasFinalEnemy,
        float cylinderBuild)
    {
        comboCount = comboCount >= int.MaxValue
            ? int.MaxValue
            : comboCount + 1;
        comboRemaining = comboDuration;
        comboPunchRemaining = 0.3f;
        UpdateComboText();

        float tier = GetComboTier();
        float specialBoost = (wasCritical ? 0.12f : 0f)
            + (wasFinalEnemy ? 0.22f : 0f);
        float damageRatio = targetMaxHealth <= 0
            ? 0f
            : Mathf.Clamp01((float)appliedDamage / targetMaxHealth);
        float intensity = Mathf.Clamp01(
            0.68f
            + Mathf.Sqrt(damageRatio) * 0.2f
            + tier * 0.2f
            + specialBoost);
        intensity *= Mathf.Lerp(0.95f, 1.15f, Mathf.Clamp01(cylinderBuild));

        CombatCameraShake.Play(killCameraShake * Mathf.Lerp(0.85f, 1.65f, intensity));
        PlayKillAccent(
            worldPosition,
            tier,
            wasCritical,
            wasFinalEnemy,
            cylinderBuild);
        StartVolumePulse(intensity);
        StartSlowMotion(intensity, wasFinalEnemy);
        QueueFullscreenImpact(
            worldPosition,
            horizontalDirection,
            intensity,
            fullscreenImpactDuration * (wasFinalEnemy ? 1.3f : 1f),
            wasCritical,
            wasFinalEnemy);
    }

    private void UpdateCombo(float deltaTime)
    {
        if (comboCount <= 0)
        {
            if (comboTimer != null)
            {
                comboTimer.fillAmount = 0f;
            }

            return;
        }

        if (!GamePauseController.IsPaused)
        {
            comboRemaining = Mathf.Max(0f, comboRemaining - deltaTime);
        }

        float normalized = comboDuration <= 0f
            ? 0f
            : Mathf.Clamp01(comboRemaining / comboDuration);

        if (comboTimer != null)
        {
            comboTimer.fillAmount = normalized;
            float danger = 1f - Mathf.Clamp01(normalized / 0.3f);
            float blink = danger > 0f
                ? 0.72f + Mathf.Sin(Time.unscaledTime * 22f) * 0.28f
                : 0f;
            comboTimer.color = Color.Lerp(
                timerBaseColor,
                timerDangerColor,
                danger * blink);
        }

        if (comboRemaining <= 0f)
        {
            comboCount = 0;
            cylinderDamage = 0;
            displayedCylinderDamage = 0f;
            damageHoldRemaining = 0f;
            comboPunchRemaining = 0.22f;
            UpdateDamageText(false);
        }
    }

    private void UpdateDamage(float deltaTime)
    {
        overkillFlashRemaining = Mathf.Max(
            0f,
            overkillFlashRemaining - deltaTime);

        if (displayedCylinderDamage < cylinderDamage)
        {
            float remaining = cylinderDamage - displayedCylinderDamage;
            displayedCylinderDamage += Mathf.Max(
                1f,
                remaining * Mathf.Min(1f, deltaTime * 18f));

            if (cylinderDamage - displayedCylinderDamage < 0.75f)
            {
                displayedCylinderDamage = cylinderDamage;
            }

            UpdateDamageText(overkillFlashRemaining > 0f);
        }

        if (!cylinderActive && damageHoldRemaining > 0f
            && !GamePauseController.IsPaused)
        {
            damageHoldRemaining = Mathf.Max(
                0f,
                damageHoldRemaining - deltaTime);
        }
    }

    private void AnimateUi(float deltaTime)
    {
        if (!uiBound)
        {
            return;
        }

        float comboTargetAlpha = comboCount > 0 ? 1f : 0f;
        float damageTargetAlpha = comboCount > 0
            || cylinderActive || damageHoldRemaining > 0f
            ? 1f
            : 0f;
        comboCanvasGroup.alpha = Mathf.MoveTowards(
            comboCanvasGroup.alpha,
            comboTargetAlpha,
            deltaTime * (comboTargetAlpha > 0f ? 12f : 5f));
        comboTimerCanvasGroup.alpha = comboCanvasGroup.alpha;
        damageCanvasGroup.alpha = Mathf.MoveTowards(
            damageCanvasGroup.alpha,
            damageTargetAlpha,
            deltaTime * (damageTargetAlpha > 0f ? 14f : 4f));

        comboPunchRemaining = Mathf.Max(0f, comboPunchRemaining - deltaTime);
        damagePunchRemaining = Mathf.Max(0f, damagePunchRemaining - deltaTime);
        ApplyPunch(
            comboRect,
            comboBaseScale,
            comboBaseRotation,
            comboPunchRemaining,
            0.3f,
            0.34f,
            3.5f);
        ApplyPunch(
            damageRect,
            damageBaseScale,
            damageBaseRotation,
            damagePunchRemaining,
            0.24f,
            0.22f,
            -2.2f);

        if (comboText != null && comboCount > 0)
        {
            comboText.color = Color.Lerp(
                comboLowColor,
                comboHighColor,
                GetComboTier());
        }
    }

    private static void ApplyPunch(
        RectTransform target,
        Vector3 baseScale,
        Quaternion baseRotation,
        float remaining,
        float duration,
        float scaleStrength,
        float rotationStrength)
    {
        if (target == null)
        {
            return;
        }

        if (remaining <= 0f || duration <= 0f)
        {
            target.localScale = baseScale;
            target.localRotation = baseRotation;
            return;
        }

        float elapsed = 1f - Mathf.Clamp01(remaining / duration);
        float envelope = 1f - elapsed;
        float overshoot = Mathf.Sin(elapsed * Mathf.PI) * envelope;
        float shake = Mathf.Sin(elapsed * Mathf.PI * 5f) * envelope;
        target.localScale = baseScale * (1f + overshoot * scaleStrength);
        target.localRotation = baseRotation
            * Quaternion.Euler(0f, 0f, shake * rotationStrength);
    }

    private void UpdateComboText()
    {
        if (comboText == null)
        {
            return;
        }

        comboText.text = $"combo <size=128>{comboCount}</size>";
    }

    private void UpdateDamageText(bool wasOverkill)
    {
        if (currentDamageText == null)
        {
            return;
        }

        string colorOpen = wasOverkill ? "<color=#FF9B45>" : string.Empty;
        string colorClose = wasOverkill ? "</color>" : string.Empty;
        int displayedDamage = Mathf.Clamp(
            Mathf.RoundToInt(displayedCylinderDamage),
            0,
            cylinderDamage);
        currentDamageText.text =
            $"DMG {colorOpen}<size=42>{displayedDamage:N0}</size>{colorClose}";
        currentDamageText.color = wasOverkill
            ? Color.Lerp(damageBaseColor, comboHighColor, 0.55f)
            : damageBaseColor;
    }

    private float GetComboTier()
    {
        if (comboCount >= 10)
        {
            return 1f;
        }

        if (comboCount >= 6)
        {
            return 0.72f;
        }

        if (comboCount >= 3)
        {
            return 0.42f;
        }

        return 0.12f;
    }

    private void BindUi()
    {
        Transform feedbackPanel = FindFeedbackPanel();

        if (feedbackPanel == null)
        {
            return;
        }

        comboText = FindDescendant(feedbackPanel, ComboTextName)
            ?.GetComponent<TMP_Text>();
        comboTimer = FindDescendant(feedbackPanel, ComboTimerName)
            ?.GetComponent<Image>();
        currentDamageText = FindDescendant(feedbackPanel, CurrentDamageTextName)
            ?.GetComponent<TMP_Text>();

        if (comboText == null || comboTimer == null
            || currentDamageText == null)
        {
            return;
        }

        comboTimer.type = Image.Type.Filled;
        comboTimer.fillAmount = comboCount > 0 ? 1f : 0f;
        comboRect = comboText.rectTransform;
        damageRect = currentDamageText.rectTransform;
        comboBaseScale = comboRect.localScale;
        damageBaseScale = damageRect.localScale;
        comboBaseRotation = comboRect.localRotation;
        damageBaseRotation = damageRect.localRotation;
        damageBaseColor = currentDamageText.color;
        timerBaseColor = comboTimer.color;
        comboCanvasGroup = GetOrAddCanvasGroup(comboText.gameObject);
        comboTimerCanvasGroup = GetOrAddCanvasGroup(
            comboTimer.transform.parent == null
                ? comboTimer.gameObject
                : comboTimer.transform.parent.gameObject);
        damageCanvasGroup = GetOrAddCanvasGroup(currentDamageText.gameObject);
        comboCanvasGroup.alpha = comboCount > 0 ? 1f : 0f;
        comboTimerCanvasGroup.alpha = comboCanvasGroup.alpha;
        damageCanvasGroup.alpha = cylinderActive ? 1f : 0f;
        uiBound = true;
        UpdateComboText();
        UpdateDamageText(false);
    }

    private static Transform FindFeedbackPanel()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Canvas canvas in canvases)
        {
            Transform panel = FindDescendant(canvas.transform, FeedbackPanelName);

            if (panel != null)
            {
                return panel;
            }
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.AddComponent<CanvasGroup>();
    }

    private void RefreshUiImmediate()
    {
        if (!uiBound)
        {
            return;
        }

        UpdateComboText();
        UpdateDamageText(false);
        comboTimer.fillAmount = comboCount > 0 ? 1f : 0f;
    }

    private void ResetUiTransforms()
    {
        if (comboRect != null)
        {
            comboRect.localScale = comboBaseScale;
            comboRect.localRotation = comboBaseRotation;
        }

        if (damageRect != null)
        {
            damageRect.localScale = damageBaseScale;
            damageRect.localRotation = damageBaseRotation;
        }
    }

    private void BindVolume()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return;
        }

        cameraVolume = mainCamera.GetComponent<Volume>();

        if (cameraVolume == null)
        {
            return;
        }

        VolumeProfile profile = cameraVolume.profile;
        GetOrAdd(profile, out chromaticAberration);
        GetOrAdd(profile, out bloom);
        GetOrAdd(profile, out vignette);
        GetOrAdd(profile, out lensDistortion);
        GetOrAdd(profile, out colorAdjustments);
        chromaticBaseActive = chromaticAberration.active;
        bloomBaseActive = bloom.active;
        vignetteBaseActive = vignette.active;
        lensBaseActive = lensDistortion.active;
        colorBaseActive = colorAdjustments.active;
        chromaticBase = chromaticAberration.intensity.value;
        bloomBase = bloom.intensity.value;
        vignetteBase = vignette.intensity.value;
        lensBase = lensDistortion.intensity.value;
        contrastBase = colorAdjustments.contrast.value;
        chromaticBaseOverride = chromaticAberration.intensity.overrideState;
        bloomBaseOverride = bloom.intensity.overrideState;
        vignetteBaseOverride = vignette.intensity.overrideState;
        lensBaseOverride = lensDistortion.intensity.overrideState;
        contrastBaseOverride = colorAdjustments.contrast.overrideState;
    }

    private static void GetOrAdd<T>(VolumeProfile profile, out T component)
        where T : VolumeComponent
    {
        if (!profile.TryGet(out component))
        {
            component = profile.Add<T>(true);
            component.active = false;
        }
    }

    private void StartVolumePulse(float intensity)
    {
        if (cameraVolume == null)
        {
            BindVolume();
        }

        if (cameraVolume == null)
        {
            return;
        }

        if (volumePulseCoroutine != null)
        {
            StopCoroutine(volumePulseCoroutine);
            RestoreVolume();
        }

        volumePulseCoroutine = StartCoroutine(VolumePulseRoutine(intensity));
    }

    private IEnumerator VolumePulseRoutine(float intensity)
    {
        chromaticAberration.active = true;
        bloom.active = true;
        vignette.active = true;
        lensDistortion.active = true;
        colorAdjustments.active = true;
        chromaticAberration.intensity.overrideState = true;
        bloom.intensity.overrideState = true;
        vignette.intensity.overrideState = true;
        lensDistortion.intensity.overrideState = true;
        colorAdjustments.contrast.overrideState = true;
        float elapsed = 0f;

        while (elapsed < volumePulseDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / volumePulseDuration);
            float attack = Mathf.Clamp01(progress / 0.16f);
            float release = 1f - Mathf.SmoothStep(0f, 1f, progress);
            float pulse = Mathf.Min(attack, release) * intensity;
            chromaticAberration.intensity.value = Mathf.Clamp01(
                chromaticBase + chromaticBoost * pulse);
            bloom.intensity.value = bloomBase + bloomBoost * pulse;
            vignette.intensity.value = Mathf.Clamp01(
                vignetteBase + vignetteBoost * pulse);
            lensDistortion.intensity.value = Mathf.Clamp(
                lensBase + lensDistortionBoost * pulse,
                -1f,
                1f);
            colorAdjustments.contrast.value = Mathf.Clamp(
                contrastBase + contrastBoost * pulse,
                -100f,
                100f);
        }

        RestoreVolume();
        volumePulseCoroutine = null;
    }

    private void RestoreVolume()
    {
        if (chromaticAberration == null)
        {
            return;
        }

        chromaticAberration.intensity.value = chromaticBase;
        bloom.intensity.value = bloomBase;
        vignette.intensity.value = vignetteBase;
        lensDistortion.intensity.value = lensBase;
        colorAdjustments.contrast.value = contrastBase;
        chromaticAberration.intensity.overrideState = chromaticBaseOverride;
        bloom.intensity.overrideState = bloomBaseOverride;
        vignette.intensity.overrideState = vignetteBaseOverride;
        lensDistortion.intensity.overrideState = lensBaseOverride;
        colorAdjustments.contrast.overrideState = contrastBaseOverride;
        chromaticAberration.active = chromaticBaseActive;
        bloom.active = bloomBaseActive;
        vignette.active = vignetteBaseActive;
        lensDistortion.active = lensBaseActive;
        colorAdjustments.active = colorBaseActive;
    }

    private void StartSlowMotion(float intensity, bool wasFinalEnemy)
    {
        if (slowMotionCoroutine != null)
        {
            StopCoroutine(slowMotionCoroutine);
            slowMotionCoroutine = null;
        }

        float scale = Mathf.Lerp(0.52f, killSlowMotionScale, intensity);
        float durationMultiplier = wasFinalEnemy ? 1.65f : 1f;
        slowMotionCoroutine = StartCoroutine(SlowMotionRoutine(
            scale,
            killSlowMotionHold * durationMultiplier,
            killSlowMotionRecovery * durationMultiplier));
    }

    private IEnumerator SlowMotionRoutine(
        float targetScale,
        float holdDuration,
        float recoveryDuration)
    {
        while (Time.timeScale <= 0f || GamePauseController.IsPaused)
        {
            yield return null;
        }

        if (!ownsTimeScale)
        {
            slowMotionBaseScale = Time.timeScale;
            ownsTimeScale = true;
        }
        float elapsed = 0f;

        while (elapsed < holdDuration)
        {
            yield return null;

            if (GamePauseController.IsPaused || Time.timeScale <= 0f)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = targetScale;
        }

        elapsed = 0f;

        while (elapsed < recoveryDuration)
        {
            yield return null;

            if (GamePauseController.IsPaused || Time.timeScale <= 0f)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / recoveryDuration);
            Time.timeScale = Mathf.Lerp(
                targetScale,
                slowMotionBaseScale,
                Mathf.SmoothStep(0f, 1f, progress));
        }

        RestoreSlowMotion();
        slowMotionCoroutine = null;
    }

    private void RestoreSlowMotion()
    {
        if (ownsTimeScale)
        {
            Time.timeScale = slowMotionBaseScale;
        }

        ownsTimeScale = false;
    }

    private void CancelSlowMotionAndRestore()
    {
        if (slowMotionCoroutine != null)
        {
            StopCoroutine(slowMotionCoroutine);
            slowMotionCoroutine = null;
        }

        RestoreSlowMotion();
    }

    private void QueueFullscreenImpact(
        Vector3 worldPosition,
        int horizontalDirection,
        float intensity,
        float duration,
        bool wasCritical,
        bool wasFinalEnemy)
    {
        if (!fullscreenImpactEnabled || duration <= 0f || intensity <= 0f)
        {
            return;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return;
        }

        int selectedIndex = 0;
        float oldestProgress = -1f;

        for (int impactIndex = 0;
             impactIndex < fullscreenImpacts.Length;
             impactIndex++)
        {
            FullscreenImpactState candidate = fullscreenImpacts[impactIndex];

            if (!candidate.Active)
            {
                selectedIndex = impactIndex;
                oldestProgress = float.MaxValue;
                break;
            }

            float progress = candidate.Duration <= 0f
                ? 1f
                : candidate.Elapsed / candidate.Duration;

            if (progress > oldestProgress)
            {
                oldestProgress = progress;
                selectedIndex = impactIndex;
            }
        }

        Vector3 viewportPoint = mainCamera.WorldToViewportPoint(worldPosition);
        fullscreenImpacts[selectedIndex] = new FullscreenImpactState
        {
            Active = true,
            Center = new Vector2(
                Mathf.Clamp01(viewportPoint.x),
                Mathf.Clamp01(viewportPoint.y)),
            Direction = horizontalDirection == 0
                ? Vector2.right
                : new Vector2(Mathf.Sign(horizontalDirection), 0f),
            Elapsed = 0f,
            Duration = duration,
            Intensity = Mathf.Clamp01(intensity),
            Critical = wasCritical,
            FinalKill = wasFinalEnemy
        };
        ApplyFullscreenGlobals();
    }

    private void UpdateFullscreenImpacts(float deltaTime)
    {
        if (!fullscreenImpactEnabled)
        {
            ResetFullscreenImpact();
            return;
        }

        if (!GamePauseController.IsPaused)
        {
            for (int impactIndex = 0;
                 impactIndex < fullscreenImpacts.Length;
                 impactIndex++)
            {
                FullscreenImpactState impact = fullscreenImpacts[impactIndex];

                if (!impact.Active)
                {
                    continue;
                }

                impact.Elapsed += deltaTime;

                if (impact.Elapsed >= impact.Duration)
                {
                    impact.Active = false;
                }

                fullscreenImpacts[impactIndex] = impact;
            }
        }

        ApplyFullscreenGlobals();
    }

    private void ApplyFullscreenGlobals()
    {
        float maximumStrength = 0f;
        for (int impactIndex = 0;
             impactIndex < fullscreenImpacts.Length;
             impactIndex++)
        {
            FullscreenImpactState impact = fullscreenImpacts[impactIndex];
            float progress = impact.Active && impact.Duration > 0f
                ? Mathf.Clamp01(impact.Elapsed / impact.Duration)
                : 1f;
            float attack = Mathf.Clamp01(progress / 0.08f);
            float release = 1f - Mathf.SmoothStep(0f, 1f, progress);
            float strength = impact.Active
                ? impact.Intensity * Mathf.Min(attack, release)
                : 0f;
            fullscreenCenters[impactIndex] = new Vector4(
                impact.Center.x,
                impact.Center.y,
                0f,
                0f);
            fullscreenDirections[impactIndex] = new Vector4(
                impact.Direction.x,
                impact.Direction.y,
                0f,
                0f);
            fullscreenParams[impactIndex] = new Vector4(
                progress,
                strength,
                impact.Critical ? 1f : 0f,
                impact.FinalKill ? 1f : 0f);
            maximumStrength = Mathf.Max(maximumStrength, strength);

        }

        Shader.SetGlobalVectorArray(FullscreenCentersId, fullscreenCenters);
        Shader.SetGlobalVectorArray(
            FullscreenDirectionsId,
            fullscreenDirections);
        Shader.SetGlobalVectorArray(FullscreenParamsId, fullscreenParams);
        Shader.SetGlobalColor(FullscreenColorId, fullscreenImpactColor);
        Shader.SetGlobalFloat(
            FullscreenAspectId,
            Screen.height <= 0 ? 1f : (float)Screen.width / Screen.height);
        Shader.SetGlobalFloat(FullscreenShockwaveId, shockwaveStrength);
        Shader.SetGlobalFloat(FullscreenRgbSplitId, rgbSplitStrength);
        Shader.SetGlobalFloat(FullscreenRadialZoomId, radialZoomStrength);
        Shader.SetGlobalFloat(FullscreenTearId, directionalTearStrength);
        Shader.SetGlobalFloat(FullscreenIntensityId, maximumStrength);
    }

    private void ResetFullscreenImpact()
    {
        for (int impactIndex = 0;
             impactIndex < fullscreenImpacts.Length;
             impactIndex++)
        {
            fullscreenImpacts[impactIndex] = default;
            fullscreenCenters[impactIndex] = Vector4.zero;
            fullscreenDirections[impactIndex] = Vector4.zero;
            fullscreenParams[impactIndex] = Vector4.zero;
        }

        Shader.SetGlobalVectorArray(FullscreenCentersId, fullscreenCenters);
        Shader.SetGlobalVectorArray(
            FullscreenDirectionsId,
            fullscreenDirections);
        Shader.SetGlobalVectorArray(FullscreenParamsId, fullscreenParams);
        Shader.SetGlobalFloat(FullscreenIntensityId, 0f);
    }

    private void InitializeAudio()
    {
        audioSource = CreateImpactAudioSource();
        accentAudioSource = CreateImpactAudioSource();

        if (hitAccentClip == null)
        {
            hitAccentClip = CreateRuntimeHitAccent();
        }

        if (criticalAccentClip == null)
        {
            criticalAccentClip = CreateRuntimeCriticalAccent();
        }

        if (killAccentClip == null)
        {
            killAccentClip = CreateRuntimeKillAccent();
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return;
        }

        lowPassFilter = mainCamera.GetComponent<AudioLowPassFilter>();

        if (lowPassFilter == null)
        {
            lowPassFilter = mainCamera.gameObject.AddComponent<AudioLowPassFilter>();
            createdLowPassFilter = true;
            lowPassFilter.cutoffFrequency = 22000f;
            lowPassFilter.enabled = false;
        }

        lowPassBaseEnabled = lowPassFilter.enabled;
        lowPassBaseCutoff = lowPassFilter.cutoffFrequency;
    }

    private AudioSource CreateImpactAudioSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.bypassListenerEffects = true;
        source.priority = 48;
        return source;
    }

    private void PlayHitAccent(
        Vector3 worldPosition,
        float intensity,
        bool wasCritical,
        float cylinderBuild)
    {
        if (audioSource == null)
        {
            return;
        }

        float pan = GetImpactPan(worldPosition);
        audioSource.panStereo = pan;
        audioSource.pitch = 0.96f
            + Mathf.Clamp01(cylinderBuild) * 0.08f
            + Mathf.Clamp01(intensity) * 0.07f;

        if (hitAccentClip != null)
        {
            audioSource.PlayOneShot(
                hitAccentClip,
                hitAccentVolume * Mathf.Lerp(0.62f, 1f, intensity));
        }

        if (wasCritical && criticalAccentClip != null)
        {
            audioSource.PlayOneShot(
                criticalAccentClip,
                criticalAccentVolume * Mathf.Lerp(0.82f, 1f, intensity));
            StartAudioDuck(4300f, 0.14f);
        }
        else
        {
            StartAudioDuck(9000f, 0.075f);
        }
    }

    private void PlayKillAccent(
        Vector3 worldPosition,
        float tier,
        bool wasCritical,
        bool wasFinalEnemy,
        float cylinderBuild)
    {
        if (audioSource == null || accentAudioSource == null)
        {
            return;
        }

        float pan = GetImpactPan(worldPosition);
        float build = Mathf.Clamp01(cylinderBuild);
        audioSource.panStereo = pan;
        audioSource.pitch = 1f + build * 0.08f + tier * 0.08f;

        if (hitAccentClip != null)
        {
            audioSource.PlayOneShot(hitAccentClip, hitAccentVolume);
        }

        if (wasCritical && criticalAccentClip != null)
        {
            audioSource.PlayOneShot(
                criticalAccentClip,
                criticalAccentVolume);
        }

        accentAudioSource.panStereo = pan * 0.65f;
        accentAudioSource.pitch = 0.88f + tier * 0.2f + build * 0.08f
            + (wasFinalEnemy ? -0.08f : 0f);

        if (killAccentClip != null)
        {
            accentAudioSource.PlayOneShot(
                killAccentClip,
                killAccentVolume * (wasFinalEnemy ? 1f : 0.9f));
        }

        float cutoff = wasFinalEnemy
            ? 1200f
            : Mathf.Lerp(3100f, 1700f, tier);
        StartAudioDuck(cutoff, volumePulseDuration);
    }

    private static float GetImpactPan(Vector3 worldPosition)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return 0f;
        }

        float viewportX = mainCamera.WorldToViewportPoint(worldPosition).x;
        return Mathf.Clamp((viewportX - 0.5f) * 1.4f, -0.7f, 0.7f);
    }

    private void StartAudioDuck(float cutoff, float duration)
    {
        if (lowPassFilter == null)
        {
            return;
        }

        lowPassFilter.enabled = true;
        lowPassFilter.cutoffFrequency = Mathf.Clamp(cutoff, 800f, 22000f);

        if (audioFilterCoroutine != null)
        {
            StopCoroutine(audioFilterCoroutine);
        }

        audioFilterCoroutine = StartCoroutine(
            RestoreAudioFilterRoutine(Mathf.Max(0.01f, duration)));
    }

    private IEnumerator RestoreAudioFilterRoutine(float duration)
    {
        float elapsed = 0f;
        float startCutoff = lowPassFilter == null
            ? lowPassBaseCutoff
            : lowPassFilter.cutoffFrequency;

        while (elapsed < duration && lowPassFilter != null)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            lowPassFilter.cutoffFrequency = Mathf.Lerp(
                startCutoff,
                lowPassBaseCutoff,
                Mathf.SmoothStep(0f, 1f, progress));
        }

        RestoreAudioFilter();
        audioFilterCoroutine = null;
    }

    private void RestoreAudioFilter()
    {
        if (lowPassFilter == null)
        {
            return;
        }

        lowPassFilter.cutoffFrequency = lowPassBaseCutoff;
        lowPassFilter.enabled = lowPassBaseEnabled;

    }

    private static AudioClip CreateRuntimeHitAccent()
    {
        const int sampleRate = 44100;
        const float duration = 0.085f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float time = (float)sampleIndex / sampleRate;
            float progress = time / duration;
            float body = Mathf.Sin(
                2f * Mathf.PI * (195f - progress * 70f) * time);
            float metallic = Mathf.Sin(2f * Mathf.PI * 1380f * time);
            float noise = GetDeterministicNoise(sampleIndex);
            float click = progress < 0.18f
                ? noise * (1f - progress / 0.18f)
                : 0f;
            samples[sampleIndex] = Mathf.Clamp(
                body * Mathf.Pow(1f - progress, 2.5f) * 0.56f
                + metallic * Mathf.Pow(1f - progress, 7f) * 0.16f
                + click * 0.32f,
                -1f,
                1f);
        }

        return CreateRuntimeClip("Runtime Hit Accent", samples, sampleRate);
    }

    private static AudioClip CreateRuntimeCriticalAccent()
    {
        const int sampleRate = 44100;
        const float duration = 0.14f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float time = (float)sampleIndex / sampleRate;
            float progress = time / duration;
            float crack = GetDeterministicNoise(sampleIndex + 941)
                * Mathf.Pow(1f - progress, 10f);
            float punch = Mathf.Sin(2f * Mathf.PI * 108f * time)
                * Mathf.Pow(1f - progress, 2.2f);
            float ring = (
                Mathf.Sin(2f * Mathf.PI * 1720f * time)
                + Mathf.Sin(2f * Mathf.PI * 2470f * time) * 0.6f)
                * Mathf.Pow(1f - progress, 5.5f);
            samples[sampleIndex] = Mathf.Clamp(
                crack * 0.44f + punch * 0.48f + ring * 0.17f,
                -1f,
                1f);
        }

        return CreateRuntimeClip(
            "Runtime Critical Accent",
            samples,
            sampleRate);
    }

    private static AudioClip CreateRuntimeKillAccent()
    {
        const int sampleRate = 44100;
        const float duration = 0.24f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float time = (float)sampleIndex / sampleRate;
            float progress = time / duration;
            float decay = Mathf.Pow(1f - progress, 2.15f);
            float sub = Mathf.Sin(
                2f * Mathf.PI * (67f - progress * 24f) * time);
            float body = Mathf.Sin(2f * Mathf.PI * 118f * time);
            float noise = GetDeterministicNoise(sampleIndex + 1973);
            float click = sampleIndex < sampleRate * 0.018f
                ? noise * (1f - time / 0.018f)
                : 0f;
            float chime = Mathf.Sin(2f * Mathf.PI * 520f * time)
                * Mathf.Pow(1f - progress, 5f);
            samples[sampleIndex] = Mathf.Clamp(
                sub * decay * 0.68f
                + body * Mathf.Pow(1f - progress, 3.3f) * 0.25f
                + click * 0.24f
                + chime * 0.14f,
                -1f,
                1f);
        }

        return CreateRuntimeClip("Runtime Kill Accent", samples, sampleRate);
    }

    private static float GetDeterministicNoise(int sampleIndex)
    {
        float noiseSeed = Mathf.Sin(sampleIndex * 12.9898f) * 43758.5453f;
        return (noiseSeed - Mathf.Floor(noiseSeed)) * 2f - 1f;
    }

    private static AudioClip CreateRuntimeClip(
        string clipName,
        float[] samples,
        int sampleRate)
    {
        AudioClip clip = AudioClip.Create(
            clipName,
            samples.Length,
            1,
            sampleRate,
            false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static void DestroyRuntimeClip(AudioClip clip, string clipName)
    {
        if (clip != null && clip.name == clipName)
        {
            Destroy(clip);
        }
    }
}
