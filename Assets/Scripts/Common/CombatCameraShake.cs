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
    private static CombatAccessibilitySettings instance;

    [Header("Combat Presentation Accessibility")]
    [SerializeField] private bool reduceScreenFlashes;
    [SerializeField] private bool reduceCameraShake;
    [SerializeField] private bool reduceTimeEffects;
    [SerializeField] private bool reduceParticleDensity;

    public static float FlashMultiplier =>
        instance != null && instance.reduceScreenFlashes ? 0.28f : 1f;
    public static float CameraShakeMultiplier =>
        instance != null && instance.reduceCameraShake ? 0f : 1f;
    public static float TimeEffectMultiplier =>
        instance != null && instance.reduceTimeEffects ? 0f : 1f;
    public static float ParticleDensityMultiplier =>
        instance != null && instance.reduceParticleDensity ? 0.45f : 1f;

    private void Awake()
    {
        instance = this;
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
}

[DefaultExecutionOrder(10000)]
public sealed class CombatCameraShake : MonoBehaviour
{
    private const float DefaultDuration = 0.18f;
    private const float MinimumNoiseFrequency = 1.2f;

    [Header("Guaranteed Smooth Return")]
    [Min(0.1f)]
    [SerializeField] private float minimumReturnDuration = 0.35f;
    [Min(2)]
    [SerializeField] private int minimumReturnFrameCount = 12;

    private static CombatCameraShake instance;
    private Coroutine shakeRoutine;
    private Vector3 baseLocalPosition;
    private CinemachineBasicMultiChannelPerlin cinemachineNoise;
    private float baseNoiseAmplitude;
    private float baseNoiseFrequency;
    private bool noiseBaseCaptured;
    private bool isRotationReturning;
    private Quaternion rotationReturnStart = Quaternion.identity;
    private float rotationReturnProgress;

    public static void Play(float strength)
    {
        Play(strength, DefaultDuration);
    }

    public static void Play(float strength, float duration)
    {
        PlayInternal(
            strength,
            duration,
            duration,
            MinimumNoiseFrequency,
            true,
            true);
    }

    public static void PlayBulletRecoil(
        float strength,
        float attackDuration,
        float recoveryDuration,
        float frequency)
    {
        PlayInternal(
            strength,
            Mathf.Max(0f, attackDuration),
            Mathf.Max(0f, recoveryDuration),
            Mathf.Max(0f, frequency),
            false,
            false);
    }

    private static void PlayInternal(
        float strength,
        float shakeDuration,
        float returnDuration,
        float frequency,
        bool startAtFullStrength,
        bool decayDuringShake)
    {
        strength *= CombatAccessibilitySettings.CameraShakeMultiplier;

        if (strength <= 0f
            || shakeDuration <= 0f && returnDuration <= 0f
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

        instance.StartShake(
            strength,
            shakeDuration,
            returnDuration,
            frequency,
            startAtFullStrength,
            decayDuringShake);
    }

    private void Awake()
    {
        instance = this;
        baseLocalPosition = transform.localPosition;
        BindCinemachineNoise();

        if (cinemachineNoise == null)
        {
            transform.localRotation = Quaternion.identity;
        }
    }

    private void StartShake(
        float strength,
        float shakeDuration,
        float returnDuration,
        float frequency,
        bool startAtFullStrength,
        bool decayDuringShake)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        isRotationReturning = false;

        BindCinemachineNoise();

        if (cinemachineNoise != null && startAtFullStrength)
        {
            cinemachineNoise.AmplitudeGain = Mathf.Max(
                cinemachineNoise.AmplitudeGain,
                baseNoiseAmplitude + strength);
            cinemachineNoise.FrequencyGain = Mathf.Max(
                baseNoiseFrequency,
                frequency);
        }

        shakeRoutine = StartCoroutine(ShakeRoutine(
            strength,
            shakeDuration,
            returnDuration,
            frequency,
            decayDuringShake));
    }

