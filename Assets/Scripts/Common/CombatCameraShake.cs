using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using VolFx;

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

public static class OldMoviePresentationSettings
{
    private const string PreferenceKey = "Presentation.OldMovie.Enabled";
    private static bool enabled = true;

    public static bool Enabled => enabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        enabled = PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static void SetEnabled(bool value)
    {
        PreviewEnabled(value);
        PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void PreviewEnabled(bool value)
    {
        enabled = value;
        ApplyToLoadedVolumes();
    }

    private static void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        ApplyToLoadedVolumes();
    }

    private static void ApplyToLoadedVolumes()
    {
        foreach (Volume volume in Object.FindObjectsByType<Volume>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            ApplyToProfile(volume.sharedProfile);
            ApplyToProfile(volume.profile);
        }
    }

    private static void ApplyToProfile(VolumeProfile profile)
    {
        if (profile != null && profile.TryGet(out OldMovieVol oldMovie))
        {
            oldMovie.active = enabled;
        }
    }
}

public static class GraphicsSaturationSettings
{
    private const string PreferenceKey = "Graphics.Saturation.v2";
    private const string RuntimeVolumeName = "@_GraphicsSaturationVolume";
    private const float DefaultSaturation = 0.85f;
    private const float RuntimeVolumePriority = 10000f;

    private static bool hasLoaded;
    private static float saturation = DefaultSaturation;
    private static Volume runtimeVolume;
    private static VolumeProfile runtimeProfile;
    private static ColorAdjustments colorAdjustments;

    public static float Saturation
    {
        get
        {
            Load();
            return saturation;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        hasLoaded = false;
        Load();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static void SetSaturation(float value)
    {
        PreviewSaturation(value);
        PlayerPrefs.SetFloat(PreferenceKey, saturation);
        PlayerPrefs.Save();
    }

    public static void PreviewSaturation(float value)
    {
        saturation = Mathf.Clamp01(value);
        hasLoaded = true;
        ApplyToLoadedScene();
    }

    private static void Load()
    {
        if (hasLoaded)
        {
            return;
        }

        saturation = Mathf.Clamp01(PlayerPrefs.GetFloat(
            PreferenceKey,
            DefaultSaturation));
        hasLoaded = true;
    }

    private static void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        ApplyToLoadedScene();
    }

    private static void ApplyToLoadedScene()
    {
        Load();
        EnsureRuntimeVolume();

        if (colorAdjustments == null)
        {
            return;
        }

        colorAdjustments.active = true;
        colorAdjustments.saturation.Override(
            Mathf.Lerp(-100f, 0f, saturation));
    }

    private static void EnsureRuntimeVolume()
    {
        if (runtimeVolume != null && colorAdjustments != null)
        {
            return;
        }

        if (runtimeProfile != null)
        {
            Object.Destroy(runtimeProfile);
        }

        GameObject host = new GameObject(RuntimeVolumeName);
        host.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        runtimeVolume = host.AddComponent<Volume>();
        runtimeVolume.isGlobal = true;
        runtimeVolume.priority = RuntimeVolumePriority;
        runtimeVolume.weight = 1f;

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.hideFlags = HideFlags.DontSave;
        colorAdjustments = runtimeProfile.Add<ColorAdjustments>(false);
        colorAdjustments.active = true;
        runtimeVolume.sharedProfile = runtimeProfile;
    }
}

[DisallowMultipleComponent]
public sealed class CombatAccessibilitySettings : MonoBehaviour
{
    private const string PresentationIntensityPreferenceKey =
        "Combat.Presentation.Intensity";
    private const float DefaultPresentationIntensity = 0.5f;

    private static CombatAccessibilitySettings instance;
    private static bool hasLoadedPresentationIntensity;
    private static float presentationIntensity = DefaultPresentationIntensity;

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
        PreviewPresentationIntensity(value);
        PlayerPrefs.SetFloat(
            PresentationIntensityPreferenceKey,
            presentationIntensity);
        PlayerPrefs.Save();
    }

    public static void PreviewPresentationIntensity(float value)
    {
        presentationIntensity = Mathf.Clamp01(value);
        hasLoadedPresentationIntensity = true;
    }

    private static void LoadPresentationIntensity()
    {
        if (hasLoadedPresentationIntensity)
        {
            return;
        }

        presentationIntensity = Mathf.Clamp01(PlayerPrefs.GetFloat(
            PresentationIntensityPreferenceKey,
            DefaultPresentationIntensity));
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

        if (GamePauseController.IsPaused
            || strength <= 0f
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

    public static void CancelForPause()
    {
        instance?.CancelActiveShake();
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
        CancelActiveShake();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void CancelActiveShake()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        RestoreCameraTransform();
        activeStrength = 0f;
        activeDuration = 0f;
        elapsed = 0f;
        startingStrength = 0f;
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
