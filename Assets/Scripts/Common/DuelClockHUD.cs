using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DuelClockHUD : MonoBehaviour
{
    internal const int CurrentLayoutVersion = 4;

    [Header("State Source")]
    [SerializeField] private WaveManager waveManager;

    [SerializeField, HideInInspector] private int authoredLayoutVersion;

    [Header("View")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image progressFill;
    [SerializeField] private Color progressStartColor =
        new Color32(247, 191, 62, 255);
    [SerializeField] private Color progressEndColor =
        new Color32(231, 77, 42, 255);
    [SerializeField, Min(0.01f)] private float fillLerpSpeed = 12f;
    [SerializeField, Min(0.01f)] private float beatFillLerpSpeed = 28f;
    [SerializeField, Min(0f)] private float beatFullHoldDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float beatPulseDuration = 0.36f;
    [SerializeField, Min(1f)] private float beatPulseScale = 1.12f;
    [SerializeField] private Color beatPulseColor =
        new Color32(247, 191, 62, 255);
    [SerializeField] private TMP_Text titleText;
    [FormerlySerializedAs("beatCountText")]
    [SerializeField] private TMP_Text enemyCountText;
    [SerializeField] private Color allEnemiesSpawnedTextColor =
        new Color32(145, 148, 158, 255);
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text actionPreviewText;

    private readonly DuelClockFillAnimation fillAnimation =
        new DuelClockFillAnimation();
    private DuelClockController clockController;
    private Coroutine delayedBindRoutine;
    private Image hudBackdrop;
    private Color baseBackdropColor;
    private Vector3 baseHudScale = Vector3.one;
    private int displayedProgress = int.MinValue;
    private long displayedRemainingEnemyCount = long.MinValue;
    private int displayedNextWaveProgress = int.MinValue;
    private int displayedEnemyWaveCount = int.MinValue;
    private bool displayedAllEnemiesSpawned;
    private Color activeEnemyCountTextColor = Color.white;
    private float pulseElapsed;
    private bool pulseActive;
    private bool enemyProgressDirty = true;

    private void Awake()
    {
        canvasGroup ??= GetComponent<CanvasGroup>();

        if (enemyCountText != null)
        {
            activeEnemyCountTextColor = enemyCountText.color;
        }

        CachePulseView();
        ResolveWaveManager();
        SetVisible(false);
    }

    private void OnEnable()
    {
        CachePulseView();
        ResolveWaveManager();
        SubscribeToWaveManager();
        enemyProgressDirty = true;
        TryBindClockController();
        ScheduleDelayedBind();
    }

    private void LateUpdate()
    {
        if (GamePauseController.IsPaused
            || FirstRunGuideController.IsGuidePanelOpen)
        {
            return;
        }

        float unscaledDeltaTime = Time.unscaledDeltaTime;

        if (progressFill != null && fillAnimation.IsInitialized)
        {
            DuelClockFillFrame frame = fillAnimation.Advance(
                progressFill.fillAmount,
                fillLerpSpeed,
                beatFillLerpSpeed,
                beatFullHoldDuration,
                unscaledDeltaTime);
            progressFill.fillAmount = frame.FillAmount;
            UpdateProgressFillColor(frame.FillAmount);
            UpdateProgressText(frame.FillAmount);

            if (frame.BeatReached)
            {
                TriggerBeatPulse();
            }
        }

        UpdateBeatPulse(unscaledDeltaTime);
    }

    private void OnDisable()
    {
        if (delayedBindRoutine != null)
        {
            StopCoroutine(delayedBindRoutine);
            delayedBindRoutine = null;
        }

        UnsubscribeFromWaveManager();
        BindClockController(null);
        fillAnimation.Clear();
        ResetPulseVisual();
    }

    private void HandleWaveStateChanged()
    {
        enemyProgressDirty = true;
        TryBindClockController();
        ScheduleDelayedBind();
    }

    private void HandleClockStateChanged()
    {
        Refresh();
    }

    private void TryBindClockController()
    {
        DuelClockController candidate = waveManager == null
            ? null
            : waveManager.GetComponent<DuelClockController>();
        BindClockController(candidate);
    }

    private void BindClockController(DuelClockController candidate)
    {
        if (clockController == candidate)
        {
            Refresh();
            return;
        }

        if (clockController != null)
        {
            clockController.StateChanged -= HandleClockStateChanged;
        }

        clockController = candidate;

        if (clockController != null)
        {
            clockController.StateChanged += HandleClockStateChanged;
        }

        ResetDisplayedValues();
        Refresh();
    }

    private void ScheduleDelayedBind()
    {
        if (!isActiveAndEnabled || delayedBindRoutine != null)
        {
            return;
        }

        delayedBindRoutine = StartCoroutine(BindAfterCurrentFrame());
    }

    private IEnumerator BindAfterCurrentFrame()
    {
        yield return null;
        delayedBindRoutine = null;
        TryBindClockController();
    }

    private void Refresh()
    {
        bool visible = clockController != null && clockController.IsActive;
        SetVisible(visible);

        if (!visible)
        {
            fillAnimation.Clear();
            ResetPulseVisual();
            return;
        }

        double progress = clockController.Progress;
        float normalizedProgress = Mathf.Clamp01(
            (float)(progress / DuelClockState.CycleLength));

        if (!fillAnimation.IsInitialized)
        {
            fillAnimation.Reset(
                normalizedProgress,
                clockController.CumulativeBeats);

            if (progressFill != null)
            {
                progressFill.fillAmount = normalizedProgress;
                UpdateProgressFillColor(normalizedProgress);
                UpdateProgressText(normalizedProgress);
            }
        }
        else
        {
            fillAnimation.Observe(
                normalizedProgress,
                clockController.CumulativeBeats);
        }

        if (titleText != null && titleText.text != "DUEL CLOCK")
        {
            titleText.text = "DUEL CLOCK";
        }

        if (progressFill == null)
        {
            UpdateProgressText(normalizedProgress);
        }

        RefreshEnemyProgress();
        RefreshNextWaveProgress();
    }

    internal static string FormatProgressPercentage(int wholeProgress)
    {
        return $"{Mathf.Clamp(wholeProgress, 0, 100)}%";
    }

    internal static string FormatRemainingEnemyCount(long remainingCount)
    {
        return $"남은 적 수: {Math.Max(0L, remainingCount)}";
    }

    internal static string FormatNextWaveProgress(
        int currentCount,
        int enemyWaveCount)
    {
        int sanitizedTotal = Mathf.Max(1, enemyWaveCount);
        int sanitizedCurrent = Mathf.Clamp(
            currentCount,
            0,
            sanitizedTotal - 1);
        return $"적 스폰까지 ({sanitizedCurrent}/{sanitizedTotal})";
    }

    internal static string FormatAllEnemiesSpawned()
    {
        return "모든 적 스폰됨";
    }

    internal static float CalculateBeatPulseStrength(float normalizedTime)
    {
        if (!IsFinite(normalizedTime))
        {
            return 0f;
        }

        float clampedTime = Mathf.Clamp01(normalizedTime);
        float sine = Mathf.Max(
            0f,
            Mathf.Sin(clampedTime * Mathf.PI));
        return Mathf.Pow(sine, 0.55f);
    }

    internal static Vector3 SanitizeScale(Vector3 scale)
    {
        return IsFinite(scale.x)
            && IsFinite(scale.y)
            && IsFinite(scale.z)
            ? scale
            : Vector3.one;
    }

    internal static float CalculateSmoothedFill(
        float current,
        float target,
        float speed,
        float unscaledDeltaTime)
    {
        float clampedTarget = Mathf.Clamp01(target);

        if (speed <= 0f)
        {
            return clampedTarget;
        }

        if (unscaledDeltaTime <= 0f)
        {
            return Mathf.Clamp01(current);
        }

        float lerpAmount = 1f - Mathf.Exp(
            -speed * unscaledDeltaTime);
        return Mathf.Lerp(
            Mathf.Clamp01(current),
            clampedTarget,
            lerpAmount);
    }

    internal static Color EvaluateProgressColor(
        float normalizedProgress,
        Color startColor,
        Color endColor)
    {
        float progress = IsFinite(normalizedProgress)
            ? Mathf.Clamp01(normalizedProgress)
            : 0f;
        return Color.Lerp(startColor, endColor, progress);
    }

    private void RefreshEnemyProgress()
    {
        if (!enemyProgressDirty)
        {
            return;
        }

        EnemyBattleProgress progress = waveManager == null
            ? new EnemyBattleProgress(0L, 0L)
            : waveManager.EnemyProgress;

        if (actionPreviewText != null
            && displayedRemainingEnemyCount != progress.RemainingCount)
        {
            actionPreviewText.text = FormatRemainingEnemyCount(
                progress.RemainingCount);
        }

        displayedRemainingEnemyCount = progress.RemainingCount;
        enemyProgressDirty = false;
    }

    private void UpdateProgressText(float normalizedProgress)
    {
        int wholeProgress = Mathf.Clamp(
            Mathf.FloorToInt(Mathf.Clamp01(normalizedProgress) * 100f),
            0,
            100);

        if (progressText != null && displayedProgress != wholeProgress)
        {
            progressText.text = FormatProgressPercentage(wholeProgress);
        }

        displayedProgress = wholeProgress;
    }

    private void UpdateProgressFillColor(float normalizedProgress)
    {
        if (progressFill != null)
        {
            progressFill.color = EvaluateProgressColor(
                normalizedProgress,
                progressStartColor,
                progressEndColor);
        }
    }

    private void RefreshNextWaveProgress()
    {
        if (clockController == null)
        {
            return;
        }

        int nextWaveProgress = clockController.EnemyWaveProgress;
        int enemyWaveCount = clockController.EnemyWaveCount;
        bool allEnemiesSpawned = waveManager == null
            || !waveManager.HasRemainingEnemiesToSpawn;

        if (enemyCountText != null
            && (displayedNextWaveProgress != nextWaveProgress
                || displayedEnemyWaveCount != enemyWaveCount
                || displayedAllEnemiesSpawned != allEnemiesSpawned))
        {
            enemyCountText.text = allEnemiesSpawned
                ? FormatAllEnemiesSpawned()
                : FormatNextWaveProgress(
                    nextWaveProgress,
                    enemyWaveCount);
            enemyCountText.color = allEnemiesSpawned
                ? allEnemiesSpawnedTextColor
                : activeEnemyCountTextColor;
        }

        displayedNextWaveProgress = nextWaveProgress;
        displayedEnemyWaveCount = enemyWaveCount;
        displayedAllEnemiesSpawned = allEnemiesSpawned;
    }

    private void CachePulseView()
    {
        hudBackdrop ??= GetComponent<Image>();
        Vector3 currentScale = transform.localScale;
        bool hasValidScale = IsFinite(currentScale.x)
            && IsFinite(currentScale.y)
            && IsFinite(currentScale.z);
        baseHudScale = SanitizeScale(currentScale);

        if (!hasValidScale)
        {
            transform.localScale = baseHudScale;
        }

        if (hudBackdrop != null)
        {
            baseBackdropColor = hudBackdrop.color;
        }
    }

    private void TriggerBeatPulse()
    {
        pulseElapsed = 0f;
        pulseActive = true;
    }

    private void UpdateBeatPulse(float unscaledDeltaTime)
    {
        if (!pulseActive)
        {
            return;
        }

        float duration = IsFinite(beatPulseDuration)
            ? Mathf.Max(0.01f, beatPulseDuration)
            : 0.01f;
        float deltaTime = IsFinite(unscaledDeltaTime)
            ? Mathf.Max(0f, unscaledDeltaTime)
            : 0f;
        pulseElapsed = IsFinite(pulseElapsed)
            ? pulseElapsed + deltaTime
            : duration;
        float normalizedTime = Mathf.Clamp01(pulseElapsed / duration);
        float strength = CalculateBeatPulseStrength(normalizedTime);
        float targetScale = IsFinite(beatPulseScale)
            ? Mathf.Max(1f, beatPulseScale)
            : 1f;
        baseHudScale = SanitizeScale(baseHudScale);
        Vector3 animatedScale = baseHudScale
            * Mathf.Lerp(1f, targetScale, strength);
        transform.localScale = SanitizeScale(animatedScale);

        if (hudBackdrop != null)
        {
            hudBackdrop.color = Color.Lerp(
                baseBackdropColor,
                beatPulseColor,
                strength);
        }

        if (normalizedTime >= 1f)
        {
            ResetPulseVisual();
        }
    }

    private void ResetPulseVisual()
    {
        pulseActive = false;
        pulseElapsed = 0f;
        baseHudScale = SanitizeScale(baseHudScale);
        transform.localScale = baseHudScale;

        if (hudBackdrop != null)
        {
            hudBackdrop.color = baseBackdropColor;
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void ResolveWaveManager()
    {
        if (waveManager == null)
        {
            waveManager = FindFirstObjectByType<WaveManager>();
        }
    }

    private void SubscribeToWaveManager()
    {
        if (waveManager != null)
        {
            waveManager.StateChanged -= HandleWaveStateChanged;
            waveManager.StateChanged += HandleWaveStateChanged;
        }
    }

    private void UnsubscribeFromWaveManager()
    {
        if (waveManager != null)
        {
            waveManager.StateChanged -= HandleWaveStateChanged;
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void ResetDisplayedValues()
    {
        displayedProgress = int.MinValue;
        displayedRemainingEnemyCount = long.MinValue;
        displayedNextWaveProgress = int.MinValue;
        displayedEnemyWaveCount = int.MinValue;
        displayedAllEnemiesSpawned = false;
        enemyProgressDirty = true;
        fillAnimation.Clear();
        ResetPulseVisual();
    }
}

internal readonly struct DuelClockFillFrame
{
    public DuelClockFillFrame(float fillAmount, bool beatReached)
    {
        FillAmount = fillAmount;
        BeatReached = beatReached;
    }

    public float FillAmount { get; }
    public bool BeatReached { get; }
}

internal sealed class DuelClockFillAnimation
{
    private const float CompletionEpsilon = 0.0025f;

    private FillPhase phase;
    private float liveTarget;
    private float holdElapsed;
    private long observedCumulativeBeats = long.MinValue;
    private long pendingBeatCount;

    public bool IsInitialized =>
        observedCumulativeBeats != long.MinValue;

    public void Clear()
    {
        phase = FillPhase.FollowingClock;
        liveTarget = 0f;
        holdElapsed = 0f;
        observedCumulativeBeats = long.MinValue;
        pendingBeatCount = 0L;
    }

    public void Reset(float normalizedProgress, long cumulativeBeats)
    {
        phase = FillPhase.FollowingClock;
        liveTarget = Mathf.Clamp01(normalizedProgress);
        holdElapsed = 0f;
        observedCumulativeBeats = Math.Max(0L, cumulativeBeats);
        pendingBeatCount = 0L;
    }

    public void Observe(float normalizedProgress, long cumulativeBeats)
    {
        float sanitizedTarget = Mathf.Clamp01(normalizedProgress);
        long sanitizedBeats = Math.Max(0L, cumulativeBeats);

        if (!IsInitialized || sanitizedBeats < observedCumulativeBeats)
        {
            Reset(sanitizedTarget, sanitizedBeats);
            return;
        }

        liveTarget = sanitizedTarget;
        long addedBeats = sanitizedBeats - observedCumulativeBeats;
        observedCumulativeBeats = sanitizedBeats;

        if (addedBeats <= 0L)
        {
            return;
        }

        pendingBeatCount = addedBeats > long.MaxValue - pendingBeatCount
            ? long.MaxValue
            : pendingBeatCount + addedBeats;

        if (phase == FillPhase.FollowingClock
            || phase == FillPhase.FillingOverflow)
        {
            phase = FillPhase.FillingBeat;
        }
    }

    public DuelClockFillFrame Advance(
        float currentFill,
        float normalSpeed,
        float beatSpeed,
        float fullHoldDuration,
        float unscaledDeltaTime)
    {
        float current = Mathf.Clamp01(currentFill);

        if (!IsInitialized || unscaledDeltaTime <= 0f)
        {
            return new DuelClockFillFrame(current, false);
        }

        switch (phase)
        {
            case FillPhase.FillingBeat:
                current = DuelClockHUD.CalculateSmoothedFill(
                    current,
                    1f,
                    beatSpeed,
                    unscaledDeltaTime);

                if (1f - current <= CompletionEpsilon)
                {
                    current = 1f;
                    holdElapsed = 0f;
                    phase = FillPhase.HoldingBeat;
                    return new DuelClockFillFrame(current, true);
                }

                break;

            case FillPhase.HoldingBeat:
                holdElapsed += unscaledDeltaTime;

                if (holdElapsed >= Mathf.Max(0f, fullHoldDuration))
                {
                    pendingBeatCount = Math.Max(0L, pendingBeatCount - 1L);
                    current = 0f;
                    phase = pendingBeatCount > 0L
                        ? FillPhase.FillingBeat
                        : FillPhase.FillingOverflow;
                }

                break;

            case FillPhase.FillingOverflow:
                current = DuelClockHUD.CalculateSmoothedFill(
                    current,
                    liveTarget,
                    beatSpeed,
                    unscaledDeltaTime);

                if (Mathf.Abs(current - liveTarget) <= CompletionEpsilon)
                {
                    current = liveTarget;
                    phase = FillPhase.FollowingClock;
                }

                break;

            default:
                current = DuelClockHUD.CalculateSmoothedFill(
                    current,
                    liveTarget,
                    normalSpeed,
                    unscaledDeltaTime);
                break;
        }

        return new DuelClockFillFrame(current, false);
    }

    private enum FillPhase
    {
        FollowingClock,
        FillingBeat,
        HoldingBeat,
        FillingOverflow
    }
}
