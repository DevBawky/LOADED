using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatFeedbackController : MonoBehaviour
{
    private const int MaxFullscreenImpacts = 4;
    private const string FeedbackPanelName = "Panel | Feedback";
    private const string ComboTextName = "Text | Combo";
    private const string ComboCountRootName = "Image | Combo Timer BG";
    private const string CurrentDamageTextName = "Text | Current Damage";
    private const string DodgeSfxId = "SFX_Evade";
    private const float BaseKillTier = 0.12f;
    private static readonly int FullscreenCentersId =
        Shader.PropertyToID("_KillImpactCenters");
    private static readonly int FullscreenDirectionsId =
        Shader.PropertyToID("_KillImpactDirections");
    private static readonly int FullscreenParamsId =
        Shader.PropertyToID("_KillImpactParams");
    private static readonly int FullscreenColorsId =
        Shader.PropertyToID("_KillImpactColors");
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
        public float FeedbackMultiplier;
        public CombatImpactTier Tier;
        public Color Color;
        public bool FinalKill;
        public bool ShotPulse;
    }

    public readonly struct DefeatPresentationCue
    {
        public DefeatPresentationCue(
            float feedbackMultiplier,
            float presentationTime,
            bool wasFinalEnemy)
        {
            FeedbackMultiplier = Mathf.Max(0f, feedbackMultiplier);
            PresentationTime = Mathf.Max(0f, presentationTime);
            WasFinalEnemy = wasFinalEnemy;
        }

        public float FeedbackMultiplier { get; }
        public float PresentationTime { get; }
        public bool WasFinalEnemy { get; }
    }

    private readonly struct DefeatFeedbackRequest
    {
        public DefeatFeedbackRequest(
            Vector3 worldPosition,
            int horizontalDirection,
            int firingSequenceDefeatCount,
            float feedbackMultiplier,
            float baseIntensity,
            float amplifiedIntensity,
            bool showComboText)
        {
            WorldPosition = worldPosition;
            HorizontalDirection = horizontalDirection;
            FiringSequenceDefeatCount = firingSequenceDefeatCount;
            FeedbackMultiplier = feedbackMultiplier;
            BaseIntensity = baseIntensity;
            AmplifiedIntensity = amplifiedIntensity;
            ShowComboText = showComboText;
        }

        public Vector3 WorldPosition { get; }
        public int HorizontalDirection { get; }
        public int FiringSequenceDefeatCount { get; }
        public float FeedbackMultiplier { get; }
        public float BaseIntensity { get; }
        public float AmplifiedIntensity { get; }
        public bool ShowComboText { get; }
    }

    [Header("Combo")]
    [Min(1)]
    [FormerlySerializedAs("comboTurnLimit")]
    [SerializeField] private int comboCountLimit = 8;
    [Min(0.01f)]
    [FormerlySerializedAs("turnDrainDuration")]
    [SerializeField] private float countDrainDuration = 0.2f;
    [SerializeField] private Color comboLowColor = Color.white;
    [FormerlySerializedAs("comboHighColor")]
    [SerializeField] private Color comboMidColor =
        new Color(1f, 0.42f, 0.12f, 1f);
    [FormerlySerializedAs("timerDangerColor")]
    [SerializeField] private Color comboCriticalColor =
        new Color(1f, 0.18f, 0.08f, 1f);
    [Min(0f)]
    [FormerlySerializedAs("comboFeedbackStrengthPerKill")]
    [FormerlySerializedAs("comboFeedbackStrengthPerAdditionalKill")]
    [FormerlySerializedAs("firingSequenceFeedbackStrengthPerKill")]
    [SerializeField] private float firingSequenceFeedbackStrengthPerKill =
        0.2f;
    [Min(0.05f)]
    [SerializeField] private float defeatPresentationInterval = 0.18f;

    [Header("Kill Combo Bonus")]
    [SerializeField] private TextMeshPro killComboTextPrefab;
    [Min(0)]
    [SerializeField] private int comboGoldPerKill = 10;
    [Min(0.1f)]
    [SerializeField] private float killComboTextDuration = 0.85f;
    [Min(0f)]
    [SerializeField] private float killComboTextDurationPerAdditionalKill = 0.12f;
    [Min(0f)]
    [SerializeField] private float maximumKillComboTextDurationBonus = 1.2f;
    [Min(0.01f)]
    [SerializeField] private float killComboTextScale = 0.13f;
    [Range(0f, 0.5f)]
    [SerializeField] private float maximumComboTextScaleBonus = 0.2f;
    [SerializeField] private Color secondKillTextColor =
        new Color(1f, 0.46f, 0.08f, 1f);
    [SerializeField] private Color highComboTextColor =
        new Color(1f, 0.12f, 0.06f, 1f);
    [SerializeField] private Color kickReadyTextColor =
        new Color(0.35f, 0.85f, 1f, 1f);
    [SerializeField] private Color bulletDestroyedTextColor =
        new Color(0.2f, 0.22f, 0.24f, 1f);

    [Header("Player Dodge")]
    [SerializeField] private Color dodgeTextColor =
        new Color(0.45f, 0.92f, 1f, 1f);
    [Min(0.05f)]
    [SerializeField] private float dodgeTextDuration = 0.42f;
    [Min(0.01f)]
    [SerializeField] private float dodgeTextScale = 0.92f;
    [Min(0.05f)]
    [SerializeField] private float dodgeSustainedEffectDuration = 0.5f;
    [Min(0.05f)]
    [SerializeField] private float dodgeFullscreenDuration = 0.18f;
    [Range(0f, 1f)]
    [SerializeField] private float dodgeFullscreenIntensity = 0.46f;
    [Range(0.05f, 1f)]
    [SerializeField] private float dodgeSlowMotionScale = 0.18f;
    [Range(0.01f, 1f)]
    [SerializeField] private float dodgeInitialSlowMotionScale = 0.05f;
    [Min(0f)]
    [SerializeField] private float dodgeInitialSlowMotionDuration = 0.05f;
    [Min(0.01f)]
    [SerializeField] private float dodgeSlowMotionGlideDuration = 0.08f;
    [Min(0f)]
    [SerializeField] private float dodgeSlowMotionHold = 0.1f;
    [Min(0.01f)]
    [SerializeField] private float dodgeSlowMotionRecovery = 0.08f;
    [Range(0f, 1f)]
    [SerializeField] private float dodgeVolumeStrength = 0.82f;
    [Min(0.05f)]
    [SerializeField] private float dodgeVolumeDuration = 0.22f;
    [Range(0f, 3f)]
    [SerializeField] private float dodgeChromaticMultiplier = 1.15f;
    [Range(0f, 3f)]
    [SerializeField] private float dodgeBloomMultiplier = 0.55f;
    [Range(0f, 3f)]
    [SerializeField] private float dodgeVignetteMultiplier = 2.4f;
    [Range(0f, 3f)]
    [SerializeField] private float dodgeLensDistortionMultiplier = 1.35f;
    [Range(0f, 3f)]
    [SerializeField] private float dodgeContrastMultiplier = 0.45f;
    [Range(1, 6)]
    [SerializeField] private int dodgeAfterimageCount = 5;
    [Min(0.01f)]
    [SerializeField] private float dodgeAfterimageInterval = 0.07f;
    [Min(0.05f)]
    [SerializeField] private float dodgeAfterimageDuration = 0.32f;
    [Min(0.05f)]
    [SerializeField] private float dodgeOriginGhostDuration = 0.48f;

    [Header("Kill Motion")]
    [Range(0.05f, 1f)]
    [SerializeField] private float killSlowMotionScale = 0.32f;
    [Min(0f)]
    [SerializeField] private float killSlowMotionHold = 0.09f;
    [Min(0.01f)]
    [SerializeField] private float killSlowMotionRecovery = 0.18f;
    [Header("Critical Hit")]
    [Range(0.05f, 1f)]
    [SerializeField] private float criticalSlowMotionScale = 0.62f;
    [Min(0f)]
    [SerializeField] private float criticalSlowMotionHold = 0.025f;
    [Min(0.01f)]
    [SerializeField] private float criticalSlowMotionRecovery = 0.075f;
    [Range(0f, 1f)]
    [SerializeField] private float criticalVolumeStrength = 0.48f;

    [Header("Devastating Hit (75% Max Health)")]
    [Range(0.05f, 1f)]
    [SerializeField] private float devastatingSlowMotionScale = 0.5f;
    [Min(0f)]
    [SerializeField] private float devastatingSlowMotionHold = 0.045f;
    [Min(0.01f)]
    [SerializeField] private float devastatingSlowMotionRecovery = 0.11f;
    [Range(0f, 1f)]
    [SerializeField] private float devastatingVolumeStrength = 0.72f;

    [Header("Kick Impact")]
    [Range(0.05f, 1f)]
    [SerializeField] private float kickSlowMotionScale = 0.42f;
    [Min(0f)]
    [SerializeField] private float kickSlowMotionHold = 0.045f;
    [Min(0.01f)]
    [SerializeField] private float kickSlowMotionRecovery = 0.13f;
    [Range(0f, 1f)]
    [SerializeField] private float kickVolumeStrength = 0.82f;
    [Min(0.05f)]
    [SerializeField] private float kickFullscreenDuration = 0.2f;

    [Header("Unified Camera Shake")]
    [Min(0f)]
    [FormerlySerializedAs("maximumDamageShakeStrength")]
    [SerializeField] private float cameraShakeStrength = 0.055f;
    [Min(0f)]
    [FormerlySerializedAs("maximumDamageShakeDuration")]
    [SerializeField] private float cameraShakeDuration = 0.18f;
    [Min(0f)]
    [SerializeField] private float shotShakeMultiplier = 0.5f;
    [Min(0f)]
    [SerializeField] private float explosionShakeMultiplier = 1.5f;
    [Min(0f)]
    [SerializeField] private float killShakeMultiplier = 1f;

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
    private TMP_Text comboText;
    private TMP_Text currentDamageText;
    private Transform comboCountRoot;
    private readonly List<Image> comboCountValues = new List<Image>();
    private CanvasGroup comboCanvasGroup;
    private CanvasGroup comboCountCanvasGroup;
    private CanvasGroup damageCanvasGroup;
    private RectTransform comboRect;
    private RectTransform damageRect;
    private Vector3 comboBaseScale = Vector3.one;
    private Vector3 damageBaseScale = Vector3.one;
    private Quaternion comboBaseRotation = Quaternion.identity;
    private Quaternion damageBaseRotation = Quaternion.identity;
    private Color damageBaseColor = Color.white;

    private int comboCount;
    private int comboCountsRemaining;
    private int firingSequenceDefeatCount;
    private float firingSequenceBaseIntensity;
    private int cylinderDamage;
    private float displayedCylinderDamage;
    private float damageHoldRemaining;
    private float comboPunchRemaining;
    private float comboPunchStrengthMultiplier = 1f;
    private float damagePunchRemaining;
    private float overkillFlashRemaining;
    private bool cylinderActive;
    private bool comboResetDuringCurrentCount;
    private bool uiBound;
    private PlayerMove playerMove;
    private WaveManager waveManager;
    private Coroutine countDrainCoroutine;

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
    private float currentVolumePulseStrength;
    private Coroutine timeEffectCoroutine;
    private readonly FullscreenImpactState[] fullscreenImpacts =
        new FullscreenImpactState[MaxFullscreenImpacts];
    private readonly Queue<FullscreenImpactState> pendingFullscreenImpacts =
        new Queue<FullscreenImpactState>();
    private readonly Vector4[] fullscreenCenters =
        new Vector4[MaxFullscreenImpacts];
    private readonly Vector4[] fullscreenDirections =
        new Vector4[MaxFullscreenImpacts];
    private readonly Vector4[] fullscreenParams =
        new Vector4[MaxFullscreenImpacts];
    private readonly Vector4[] fullscreenColors =
        new Vector4[MaxFullscreenImpacts];

    private float slowMotionBaseScale = 1f;
    private bool ownsTimeScale;
    private float hitStopRemaining;
    private bool slowMotionActive;
    private float slowMotionStartScale = 1f;
    private float slowMotionCurrentScale = 1f;
    private float slowMotionTargetScale = 1f;
    private float slowMotionAttackDuration;
    private float slowMotionHoldDuration;
    private bool slowMotionHasSecondaryPhase;
    private float slowMotionSecondaryTargetScale = 1f;
    private float slowMotionSecondaryTransitionDuration;
    private float slowMotionSecondaryHoldDuration;
    private float slowMotionRecoveryDuration;
    private float slowMotionElapsed;
    private float defeatPresentationClock;
    private float nextDefeatPresentationTime;
    private float lastDodgeFeedbackTime = float.NegativeInfinity;
    private int defeatPresentationGeneration;
    private CurrencyManager currencyManager;
    private readonly List<GameObject> spawnedComboTexts =
        new List<GameObject>();
    private readonly List<GameObject> activeDodgeAfterimages =
        new List<GameObject>();
    private Coroutine dodgeTrailCoroutine;

    public event System.Action<int, int, float> DefeatPerformanceRecorded;

    public int ComboCount => comboCount;
    public float NextFiringSequenceDefeatFeedbackMultiplier =>
        GetFiringSequenceFeedbackMultiplier(
            firingSequenceDefeatCount >= int.MaxValue
                ? int.MaxValue
                : firingSequenceDefeatCount + 1);
    public int CylinderDamage => cylinderDamage;

    public void CaptureRunState(RunSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        saveData.comboCount = comboCount;
        // Legacy JSON field names remain unchanged for version 3 save
        // compatibility. Their values now represent Duel Clock counts.
        saveData.comboTurnsRemaining = comboCountsRemaining;
        saveData.comboResetSinceLastTurn = comboResetDuringCurrentCount;
        saveData.cylinderDamage = cylinderDamage;
        saveData.firingSequenceDefeatCount = firingSequenceDefeatCount;
        saveData.cylinderActive = cylinderActive;
    }

    public void RestoreRunState(RunSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        StopCountDrainAnimation();
        comboCount = Mathf.Max(0, saveData.comboCount);
        comboCountsRemaining = Mathf.Clamp(
            saveData.comboTurnsRemaining,
            0,
            GetComboCountLimit());
        comboResetDuringCurrentCount = saveData.comboResetSinceLastTurn;
        cylinderDamage = Mathf.Max(0, saveData.cylinderDamage);
        displayedCylinderDamage = cylinderDamage;
        firingSequenceDefeatCount = Mathf.Max(
            0,
            saveData.firingSequenceDefeatCount);
        cylinderActive = saveData.cylinderActive;
        damageHoldRemaining = cylinderDamage > 0 ? 1.35f : 0f;
        comboPunchRemaining = 0f;
        comboPunchStrengthMultiplier = 1f;
        damagePunchRemaining = 0f;
        overkillFlashRemaining = 0f;
        BindUi();
        UpdateComboText();
        UpdateDamageText(false);
        RefreshComboCountValues();

        if (comboCanvasGroup != null)
        {
            comboCanvasGroup.alpha = comboCount > 0 ? 1f : 0f;
        }

        if (comboCountCanvasGroup != null)
        {
            comboCountCanvasGroup.alpha = comboCount > 0 ? 1f : 0f;
        }

        if (damageCanvasGroup != null)
        {
            damageCanvasGroup.alpha = comboCount > 0
                || cylinderDamage > 0 || cylinderActive
                    ? 1f
                    : 0f;
        }
    }

    private void Awake()
    {
        currencyManager = FindFirstObjectByType<CurrencyManager>();
        playerMove = GetComponent<PlayerMove>();
        waveManager = FindFirstObjectByType<WaveManager>();
        BindUi();
        BindVolume();
    }

    private void OnEnable()
    {
        playerMove ??= GetComponent<PlayerMove>();
        EnemyController.PlayerStatusDefeated -= HandlePlayerStatusDefeated;
        EnemyController.PlayerStatusDefeated += HandlePlayerStatusDefeated;

        waveManager ??= FindFirstObjectByType<WaveManager>();

        if (waveManager != null)
        {
            waveManager.EnemyTurnCycleCompleted -= HandleCountCompleted;
            waveManager.EnemyTurnCycleCompleted += HandleCountCompleted;
        }
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

        if (!GamePauseController.IsPaused)
        {
            defeatPresentationClock += deltaTime;
        }

        UpdateDamage(deltaTime);
        UpdateFullscreenImpacts(deltaTime);
        AnimateUi(deltaTime);
    }

    private void OnDisable()
    {
        EnemyController.PlayerStatusDefeated -= HandlePlayerStatusDefeated;

        if (waveManager != null)
        {
            waveManager.EnemyTurnCycleCompleted -= HandleCountCompleted;
        }

        StopCountDrainAnimation();
        ResetFiringSequenceFeedback();
        CancelSlowMotionAndRestore();
        RestoreVolume();
        ResetFullscreenImpact();
        ResetUiTransforms();
        ClearComboKillTexts();
        ClearDodgeAfterimages();
        defeatPresentationGeneration++;
        nextDefeatPresentationTime = defeatPresentationClock;
    }

    private void OnDestroy() => CancelSlowMotionAndRestore();

    public void BeginCylinder()
    {
        GameStatistics.BeginCylinder();
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

    public void BeginFiringSequence()
    {
        SoundManager.ResetComboPitch();
        ResetFiringSequenceFeedback();
    }

    public void EndCylinder()
    {
        SoundManager.ResetComboPitch();
        GameStatistics.EndCylinder();
        cylinderActive = false;
        damageHoldRemaining = cylinderDamage > 0 ? 1.35f : 0.3f;
        ResetFiringSequenceFeedback();
    }

    public void ResetCombo(bool preserveActivePresentation = false)
    {
        if (!preserveActivePresentation)
        {
            RestoreActiveKillFeedback();
        }

        comboCount = 0;
        ResetFiringSequenceFeedback();
        comboCountsRemaining = 0;
        comboResetDuringCurrentCount = false;
        StopCountDrainAnimation();
        cylinderDamage = 0;
        displayedCylinderDamage = 0f;
        damageHoldRemaining = 0f;
        comboPunchRemaining = 0f;
        comboPunchStrengthMultiplier = 1f;
        damagePunchRemaining = 0f;
        overkillFlashRemaining = 0f;
        cylinderActive = false;
        UpdateComboText();
        UpdateDamageText(false);

        RefreshComboCountValues();

        if (comboCanvasGroup != null)
        {
            comboCanvasGroup.alpha = 0f;
        }

        if (comboCountCanvasGroup != null)
        {
            comboCountCanvasGroup.alpha = 0f;
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
        float cylinderBuild,
        bool canTriggerDevastatingFeedback = true)
    {
        float damageRatio = targetMaxHealth <= 0
            ? 0f
            : Mathf.Clamp01((float)appliedDamage / targetMaxHealth);
        bool wasDevastating = canTriggerDevastatingFeedback
            && damageRatio >= CombatImpactTierUtility.DevastatingDamageRatio;
        CombatImpactTier impactTier = wasDevastating
            ? CombatImpactTier.Devastating
            : wasCritical
                ? CombatImpactTier.Critical
                : CombatImpactTier.Normal;
        float intensity = Mathf.Clamp01(
            minimumHitIntensity
            + Mathf.Sqrt(damageRatio) * 0.58f
            + (wasCritical ? 0.2f : 0f));
        intensity *= Mathf.Lerp(0.9f, 1.16f, Mathf.Clamp01(cylinderBuild));
        if (impactTier == CombatImpactTier.Devastating)
        {
            intensity = Mathf.Max(0.82f, intensity);
        }

        if (impactTier == CombatImpactTier.Devastating)
        {
            StartVolumePulse(intensity * devastatingVolumeStrength);
            StartSlowMotion(
                intensity,
                devastatingSlowMotionScale,
                devastatingSlowMotionHold,
                devastatingSlowMotionRecovery);
        }
        else if (impactTier == CombatImpactTier.Critical)
        {
            StartVolumePulse(intensity * criticalVolumeStrength);
            StartSlowMotion(
                intensity,
                criticalSlowMotionScale,
                criticalSlowMotionHold,
                criticalSlowMotionRecovery);
        }

    }

    public void PlayOpticalImpact(
        Vector3 worldPosition,
        int horizontalDirection,
        CombatImpactTier impactTier,
        Color impactColor,
        float feedbackMultiplier = 1f,
        bool wasFinalEnemy = false)
    {
        float intensity = impactTier switch
        {
            CombatImpactTier.Defeat => 0.8f,
            CombatImpactTier.Devastating => 0.72f,
            CombatImpactTier.Critical => 0.6f,
            _ => 0.48f
        };
        float duration = impactTier switch
        {
            CombatImpactTier.Defeat => fullscreenImpactDuration
                * (wasFinalEnemy ? 1.08f : 0.88f),
            CombatImpactTier.Devastating => hitFullscreenDuration * 1.7f,
            CombatImpactTier.Critical => hitFullscreenDuration * 1.25f,
            _ => hitFullscreenDuration * 0.9f
        };
        QueueFullscreenImpact(
            worldPosition,
            horizontalDirection,
            intensity,
            duration,
            impactTier,
            impactColor,
            wasFinalEnemy,
            feedbackMultiplier);
    }

    public void PlayShotOpticalKick(
        Vector3 worldPosition,
        int horizontalDirection,
        Color shotColor,
        bool isCritical)
    {
        float intensity = isCritical ? 0.5f : 0.38f;
        float duration = hitFullscreenDuration * (isCritical ? 0.58f : 0.46f);
        QueueFullscreenImpact(
            worldPosition,
            horizontalDirection,
            intensity,
            duration,
            isCritical
                ? CombatImpactTier.Critical
                : CombatImpactTier.Normal,
            shotColor,
            false,
            1f,
            true);
    }

    public void RecordKickImpact(
        Vector3 worldPosition,
        int horizontalDirection,
        float strength = 1f)
    {
        float intensity = Mathf.Clamp01(strength);
        StartVolumePulse(intensity * kickVolumeStrength);
        StartSlowMotion(
            intensity,
            kickSlowMotionScale,
            kickSlowMotionHold,
            kickSlowMotionRecovery);
        QueueFullscreenImpact(
            worldPosition,
            horizontalDirection,
            intensity,
            kickFullscreenDuration,
            CombatImpactTier.Critical,
            fullscreenImpactColor,
            false);
    }

    public void RecordShotCameraShake()
    {
        CombatCameraShake.Play(
            cameraShakeStrength * shotShakeMultiplier,
            cameraShakeDuration);
    }

    public void RecordExplosionCameraShake()
    {
        CombatCameraShake.Play(
            cameraShakeStrength * explosionShakeMultiplier,
            cameraShakeDuration);
    }

    public void RecordPlayerDamageCameraShake()
    {
        CombatCameraShake.Play(
            cameraShakeStrength * killShakeMultiplier,
            cameraShakeDuration);
    }

    public DefeatPresentationCue RecordDefeat(
        Vector3 worldPosition,
        int horizontalDirection,
        int appliedDamage,
        int targetMaxHealth,
        bool wasCritical,
        bool wasFinalEnemy,
        float cylinderBuild,
        int targetHealthBeforeDamage = -1,
        bool countsForFiringSequence = true)
    {
        comboCount = comboCount >= int.MaxValue
            ? int.MaxValue
            : comboCount + 1;
        GameStatistics.RecordComboKills(comboCount);
        if (countsForFiringSequence)
        {
            firingSequenceDefeatCount =
                firingSequenceDefeatCount >= int.MaxValue
                    ? int.MaxValue
                    : firingSequenceDefeatCount + 1;
        }

        int presentationKillCount = countsForFiringSequence
            ? Mathf.Max(1, firingSequenceDefeatCount)
            : 1;
        float defeatFeedbackMultiplier =
            GetFiringSequenceFeedbackMultiplier(presentationKillCount);
        float overkillPercent = targetMaxHealth <= 0
            ? 0f
            : Mathf.Max(0f, appliedDamage - targetMaxHealth)
                * 100f / targetMaxHealth;
        DefeatPerformanceRecorded?.Invoke(
            comboCount,
            firingSequenceDefeatCount,
            overkillPercent);
        waveManager ??= FindFirstObjectByType<WaveManager>();
        comboCountsRemaining = GetComboCountLimit();
        comboResetDuringCurrentCount = waveManager != null
            && waveManager.IsResolvingTurn;
        StopCountDrainAnimation();
        RefreshComboCountValues();
        UpdateComboText();

        if (comboCount > 1 && comboGoldPerKill > 0)
        {
            currencyManager ??= FindFirstObjectByType<CurrencyManager>();
            long calculatedBonus = (long)(comboCount - 1)
                * comboGoldPerKill;
            int comboBonus = calculatedBonus >= int.MaxValue
                ? int.MaxValue
                : (int)calculatedBonus;
            currencyManager?.AddMoneyFromWorld(comboBonus, worldPosition);
        }

        float specialBoost = (wasCritical ? 0.12f : 0f)
            + (wasFinalEnemy ? 0.22f : 0f);
        float damageRatio = targetMaxHealth <= 0
            ? 0f
            : Mathf.Clamp01((float)appliedDamage / targetMaxHealth);
        float calculatedBaseIntensity = Mathf.Clamp01(
            0.68f
            + Mathf.Sqrt(damageRatio) * 0.2f
            + BaseKillTier * 0.2f
            + specialBoost);
        calculatedBaseIntensity *= Mathf.Lerp(
            0.95f,
            1.15f,
            Mathf.Clamp01(cylinderBuild));
        if (countsForFiringSequence
            && (firingSequenceDefeatCount == 1
                || firingSequenceBaseIntensity <= 0f))
        {
            firingSequenceBaseIntensity = calculatedBaseIntensity;
        }

        float baseIntensity = countsForFiringSequence
            ? firingSequenceBaseIntensity
            : calculatedBaseIntensity;
        float amplifiedIntensity = baseIntensity
            * defeatFeedbackMultiplier;

        float presentationDelay = ReserveDefeatPresentationDelay(
            defeatPresentationClock,
            defeatPresentationInterval);
        DefeatFeedbackRequest request = new DefeatFeedbackRequest(
            worldPosition,
            horizontalDirection,
            firingSequenceDefeatCount,
            defeatFeedbackMultiplier,
            baseIntensity,
            amplifiedIntensity,
            countsForFiringSequence);

        if (presentationDelay <= 0f)
        {
            PlayDefeatFeedback(request);
        }
        else
        {
            StartCoroutine(PlayDefeatFeedbackAfterDelay(
                request,
                presentationDelay,
                defeatPresentationGeneration));
        }

        return new DefeatPresentationCue(
            defeatFeedbackMultiplier,
            defeatPresentationClock + presentationDelay,
            wasFinalEnemy);
    }

    public float GetRemainingDefeatPresentationDelay(
        DefeatPresentationCue cue)
    {
        return Mathf.Max(
            0f,
            cue.PresentationTime - defeatPresentationClock);
    }

    private float ReserveDefeatPresentationDelay(
        float currentTime,
        float interval)
    {
        float presentationTime = CalculateDefeatPresentationTime(
            currentTime,
            nextDefeatPresentationTime);
        nextDefeatPresentationTime = presentationTime
            + Mathf.Max(0f, interval);
        return Mathf.Max(0f, presentationTime - currentTime);
    }

    internal static float CalculateDefeatPresentationTime(
        float currentTime,
        float nextPresentationTime)
    {
        return Mathf.Max(currentTime, nextPresentationTime);
    }

    private IEnumerator PlayDefeatFeedbackAfterDelay(
        DefeatFeedbackRequest request,
        float delay,
        int generation)
    {
        float remaining = Mathf.Max(0f, delay);

        while (remaining > 0f)
        {
            yield return null;

            if (generation != defeatPresentationGeneration)
            {
                yield break;
            }

            if (!GamePauseController.IsPaused)
            {
                remaining -= Time.unscaledDeltaTime;
            }
        }

        if (generation == defeatPresentationGeneration)
        {
            PlayDefeatFeedback(request);
        }
    }

    private void PlayDefeatFeedback(DefeatFeedbackRequest request)
    {
        SoundManager.PlayComboDie(
            Mathf.Max(1, request.FiringSequenceDefeatCount));
        comboPunchRemaining = 0.3f;
        comboPunchStrengthMultiplier = request.FeedbackMultiplier;

        if (request.ShowComboText)
        {
            SpawnKillComboText(
                request.WorldPosition,
                request.FiringSequenceDefeatCount,
                request.FeedbackMultiplier);
        }

        CombatCameraShake.Play(
            cameraShakeStrength * killShakeMultiplier,
            cameraShakeDuration);
        StartVolumePulse(request.AmplifiedIntensity);
        StartSlowMotion(
            request.BaseIntensity,
            killSlowMotionScale,
            killSlowMotionHold,
            killSlowMotionRecovery,
            request.FeedbackMultiplier);
    }

    private void HandlePlayerStatusDefeated(
        EnemyController enemy,
        int damage,
        int healthBeforeDamage)
    {
        if (enemy == null)
        {
            return;
        }

        playerMove ??= GetComponent<PlayerMove>();

        // Bullet-driven indirect defeats are finalized by PlayerShoot so
        // they keep their firing-sequence count and exact effect damage.
        if (playerMove != null && playerMove.IsShooting)
        {
            return;
        }

        int horizontalDirection = playerMove == null
            ? 0
            : enemy.transform.position.x >= playerMove.transform.position.x
                ? 1
                : -1;
        WaveManager waveManager = FindFirstObjectByType<WaveManager>();
        CombatPresentation presentation = GetComponent<CombatPresentation>();
        CombatPresentation.EnemySnapshot snapshot = presentation == null
            ? default
            : presentation.CaptureEnemy(enemy);
        DefeatPresentationCue cue = RecordDefeat(
            enemy.transform.position,
            horizontalDirection,
            Mathf.Max(0, damage),
            enemy.MaxHealth,
            false,
            waveManager != null && waveManager.ActiveEnemies.Count <= 1,
            0f,
            Mathf.Max(0, healthBeforeDamage),
            false);
        presentation?.PlayImpact(
            snapshot,
            horizontalDirection,
            null,
            CombatImpactTier.Defeat,
            cue.FeedbackMultiplier,
            GetRemainingDefeatPresentationDelay(cue),
            cue.WasFinalEnemy);
    }

    private void SpawnKillComboText(
        Vector3 worldPosition,
        int firingSequenceKillCount,
        float feedbackMultiplier)
    {
        int cylinderKillCount = Mathf.Max(1, firingSequenceKillCount);
        string message = cylinderKillCount <= 1
            ? "적 처치!"
            : $"{cylinderKillCount}연속 처치!";
        Color color = cylinderKillCount switch
        {
            1 => Color.white,
            2 => secondKillTextColor,
            _ => highComboTextColor
        };
        float sequenceGrowth = 1f + Mathf.Min(
            maximumComboTextScaleBonus,
            Mathf.Max(0, cylinderKillCount - 1) * 0.025f);
        float comboGrowth = sequenceGrowth * Mathf.Lerp(
            1f,
            Mathf.Max(1f, feedbackMultiplier),
            0.5f);
        float duration = killComboTextDuration + Mathf.Min(
            maximumKillComboTextDurationBonus,
            Mathf.Max(0, cylinderKillCount - 1)
                * killComboTextDurationPerAdditionalKill);
        duration *= Mathf.Lerp(
            1f,
            Mathf.Max(1f, feedbackMultiplier),
            0.2f);

        SpawnAnimatedCombatText(
            "Text | Kill Combo",
            message,
            color,
            worldPosition,
            comboGrowth,
            duration);
    }

    public void RecordKickReady(Vector3 worldPosition)
    {
        SpawnAnimatedCombatText(
            "Text | Kick Ready",
            "발차기 준비!",
            kickReadyTextColor,
            worldPosition,
            1f,
            killComboTextDuration);
    }

    public void RecordPlayerDodge(
        Vector3 dodgeOrigin,
        int horizontalDirection)
    {
        const float duplicateGuardDuration = 0.05f;

        if (Time.unscaledTime - lastDodgeFeedbackTime
            < duplicateGuardDuration)
        {
            return;
        }

        lastDodgeFeedbackTime = Time.unscaledTime;
        int direction = horizontalDirection == 0
            ? 1
            : horizontalDirection;
        float sustainedEffectDuration = CalculateDodgePresentationDuration(
            dodgeSustainedEffectDuration,
            dodgeFullscreenDuration,
            dodgeVolumeDuration);
        SoundManager.PlaySfx(DodgeSfxId);
        SpawnAnimatedCombatText(
            "Text | Player Dodge",
            "회피!",
            dodgeTextColor,
            dodgeOrigin,
            dodgeTextScale,
            dodgeTextDuration,
            direction,
            true);
        StartDodgeAfterimages(
            dodgeOrigin,
            direction,
            dodgeTextColor,
            dodgeAfterimageCount,
            dodgeAfterimageInterval,
            dodgeAfterimageDuration,
            dodgeOriginGhostDuration);
        StartDodgeSlowMotion();
        StartDodgeVolumePulse(sustainedEffectDuration);
        QueueFullscreenImpact(
            dodgeOrigin,
            direction,
            dodgeFullscreenIntensity,
            sustainedEffectDuration,
            CombatImpactTier.Critical,
            dodgeTextColor,
            false,
            1f,
            true);
        CombatCameraShake.Play(
            cameraShakeStrength * 0.32f,
            cameraShakeDuration * 0.65f);
    }

    internal static float CalculateDodgePresentationDuration(
        float sustainedDuration,
        float fullscreenDuration,
        float volumeDuration)
    {
        return Mathf.Max(
            0.05f,
            sustainedDuration,
            fullscreenDuration,
            volumeDuration);
    }

    public void RecordBulletDestroyed(Vector3 worldPosition)
    {
        SpawnAnimatedCombatText(
            "Text | Bullet Destroyed",
            "탄 파괴됨..",
            bulletDestroyedTextColor,
            worldPosition,
            1f,
            killComboTextDuration);
    }

    private void SpawnAnimatedCombatText(
        string objectName,
        string message,
        Color color,
        Vector3 worldPosition,
        float scaleMultiplier,
        float duration,
        int horizontalDirection = 0,
        bool useDodgeMotion = false)
    {
        TextMeshPro text;
        GameObject textObject;
        Vector3 prefabScale;

        if (killComboTextPrefab != null)
        {
            text = Instantiate(killComboTextPrefab);
            textObject = text.gameObject;
            prefabScale = textObject.transform.localScale;
        }
        else
        {
            textObject = new GameObject(
                objectName,
                typeof(TextMeshPro));
            text = textObject.GetComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = 6f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.outlineColor = new Color(0.08f, 0.02f, 0.01f, 0.95f);
            text.outlineWidth = 0.18f;
            text.sortingOrder = short.MaxValue - 8;
            prefabScale = Vector3.one * killComboTextScale;
        }

        textObject.name = objectName;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.text = message;
        Vector2 preferredSize = text.GetPreferredValues(message);
        Vector2 textAreaSize = text.rectTransform.sizeDelta;
        textAreaSize.x = Mathf.Max(textAreaSize.x, preferredSize.x + 1f);
        text.rectTransform.sizeDelta = textAreaSize;
        color.a = 1f;
        text.color = color;
        textObject.transform.position = worldPosition
            + new Vector3(0f, 0.72f, -1f);
        Vector3 targetScale = prefabScale * Mathf.Max(0.01f, scaleMultiplier);
        textObject.transform.localScale = targetScale * 0.12f;
        spawnedComboTexts.Add(textObject);

        if (useDodgeMotion)
        {
            StartCoroutine(AnimateDodgeCombatText(
                textObject,
                text,
                targetScale,
                Mathf.Max(0.01f, duration),
                horizontalDirection));
        }
        else
        {
            StartCoroutine(AnimateKillComboText(
                textObject,
                text,
                targetScale,
                Mathf.Max(0.01f, duration),
                horizontalDirection));
        }
    }

    private IEnumerator AnimateDodgeCombatText(
        GameObject textObject,
        TextMeshPro text,
        Vector3 targetScale,
        float duration,
        int horizontalDirection)
    {
        Vector3 startPosition = textObject.transform.position;
        Quaternion startRotation = textObject.transform.rotation;
        Color startColor = text.color;
        float direction = horizontalDirection == 0
            ? 1f
            : Mathf.Sign(horizontalDirection);
        float elapsed = 0f;

        while (elapsed < duration && textObject != null)
        {
            yield return null;

            if (textObject == null || text == null)
            {
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float entrance = Mathf.Clamp01(progress / 0.16f);
            float entranceEase = 1f - Mathf.Pow(1f - entrance, 4f);
            float overshoot = Mathf.Sin(entrance * Mathf.PI) * 0.5f;
            float release = Mathf.InverseLerp(0.42f, 1f, progress);
            float releaseEase = Mathf.SmoothStep(0f, 1f, release);
            Vector3 settledScale = targetScale
                * (Mathf.Lerp(0.12f, 1f, entranceEase) + overshoot);
            Vector3 releasedScale = new Vector3(
                targetScale.x * 1.22f,
                targetScale.y * 0.82f,
                targetScale.z);
            textObject.transform.localScale = Vector3.Lerp(
                settledScale,
                releasedScale,
                releaseEase);
            textObject.transform.rotation = startRotation
                * Quaternion.Euler(
                    0f,
                    0f,
                    -direction * Mathf.Sin(progress * Mathf.PI) * 7f
                        * (1f - releaseEase));
            textObject.transform.position = startPosition + new Vector3(
                direction * Mathf.Lerp(0f, 0.38f, releaseEase),
                Mathf.Lerp(0f, 0.28f, Mathf.SmoothStep(0f, 1f, progress)),
                0f);
            Color color = startColor;
            color.a = 1f - releaseEase;
            text.color = color;
        }

        spawnedComboTexts.Remove(textObject);

        if (textObject != null)
        {
            Destroy(textObject);
        }
    }

    private IEnumerator AnimateKillComboText(
        GameObject textObject,
        TextMeshPro text,
        Vector3 targetScale,
        float duration,
        int horizontalDirection)
    {
        Vector3 startPosition = textObject.transform.position;
        Quaternion startRotation = textObject.transform.rotation;
        Color startColor = text.color;
        float horizontalDrift = horizontalDirection == 0
            ? Random.Range(-0.12f, 0.12f)
            : Mathf.Sign(horizontalDirection) * 0.18f;
        float rotationDirection = horizontalDirection == 0
            ? Random.value < 0.5f ? -1f : 1f
            : -Mathf.Sign(horizontalDirection);
        float elapsed = 0f;

        while (elapsed < duration && textObject != null)
        {
            yield return null;

            if (textObject == null || text == null)
            {
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(
                elapsed / duration);
            float entrance = Mathf.Clamp01(progress / 0.22f);
            float entranceEase = 1f - Mathf.Pow(1f - entrance, 3f);
            float pop = Mathf.Sin(entrance * Mathf.PI) * 0.42f;
            float settleProgress = Mathf.Clamp01(
                (progress - 0.22f) / 0.38f);
            float settle = Mathf.Sin(settleProgress * Mathf.PI * 4f)
                * (1f - settleProgress)
                * 0.09f;
            float scale = Mathf.Lerp(0.12f, 1f, entranceEase)
                + pop + settle;
            textObject.transform.localScale = targetScale * scale;
            float rotationEnvelope = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(progress / 0.55f));
            float rotation = rotationDirection
                * Mathf.Sin(progress * Mathf.PI * 7f)
                * 9f
                * rotationEnvelope;
            textObject.transform.rotation = startRotation
                * Quaternion.Euler(0f, 0f, rotation);
            float entranceShake = Mathf.Sin(progress * Mathf.PI * 10f)
                * rotationEnvelope
                * 0.055f;
            textObject.transform.position = startPosition + new Vector3(
                horizontalDrift * progress
                    + entranceShake * rotationDirection,
                Mathf.SmoothStep(0f, 0.5f, progress),
                0f);
            Color color = startColor;
            color.a = 1f - Mathf.SmoothStep(0f, 1f, progress);
            text.color = color;
        }

        spawnedComboTexts.Remove(textObject);

        if (textObject != null)
        {
            Destroy(textObject);
        }
    }

    private void ClearComboKillTexts()
    {
        foreach (GameObject textObject in spawnedComboTexts)
        {
            if (textObject != null)
            {
                Destroy(textObject);
            }
        }

        spawnedComboTexts.Clear();
    }

    private void StartDodgeAfterimages(
        Vector3 dodgeOrigin,
        int horizontalDirection,
        Color accentColor,
        int authoredCount,
        float interval,
        float duration,
        float originGhostDuration)
    {
        if (GamePauseController.IsPaused || authoredCount <= 0
            || duration <= 0f)
        {
            return;
        }

        SpriteRenderer[] sources =
            GetComponentsInChildren<SpriteRenderer>(true);
        int afterimageCount = CalculateDodgeAfterimageCount(
            authoredCount,
            CombatAccessibilitySettings.ParticleDensityMultiplier);

        if (afterimageCount <= 0 || sources.Length == 0)
        {
            return;
        }

        if (dodgeTrailCoroutine != null)
        {
            StopCoroutine(dodgeTrailCoroutine);
            dodgeTrailCoroutine = null;
        }

        Vector3 travel = ResolveDodgeTravel(
            transform.position - dodgeOrigin,
            horizontalDirection);
        Vector3 originOffset = dodgeOrigin - transform.position;

        foreach (SpriteRenderer source in sources)
        {
            if (!IsValidDodgeAfterimageSource(source))
            {
                continue;
            }

            SpawnDodgeAfterimage(
                source,
                originOffset,
                travel,
                accentColor,
                0,
                afterimageCount,
                Mathf.Max(duration, originGhostDuration),
                true);
        }

        dodgeTrailCoroutine = StartCoroutine(DodgeTrailRoutine(
            sources,
            dodgeOrigin,
            horizontalDirection,
            accentColor,
            afterimageCount,
            Mathf.Max(0.01f, interval),
            duration));
    }

    private IEnumerator DodgeTrailRoutine(
        SpriteRenderer[] sources,
        Vector3 dodgeOrigin,
        int horizontalDirection,
        Color accentColor,
        int afterimageCount,
        float interval,
        float duration)
    {
        for (int afterimageIndex = 0;
             afterimageIndex < afterimageCount;
             afterimageIndex++)
        {
            if (afterimageIndex > 0)
            {
                yield return WaitForDodgeTrailInterval(interval);
            }

            Vector3 travel = ResolveDodgeTravel(
                transform.position - dodgeOrigin,
                horizontalDirection);

            foreach (SpriteRenderer source in sources)
            {
                if (!IsValidDodgeAfterimageSource(source))
                {
                    continue;
                }

                SpawnDodgeAfterimage(
                    source,
                    Vector3.zero,
                    travel,
                    accentColor,
                    afterimageIndex,
                    afterimageCount,
                    duration,
                    false);
            }
        }

        dodgeTrailCoroutine = null;
    }

    private static IEnumerator WaitForDodgeTrailInterval(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                elapsed += Time.unscaledDeltaTime;
            }
        }
    }

    private static bool IsValidDodgeAfterimageSource(
        SpriteRenderer source)
    {
        return source != null && source.enabled && source.sprite != null;
    }

    private static Vector3 ResolveDodgeTravel(
        Vector3 travel,
        int horizontalDirection)
    {
        if (travel.sqrMagnitude > 0.0001f)
        {
            return travel;
        }

        int direction = horizontalDirection == 0
            ? 1
            : horizontalDirection;
        return Vector3.right * direction * 0.5f;
    }

    internal static int CalculateDodgeAfterimageCount(
        int authoredCount,
        float densityMultiplier)
    {
        if (authoredCount <= 0 || densityMultiplier <= 0f)
        {
            return 0;
        }

        return Mathf.Clamp(
            Mathf.RoundToInt(authoredCount * densityMultiplier),
            1,
            authoredCount);
    }

    private void SpawnDodgeAfterimage(
        SpriteRenderer source,
        Vector3 positionOffset,
        Vector3 travel,
        Color accentColor,
        int afterimageIndex,
        int afterimageCount,
        float duration,
        bool isOriginGhost)
    {
        GameObject afterimage = new GameObject(
            isOriginGhost
                ? "Sprite | Dodge Origin Ghost"
                : $"Sprite | Dodge Afterimage {afterimageIndex + 1}");
        afterimage.transform.position = source.transform.position
            + positionOffset;
        afterimage.transform.rotation = source.transform.rotation;
        afterimage.transform.localScale = source.transform.lossyScale;

        SpriteRenderer renderer = afterimage.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.flipX = source.flipX;
        renderer.flipY = source.flipY;
        renderer.drawMode = source.drawMode;
        renderer.size = source.size;
        renderer.maskInteraction = source.maskInteraction;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder
            - afterimageIndex - (isOriginGhost ? 1 : 2);

        float normalizedIndex = afterimageCount <= 1
            ? 0f
            : afterimageIndex / (float)(afterimageCount - 1);
        Color color = Color.Lerp(
            source.color,
            accentColor,
            isOriginGhost ? 0.9f : 0.76f);
        color.a = (isOriginGhost
                ? 0.82f
                : Mathf.Lerp(0.72f, 0.28f, normalizedIndex))
            * CombatAccessibilitySettings.FlashMultiplier;
        renderer.color = color;
        activeDodgeAfterimages.Add(afterimage);
        StartCoroutine(AnimateDodgeAfterimage(
            afterimage,
            renderer,
            color,
            isOriginGhost
                ? Vector3.zero
                : -travel.normalized
                    * Mathf.Lerp(0.08f, 0.16f, normalizedIndex),
            isOriginGhost
                ? duration
                : duration * Mathf.Lerp(1f, 0.72f, normalizedIndex),
            isOriginGhost));
    }

    private IEnumerator AnimateDodgeAfterimage(
        GameObject afterimage,
        SpriteRenderer renderer,
        Color startColor,
        Vector3 drift,
        float duration,
        bool isOriginGhost)
    {
        Vector3 startPosition = afterimage.transform.position;
        Vector3 startScale = afterimage.transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration && afterimage != null
               && renderer != null)
        {
            yield return null;

            if (afterimage == null || renderer == null)
            {
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float release = Mathf.SmoothStep(0f, 1f, progress);
            afterimage.transform.position = startPosition + drift * release;
            afterimage.transform.localScale = Vector3.Lerp(
                startScale,
                new Vector3(
                    startScale.x * (isOriginGhost ? 1.15f : 1.08f),
                    startScale.y * (isOriginGhost ? 0.86f : 0.92f),
                    startScale.z),
                release);
            Color color = startColor;
            color.a = startColor.a * (1f - release);
            renderer.color = color;
        }

        activeDodgeAfterimages.Remove(afterimage);

        if (afterimage != null)
        {
            Destroy(afterimage);
        }
    }

    private void ClearDodgeAfterimages()
    {
        if (dodgeTrailCoroutine != null)
        {
            StopCoroutine(dodgeTrailCoroutine);
            dodgeTrailCoroutine = null;
        }

        foreach (GameObject afterimage in activeDodgeAfterimages)
        {
            if (afterimage != null)
            {
                Destroy(afterimage);
            }
        }

        activeDodgeAfterimages.Clear();
    }

    private void HandleCountCompleted(int _)
    {
        if (comboCount <= 0 || comboCountsRemaining <= 0)
        {
            return;
        }

        int previousRemaining = comboCountsRemaining;
        comboCountsRemaining = CalculateRemainingComboCounts(
            comboCountsRemaining,
            comboResetDuringCurrentCount);
        comboResetDuringCurrentCount = false;

        if (comboCountsRemaining == previousRemaining)
        {
            RefreshComboCountValues();
            return;
        }

        int drainedIndex = Mathf.Clamp(
            comboCountsRemaining,
            0,
            Mathf.Max(0, comboCountValues.Count - 1));
        StopCountDrainAnimation();

        if (comboCountValues.Count == 0)
        {
            if (comboCountsRemaining <= 0)
            {
                ExpireCombo();
            }

            return;
        }

        countDrainCoroutine = StartCoroutine(DrainComboCount(
            comboCountValues[drainedIndex],
            comboCountsRemaining <= 0));
    }

    internal static int CalculateRemainingComboCounts(
        int remainingCounts,
        bool defeatOccurredDuringCount)
    {
        int sanitizedRemaining = Mathf.Max(0, remainingCounts);
        return defeatOccurredDuringCount
            ? sanitizedRemaining
            : Mathf.Max(0, sanitizedRemaining - 1);
    }

    private IEnumerator DrainComboCount(
        Image countValue,
        bool expireAfterDrain)
    {
        float duration = Mathf.Max(0.01f, countDrainDuration);
        float elapsed = 0f;
        float startFill = countValue == null ? 1f : countValue.fillAmount;

        while (elapsed < duration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;

            if (countValue != null)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                countValue.fillAmount = Mathf.Lerp(
                    startFill,
                    0f,
                    progress);
            }
        }

        if (countValue != null)
        {
            countValue.fillAmount = 0f;
        }

        countDrainCoroutine = null;

        if (expireAfterDrain && comboCountsRemaining <= 0)
        {
            ExpireCombo();
        }
    }

    private void StopCountDrainAnimation()
    {
        if (countDrainCoroutine == null)
        {
            return;
        }

        StopCoroutine(countDrainCoroutine);
        countDrainCoroutine = null;
    }

    private void ExpireCombo()
    {
        SoundManager.ResetComboPitch();
        comboCount = 0;
        comboCountsRemaining = 0;
        comboResetDuringCurrentCount = false;
        cylinderDamage = 0;
        displayedCylinderDamage = 0f;
        damageHoldRemaining = 0f;
        comboPunchRemaining = 0.22f;
        comboPunchStrengthMultiplier = 1f;
        UpdateComboText();
        UpdateDamageText(false);
        RefreshComboCountValues();
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
        comboCountCanvasGroup.alpha = comboCanvasGroup.alpha;
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
            0.34f * comboPunchStrengthMultiplier,
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
            comboText.color = GetComboColor();
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
        comboText.color = GetComboColor();
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
            ? Color.Lerp(damageBaseColor, comboMidColor, 0.55f)
            : damageBaseColor;
    }

    private Color GetComboColor()
    {
        if (comboCount >= 10)
        {
            return comboCriticalColor;
        }

        if (comboCount >= 5)
        {
            return comboMidColor;
        }

        return comboLowColor;
    }

    internal float GetFiringSequenceFeedbackMultiplier(int killCount)
    {
        return CalculateFiringSequenceFeedbackMultiplier(
            killCount,
            firingSequenceFeedbackStrengthPerKill);
    }

    internal static float CalculateFiringSequenceFeedbackMultiplier(
        int killCount,
        float strengthPerKill)
    {
        return 1f + Mathf.Max(0, killCount - 1)
            * Mathf.Max(0f, strengthPerKill);
    }

    private void ResetFiringSequenceFeedback()
    {
        firingSequenceDefeatCount = 0;
        firingSequenceBaseIntensity = 0f;
    }

    private void RestoreActiveKillFeedback()
    {
        if (volumePulseCoroutine != null)
        {
            StopCoroutine(volumePulseCoroutine);
            volumePulseCoroutine = null;
        }

        RestoreVolume();
        CancelSlowMotionAndRestore();
        ResetFullscreenImpact();
        ClearComboKillTexts();
        ClearDodgeAfterimages();
        defeatPresentationGeneration++;
        nextDefeatPresentationTime = defeatPresentationClock;
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
        comboCountRoot = FindDescendant(feedbackPanel, ComboCountRootName);
        CollectComboCountValues();
        currentDamageText = FindDescendant(feedbackPanel, CurrentDamageTextName)
            ?.GetComponent<TMP_Text>();

        if (comboText == null || comboCountRoot == null
            || comboCountValues.Count == 0
            || currentDamageText == null)
        {
            return;
        }

        foreach (Image countValue in comboCountValues)
        {
            countValue.type = Image.Type.Filled;
        }

        comboRect = comboText.rectTransform;
        damageRect = currentDamageText.rectTransform;
        comboBaseScale = comboRect.localScale;
        damageBaseScale = damageRect.localScale;
        comboBaseRotation = comboRect.localRotation;
        damageBaseRotation = damageRect.localRotation;
        damageBaseColor = currentDamageText.color;
        comboCanvasGroup = GetOrAddCanvasGroup(comboText.gameObject);
        comboCountCanvasGroup = GetOrAddCanvasGroup(comboCountRoot.gameObject);
        damageCanvasGroup = GetOrAddCanvasGroup(currentDamageText.gameObject);
        comboCanvasGroup.alpha = comboCount > 0 ? 1f : 0f;
        comboCountCanvasGroup.alpha = comboCanvasGroup.alpha;
        damageCanvasGroup.alpha = cylinderActive ? 1f : 0f;
        uiBound = true;
        UpdateComboText();
        UpdateDamageText(false);
        RefreshComboCountValues();
    }

    private void CollectComboCountValues()
    {
        comboCountValues.Clear();

        if (comboCountRoot == null)
        {
            return;
        }

        foreach (Transform countSlot in comboCountRoot)
        {
            foreach (Image image in countSlot.GetComponentsInChildren<Image>(true))
            {
                bool isCountValue = image.name.Contains("Count")
                    || image.name.Contains("Turn");
                if (isCountValue && image.name.Contains("Value"))
                {
                    comboCountValues.Add(image);
                    break;
                }
            }
        }
    }

    private int GetComboCountLimit()
    {
        int configuredLimit = Mathf.Max(1, comboCountLimit);
        return comboCountValues.Count == 0
            ? configuredLimit
            : Mathf.Min(configuredLimit, comboCountValues.Count);
    }

    private void RefreshComboCountValues()
    {
        int visibleCounts = comboCount <= 0
            ? 0
            : Mathf.Clamp(
                comboCountsRemaining,
                0,
                comboCountValues.Count);

        for (int index = 0; index < comboCountValues.Count; index++)
        {
            Image countValue = comboCountValues[index];

            if (countValue != null)
            {
                countValue.fillAmount = index < visibleCounts ? 1f : 0f;
            }
        }
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
        RefreshComboCountValues();
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
        StartVolumePulse(
            intensity,
            volumePulseDuration,
            1f,
            1f,
            1f,
            1f,
            1f);
    }

    private void StartDodgeVolumePulse(float sustainedDuration)
    {
        StartVolumePulse(
            dodgeVolumeStrength,
            Mathf.Max(dodgeVolumeDuration, sustainedDuration),
            dodgeChromaticMultiplier,
            dodgeBloomMultiplier,
            dodgeVignetteMultiplier,
            dodgeLensDistortionMultiplier,
            dodgeContrastMultiplier);
    }

    private void StartVolumePulse(
        float intensity,
        float duration,
        float chromaticMultiplier,
        float bloomMultiplier,
        float vignetteMultiplier,
        float lensDistortionMultiplier,
        float contrastMultiplier)
    {
        if (GamePauseController.IsPaused || duration <= 0f)
        {
            return;
        }

        intensity *= CombatAccessibilitySettings.FlashMultiplier;

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
            volumePulseCoroutine = null;
        }

        volumePulseCoroutine = StartCoroutine(VolumePulseRoutine(
            currentVolumePulseStrength,
            intensity,
            duration,
            Mathf.Max(0f, chromaticMultiplier),
            Mathf.Max(0f, bloomMultiplier),
            Mathf.Max(0f, vignetteMultiplier),
            Mathf.Max(0f, lensDistortionMultiplier),
            Mathf.Max(0f, contrastMultiplier)));
    }

    private IEnumerator VolumePulseRoutine(
        float startStrength,
        float intensity,
        float duration,
        float chromaticMultiplier,
        float bloomMultiplier,
        float vignetteMultiplier,
        float lensDistortionMultiplier,
        float contrastMultiplier)
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

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            const float attackPortion = 0.16f;
            float pulse;

            if (progress < attackPortion)
            {
                float attack = Mathf.SmoothStep(
                    0f,
                    1f,
                    progress / attackPortion);
                pulse = Mathf.Lerp(startStrength, intensity, attack);
            }
            else
            {
                float release = Mathf.InverseLerp(
                    attackPortion,
                    1f,
                    progress);
                pulse = intensity
                    * (1f - Mathf.SmoothStep(0f, 1f, release));
            }

            currentVolumePulseStrength = pulse;
            chromaticAberration.intensity.value = Mathf.Clamp01(
                chromaticBase
                + chromaticBoost * chromaticMultiplier * pulse);
            bloom.intensity.value = bloomBase
                + bloomBoost * bloomMultiplier * pulse;
            vignette.intensity.value = Mathf.Clamp01(
                vignetteBase
                + vignetteBoost * vignetteMultiplier * pulse);
            lensDistortion.intensity.value = Mathf.Clamp(
                lensBase
                + lensDistortionBoost
                * lensDistortionMultiplier
                * pulse,
                -1f,
                1f);
            colorAdjustments.contrast.value = Mathf.Clamp(
                contrastBase
                + contrastBoost * contrastMultiplier * pulse,
                -100f,
                100f);
        }

        RestoreVolume();
        currentVolumePulseStrength = 0f;
        volumePulseCoroutine = null;
    }

    private void RestoreVolume()
    {
        currentVolumePulseStrength = 0f;

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

    private void StartSlowMotion(
        float intensity,
        float strongestScale,
        float holdDuration,
        float recoveryDuration,
        float strengthMultiplier = 1f)
    {
        if (GamePauseController.IsPaused)
        {
            return;
        }

        float timeEffectMultiplier =
            CombatAccessibilitySettings.TimeEffectMultiplier;
        if (timeEffectMultiplier <= 0f)
        {
            return;
        }

        float scale = CalculateSlowMotionScale(
            intensity,
            strongestScale,
            strengthMultiplier,
            timeEffectMultiplier);
        EnsureTimeEffectOwnership();
        slowMotionStartScale = slowMotionActive
            ? slowMotionCurrentScale
            : slowMotionBaseScale;
        slowMotionCurrentScale = slowMotionStartScale;
        slowMotionTargetScale = scale;
        slowMotionAttackDuration = Mathf.Min(
            0.035f,
            recoveryDuration * 0.25f);
        slowMotionHoldDuration = Mathf.Max(0f, holdDuration);
        slowMotionHasSecondaryPhase = false;
        slowMotionSecondaryTargetScale = scale;
        slowMotionSecondaryTransitionDuration = 0f;
        slowMotionSecondaryHoldDuration = 0f;
        slowMotionRecoveryDuration = Mathf.Max(0.01f, recoveryDuration);
        slowMotionElapsed = 0f;
        slowMotionActive = true;
        EnsureTimeEffectRoutine();
    }

    private void StartDodgeSlowMotion()
    {
        if (GamePauseController.IsPaused)
        {
            return;
        }

        float timeEffectMultiplier =
            CombatAccessibilitySettings.TimeEffectMultiplier;
        if (timeEffectMultiplier <= 0f)
        {
            return;
        }

        float snapScale = CalculateSlowMotionScale(
            1f,
            dodgeInitialSlowMotionScale,
            1f,
            timeEffectMultiplier);
        float glideScale = CalculateSlowMotionScale(
            1f,
            dodgeSlowMotionScale,
            1f,
            timeEffectMultiplier);
        EnsureTimeEffectOwnership();
        slowMotionStartScale = slowMotionActive
            ? slowMotionCurrentScale
            : slowMotionBaseScale;
        slowMotionCurrentScale = slowMotionStartScale;
        slowMotionTargetScale = snapScale;
        slowMotionAttackDuration = Mathf.Min(
            0.02f,
            dodgeInitialSlowMotionDuration * 0.25f);
        slowMotionHoldDuration = Mathf.Max(
            0f,
            dodgeInitialSlowMotionDuration);
        slowMotionHasSecondaryPhase = true;
        slowMotionSecondaryTargetScale = glideScale;
        slowMotionSecondaryTransitionDuration = Mathf.Max(
            0.01f,
            dodgeSlowMotionGlideDuration);
        slowMotionSecondaryHoldDuration = Mathf.Max(
            0f,
            dodgeSlowMotionHold);
        slowMotionRecoveryDuration = Mathf.Max(
            0.01f,
            dodgeSlowMotionRecovery);
        slowMotionElapsed = 0f;
        slowMotionActive = true;
        EnsureTimeEffectRoutine();
    }

    internal static float CalculateSlowMotionScale(
        float intensity,
        float strongestScale,
        float strengthMultiplier,
        float timeEffectMultiplier)
    {
        float baseScale = Mathf.Lerp(
            0.72f,
            Mathf.Clamp(strongestScale, 0.01f, 1f),
            Mathf.Clamp01(intensity));
        return Mathf.Clamp(
            1f - (1f - baseScale)
            * Mathf.Max(0f, strengthMultiplier)
            * Mathf.Max(0f, timeEffectMultiplier),
            0.05f,
            1f);
    }

    public void RequestHitStop(float duration)
    {
        if (GamePauseController.IsPaused)
        {
            return;
        }

        duration *= CombatAccessibilitySettings.TimeEffectMultiplier;

        if (duration <= 0f)
        {
            return;
        }

        EnsureTimeEffectOwnership();
        hitStopRemaining = Mathf.Max(hitStopRemaining, duration);
        Time.timeScale = 0f;
        EnsureTimeEffectRoutine();
    }

    private void EnsureTimeEffectOwnership()
    {
        if (ownsTimeScale)
        {
            return;
        }

        slowMotionBaseScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        slowMotionCurrentScale = slowMotionBaseScale;
        ownsTimeScale = true;
    }

    private void EnsureTimeEffectRoutine()
    {
        if (timeEffectCoroutine == null)
        {
            timeEffectCoroutine = StartCoroutine(TimeEffectRoutine());
        }
    }

    private IEnumerator TimeEffectRoutine()
    {
        while (hitStopRemaining > 0f || slowMotionActive)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                RestoreSlowMotion();
                timeEffectCoroutine = null;
                yield break;
            }

            float deltaTime = Time.unscaledDeltaTime;

            if (hitStopRemaining > 0f)
            {
                hitStopRemaining = Mathf.Max(
                    0f,
                    hitStopRemaining - deltaTime);
                Time.timeScale = 0f;
                continue;
            }

            slowMotionElapsed += deltaTime;
            float primaryHoldEnd = slowMotionAttackDuration
                + slowMotionHoldDuration;
            float secondaryTransitionEnd = primaryHoldEnd
                + (slowMotionHasSecondaryPhase
                    ? slowMotionSecondaryTransitionDuration
                    : 0f);
            float secondaryHoldEnd = secondaryTransitionEnd
                + (slowMotionHasSecondaryPhase
                    ? slowMotionSecondaryHoldDuration
                    : 0f);
            float recoveryEnd = secondaryHoldEnd
                + slowMotionRecoveryDuration;

            if (slowMotionElapsed < slowMotionAttackDuration)
            {
                float progress = slowMotionAttackDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        slowMotionElapsed / slowMotionAttackDuration);
                slowMotionCurrentScale = Mathf.Lerp(
                    slowMotionStartScale,
                    slowMotionTargetScale,
                    Mathf.SmoothStep(0f, 1f, progress));
            }
            else if (slowMotionElapsed < primaryHoldEnd)
            {
                slowMotionCurrentScale = slowMotionTargetScale;
            }
            else if (slowMotionElapsed < secondaryTransitionEnd)
            {
                float progress = Mathf.Clamp01(
                    (slowMotionElapsed - primaryHoldEnd)
                    / slowMotionSecondaryTransitionDuration);
                slowMotionCurrentScale = Mathf.Lerp(
                    slowMotionTargetScale,
                    slowMotionSecondaryTargetScale,
                    Mathf.SmoothStep(0f, 1f, progress));
            }
            else if (slowMotionElapsed < secondaryHoldEnd)
            {
                slowMotionCurrentScale = slowMotionSecondaryTargetScale;
            }
            else if (slowMotionElapsed < recoveryEnd)
            {
                float progress = Mathf.Clamp01(
                    (slowMotionElapsed - secondaryHoldEnd)
                    / slowMotionRecoveryDuration);
                slowMotionCurrentScale = Mathf.Lerp(
                    slowMotionHasSecondaryPhase
                        ? slowMotionSecondaryTargetScale
                        : slowMotionTargetScale,
                    slowMotionBaseScale,
                    Mathf.SmoothStep(0f, 1f, progress));
            }
            else
            {
                slowMotionCurrentScale = slowMotionBaseScale;
                slowMotionActive = false;
            }

            Time.timeScale = slowMotionCurrentScale;
        }

        RestoreSlowMotion();
        timeEffectCoroutine = null;
    }

    private void RestoreSlowMotion()
    {
        if (ownsTimeScale)
        {
            Time.timeScale = slowMotionBaseScale;
        }

        hitStopRemaining = 0f;
        slowMotionActive = false;
        slowMotionElapsed = 0f;
        slowMotionCurrentScale = slowMotionBaseScale;
        slowMotionHasSecondaryPhase = false;
        slowMotionSecondaryTargetScale = slowMotionBaseScale;
        slowMotionSecondaryTransitionDuration = 0f;
        slowMotionSecondaryHoldDuration = 0f;
        ownsTimeScale = false;
    }

    private void CancelSlowMotionAndRestore()
    {
        if (timeEffectCoroutine != null)
        {
            StopCoroutine(timeEffectCoroutine);
            timeEffectCoroutine = null;
        }

        RestoreSlowMotion();
    }

    private void QueueFullscreenImpact(
        Vector3 worldPosition,
        int horizontalDirection,
        float intensity,
        float duration,
        CombatImpactTier impactTier,
        Color impactColor,
        bool wasFinalEnemy,
        float feedbackMultiplier = 1f,
        bool shotPulse = false)
    {
        intensity *= CombatAccessibilitySettings.FlashMultiplier;

        if (!fullscreenImpactEnabled || duration <= 0f || intensity <= 0f)
        {
            return;
        }

        int activeSlotMask = 0;
        for (int impactIndex = 0;
             impactIndex < fullscreenImpacts.Length;
             impactIndex++)
        {
            if (fullscreenImpacts[impactIndex].Active)
            {
                activeSlotMask |= 1 << impactIndex;
            }
        }

        int selectedIndex = FindFirstAvailableFullscreenImpactSlot(
            activeSlotMask,
            fullscreenImpacts.Length);

        Camera mainCamera = Camera.main;
        Vector3 viewportPoint = mainCamera == null
            ? new Vector3(0.5f, 0.5f, 1f)
            : mainCamera.WorldToViewportPoint(worldPosition);
        FullscreenImpactState impact = new FullscreenImpactState
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
            FeedbackMultiplier = Mathf.Max(0f, feedbackMultiplier),
            Tier = impactTier,
            Color = impactColor,
            FinalKill = wasFinalEnemy,
            ShotPulse = shotPulse
        };

        if (selectedIndex < 0)
        {
            pendingFullscreenImpacts.Enqueue(impact);
        }
        else
        {
            fullscreenImpacts[selectedIndex] = impact;
        }

        ApplyFullscreenGlobals();
    }

    internal static int FindFirstAvailableFullscreenImpactSlot(
        int activeSlotMask,
        int capacity)
    {
        int safeCapacity = Mathf.Clamp(capacity, 0, sizeof(int) * 8);

        for (int slotIndex = 0; slotIndex < safeCapacity; slotIndex++)
        {
            if ((activeSlotMask & 1 << slotIndex) == 0)
            {
                return slotIndex;
            }
        }

        return -1;
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

            ActivatePendingFullscreenImpacts();
        }

        ApplyFullscreenGlobals();
    }

    private void ActivatePendingFullscreenImpacts()
    {
        for (int impactIndex = 0;
             impactIndex < fullscreenImpacts.Length
             && pendingFullscreenImpacts.Count > 0;
             impactIndex++)
        {
            if (!fullscreenImpacts[impactIndex].Active)
            {
                fullscreenImpacts[impactIndex] =
                    pendingFullscreenImpacts.Dequeue();
            }
        }
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
            float strength = EvaluateFullscreenImpactStrength(impact);
            fullscreenCenters[impactIndex] = new Vector4(
                impact.Center.x,
                impact.Center.y,
                0f,
                0f);
            fullscreenDirections[impactIndex] = new Vector4(
                impact.Direction.x,
                impact.Direction.y,
                impact.ShotPulse ? 1f : 0f,
                0f);
            fullscreenParams[impactIndex] = new Vector4(
                progress,
                strength,
                (float)impact.Tier / (float)CombatImpactTier.Defeat,
                impact.FinalKill ? 1f : 0f);
            Color impactColor = impact.Color;
            impactColor.a = 1f;
            fullscreenColors[impactIndex] = impactColor;
            maximumStrength = Mathf.Max(maximumStrength, strength);

        }

        Shader.SetGlobalVectorArray(FullscreenCentersId, fullscreenCenters);
        Shader.SetGlobalVectorArray(
            FullscreenDirectionsId,
            fullscreenDirections);
        Shader.SetGlobalVectorArray(FullscreenParamsId, fullscreenParams);
        Shader.SetGlobalVectorArray(FullscreenColorsId, fullscreenColors);
        Shader.SetGlobalColor(FullscreenColorId, fullscreenImpactColor);
        Shader.SetGlobalFloat(
            FullscreenAspectId,
            Screen.height <= 0 ? 1f : (float)Screen.width / Screen.height);
        Shader.SetGlobalFloat(FullscreenShockwaveId, shockwaveStrength);
        Shader.SetGlobalFloat(FullscreenRgbSplitId, rgbSplitStrength);
        Shader.SetGlobalFloat(FullscreenRadialZoomId, radialZoomStrength);
        Shader.SetGlobalFloat(FullscreenTearId, directionalTearStrength);
        Shader.SetGlobalFloat(
            FullscreenIntensityId,
            GamePauseController.IsPaused ? 0f : maximumStrength);
    }

    private static float EvaluateFullscreenImpactStrength(
        FullscreenImpactState impact)
    {
        if (!impact.Active || impact.Duration <= 0f)
        {
            return 0f;
        }

        const float attackPortion = 0.1f;
        float progress = Mathf.Clamp01(impact.Elapsed / impact.Duration);
        float targetStrength = impact.Intensity * impact.FeedbackMultiplier;

        if (progress < attackPortion)
        {
            float attack = Mathf.SmoothStep(
                0f,
                1f,
                progress / attackPortion);
            return targetStrength * attack;
        }

        float release = Mathf.InverseLerp(attackPortion, 1f, progress);
        return targetStrength
            * (1f - Mathf.SmoothStep(0f, 1f, release));
    }

    private void ResetFullscreenImpact()
    {
        pendingFullscreenImpacts.Clear();

        for (int impactIndex = 0;
             impactIndex < fullscreenImpacts.Length;
             impactIndex++)
        {
            fullscreenImpacts[impactIndex] = default;
            fullscreenCenters[impactIndex] = Vector4.zero;
            fullscreenDirections[impactIndex] = Vector4.zero;
            fullscreenParams[impactIndex] = Vector4.zero;
            fullscreenColors[impactIndex] = Vector4.zero;
        }

        Shader.SetGlobalVectorArray(FullscreenCentersId, fullscreenCenters);
        Shader.SetGlobalVectorArray(
            FullscreenDirectionsId,
            fullscreenDirections);
        Shader.SetGlobalVectorArray(FullscreenParamsId, fullscreenParams);
        Shader.SetGlobalVectorArray(FullscreenColorsId, fullscreenColors);
        Shader.SetGlobalFloat(FullscreenIntensityId, 0f);
    }

    private static void HideFullscreenImpact()
    {
        Shader.SetGlobalFloat(FullscreenIntensityId, 0f);
    }

    public void CancelPresentationForPause()
    {
        if (volumePulseCoroutine != null)
        {
            StopCoroutine(volumePulseCoroutine);
            volumePulseCoroutine = null;
        }

        CancelSlowMotionAndRestore();
        RestoreVolume();
        HideFullscreenImpact();
        ClearDodgeAfterimages();
    }

}
