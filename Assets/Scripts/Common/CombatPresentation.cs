using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatPresentation : MonoBehaviour
{
    private static readonly int PrimaryColorId =
        Shader.PropertyToID("_PrimaryColor");
    private static readonly int SecondaryColorId =
        Shader.PropertyToID("_SecondaryColor");
    private static readonly int PulseColorId =
        Shader.PropertyToID("_PulseColor");
    private static readonly int ProgressId =
        Shader.PropertyToID("_Progress");
    private static readonly int IntensityId =
        Shader.PropertyToID("_Intensity");
    private static readonly int DirectionId =
        Shader.PropertyToID("_Direction");
    private static readonly int RayCountId =
        Shader.PropertyToID("_RayCount");
    private static readonly int CenterId =
        Shader.PropertyToID("_Center");
    private static readonly int AspectId =
        Shader.PropertyToID("_Aspect");

    [System.Serializable]
    public struct EnemySnapshot
    {
        public Sprite Sprite;
        public Material Material;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public Color Color;
        public int SortingLayerId;
        public int SortingOrder;

        public bool IsValid => Sprite != null;
    }

    [Header("Master")]
    [SerializeField] private bool presentationEnabled = true;
    [Range(0f, 2f)]
    [SerializeField] private float intensity = 1f;

    [Header("Muzzle Flash")]
    [SerializeField] private Material muzzleFlashMaterial;
    [SerializeField] private Material screenPulseMaterial;
    [Min(0.01f)]
    [SerializeField] private float muzzleFlashDuration = 0.11f;
    [Min(0.01f)]
    [SerializeField] private float muzzleFlashSize = 0.52f;
    [Range(2, 12)]
    [SerializeField] private int muzzleRayCount = 7;
    [Range(0f, 0.5f)]
    [SerializeField] private float shotScreenFlashAlpha = 0.075f;
    [Min(0.01f)]
    [SerializeField] private float shotScreenPulseDuration = 0.26f;
    [Range(0f, 4f)]
    [SerializeField] private float shotScreenPulseIntensity = 1.35f;
    [Range(0, 20)]
    [SerializeField] private int muzzleEmberCount = 9;
    [Min(0f)]
    [SerializeField] private float shotHitStopDuration = 0.012f;

    [Header("Hit")]
    [Min(0f)]
    [SerializeField] private float hitStopDuration = 0.035f;
    [Min(0f)]
    [SerializeField] private float criticalHitStopDuration = 0.055f;
    [Min(0f)]
    [SerializeField] private float devastatingHitStopDuration = 0.068f;
    [Min(0.01f)]
    [SerializeField] private float hitFlashDuration = 0.085f;
    [Range(2, 16)]
    [SerializeField] private int hitSparkCount = 6;
    [Range(2, 24)]
    [SerializeField] private int criticalSparkCount = 9;
    [Range(4, 32)]
    [SerializeField] private int devastatingSparkCount = 14;
    [Range(0, 12)]
    [SerializeField] private int impactStreakCount = 4;
    [Range(0f, 0.5f)]
    [SerializeField] private float devastatingScreenFlashAlpha = 0.1f;

    [Header("Defeat")]
    [Min(0f)]
    [SerializeField] private float defeatHitStopDuration = 0.075f;
    [Min(0.05f)]
    [SerializeField] private float defeatAfterimageDuration = 0.32f;
    [Min(0f)]
    [SerializeField] private float defeatKnockbackDistance = 0.42f;
    [Min(0f)]
    [SerializeField] private float defeatLiftHeight = 0.22f;
    [Range(4, 24)]
    [SerializeField] private int defeatSparkCount = 12;
    [Range(0f, 0.75f)]
    [SerializeField] private float defeatScreenFlashAlpha = 0.2f;
    [SerializeField] private Color defeatDustColor =
        new Color(0.72f, 0.35f, 0.12f, 1f);

    private readonly List<GameObject> spawnedEffects = new List<GameObject>();
    private Sprite whiteSprite;
    private Canvas flashCanvas;
    private Image flashImage;
    private Image pulseImage;
    private Material runtimeMuzzleMaterial;
    private Material runtimePulseMaterial;
    private Coroutine flashCoroutine;
    private Coroutine pulseCoroutine;
    private Coroutine hitStopCoroutine;
    private float hitStopRemaining;
    private float timeScaleBeforeHitStop = 1f;

    private float ScaledIntensity => Mathf.Max(0f, intensity);

    private void Awake()
    {
        EnsureRuntimeResources();
    }

    private void OnDisable()
    {
        RestoreTimeScale();
        ClearSpawnedEffects();

        if (flashImage != null)
        {
            flashImage.color = Color.clear;
        }

        if (pulseImage != null)
        {
            pulseImage.enabled = false;
        }
    }

    private void OnDestroy()
    {
        RestoreTimeScale();

        if (whiteSprite != null)
        {
            Texture2D texture = whiteSprite.texture;
            Destroy(whiteSprite);

            if (texture != null)
            {
                Destroy(texture);
            }
        }

        if (runtimePulseMaterial != null)
        {
            Destroy(runtimePulseMaterial);
        }

        if (runtimeMuzzleMaterial != null)
        {
            Destroy(runtimeMuzzleMaterial);
        }
    }

    public EnemySnapshot CaptureEnemy(EnemyController enemy)
    {
        if (enemy == null)
        {
            return default;
        }

        SpriteRenderer renderer = enemy.GetComponentInChildren<SpriteRenderer>();

        if (renderer == null || renderer.sprite == null)
        {
            return default;
        }

        return new EnemySnapshot
        {
            Sprite = renderer.sprite,
            Material = renderer.sharedMaterial,
            Position = renderer.transform.position,
            Rotation = renderer.transform.rotation,
            Scale = renderer.transform.lossyScale,
            Color = renderer.color,
            SortingLayerId = renderer.sortingLayerID,
            SortingOrder = renderer.sortingOrder
        };
    }

    public void PlayReload(
        BulletInstance bullet,
        PlayerCylinderUI cylinderUi)
    {
        if (!presentationEnabled)
        {
            return;
        }

        Color accent = GetAccentColor(bullet);
        cylinderUi?.PlayReloadPresentation(accent, ScaledIntensity);
    }

    public void PlayShot(
        Transform firePoint,
        BulletInstance bullet,
        bool isCritical,
        int horizontalDirection)
    {
        if (!presentationEnabled || firePoint == null)
        {
            return;
        }

        EnsureRuntimeResources();
        Color accent = GetAccentColor(bullet);
        float criticalScale = isCritical ? 1.3f : 1f;
        int direction = horizontalDirection == 0 ? 1 : horizontalDirection;
        SpawnMuzzleFlash(
            firePoint.position,
            accent,
            muzzleFlashSize * ScaledIntensity * criticalScale,
            direction,
            isCritical);
        SpawnMuzzleEmbers(
            firePoint.position,
            direction,
            accent,
            Mathf.RoundToInt(muzzleEmberCount * criticalScale));
        PlayScreenPulse(
            firePoint.position,
            accent,
            shotScreenPulseIntensity * ScaledIntensity * criticalScale,
            shotScreenPulseDuration);
        PlayScreenFlash(
            Color.Lerp(Color.white, accent, 0.35f),
            shotScreenFlashAlpha * ScaledIntensity * criticalScale,
            muzzleFlashDuration);
        PlayHitStop(shotHitStopDuration * criticalScale);
    }

    public void PlayImpact(
        EnemySnapshot snapshot,
        int horizontalDirection,
        BulletInstance bullet,
        CombatImpactTier impactTier,
        float feedbackMultiplier = 1f)
    {
        if (!presentationEnabled || !snapshot.IsValid)
        {
            return;
        }

        EnsureRuntimeResources();
        Color accent = GetAccentColor(bullet);
        float impactMultiplier = impactTier == CombatImpactTier.Defeat
            ? Mathf.Max(0f, feedbackMultiplier)
            : 1f;
        SpawnHitFlash(snapshot, accent, impactTier, impactMultiplier);
        int sparkCount = impactTier switch
        {
            CombatImpactTier.Defeat => defeatSparkCount,
            CombatImpactTier.Devastating => devastatingSparkCount,
            CombatImpactTier.Critical => criticalSparkCount,
            _ => hitSparkCount
        };
        SpawnImpactSparks(
            snapshot.Position,
            horizontalDirection,
            accent,
            sparkCount,
            snapshot.SortingLayerId,
            snapshot.SortingOrder + 2,
            impactTier,
            impactMultiplier);

        if (impactTier >= CombatImpactTier.Critical)
        {
            int streakCount = impactTier switch
            {
                CombatImpactTier.Defeat => impactStreakCount + 3,
                CombatImpactTier.Devastating => impactStreakCount + 1,
                _ => impactStreakCount
            };
            SpawnDirectionalStreaks(
                snapshot.Position,
                horizontalDirection,
                accent,
                streakCount,
                snapshot.SortingLayerId,
                snapshot.SortingOrder + 3,
                impactTier,
                impactMultiplier);
        }

        if (impactTier == CombatImpactTier.Defeat)
        {
            SpawnDefeatAfterimage(
                snapshot,
                horizontalDirection,
                accent,
                impactMultiplier);
            PlayHitStop(defeatHitStopDuration * impactMultiplier);
            PlayScreenFlash(
                Color.Lerp(Color.white, accent, 0.5f),
                defeatScreenFlashAlpha * ScaledIntensity * impactMultiplier,
                defeatAfterimageDuration * 0.55f
                    * Mathf.Sqrt(Mathf.Max(1f, impactMultiplier)));
        }
        else if (impactTier == CombatImpactTier.Devastating)
        {
            PlayHitStop(devastatingHitStopDuration);
            PlayScreenFlash(
                Color.Lerp(Color.white, accent, 0.38f),
                devastatingScreenFlashAlpha * ScaledIntensity,
                hitFlashDuration * 1.8f);
        }
        else
        {
            PlayHitStop(impactTier == CombatImpactTier.Critical
                ? criticalHitStopDuration
                : hitStopDuration);
        }
    }

    private void EnsureRuntimeResources()
    {
        if (whiteSprite == null)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Combat Presentation White Pixel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            whiteSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            whiteSprite.name = "Combat Presentation White Sprite";
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        if (flashCanvas != null && flashImage != null)
        {
            EnsurePulseImage();
            return;
        }

        GameObject canvasObject = new GameObject(
            "Combat Presentation Screen Flash",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        flashCanvas = canvasObject.GetComponent<Canvas>();
        flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        flashCanvas.sortingOrder = short.MaxValue;

        GameObject imageObject = new GameObject(
            "Flash",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        flashImage = imageObject.GetComponent<Image>();
        flashImage.raycastTarget = false;
        flashImage.color = Color.clear;
        EnsurePulseImage();
    }

    private void EnsurePulseImage()
    {
        if (pulseImage != null)
        {
            return;
        }

        Material sourceMaterial = screenPulseMaterial;
        bool sourceWasCreated = false;

        if (sourceMaterial == null)
        {
            Shader pulseShader = Shader.Find("Loaded/Combat Screen Pulse");

            if (pulseShader != null)
            {
                sourceMaterial = new Material(pulseShader)
                {
                    name = "Combat Screen Pulse (Runtime Source)"
                };
                sourceWasCreated = true;
            }
        }

        if (sourceMaterial == null || flashCanvas == null)
        {
            return;
        }

        runtimePulseMaterial = sourceWasCreated
            ? sourceMaterial
            : new Material(sourceMaterial);
        runtimePulseMaterial.name = "Combat Screen Pulse (Runtime)";
        GameObject pulseObject = new GameObject(
            "Shot Pulse",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        pulseObject.transform.SetParent(flashCanvas.transform, false);
        RectTransform rectTransform = pulseObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        pulseImage = pulseObject.GetComponent<Image>();
        pulseImage.raycastTarget = false;
        pulseImage.color = Color.white;
        pulseImage.material = runtimePulseMaterial;
        pulseImage.enabled = false;
    }

    private void SpawnMuzzleFlash(
        Vector3 position,
        Color accent,
        float size,
        int horizontalDirection,
        bool isCritical)
    {
        Material material = ResolveMuzzleMaterial();

        if (material == null)
        {
            SpawnFallbackMuzzleFlash(position, accent, size);
            return;
        }

        GameObject root = CreateEffectRoot("Shader Muzzle Flash", position);
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        int direction = horizontalDirection == 0 ? 1 : horizontalDirection;

        GameObject mainFlash = CreateSpriteObject(
            "Main Flame",
            root.transform,
            Color.white,
            222);
        mainFlash.transform.localScale = new Vector3(
            size * 2.65f,
            size * 2.15f,
            1f);
        SpriteRenderer mainRenderer = mainFlash.GetComponent<SpriteRenderer>();
        ConfigureMuzzleRenderer(
            mainRenderer,
            material,
            accent,
            direction,
            isCritical ? 3.2f : 2.55f,
            muzzleRayCount);
        renderers.Add(mainRenderer);

        GameObject echoFlash = CreateSpriteObject(
            "Rotated Echo",
            root.transform,
            Color.white,
            221);
        echoFlash.transform.localRotation = Quaternion.Euler(
            0f,
            0f,
            direction * 11f);
        echoFlash.transform.localScale = new Vector3(
            size * 2.15f,
            size * 1.72f,
            1f);
        SpriteRenderer echoRenderer = echoFlash.GetComponent<SpriteRenderer>();
        ConfigureMuzzleRenderer(
            echoRenderer,
            material,
            Color.Lerp(accent, Color.white, 0.22f),
            direction,
            isCritical ? 2.15f : 1.55f,
            muzzleRayCount + 2);
        renderers.Add(echoRenderer);

        StartCoroutine(AnimateShaderMuzzle(
            root,
            renderers,
            muzzleFlashDuration,
            direction));
    }

    private void SpawnFallbackMuzzleFlash(
        Vector3 position,
        Color accent,
        float size)
    {
        GameObject root = CreateEffectRoot("Muzzle Flash", position);
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        int rayCount = Mathf.Max(2, muzzleRayCount);

        for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
        {
            float angle = 360f * rayIndex / rayCount
                + Random.Range(-8f, 8f);
            GameObject ray = CreateSpriteObject(
                "Ray",
                root.transform,
                Color.Lerp(Color.white, accent, 0.4f),
                220);
            ray.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            ray.transform.localPosition = Quaternion.Euler(0f, 0f, angle)
                * Vector3.right * size * 0.22f;
            ray.transform.localScale = new Vector3(
                size * Random.Range(0.65f, 1.1f),
                size * Random.Range(0.06f, 0.12f),
                1f);
            renderers.Add(ray.GetComponent<SpriteRenderer>());
        }

        GameObject core = CreateSpriteObject(
            "Core",
            root.transform,
            Color.white,
            221);
        core.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        core.transform.localScale = Vector3.one * size * 0.38f;
        renderers.Add(core.GetComponent<SpriteRenderer>());
        StartCoroutine(AnimateFlashRoot(
            root,
            renderers,
            muzzleFlashDuration));
    }

    private Material ResolveMuzzleMaterial()
    {
        if (muzzleFlashMaterial != null)
        {
            return muzzleFlashMaterial;
        }

        if (runtimeMuzzleMaterial != null)
        {
            return runtimeMuzzleMaterial;
        }

        Shader shader = Shader.Find("Loaded/Combat Muzzle Flash");

        if (shader == null)
        {
            return null;
        }

        runtimeMuzzleMaterial = new Material(shader)
        {
            name = "Combat Muzzle Flash (Runtime)"
        };
        return runtimeMuzzleMaterial;
    }

    private static void ConfigureMuzzleRenderer(
        SpriteRenderer renderer,
        Material material,
        Color accent,
        int horizontalDirection,
        float effectIntensity,
        int rayCount)
    {
        renderer.sharedMaterial = material;
        Color hotColor = Color.Lerp(
            accent,
            new Color(1f, 0.82f, 0.28f, 1f),
            0.62f);
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetColor(PrimaryColorId, accent);
        propertyBlock.SetColor(SecondaryColorId, hotColor);
        propertyBlock.SetFloat(ProgressId, 0f);
        propertyBlock.SetFloat(IntensityId, effectIntensity);
        propertyBlock.SetFloat(
            DirectionId,
            horizontalDirection == 0 ? 1f : horizontalDirection);
        propertyBlock.SetFloat(RayCountId, Mathf.Max(3, rayCount));
        renderer.SetPropertyBlock(propertyBlock);
    }

    private void SpawnMuzzleEmbers(
        Vector3 position,
        int horizontalDirection,
        Color accent,
        int count)
    {
        int direction = horizontalDirection == 0 ? 1 : horizontalDirection;
        int scaledCount = Mathf.Max(
            0,
            Mathf.RoundToInt(count * ScaledIntensity));

        for (int emberIndex = 0; emberIndex < scaledCount; emberIndex++)
        {
            bool isSmoke = emberIndex % 4 == 0;
            Color color = isSmoke
                ? Color.Lerp(defeatDustColor, Color.gray, 0.35f)
                : Color.Lerp(
                    accent,
                    new Color(1f, 0.86f, 0.32f, 1f),
                    Random.Range(0.25f, 0.7f));
            GameObject ember = CreateSpriteObject(
                isSmoke ? "Muzzle Smoke" : "Muzzle Ember",
                null,
                color,
                isSmoke ? 218 : 224);
            SpriteRenderer renderer = ember.GetComponent<SpriteRenderer>();
            ember.transform.position = position
                + new Vector3(
                    direction * Random.Range(0.01f, 0.11f),
                    Random.Range(-0.04f, 0.04f),
                    0f);
            Vector2 velocity = new Vector2(
                direction * Random.Range(1.2f, isSmoke ? 2.1f : 4.1f),
                Random.Range(-0.8f, 1.25f));
            float baseSize = isSmoke
                ? Random.Range(0.06f, 0.14f)
                : Random.Range(0.025f, 0.075f);
            ember.transform.localScale = new Vector3(
                baseSize * (isSmoke ? 1.4f : 2.4f),
                baseSize,
                1f);
            ember.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            StartCoroutine(AnimateSpark(
                ember,
                renderer,
                velocity,
                isSmoke
                    ? Random.Range(0.25f, 0.42f)
                    : Random.Range(0.14f, 0.28f)));
        }
    }

    private void SpawnHitFlash(
        EnemySnapshot snapshot,
        Color accent,
        CombatImpactTier impactTier,
        float feedbackMultiplier)
    {
        float tierStrength = (float)impactTier / (float)CombatImpactTier.Defeat;
        GameObject flash = CreateSnapshotObject(
            $"{impactTier} Hit Flash",
            snapshot,
            snapshot.SortingOrder + 1);
        SpriteRenderer renderer = flash.GetComponent<SpriteRenderer>();
        renderer.color = Color.Lerp(
            Color.white,
            accent,
            Mathf.Lerp(0.12f, 0.38f, tierStrength));
        float basePeakScale = Mathf.Lerp(1.08f, 1.32f, tierStrength);
        float peakScale = 1f
            + (basePeakScale - 1f) * Mathf.Max(0f, feedbackMultiplier);
        StartCoroutine(AnimateSnapshotFlash(
            flash,
            renderer,
            peakScale,
            hitFlashDuration * Mathf.Lerp(1f, 1.65f, tierStrength),
            tierStrength));
    }

    private void SpawnDefeatAfterimage(
        EnemySnapshot snapshot,
        int horizontalDirection,
        Color accent,
        float feedbackMultiplier)
    {
        int echoCount = Mathf.Max(
            3,
            Mathf.RoundToInt(3f * Mathf.Max(1f, feedbackMultiplier)));

        for (int echoIndex = 0; echoIndex < echoCount; echoIndex++)
        {
            GameObject afterimage = CreateSnapshotObject(
                $"Defeat Afterimage {echoIndex + 1}",
                snapshot,
                snapshot.SortingOrder - echoIndex);
            SpriteRenderer renderer = afterimage.GetComponent<SpriteRenderer>();
            renderer.color = Color.Lerp(
                snapshot.Color,
                Color.Lerp(defeatDustColor, accent, 0.35f),
                0.38f + echoIndex * 0.13f);
            StartCoroutine(AnimateDefeatAfterimage(
                afterimage,
                renderer,
                horizontalDirection,
                echoIndex * 0.035f,
                1f - echoIndex * 0.16f,
                feedbackMultiplier));
        }
    }

    private void SpawnImpactSparks(
        Vector3 position,
        int horizontalDirection,
        Color accent,
        int count,
        int sortingLayerId,
        int sortingOrder,
        CombatImpactTier impactTier,
        float feedbackMultiplier = 1f)
    {
        bool isDefeated = impactTier == CombatImpactTier.Defeat;
        float effectMultiplier = isDefeated
            ? Mathf.Max(0f, feedbackMultiplier)
            : 1f;
        int direction = horizontalDirection == 0 ? 1 : horizontalDirection;
        int scaledCount = Mathf.Max(1, Mathf.RoundToInt(
            count * ScaledIntensity
            * CombatAccessibilitySettings.ParticleDensityMultiplier
            * effectMultiplier));

        for (int sparkIndex = 0; sparkIndex < scaledCount; sparkIndex++)
        {
            Color color = sparkIndex % 3 == 0
                ? defeatDustColor
                : Color.Lerp(accent, Color.white, Random.Range(0.05f, 0.45f));
            GameObject spark = CreateSpriteObject(
                isDefeated ? "Defeat Debris" : "Hit Spark",
                null,
                color,
                sortingOrder);
            SpriteRenderer renderer = spark.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = sortingLayerId;
            spark.transform.position = position
                + (Vector3)Random.insideUnitCircle * 0.05f;

            float forwardSpeed = Random.Range(0.9f, isDefeated ? 2.8f : 1.9f);
            Vector2 velocity = new Vector2(
                direction * forwardSpeed,
                Random.Range(-0.7f, isDefeated ? 1.8f : 1.1f));

            if (sparkIndex % 4 == 0)
            {
                velocity.x *= -0.35f;
            }

            velocity *= effectMultiplier;

            spark.transform.localScale = new Vector3(
                Random.Range(0.045f, 0.13f),
                Random.Range(0.018f, 0.045f),
                1f) * Mathf.Lerp(0.75f, 1.25f, ScaledIntensity * 0.5f)
                * effectMultiplier;
            spark.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            StartCoroutine(AnimateSpark(
                spark,
                renderer,
                velocity,
                isDefeated ? Random.Range(0.24f, 0.42f) : Random.Range(0.12f, 0.24f)));
        }
    }

    private void SpawnDirectionalStreaks(
        Vector3 position,
        int horizontalDirection,
        Color accent,
        int count,
        int sortingLayerId,
        int sortingOrder,
        CombatImpactTier impactTier,
        float feedbackMultiplier = 1f)
    {
        int direction = horizontalDirection == 0 ? 1 : horizontalDirection;
        float tierScale = 1f + (int)impactTier * 0.22f;
        float effectMultiplier = impactTier == CombatImpactTier.Defeat
            ? Mathf.Max(0f, feedbackMultiplier)
            : 1f;
        int scaledCount = Mathf.Max(1, Mathf.RoundToInt(
            count * CombatAccessibilitySettings.ParticleDensityMultiplier
            * effectMultiplier));

        for (int streakIndex = 0; streakIndex < scaledCount; streakIndex++)
        {
            GameObject streak = CreateSpriteObject(
                $"{impactTier} Directional Streak",
                null,
                Color.Lerp(accent, Color.white, 0.55f),
                sortingOrder);
            SpriteRenderer renderer = streak.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = sortingLayerId;
            streak.transform.position = position + new Vector3(
                -direction * Random.Range(0.02f, 0.16f),
                Random.Range(-0.16f, 0.16f),
                0f);
            streak.transform.localScale = new Vector3(
                Random.Range(0.28f, 0.62f) * tierScale * effectMultiplier,
                Random.Range(0.012f, 0.03f) * tierScale * effectMultiplier,
                1f);
            streak.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Random.Range(-9f, 9f));
            Vector2 velocity = new Vector2(
                direction * Random.Range(1.6f, 3.4f) * tierScale,
                Random.Range(-0.3f, 0.3f)) * effectMultiplier;
            StartCoroutine(AnimateSpark(
                streak,
                renderer,
                velocity,
                Random.Range(0.09f, 0.17f) * tierScale));
        }
    }

    private IEnumerator AnimateFlashRoot(
        GameObject root,
        List<SpriteRenderer> renderers,
        float duration)
    {
        float elapsed = 0f;
        Vector3 startScale = root.transform.localScale * 0.25f;
        Vector3 peakScale = root.transform.localScale;

        while (elapsed < duration && root != null)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float scaleProgress = progress < 0.28f
                ? Mathf.SmoothStep(0f, 1f, progress / 0.28f)
                : Mathf.Lerp(1f, 0.72f, (progress - 0.28f) / 0.72f);
            root.transform.localScale = Vector3.LerpUnclamped(
                startScale,
                peakScale,
                scaleProgress);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Color color = renderer.color;
                color.a = 1f - Mathf.SmoothStep(0f, 1f, progress);
                renderer.color = color;
            }
        }

        DestroyEffect(root);
    }

    private IEnumerator AnimateShaderMuzzle(
        GameObject root,
        List<SpriteRenderer> renderers,
        float duration,
        int horizontalDirection)
    {
        float elapsed = 0f;
        Vector3 startPosition = root.transform.position;
        Vector3 baseScale = root.transform.localScale;
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        while (elapsed < duration && root != null)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float attack = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(progress / 0.18f));
            float decay = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01((progress - 0.18f) / 0.82f));
            float scale = Mathf.Lerp(0.34f, 1.12f, attack)
                * Mathf.Lerp(0.78f, 1f, decay);
            root.transform.localScale = baseScale * scale;
            root.transform.position = startPosition
                + Vector3.right
                * horizontalDirection
                * 0.065f
                * Mathf.SmoothStep(0f, 1f, progress);
            root.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                horizontalDirection * progress * 3.5f);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(ProgressId, progress);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        DestroyEffect(root);
    }

    private IEnumerator AnimateSnapshotFlash(
        GameObject flash,
        SpriteRenderer renderer,
        float peakScale,
        float duration,
        float tierStrength)
    {
        Vector3 baseScale = flash.transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration && flash != null)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            float stretch = Mathf.Lerp(1f, peakScale, pulse);
            float squash = Mathf.Lerp(1f, 2f - peakScale, pulse);
            flash.transform.localScale = new Vector3(
                baseScale.x * stretch,
                baseScale.y * squash,
                baseScale.z);
            Color color = renderer.color;
            color.a = (1f - Mathf.SmoothStep(0f, 1f, progress))
                * Mathf.Lerp(0.72f, 1f, tierStrength)
                * CombatAccessibilitySettings.FlashMultiplier;
            renderer.color = color;
        }

        DestroyEffect(flash);
    }

    private IEnumerator AnimateDefeatAfterimage(
        GameObject afterimage,
        SpriteRenderer renderer,
        int horizontalDirection,
        float delay,
        float distanceScale,
        float feedbackMultiplier)
    {
        Vector3 startPosition = afterimage.transform.position;
        Vector3 startScale = afterimage.transform.localScale;
        Quaternion startRotation = afterimage.transform.rotation;
        float elapsed = 0f;
        int direction = horizontalDirection == 0 ? 1 : horizontalDirection;

        while (delay > 0f && afterimage != null)
        {
            yield return null;
            delay -= Time.unscaledDeltaTime;
        }

        while (elapsed < defeatAfterimageDuration && afterimage != null)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / defeatAfterimageDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            Vector3 position = startPosition;
            position.x += direction * defeatKnockbackDistance
                * ScaledIntensity * distanceScale * feedbackMultiplier * eased;
            position.y += Mathf.Sin(progress * Mathf.PI)
                * defeatLiftHeight * ScaledIntensity * feedbackMultiplier;
            afterimage.transform.position = position;
            afterimage.transform.rotation = startRotation
                * Quaternion.Euler(0f, 0f, -direction * 14f * eased);
            afterimage.transform.localScale = new Vector3(
                startScale.x * Mathf.Lerp(
                    1f,
                    1f + 0.12f * feedbackMultiplier,
                    eased),
                startScale.y * Mathf.Lerp(
                    1f,
                    1f - 0.32f * feedbackMultiplier,
                    eased),
                startScale.z);
            Color color = renderer.color;
            color.a = 1f - Mathf.SmoothStep(0.18f, 1f, progress);
            renderer.color = color;
        }

        DestroyEffect(afterimage);
    }

    private IEnumerator AnimateSpark(
        GameObject spark,
        SpriteRenderer renderer,
        Vector2 velocity,
        float duration)
    {
        float elapsed = 0f;
        Vector3 initialScale = spark.transform.localScale;

        while (elapsed < duration && spark != null)
        {
            yield return null;
            float deltaTime = Time.unscaledDeltaTime;
            elapsed += deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            spark.transform.position += (Vector3)(velocity * deltaTime);
            velocity.y -= 4.5f * deltaTime;
            spark.transform.localScale = Vector3.Lerp(
                initialScale,
                initialScale * 0.2f,
                progress);
            Color color = renderer.color;
            color.a = 1f - Mathf.SmoothStep(0.35f, 1f, progress);
            renderer.color = color;
        }

        DestroyEffect(spark);
    }

    private void PlayScreenPulse(
        Vector3 worldPosition,
        Color color,
        float effectIntensity,
        float duration)
    {
        effectIntensity *= CombatAccessibilitySettings.FlashMultiplier;

        if (effectIntensity <= 0f || duration <= 0f)
        {
            return;
        }

        EnsureRuntimeResources();

        if (pulseImage == null || runtimePulseMaterial == null)
        {
            PlayScreenFlash(
                color,
                Mathf.Clamp01(effectIntensity * 0.07f),
                duration * 0.45f);
            return;
        }

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
        }

        Camera worldCamera = Camera.main;
        Vector2 center = new Vector2(0.5f, 0.5f);

        if (worldCamera != null)
        {
            Vector3 viewportPosition =
                worldCamera.WorldToViewportPoint(worldPosition);

            if (viewportPosition.z >= 0f)
            {
                center = new Vector2(
                    viewportPosition.x,
                    viewportPosition.y);
            }
        }

        color.a = 1f;
        runtimePulseMaterial.SetColor(PulseColorId, color);
        runtimePulseMaterial.SetFloat(IntensityId, effectIntensity);
        runtimePulseMaterial.SetVector(
            CenterId,
            new Vector4(center.x, center.y, 0f, 0f));
        runtimePulseMaterial.SetFloat(
            AspectId,
            Screen.height <= 0
                ? 1f
                : (float)Screen.width / Screen.height);
        runtimePulseMaterial.SetFloat(ProgressId, 0f);
        pulseImage.enabled = true;
        pulseCoroutine = StartCoroutine(FadeScreenPulse(duration));
    }

    private IEnumerator FadeScreenPulse(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration
               && pulseImage != null
               && runtimePulseMaterial != null)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            runtimePulseMaterial.SetFloat(ProgressId, progress);
        }

        if (pulseImage != null)
        {
            pulseImage.enabled = false;
        }

        pulseCoroutine = null;
    }

    private void PlayScreenFlash(Color color, float alpha, float duration)
    {
        alpha *= CombatAccessibilitySettings.FlashMultiplier;

        if (alpha <= 0f || duration <= 0f)
        {
            return;
        }

        EnsureRuntimeResources();

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        Color currentColor = flashImage != null
            ? flashImage.color
            : Color.clear;
        color.a = Mathf.Clamp01(alpha);
        flashCoroutine = StartCoroutine(FadeScreenFlash(
            currentColor,
            color,
            duration));
    }

    private IEnumerator FadeScreenFlash(
        Color currentColor,
        Color peakColor,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration && flashImage != null)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            const float attackPortion = 0.12f;
            Color color;

            if (progress < attackPortion)
            {
                float attack = Mathf.SmoothStep(
                    0f,
                    1f,
                    progress / attackPortion);
                color = Color.Lerp(currentColor, peakColor, attack);
            }
            else
            {
                float release = Mathf.InverseLerp(
                    attackPortion,
                    1f,
                    progress);
                color = peakColor;
                color.a *= 1f - Mathf.SmoothStep(0f, 1f, release);
            }

            flashImage.color = color;
        }

        if (flashImage != null)
        {
            flashImage.color = Color.clear;
        }

        flashCoroutine = null;
    }

    private void PlayHitStop(float duration)
    {
        duration *= ScaledIntensity
            * CombatAccessibilitySettings.TimeEffectMultiplier;

        if (duration <= 0f)
        {
            return;
        }

        hitStopRemaining = duration;

        if (hitStopCoroutine == null)
        {
            hitStopCoroutine = StartCoroutine(HitStopRoutine());
        }
    }

    private IEnumerator HitStopRoutine()
    {
        timeScaleBeforeHitStop = Time.timeScale;

        if (timeScaleBeforeHitStop <= 0f)
        {
            hitStopCoroutine = null;
            hitStopRemaining = 0f;
            yield break;
        }

        Time.timeScale = 0f;

        while (hitStopRemaining > 0f)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                if (Time.timeScale <= 0f && timeScaleBeforeHitStop > 0f)
                {
                    Time.timeScale = timeScaleBeforeHitStop;
                }

                hitStopRemaining = 0f;
                hitStopCoroutine = null;
                yield break;
            }

            hitStopRemaining -= Time.unscaledDeltaTime;
        }

        if (Time.timeScale <= 0f && timeScaleBeforeHitStop > 0f)
        {
            Time.timeScale = timeScaleBeforeHitStop;
        }

        hitStopRemaining = 0f;
        hitStopCoroutine = null;
    }

    private void RestoreTimeScale()
    {
        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
            hitStopCoroutine = null;
        }

        if (Time.timeScale <= 0f && timeScaleBeforeHitStop > 0f)
        {
            Time.timeScale = timeScaleBeforeHitStop;
        }

        hitStopRemaining = 0f;
    }

    public void CancelHitStopForPause()
    {
        RestoreTimeScale();
    }

    private GameObject CreateEffectRoot(string effectName, Vector3 position)
    {
        GameObject root = new GameObject(effectName);
        root.transform.position = position;
        spawnedEffects.Add(root);
        return root;
    }

    private GameObject CreateSpriteObject(
        string objectName,
        Transform parent,
        Color color,
        int sortingOrder)
    {
        GameObject spriteObject = new GameObject(
            objectName,
            typeof(SpriteRenderer));
        spriteObject.transform.SetParent(parent, false);
        SpriteRenderer renderer = spriteObject.GetComponent<SpriteRenderer>();
        renderer.sprite = whiteSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        if (parent == null)
        {
            spawnedEffects.Add(spriteObject);
        }

        return spriteObject;
    }

    private GameObject CreateSnapshotObject(
        string objectName,
        EnemySnapshot snapshot,
        int sortingOrder)
    {
        GameObject snapshotObject = new GameObject(
            objectName,
            typeof(SpriteRenderer));
        snapshotObject.transform.SetPositionAndRotation(
            snapshot.Position,
            snapshot.Rotation);
        snapshotObject.transform.localScale = snapshot.Scale;
        SpriteRenderer renderer = snapshotObject.GetComponent<SpriteRenderer>();
        renderer.sprite = snapshot.Sprite;
        renderer.sharedMaterial = snapshot.Material;
        renderer.color = snapshot.Color;
        renderer.sortingLayerID = snapshot.SortingLayerId;
        renderer.sortingOrder = sortingOrder;
        spawnedEffects.Add(snapshotObject);
        return snapshotObject;
    }

    private void DestroyEffect(GameObject effect)
    {
        if (effect == null)
        {
            return;
        }

        spawnedEffects.Remove(effect);
        Destroy(effect);
    }

    private void ClearSpawnedEffects()
    {
        foreach (GameObject effect in spawnedEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }

        spawnedEffects.Clear();
    }

    private static Color GetAccentColor(BulletInstance bullet)
    {
        Color accent = bullet == null ? Color.white : bullet.PrimaryLineColor;
        accent.a = 1f;
        return accent;
    }
}
