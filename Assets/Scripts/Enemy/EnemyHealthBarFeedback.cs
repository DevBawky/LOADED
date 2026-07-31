using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnemyHealthBarFeedback : MonoBehaviour
{
    private const string DamageGhostName = "HP_DamageGhost";
    private static readonly int HealthRectId =
        Shader.PropertyToID("_HealthRect");
    private static readonly int HealthRatioId =
        Shader.PropertyToID("_HealthRatio");
    private static readonly int HitPositionId =
        Shader.PropertyToID("_HitPosition");
    private static readonly int HitStrengthId =
        Shader.PropertyToID("_HitStrength");
    private static readonly int CriticalId =
        Shader.PropertyToID("_Critical");
    private static readonly int GhostModeId =
        Shader.PropertyToID("_GhostMode");

    [Header("Reference")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Material healthBarMaterial;

    [Header("Damage Ghost")]
    [SerializeField] private Color damageGhostColor =
        new Color(1f, 0.16f, 0.015f, 0.95f);
    [SerializeField] private Color criticalGhostColor =
        new Color(1f, 0.72f, 0.06f, 1f);
    [SerializeField] private float ghostHoldDuration = 0.16f;
    [SerializeField] private float ghostCatchUpDuration = 0.28f;

    [Header("Impact")]
    [SerializeField] private float impactDuration = 0.22f;
    [SerializeField] private Vector2 impactScale =
        new Vector2(1.08f, 0.78f);
    [SerializeField] private Vector2 reboundScale =
        new Vector2(0.97f, 1.08f);
    [SerializeField] private Vector2 shakeAmplitude =
        new Vector2(0.12f, 0.045f);

    [Header("Flash")]
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private Color criticalFlashColor =
        new Color(1f, 0.94f, 0.42f, 1f);
    [SerializeField] private float flashHoldDuration = 0.045f;
    [SerializeField] private float flashRecoverDuration = 0.14f;

    [Header("Damage Shards")]
    [SerializeField] private int shardCount = 5;
    [SerializeField] private float shardDuration = 0.22f;

    private readonly List<GameObject> activeShards =
        new List<GameObject>();

    private Image damageGhostImage;
    private RectTransform barRect;
    private Vector2 baseAnchoredPosition;
    private Vector3 baseLocalScale;
    private Color baseFillColor;
    private Coroutine ghostRoutine;
    private Coroutine impactRoutine;
    private Coroutine flashRoutine;
    private Coroutine shaderHitRoutine;
    private Material fillRuntimeMaterial;
    private Material ghostRuntimeMaterial;
    private bool initialized;

    public void Initialize(Image fillImage)
    {
        if (fillImage == null)
        {
            return;
        }

        healthFillImage = fillImage;
        barRect = healthFillImage.rectTransform.parent as RectTransform;
        if (barRect == null)
        {
            return;
        }

        baseAnchoredPosition = barRect.anchoredPosition;
        baseLocalScale = barRect.localScale;
        baseFillColor = healthFillImage.color;
        EnsureDamageGhost();
        EnsureRuntimeMaterials();
        initialized = damageGhostImage != null;
    }

    public void SetValueImmediate(float normalizedHealth)
    {
        if (!EnsureInitialized())
        {
            return;
        }

        StopManagedCoroutine(ref ghostRoutine);
        StopManagedCoroutine(ref impactRoutine);
        StopManagedCoroutine(ref flashRoutine);
        StopManagedCoroutine(ref shaderHitRoutine);
        RestoreBarTransform();
        healthFillImage.color = baseFillColor;

        float value = Mathf.Clamp01(normalizedHealth);
        healthFillImage.fillAmount = value;
        damageGhostImage.fillAmount = value;
        SetShaderState(value, value, 0f, false);
    }

    public void PlayDamage(
        float normalizedHealth,
        bool isCritical,
        float impactStrength = 1f)
    {
        if (!EnsureInitialized())
        {
            return;
        }

        float targetValue = Mathf.Clamp01(normalizedHealth);
        float previousValue = healthFillImage.fillAmount;
        if (targetValue >= previousValue)
        {
            SetValueImmediate(targetValue);
            return;
        }

        float clampedStrength = Mathf.Clamp(impactStrength, 0.35f, 1.75f);
        healthFillImage.fillAmount = targetValue;
        damageGhostImage.fillAmount = Mathf.Max(
            damageGhostImage.fillAmount,
            previousValue);
        damageGhostImage.color = isCritical
            ? criticalGhostColor
            : damageGhostColor;

        StopManagedCoroutine(ref ghostRoutine);
        ghostRoutine = StartCoroutine(AnimateDamageGhost(targetValue));

        StopManagedCoroutine(ref impactRoutine);
        RestoreBarTransform();
        impactRoutine = StartCoroutine(AnimateImpact(clampedStrength));

        StopManagedCoroutine(ref flashRoutine);
        healthFillImage.color = baseFillColor;
        flashRoutine = StartCoroutine(AnimateFlash(isCritical));

        StopManagedCoroutine(ref shaderHitRoutine);
        shaderHitRoutine = StartCoroutine(AnimateShaderHit(
            targetValue,
            isCritical,
            clampedStrength));

        SpawnDamageShards(targetValue, isCritical, clampedStrength);
    }

    private bool EnsureInitialized()
    {
        if (initialized)
        {
            return true;
        }

        Initialize(healthFillImage);
        return initialized;
    }

    private void EnsureDamageGhost()
    {
        Transform existingGhost = barRect.Find(DamageGhostName);
        if (existingGhost != null)
        {
            damageGhostImage = existingGhost.GetComponent<Image>();
        }

        if (damageGhostImage == null)
        {
            GameObject ghostObject = new GameObject(
                DamageGhostName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            ghostObject.layer = healthFillImage.gameObject.layer;

            RectTransform ghostRect =
                ghostObject.GetComponent<RectTransform>();
            RectTransform fillRect = healthFillImage.rectTransform;
            ghostRect.SetParent(fillRect.parent, false);
            CopyRectTransform(fillRect, ghostRect);
            ghostRect.SetSiblingIndex(fillRect.GetSiblingIndex());
            damageGhostImage = ghostObject.GetComponent<Image>();
        }

        CopyImageSettings(healthFillImage, damageGhostImage);
        damageGhostImage.color = damageGhostColor;
        damageGhostImage.fillAmount = healthFillImage.fillAmount;
        damageGhostImage.raycastTarget = false;
    }

    private void EnsureRuntimeMaterials()
    {
        if (healthBarMaterial == null || fillRuntimeMaterial != null)
        {
            return;
        }

        fillRuntimeMaterial = new Material(healthBarMaterial)
        {
            name = $"{healthBarMaterial.name} ({name} Fill)"
        };
        ghostRuntimeMaterial = new Material(healthBarMaterial)
        {
            name = $"{healthBarMaterial.name} ({name} Ghost)"
        };
        fillRuntimeMaterial.SetFloat(GhostModeId, 0f);
        ghostRuntimeMaterial.SetFloat(GhostModeId, 1f);
        healthFillImage.material = fillRuntimeMaterial;
        damageGhostImage.material = ghostRuntimeMaterial;
        SetShaderState(
            healthFillImage.fillAmount,
            healthFillImage.fillAmount,
            0f,
            false);
    }

    private IEnumerator AnimateDamageGhost(float targetValue)
    {
        float elapsed = 0f;
        while (elapsed < ghostHoldDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        float startValue = damageGhostImage.fillAmount;
        elapsed = 0f;
        float duration = Mathf.Max(0.001f, ghostCatchUpDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float easedTime = 1f
                - Mathf.Pow(1f - normalizedTime, 3f);
            damageGhostImage.fillAmount = Mathf.Lerp(
                startValue,
                targetValue,
                easedTime);
            yield return null;
        }

        damageGhostImage.fillAmount = targetValue;
        ghostRoutine = null;
    }

    private IEnumerator AnimateShaderHit(
        float hitPosition,
        bool isCritical,
        float strength)
    {
        float duration = Mathf.Max(
            0.001f,
            impactDuration + flashRecoverDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float hitStrength = (1f - EaseIn(normalizedTime))
                * strength;
            SetShaderState(
                hitPosition,
                hitPosition,
                hitStrength,
                isCritical);
            yield return null;
        }

        SetShaderState(hitPosition, hitPosition, 0f, false);
        shaderHitRoutine = null;
    }

    private IEnumerator AnimateImpact(float strength)
    {
        float duration = Mathf.Max(
            0.001f,
            impactDuration * Mathf.Lerp(0.85f, 1.12f, strength / 1.75f));
        Vector3 compressedScale = Vector3.Scale(
            baseLocalScale,
            new Vector3(
                Mathf.Lerp(1f, impactScale.x, strength),
                Mathf.Lerp(1f, impactScale.y, strength),
                1f));
        Vector3 overshootScale = Vector3.Scale(
            baseLocalScale,
            new Vector3(
                Mathf.Lerp(1f, reboundScale.x, strength),
                Mathf.Lerp(1f, reboundScale.y, strength),
                1f));
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);

            if (normalizedTime < 0.3f)
            {
                barRect.localScale = Vector3.Lerp(
                    baseLocalScale,
                    compressedScale,
                    EaseOut(normalizedTime / 0.3f));
            }
            else if (normalizedTime < 0.58f)
            {
                barRect.localScale = Vector3.Lerp(
                    compressedScale,
                    overshootScale,
                    EaseOut((normalizedTime - 0.3f) / 0.28f));
            }
            else
            {
                barRect.localScale = Vector3.Lerp(
                    overshootScale,
                    baseLocalScale,
                    EaseOut((normalizedTime - 0.58f) / 0.42f));
            }

            Vector2 randomOffset = Vector2.Scale(
                Random.insideUnitCircle,
                shakeAmplitude);
            randomOffset *= strength * (1f - normalizedTime);
            barRect.anchoredPosition =
                baseAnchoredPosition + randomOffset;
            yield return null;
        }

        RestoreBarTransform();
        impactRoutine = null;
    }

    private IEnumerator AnimateFlash(bool isCritical)
    {
        Color flashColor = isCritical
            ? criticalFlashColor
            : hitFlashColor;
        healthFillImage.color = flashColor;

        float elapsed = 0f;
        while (elapsed < flashHoldDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        float duration = Mathf.Max(0.001f, flashRecoverDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            healthFillImage.color = Color.Lerp(
                flashColor,
                baseFillColor,
                EaseOut(normalizedTime));
            yield return null;
        }

        healthFillImage.color = baseFillColor;
        flashRoutine = null;
    }

    private void SpawnDamageShards(
        float targetValue,
        bool isCritical,
        float strength)
    {
        int count = isCritical
            ? Mathf.CeilToInt(shardCount * 1.5f)
            : shardCount;
        Rect barBounds = barRect.rect;
        float boundaryX = healthFillImage.fillOrigin == 1
            ? Mathf.Lerp(barBounds.xMax, barBounds.xMin, targetValue)
            : Mathf.Lerp(barBounds.xMin, barBounds.xMax, targetValue);
        Color shardColor = isCritical
            ? criticalFlashColor
            : damageGhostColor;

        for (int i = 0; i < count; i++)
        {
            GameObject shard = new GameObject(
                "HP_DamageShard",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            shard.layer = healthFillImage.gameObject.layer;

            RectTransform shardRect =
                shard.GetComponent<RectTransform>();
            shardRect.SetParent(barRect, false);
            shardRect.anchorMin = new Vector2(0.5f, 0.5f);
            shardRect.anchorMax = new Vector2(0.5f, 0.5f);
            shardRect.pivot = new Vector2(0.5f, 0.5f);
            float shardSize = barBounds.height
                * Random.Range(0.1f, 0.22f);
            shardRect.sizeDelta = new Vector2(shardSize, shardSize);
            shardRect.anchoredPosition = new Vector2(
                boundaryX + Random.Range(-shardSize, shardSize),
                Random.Range(
                    barBounds.yMin * 0.45f,
                    barBounds.yMax * 0.45f));
            shardRect.localEulerAngles = new Vector3(
                0f,
                0f,
                Random.Range(0f, 180f));

            Image shardImage = shard.GetComponent<Image>();
            shardImage.color = shardColor;
            shardImage.raycastTarget = false;
            activeShards.Add(shard);

            Vector2 velocity = new Vector2(
                Random.Range(-1.2f, 1.8f),
                Random.Range(-0.45f, 1.5f));
            velocity *= barBounds.height * strength;
            StartCoroutine(AnimateShard(
                shard,
                shardRect,
                shardImage,
                velocity,
                barBounds.height));
        }
    }

    private IEnumerator AnimateShard(
        GameObject shard,
        RectTransform shardRect,
        Image shardImage,
        Vector2 velocity,
        float barHeight)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.001f, shardDuration);
        Vector2 startPosition = shardRect.anchoredPosition;
        float rotationSpeed = Random.Range(-520f, 520f);
        float gravity = barHeight * 8f;
        Color startColor = shardImage.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            shardRect.anchoredPosition = startPosition
                + velocity * elapsed
                + Vector2.down * (0.5f * gravity * elapsed * elapsed);
            shardRect.Rotate(0f, 0f, rotationSpeed * Time.unscaledDeltaTime);
            shardRect.localScale = Vector3.one
                * (1f - normalizedTime * 0.55f);

            Color color = startColor;
            color.a *= 1f - EaseIn(normalizedTime);
            shardImage.color = color;
            yield return null;
        }

        activeShards.Remove(shard);
        Destroy(shard);
    }

    private static void CopyRectTransform(
        RectTransform source,
        RectTransform destination)
    {
        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.anchoredPosition = source.anchoredPosition;
        destination.sizeDelta = source.sizeDelta;
        destination.pivot = source.pivot;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static void CopyImageSettings(Image source, Image destination)
    {
        destination.sprite = source.sprite;
        destination.material = source.material;
        destination.type = source.type;
        destination.preserveAspect = source.preserveAspect;
        destination.fillCenter = source.fillCenter;
        destination.fillMethod = source.fillMethod;
        destination.fillOrigin = source.fillOrigin;
        destination.fillClockwise = source.fillClockwise;
        destination.fillAmount = source.fillAmount;
        destination.pixelsPerUnitMultiplier =
            source.pixelsPerUnitMultiplier;
    }

    private void SetShaderState(
        float healthRatio,
        float hitPosition,
        float hitStrength,
        bool isCritical)
    {
        if (fillRuntimeMaterial == null || healthFillImage == null)
        {
            return;
        }

        Rect fillRect = healthFillImage.rectTransform.rect;
        Vector4 rectData = new Vector4(
            fillRect.xMin,
            fillRect.yMin,
            1f / Mathf.Max(fillRect.width, 0.001f),
            1f / Mathf.Max(fillRect.height, 0.001f));
        SetMaterialState(
            fillRuntimeMaterial,
            rectData,
            healthRatio,
            hitPosition,
            hitStrength,
            isCritical);
        SetMaterialState(
            ghostRuntimeMaterial,
            rectData,
            healthRatio,
            hitPosition,
            hitStrength,
            isCritical);
    }

    private static void SetMaterialState(
        Material material,
        Vector4 rectData,
        float healthRatio,
        float hitPosition,
        float hitStrength,
        bool isCritical)
    {
        if (material == null)
        {
            return;
        }

        material.SetVector(HealthRectId, rectData);
        material.SetFloat(HealthRatioId, healthRatio);
        material.SetFloat(HitPositionId, hitPosition);
        material.SetFloat(HitStrengthId, hitStrength);
        material.SetFloat(CriticalId, isCritical ? 1f : 0f);
    }

    private void StopManagedCoroutine(ref Coroutine routine)
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }

    private void RestoreBarTransform()
    {
        if (barRect == null)
        {
            return;
        }

        barRect.anchoredPosition = baseAnchoredPosition;
        barRect.localScale = baseLocalScale;
    }

    private static float EaseOut(float value)
    {
        float clampedValue = Mathf.Clamp01(value);
        return 1f - Mathf.Pow(1f - clampedValue, 3f);
    }

    private static float EaseIn(float value)
    {
        float clampedValue = Mathf.Clamp01(value);
        return clampedValue * clampedValue;
    }

    private void OnDisable()
    {
        StopManagedCoroutine(ref ghostRoutine);
        StopManagedCoroutine(ref impactRoutine);
        StopManagedCoroutine(ref flashRoutine);
        StopManagedCoroutine(ref shaderHitRoutine);
        RestoreBarTransform();

        if (initialized && healthFillImage != null)
        {
            healthFillImage.color = baseFillColor;
            SetShaderState(
                healthFillImage.fillAmount,
                healthFillImage.fillAmount,
                0f,
                false);
        }

        foreach (GameObject shard in activeShards)
        {
            if (shard != null)
            {
                Destroy(shard);
            }
        }

        activeShards.Clear();
    }

    private void OnDestroy()
    {
        if (fillRuntimeMaterial != null)
        {
            Destroy(fillRuntimeMaterial);
        }

        if (ghostRuntimeMaterial != null)
        {
            Destroy(ghostRuntimeMaterial);
        }
    }
}
