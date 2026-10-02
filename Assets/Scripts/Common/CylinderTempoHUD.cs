using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CylinderTempoHUD : MonoBehaviour
{
    private static readonly int ResolveId = Shader.PropertyToID("_Resolve");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");

    [Header("State References")]
    [SerializeField] private DuelClockController tempoController;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private BoardManager boardManager;

    [Header("Tempo Slots")]
    [SerializeField] private Image[] tempoImages =
        new Image[DuelClockController.TempoCapacity];
    [SerializeField] private Material solarResolveMaterial;

    [Header("Enemy Counts")]
    [SerializeField] private TMP_Text spawnLeftText;
    [SerializeField] private TMP_Text leftEnemyText;
    [SerializeField] private string spawnLeftFormat =
        "남은 적: <size=30>{0}";
    [SerializeField] private string leftEnemyFormat =
        "생존 적: <size=30> {0}";

    [Header("Phase")]
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private string playerPhaseLabel = "PLAYER PHASE";
    [SerializeField] private Color playerPhaseColor =
        new Color(0f, 1f, 236f / 255f, 1f);
    [SerializeField] private string enemyPhaseLabel = "ENEMY PHASE";
    [SerializeField] private Color enemyPhaseColor =
        new Color(1f, 64f / 255f, 24f / 255f, 1f);
    [Min(1f)]
    [SerializeField] private float enemyPhasePulseScale = 1.08f;
    [Min(0.01f)]
    [SerializeField] private float enemyPhasePulseSpeed = 1.8f;
    [Range(0f, 1f)]
    [SerializeField] private float enemyPhasePulseBrighten = 0.3f;

    [Header("Full Tempo Feedback")]
    [Min(0.01f)]
    [SerializeField] private float fullPulseDuration = 0.28f;
    [Min(1f)]
    [SerializeField] private float fullPulseScale = 1.1f;

    private readonly Material[] slotMaterials =
        new Material[DuelClockController.TempoCapacity];
    private readonly float[] displayedResolve =
        new float[DuelClockController.TempoCapacity];
    private readonly float[] slotRestAlpha =
        new float[DuelClockController.TempoCapacity];

    private Coroutine tempoAnimation;
    private Vector3 restScale;
    private RectTransform phaseRectTransform;
    private Vector3 phaseRestScale = Vector3.one;
    private FontStyles phaseRestFontStyle;
    private bool wasCycleReserved;
    private bool isEnemyPhase;
    private float phasePulseElapsed;

    private void Awake()
    {
        restScale = transform.localScale;
        CapturePhaseRestState();
        CreateSlotMaterials();
        SnapTempoToState();
        RefreshEnemyCounts();
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshEnemyCounts();
        AnimateToCurrentTempo();
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (tempoAnimation != null)
        {
            StopCoroutine(tempoAnimation);
            tempoAnimation = null;
        }

        transform.localScale = restScale;
        ResetSlotPulses();
        ResetPhasePresentation();
    }

    private void OnDestroy()
    {
        for (int index = 0; index < slotMaterials.Length; index++)
        {
            if (slotMaterials[index] != null)
            {
                Destroy(slotMaterials[index]);
            }
        }
    }

    private void OnValidate()
    {
        fullPulseDuration = Mathf.Max(0.01f, fullPulseDuration);
        fullPulseScale = Mathf.Max(1f, fullPulseScale);
        enemyPhasePulseScale = Mathf.Max(1f, enemyPhasePulseScale);
        enemyPhasePulseSpeed = Mathf.Max(0.01f, enemyPhasePulseSpeed);
        enemyPhasePulseBrighten = Mathf.Clamp01(
            enemyPhasePulseBrighten);
    }

    private void Update()
    {
        if (!isEnemyPhase || phaseText == null)
        {
            return;
        }

        phasePulseElapsed += Time.unscaledDeltaTime;
        float pulse = (Mathf.Sin(
                phasePulseElapsed
                * enemyPhasePulseSpeed
                * Mathf.PI
                * 2f)
            + 1f) * 0.5f;

        if (phaseRectTransform != null)
        {
            phaseRectTransform.localScale = phaseRestScale
                * Mathf.Lerp(1f, enemyPhasePulseScale, pulse);
        }

        phaseText.color = Color.Lerp(
            enemyPhaseColor,
            Color.white,
            pulse * enemyPhasePulseBrighten);
    }

    private void Subscribe()
    {
        if (tempoController != null)
        {
            tempoController.StateChanged -= HandleTempoStateChanged;
            tempoController.StateChanged += HandleTempoStateChanged;
        }

        if (waveManager != null)
        {
            waveManager.StateChanged -= HandleWaveStateChanged;
            waveManager.StateChanged += HandleWaveStateChanged;
        }
    }

    private void Unsubscribe()
    {
        if (tempoController != null)
        {
            tempoController.StateChanged -= HandleTempoStateChanged;
        }

        if (waveManager != null)
        {
            waveManager.StateChanged -= HandleWaveStateChanged;
        }
    }

    private void HandleTempoStateChanged()
    {
        AnimateToCurrentTempo();
    }

    private void HandleWaveStateChanged()
    {
        RefreshEnemyCounts();
    }

    private void AnimateToCurrentTempo()
    {
        if (!isActiveAndEnabled || tempoController == null)
        {
            return;
        }

        if (tempoAnimation != null)
        {
            StopCoroutine(tempoAnimation);
            tempoAnimation = null;
            transform.localScale = restScale;
            ResetSlotPulses();
        }

        float targetTempo = Mathf.Clamp(
            (float)tempoController.TempoProgress,
            0f,
            DuelClockController.TempoCapacity);
        bool cycleReserved = tempoController.IsTempoCycleReserved;
        bool shouldPulse = cycleReserved && !wasCycleReserved;

        ApplyTempoImmediately(targetTempo);
        ApplyPhasePresentation(cycleReserved);
        wasCycleReserved = cycleReserved;

        if (shouldPulse)
        {
            tempoAnimation = StartCoroutine(PlayFullPulseRoutine());
        }
    }

    private void ApplyTempoImmediately(float targetTempo)
    {
        int litSlotCount = Mathf.Clamp(
            Mathf.RoundToInt(targetTempo),
            0,
            displayedResolve.Length);

        for (int index = 0; index < displayedResolve.Length; index++)
        {
            SetSlotResolve(index, index < litSlotCount ? 1f : 0f);
            SetSlotPulse(index, 0f);
        }
    }

    private IEnumerator PlayFullPulseRoutine()
    {
        yield return PlayFullPulse();
        tempoAnimation = null;
    }

    private IEnumerator PlayFullPulse()
    {
        float elapsed = 0f;

        while (elapsed < fullPulseDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / fullPulseDuration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            transform.localScale = restScale
                * Mathf.Lerp(1f, fullPulseScale, pulse);

            for (int index = 0; index < slotMaterials.Length; index++)
            {
                SetSlotPulse(index, pulse);
            }
        }

        transform.localScale = restScale;

        ResetSlotPulses();
    }

    private void CreateSlotMaterials()
    {
        if (solarResolveMaterial == null || tempoImages == null)
        {
            return;
        }

        int count = Mathf.Min(tempoImages.Length, slotMaterials.Length);

        for (int index = 0; index < count; index++)
        {
            Image image = tempoImages[index];

            if (image == null)
            {
                continue;
            }

            slotRestAlpha[index] = image.color.a;

            Material material = new Material(solarResolveMaterial)
            {
                name = $"Cylinder Tempo Slot {index + 1} (Instance)",
                hideFlags = HideFlags.HideAndDontSave
            };
            slotMaterials[index] = material;
            image.material = material;
            image.raycastTarget = false;
        }
    }

    private void SnapTempoToState()
    {
        float tempo = tempoController == null
            ? 0f
            : Mathf.Clamp(
                (float)tempoController.TempoProgress,
                0f,
                DuelClockController.TempoCapacity);

        ApplyTempoImmediately(tempo);

        wasCycleReserved = tempoController != null
            && tempoController.IsTempoCycleReserved;
        ApplyPhasePresentation(wasCycleReserved);
    }

    private void CapturePhaseRestState()
    {
        if (phaseText == null)
        {
            return;
        }

        phaseRectTransform = phaseText.rectTransform;
        phaseRestScale = phaseRectTransform == null
            ? Vector3.one
            : phaseRectTransform.localScale;
        phaseRestFontStyle = phaseText.fontStyle;
        phaseText.raycastTarget = false;
    }

    private void ApplyPhasePresentation(bool enemyPhase)
    {
        if (boardManager != null)
        {
            boardManager.SetGridBorderColor(
                enemyPhase ? enemyPhaseColor : playerPhaseColor);
        }
        if (phaseText == null)
        {
            return;
        }

        isEnemyPhase = enemyPhase;
        phasePulseElapsed = 0f;

        if (phaseRectTransform != null)
        {
            phaseRectTransform.localScale = phaseRestScale;
        }

        if (enemyPhase)
        {
            phaseText.text = enemyPhaseLabel;
            phaseText.color = enemyPhaseColor;
            phaseText.fontStyle = phaseRestFontStyle | FontStyles.Bold;
            return;
        }

        phaseText.text = playerPhaseLabel;
        phaseText.color = playerPhaseColor;
        phaseText.fontStyle = phaseRestFontStyle;
    }

    private void ResetPhasePresentation()
    {
        if (boardManager != null)
        {
            boardManager.SetGridBorderColor(null);
        }
        isEnemyPhase = false;
        phasePulseElapsed = 0f;

        if (phaseText == null)
        {
            return;
        }

        if (phaseRectTransform != null)
        {
            phaseRectTransform.localScale = phaseRestScale;
        }

        phaseText.text = playerPhaseLabel;
        phaseText.color = playerPhaseColor;
        phaseText.fontStyle = phaseRestFontStyle;
    }

    private void SetSlotResolve(int index, float value)
    {
        if (index < 0 || index >= displayedResolve.Length)
        {
            return;
        }

        displayedResolve[index] = Mathf.Clamp01(value);

        if (tempoImages != null && index < tempoImages.Length)
        {
            Image image = tempoImages[index];

            if (image != null)
            {
                Color color = image.color;
                color.a = displayedResolve[index] > 0f
                    ? 1f
                    : slotRestAlpha[index];
                image.color = color;
            }
        }

        if (slotMaterials[index] != null)
        {
            slotMaterials[index].SetFloat(
                ResolveId,
                displayedResolve[index]);
        }
    }

    private void SetSlotPulse(int index, float value)
    {
        if (index >= 0 && index < slotMaterials.Length
            && slotMaterials[index] != null)
        {
            slotMaterials[index].SetFloat(PulseId, Mathf.Clamp01(value));
        }
    }

    private void ResetSlotPulses()
    {
        for (int index = 0; index < slotMaterials.Length; index++)
        {
            SetSlotPulse(index, 0f);
        }
    }

    private void RefreshEnemyCounts()
    {
        int remainingSpawnCount = waveManager == null
            ? 0
            : waveManager.RemainingUnspawnedEnemyCount;
        int livingEnemyCount = waveManager == null
            ? 0
            : waveManager.LivingEnemyCount;

        if (spawnLeftText != null)
        {
            spawnLeftText.text = string.Format(
                CultureInfo.InvariantCulture,
                spawnLeftFormat,
                remainingSpawnCount);
        }

        if (leftEnemyText != null)
        {
            leftEnemyText.text = string.Format(
                CultureInfo.InvariantCulture,
                leftEnemyFormat,
                livingEnemyCount);
        }
    }

}
