using System.Collections;
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

public sealed class CombatCameraShake : MonoBehaviour
{
    private static CombatCameraShake instance;
    private Coroutine shakeRoutine;
    private Vector3 baseLocalPosition;

    public static void Play(float strength)
    {
        strength *= CombatAccessibilitySettings.CameraShakeMultiplier;

        if (strength <= 0f || Camera.main == null)
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

        instance.StartShake(strength);
    }

    private void Awake()
    {
        instance = this;
        baseLocalPosition = transform.localPosition;
    }

    private void StartShake(float strength)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            RestoreCameraTransform();
        }

        baseLocalPosition = transform.localPosition;
        transform.localRotation = Quaternion.identity;
        shakeRoutine = StartCoroutine(ShakeRoutine(strength));
    }

    private IEnumerator ShakeRoutine(float strength)
    {
        const float duration = 0.18f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * strength * fade;
            transform.localPosition = baseLocalPosition
                + new Vector3(offset.x, offset.y, 0f);
        }

        RestoreCameraTransform();
        shakeRoutine = null;
    }

    private void OnDisable()
    {
        RestoreCameraTransform();
        shakeRoutine = null;

        if (instance == this)
        {
            instance = null;
        }
    }

    private void RestoreCameraTransform()
    {
        transform.localPosition = baseLocalPosition;
        transform.localRotation = Quaternion.identity;
    }
}