    private IEnumerator ShakeRoutine(
        float strength,
        float requestedShakeDuration,
        float requestedReturnDuration,
        float frequency,
        bool decayDuringShake)
    {
        const float returnStartStrengthRatio = 0.2f;
        float shakeDuration = requestedShakeDuration;
        float returnDuration = Mathf.Max(
            requestedReturnDuration,
            minimumReturnDuration);
        int requiredReturnFrames = Mathf.Max(2, minimumReturnFrameCount);
        float startingAmplitude = cinemachineNoise == null
            ? 0f
            : cinemachineNoise.AmplitudeGain;
        Vector3 startingFallbackOffset = transform.localPosition
            - baseLocalPosition;
        float noiseSeed = Random.Range(0f, 1000f);
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = shakeDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / shakeDuration);
            float fade = decayDuringShake
                ? Mathf.Lerp(
                    1f,
                    returnStartStrengthRatio,
                    Mathf.SmoothStep(0f, 1f, progress))
                : 1f;
            float attack = Mathf.SmoothStep(0f, 1f, progress);

            if (cinemachineNoise != null)
            {
                cinemachineNoise.AmplitudeGain = Mathf.Lerp(
                    startingAmplitude,
                    baseNoiseAmplitude + strength * fade,
                    attack);
                cinemachineNoise.FrequencyGain = Mathf.Max(
                    baseNoiseFrequency,
                    frequency);
            }
            else
            {
                float sampleTime = elapsed * 18f;
                float offsetX = Mathf.PerlinNoise(noiseSeed, sampleTime) * 2f - 1f;
                float offsetY = Mathf.PerlinNoise(noiseSeed + 37.1f, sampleTime) * 2f - 1f;
                Vector3 noiseOffset = new Vector3(offsetX, offsetY, 0f)
                    * strength * fade;
                transform.localPosition = baseLocalPosition
                    + Vector3.Lerp(startingFallbackOffset, noiseOffset, attack);
            }
        }

        Vector3 returnStartPosition = transform.localPosition;
        Quaternion returnStartRotation = transform.localRotation;
        float returnStartAmplitude = cinemachineNoise == null
            ? 0f
            : cinemachineNoise.AmplitudeGain;

        if (cinemachineNoise != null)
        {
            cinemachineNoise.FrequencyGain = 0f;
            rotationReturnStart = returnStartRotation;
            rotationReturnProgress = 0f;
            isRotationReturning = true;
        }

        elapsed = 0f;
        int returnFrameCount = 0;

        while (elapsed < returnDuration
               || returnFrameCount < requiredReturnFrames)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            returnFrameCount++;
            float timeProgress = returnDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / returnDuration);
            float frameProgress = Mathf.Clamp01(
                (float)returnFrameCount / requiredReturnFrames);
            float progress = Mathf.Min(timeProgress, frameProgress);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            rotationReturnProgress = eased;
            if (cinemachineNoise != null)
            {
                cinemachineNoise.AmplitudeGain = Mathf.Lerp(
                    returnStartAmplitude,
                    baseNoiseAmplitude,
                    eased);
            }
            else
            {
                transform.localPosition = Vector3.Lerp(
                    returnStartPosition,
                    baseLocalPosition,
                    eased);
                transform.localRotation = Quaternion.Lerp(
                    returnStartRotation,
                    Quaternion.identity,
                    eased);
            }
        }

        RestoreCameraTransform();
        shakeRoutine = null;
    }

    private void LateUpdate()
    {
        if (!isRotationReturning)
        {
            return;
        }

        transform.localRotation = Quaternion.Slerp(
            rotationReturnStart,
            Quaternion.identity,
            rotationReturnProgress);

        if (rotationReturnProgress >= 1f)
        {
            transform.localRotation = Quaternion.identity;
            isRotationReturning = false;
        }
    }

    private void OnDisable()
    {
        isRotationReturning = false;
        RestoreCameraTransform();
        shakeRoutine = null;

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
            transform.localRotation = Quaternion.identity;
        }
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
