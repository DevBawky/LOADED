using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public enum CombatImpactTier
{
    Normal = 0,
    Critical = 1,
    Devastating = 2,
    Defeat = 3
}

public static class CombatImpactTierUtility
{
    public const float DevastatingDamageRatio = 0.6f;

    public static CombatImpactTier Resolve(
        bool isCritical,
        int damage,
        int targetMaxHealth,
        bool isDefeated)
    {
        if (isDefeated)
        {
            return CombatImpactTier.Defeat;
        }

        if (targetMaxHealth > 0
            && (float)damage / targetMaxHealth >= DevastatingDamageRatio)
        {
            return CombatImpactTier.Devastating;
        }

        return isCritical
            ? CombatImpactTier.Critical
            : CombatImpactTier.Normal;
    }
}

[DisallowMultipleComponent]
public sealed class CombatAccessibilitySettings : MonoBehaviour
{
    private const string PresentationIntensityPreferenceKey =
        "Combat.Presentation.Intensity";

    private static CombatAccessibilitySettings instance;
    private static bool hasLoadedPresentationIntensity;
    private static float presentationIntensity = 0.7f;

    [Header("Combat Presentation Accessibility")]
    [SerializeField] private bool reduceScreenFlashes;
    [SerializeField] private bool reduceCameraShake;
    [SerializeField] private bool reduceTimeEffects;
    [SerializeField] private bool reduceParticleDensity;

    public static float PresentationIntensity
    {
        get
        {
            LoadPresentationIntensity();
            return presentationIntensity;
        }
    }

    public static float FlashMultiplier => PresentationIntensity
        * (instance != null && instance.reduceScreenFlashes ? 0.28f : 1f);
    public static float CameraShakeMultiplier => PresentationIntensity
        * (instance != null && instance.reduceCameraShake ? 0f : 1f);
    public static float TimeEffectMultiplier => PresentationIntensity
        * (instance != null && instance.reduceTimeEffects ? 0f : 1f);
    public static float ParticleDensityMultiplier => PresentationIntensity
        * (instance != null && instance.reduceParticleDensity ? 0.45f : 1f);

    private void Awake()
    {
        instance = this;
        LoadPresentationIntensity();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static void Ensure(GameObject host)
    {
        if (instance != null || host == null)
        {
            return;
        }

        instance = host.GetComponent<CombatAccessibilitySettings>();

        if (instance == null)
        {
            instance = host.AddComponent<CombatAccessibilitySettings>();
        }
    }

    public void SetReduceScreenFlashes(bool value) =>
        reduceScreenFlashes = value;
    public void SetReduceCameraShake(bool value) =>
        reduceCameraShake = value;
    public void SetReduceTimeEffects(bool value) =>
        reduceTimeEffects = value;
    public void SetReduceParticleDensity(bool value) =>
        reduceParticleDensity = value;

    public static void SetPresentationIntensity(float value)
    {
        presentationIntensity = Mathf.Clamp01(value);
        hasLoadedPresentationIntensity = true;
        PlayerPrefs.SetFloat(
            PresentationIntensityPreferenceKey,
            presentationIntensity);
        PlayerPrefs.Save();
    }

    private static void LoadPresentationIntensity()
    {
        if (hasLoadedPresentationIntensity)
        {
            return;
        }

        presentationIntensity = Mathf.Clamp01(PlayerPrefs.GetFloat(
            PresentationIntensityPreferenceKey,
            0.7f));
        hasLoadedPresentationIntensity = true;
    }
}

[DefaultExecutionOrder(12000)]
public sealed class CombatCameraShake : MonoBehaviour
{
    private const float DefaultDuration = 0.18f;
    private const float StrengthComparisonTolerance = 0.0001f;

    [Header("Unified Shake Envelope")]
    [Range(0f, 0.5f)]
    [SerializeField] private float attackRatio = 0.12f;
    [Range(0.1f, 1f)]
    [SerializeField] private float rotationReturnRatio = 0.45f;
    [Min(0f)]
    [SerializeField] private float noiseFrequency = 1.2f;

    private static CombatCameraShake instance;
    private Coroutine shakeRoutine;
    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private CinemachineBasicMultiChannelPerlin cinemachineNoise;
    private float baseNoiseAmplitude;
    private float baseNoiseFrequency;
    private bool noiseBaseCaptured;
    private float activeStrength;
    private float activeDuration;
    private float elapsed;
    private float startingStrength;
    private float noiseSeed;

    public static void Play(float strength)
    {
        Play(strength, DefaultDuration);
    }

    public static void Play(float strength, float duration)
    {
        strength *= CombatAccessibilitySettings.CameraShakeMultiplier;

        if (strength <= 0f
            || duration <= 0f
            || Camera.main == null)
        {
            return;
        }

        if (instance == null)
        {
            instance = Camera.main.GetComponent<CombatCameraShake>();
        }

        if (instance == null)
        {
            instance = Camera.main.gameObject.AddComponent<CombatCameraShake>();
        }

        instance.RequestShake(strength, duration);
    }

