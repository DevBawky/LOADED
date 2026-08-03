using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatFeedbackController : MonoBehaviour
{
    private const string FeedbackPanelName = "Panel | Feedback";
    private const string ComboTextName = "Text | Combo";
    private const string ComboTimerName = "Image | Combo Timer";
    private const string CurrentDamageTextName = "Text | Current Damage";
    private static readonly int FullscreenCenterId =
        Shader.PropertyToID("_KillImpactCenter");
    private static readonly int FullscreenDirectionId =
        Shader.PropertyToID("_KillImpactDirection");
    private static readonly int FullscreenColorId =
        Shader.PropertyToID("_KillImpactColor");
    private static readonly int FullscreenProgressId =
        Shader.PropertyToID("_KillImpactProgress");
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
    private static readonly int FullscreenCriticalId =
        Shader.PropertyToID("_KillImpactCritical");
    private static readonly int FullscreenFinalId =
        Shader.PropertyToID("_KillImpactFinal");

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

    [Header("Audio")]
    [SerializeField] private AudioClip killAccentClip;
    [Range(0f, 1f)]
    [SerializeField] private float killAccentVolume = 0.7f;

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
    private float comboRemaining;
    private float damageHoldRemaining;
    private float comboPunchRemaining;
    private float damagePunchRemaining;
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
    private Coroutine fullscreenImpactCoroutine;

    private AudioSource audioSource;
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
        AnimateUi(deltaTime);
    }

    private void OnDisable()
    {
        RestoreSlowMotion();
        RestoreVolume();
        RestoreAudioFilter();
        ResetFullscreenImpact();
        ResetUiTransforms();
    }

    private void OnDestroy()
    {
        if (killAccentClip != null
            && killAccentClip.name == "Runtime Kill Accent")
        {
            Destroy(killAccentClip);
        }

        if (createdLowPassFilter && lowPassFilter != null)
        {
            Destroy(lowPassFilter);
        }
    }

    public void BeginCylinder()
    {
        cylinderActive = true;
        cylinderDamage = 0;
        damageHoldRemaining = 0f;
        UpdateDamageText(false);

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

    public void RecordDamage(int appliedDamage, bool wasOverkill = false)
    {
        if (appliedDamage <= 0)
        {
            return;
        }

        long combined = (long)cylinderDamage + appliedDamage;
        cylinderDamage = combined >= int.MaxValue
            ? int.MaxValue
            : (int)combined;
        damagePunchRemaining = 0.24f;
        damageHoldRemaining = Mathf.Max(damageHoldRemaining, 0.8f);
        UpdateDamageText(wasOverkill);
    }

    public void RecordDefeat(
        Vector3 worldPosition,
        int horizontalDirection,
        bool wasCritical,
        bool wasFinalEnemy)
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
        float intensity = Mathf.Clamp01(0.72f + tier * 0.28f + specialBoost);

        CombatCameraShake.Play(killCameraShake * Mathf.Lerp(0.85f, 1.65f, intensity));
        PlayKillAccent(tier, wasFinalEnemy);
        StartVolumePulse(intensity);
        StartSlowMotion(intensity, wasFinalEnemy);
        StartFullscreenImpact(
            worldPosition,
            horizontalDirection,
            intensity,
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
            comboPunchRemaining = 0.22f;
        }
    }

    private void UpdateDamage(float deltaTime)
    {
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
        float damageTargetAlpha = cylinderActive || damageHoldRemaining > 0f
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
        currentDamageText.text =
            $"DMG {colorOpen}<size=42>{cylinderDamage:N0}</size>{colorClose}";
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
            RestoreSlowMotion();
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

        slowMotionBaseScale = Time.timeScale;
        ownsTimeScale = true;
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
        if (ownsTimeScale && Time.timeScale > 0f)
        {
            Time.timeScale = slowMotionBaseScale;
        }

        ownsTimeScale = false;
    }

    private void StartFullscreenImpact(
        Vector3 worldPosition,
        int horizontalDirection,
        float intensity,
        bool wasCritical,
        bool wasFinalEnemy)
    {
        if (!fullscreenImpactEnabled)
        {
            return;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return;
        }

        if (fullscreenImpactCoroutine != null)
        {
            StopCoroutine(fullscreenImpactCoroutine);
            ResetFullscreenImpact();
        }

        Vector3 viewportPoint = mainCamera.WorldToViewportPoint(worldPosition);
        Vector2 center = new Vector2(
            Mathf.Clamp01(viewportPoint.x),
            Mathf.Clamp01(viewportPoint.y));
        Vector2 direction = horizontalDirection == 0
            ? Vector2.right
            : new Vector2(Mathf.Sign(horizontalDirection), 0f);
        fullscreenImpactCoroutine = StartCoroutine(
            FullscreenImpactRoutine(
                center,
                direction,
                intensity,
                wasCritical,
                wasFinalEnemy));
    }

    private IEnumerator FullscreenImpactRoutine(
        Vector2 center,
        Vector2 direction,
        float intensity,
        bool wasCritical,
        bool wasFinalEnemy)
    {
        float duration = fullscreenImpactDuration
            * (wasFinalEnemy ? 1.3f : 1f);
        float elapsed = 0f;
        Shader.SetGlobalVector(
            FullscreenCenterId,
            new Vector4(center.x, center.y, 0f, 0f));
        Shader.SetGlobalVector(
            FullscreenDirectionId,
            new Vector4(direction.x, direction.y, 0f, 0f));
        Shader.SetGlobalColor(FullscreenColorId, fullscreenImpactColor);
        Shader.SetGlobalFloat(
            FullscreenAspectId,
            Screen.height <= 0 ? 1f : (float)Screen.width / Screen.height);
        Shader.SetGlobalFloat(FullscreenShockwaveId, shockwaveStrength);
        Shader.SetGlobalFloat(FullscreenRgbSplitId, rgbSplitStrength);
        Shader.SetGlobalFloat(FullscreenRadialZoomId, radialZoomStrength);
        Shader.SetGlobalFloat(FullscreenTearId, directionalTearStrength);
        Shader.SetGlobalFloat(FullscreenCriticalId, wasCritical ? 1f : 0f);
        Shader.SetGlobalFloat(FullscreenFinalId, wasFinalEnemy ? 1f : 0f);

        while (elapsed < duration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float attack = Mathf.Clamp01(progress / 0.08f);
            float release = 1f - Mathf.SmoothStep(0f, 1f, progress);
            float envelope = Mathf.Min(attack, release);
            Shader.SetGlobalFloat(FullscreenProgressId, progress);
            Shader.SetGlobalFloat(
                FullscreenIntensityId,
                intensity * envelope);
        }

        ResetFullscreenImpact();
        fullscreenImpactCoroutine = null;
    }

    private static void ResetFullscreenImpact()
    {
        Shader.SetGlobalFloat(FullscreenIntensityId, 0f);
        Shader.SetGlobalFloat(FullscreenProgressId, 1f);
    }

    private void InitializeAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;

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

    private void PlayKillAccent(float tier, bool wasFinalEnemy)
    {
        if (audioSource == null || killAccentClip == null)
        {
            return;
        }

        audioSource.pitch = 0.96f + tier * 0.34f
            + (wasFinalEnemy ? -0.08f : 0f);
        audioSource.PlayOneShot(
            killAccentClip,
            killAccentVolume * (wasFinalEnemy ? 1f : 0.82f));

        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = true;
            lowPassFilter.cutoffFrequency = Mathf.Lerp(3400f, 1550f, tier);

            if (audioFilterCoroutine != null)
            {
                StopCoroutine(audioFilterCoroutine);
            }

            audioFilterCoroutine = StartCoroutine(
                RestoreAudioFilterRoutine(volumePulseDuration));
        }
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

    private static AudioClip CreateRuntimeKillAccent()
    {
        const int sampleRate = 44100;
        const float duration = 0.18f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float time = (float)sampleIndex / sampleRate;
            float progress = time / duration;
            float decay = Mathf.Pow(1f - progress, 2.4f);
            float thump = Mathf.Sin(2f * Mathf.PI * (88f - progress * 28f) * time);
            float noiseSeed = Mathf.Sin(sampleIndex * 12.9898f) * 43758.5453f;
            float noise = (noiseSeed - Mathf.Floor(noiseSeed)) * 2f - 1f;
            float click = sampleIndex < sampleRate * 0.018f
                ? noise * (1f - time / 0.018f)
                : 0f;
            float chime = Mathf.Sin(2f * Mathf.PI * 440f * time)
                * Mathf.Pow(1f - progress, 5f);
            samples[sampleIndex] = Mathf.Clamp(
                thump * decay * 0.65f + click * 0.18f + chime * 0.17f,
                -1f,
                1f);
        }

        AudioClip clip = AudioClip.Create(
            "Runtime Kill Accent",
            sampleCount,
            1,
            sampleRate,
            false);
        clip.SetData(samples, 0);
        return clip;
    }
}