    private void Awake()
    {
        instance = this;
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = Quaternion.identity;
        BindCinemachineNoise();

        if (cinemachineNoise == null)
        {
            transform.localRotation = baseLocalRotation;
        }
    }

    private void RequestShake(float strength, float duration)
    {
        bool isActive = shakeRoutine != null;
        if (isActive
            && strength < activeStrength - StrengthComparisonTolerance)
        {
            return;
        }

        BindCinemachineNoise();
        startingStrength = GetCurrentAppliedStrength();
        activeStrength = Mathf.Max(activeStrength, strength);
        activeDuration = duration;
        elapsed = 0f;

        if (!isActive)
        {
            ReseedShakePattern();
            shakeRoutine = StartCoroutine(ShakeRoutine());
        }
    }

    private void ReseedShakePattern()
    {
        if (cinemachineNoise != null)
        {
            // Give each independent shake sequence a fresh, unbiased X/Y
            // phase. Never reseed while a shake is already visible: changing
            // the phase at non-zero amplitude causes a one-sided camera jump,
            // especially when a defeat shake strengthens the shot shake.
            cinemachineNoise.ReSeed();
            return;
        }

        noiseSeed = Random.Range(0f, 1000f);
    }

    private IEnumerator ShakeRoutine()
    {
        while (elapsed < activeDuration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = activeDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / activeDuration);
            ApplyStrength(EvaluateEnvelope(progress));
        }

        RestoreCameraTransform();
        activeStrength = 0f;
        activeDuration = 0f;
        elapsed = 0f;
        startingStrength = 0f;
        shakeRoutine = null;
    }

    private float EvaluateEnvelope(float progress)
    {
        float clampedAttackRatio = Mathf.Clamp(attackRatio, 0f, 0.5f);
        if (clampedAttackRatio > 0f && progress < clampedAttackRatio)
        {
            float attackProgress = Mathf.SmoothStep(
                0f,
                1f,
                progress / clampedAttackRatio);
            return Mathf.Lerp(
                startingStrength,
                activeStrength,
                attackProgress);
        }

        float releaseProgress = clampedAttackRatio >= 1f
            ? 1f
            : Mathf.InverseLerp(clampedAttackRatio, 1f, progress);
        return activeStrength
            * (1f - Mathf.SmoothStep(0f, 1f, releaseProgress));
    }

    private void ApplyStrength(float strength)
    {
        if (cinemachineNoise != null)
        {
            cinemachineNoise.AmplitudeGain = baseNoiseAmplitude + strength;
            cinemachineNoise.FrequencyGain = Mathf.Max(0f, noiseFrequency);
            return;
        }

        float sampleTime = Time.unscaledTime * 18f;
        float offsetX = Mathf.PerlinNoise(noiseSeed, sampleTime) * 2f - 1f;
        float offsetY = Mathf.PerlinNoise(noiseSeed + 37.1f, sampleTime) * 2f - 1f;
        transform.localPosition = baseLocalPosition
            + new Vector3(offsetX, offsetY, 0f) * strength;
    }

    private float GetCurrentAppliedStrength()
    {
        if (cinemachineNoise != null && noiseBaseCaptured)
        {
            return Mathf.Max(
                0f,
                cinemachineNoise.AmplitudeGain - baseNoiseAmplitude);
        }

        return Vector3.Distance(transform.localPosition, baseLocalPosition);
    }

    private void LateUpdate()
    {
        if (shakeRoutine == null)
        {
            transform.localRotation = baseLocalRotation;
            return;
        }

        float progress = activeDuration <= 0f
            ? 1f
            : Mathf.Clamp01(elapsed / activeDuration);
        float returnStart = 1f - Mathf.Clamp01(rotationReturnRatio);
        if (progress <= returnStart)
        {
            return;
        }

        float returnProgress = Mathf.InverseLerp(
            returnStart,
            1f,
            progress);
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            baseLocalRotation,
            Mathf.SmoothStep(0f, 1f, returnProgress));
    }

    private void OnDisable()
    {
        RestoreCameraTransform();
        shakeRoutine = null;
        activeStrength = 0f;

        if (instance == this)
        {
            instance = null;
        }
    }

    private void RestoreCameraTransform()
    {
        if (cinemachineNoise != null && noiseBaseCaptured)
        {
            cinemachineNoise.AmplitudeGain = baseNoiseAmplitude;
            cinemachineNoise.FrequencyGain = baseNoiseFrequency;
        }
        else
        {
            transform.localPosition = baseLocalPosition;
        }

        transform.localRotation = baseLocalRotation;
    }

    private void BindCinemachineNoise()
    {
        cinemachineNoise ??=
            GetComponent<CinemachineBasicMultiChannelPerlin>();

        if (cinemachineNoise == null || noiseBaseCaptured)
        {
            return;
        }

        baseNoiseAmplitude = cinemachineNoise.AmplitudeGain;
        baseNoiseFrequency = cinemachineNoise.FrequencyGain;
        noiseBaseCaptured = true;
    }
}
