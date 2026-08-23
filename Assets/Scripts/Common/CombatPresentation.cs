using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class CombatPresentation : MonoBehaviour
{
    private static readonly int PrimaryColorId =
        Shader.PropertyToID("_PrimaryColor");
    private static readonly int SecondaryColorId =
        Shader.PropertyToID("_SecondaryColor");
    private static readonly int ProgressId =
        Shader.PropertyToID("_Progress");
    private static readonly int IntensityId =
        Shader.PropertyToID("_Intensity");
    private static readonly int DirectionId =
        Shader.PropertyToID("_Direction");
    private static readonly int RayCountId =
        Shader.PropertyToID("_RayCount");
    private static readonly int EffectModeId =
        Shader.PropertyToID("_EffectMode");

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
        public bool Captured;

        public bool IsValid => Captured || Sprite != null;
        public bool HasSprite => Sprite != null;
    }

    internal readonly struct ImpactSignature
    {
        public ImpactSignature(
            bool usesSnapAccent,
            bool usesPrecisionLock,
            bool usesCompressionBurst,
            bool usesDefeatSilhouette,
            bool usesFinalExecutionSeal)
        {
            UsesSnapAccent = usesSnapAccent;
            UsesPrecisionLock = usesPrecisionLock;
            UsesCompressionBurst = usesCompressionBurst;
            UsesDefeatSilhouette = usesDefeatSilhouette;
            UsesFinalExecutionSeal = usesFinalExecutionSeal;
        }

        public bool UsesSnapAccent { get; }
        public bool UsesPrecisionLock { get; }
        public bool UsesCompressionBurst { get; }
        public bool UsesDefeatSilhouette { get; }
        public bool UsesFinalExecutionSeal { get; }
    }

    [Header("Master")]
    [SerializeField] private bool presentationEnabled = true;
    [Range(0f, 2f)]
    [SerializeField] private float intensity = 1f;

    [Header("Muzzle Flash")]
    [SerializeField] private Material muzzleFlashMaterial;
    [Min(0.01f)]
    [SerializeField] private float muzzleFlashDuration = 0.11f;
    [Min(0.01f)]
    [SerializeField] private float muzzleFlashSize = 0.52f;
    [Range(2, 12)]
    [SerializeField] private int muzzleRayCount = 7;
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
    [Range(2, 16)]
    [SerializeField] private int hitSparkCount = 6;
    [Range(2, 24)]
    [SerializeField] private int criticalSparkCount = 9;
    [Range(4, 32)]
    [SerializeField] private int devastatingSparkCount = 14;
    [Range(0, 12)]
    [SerializeField] private int impactStreakCount = 4;

    [Header("Impact Signatures")]
    [Min(0.05f)]
    [SerializeField] private float normalSnapDuration = 0.09f;
    [Min(0.05f)]
    [SerializeField] private float criticalPrecisionDuration = 0.14f;
    [Min(0.05f)]
    [SerializeField] private float devastatingCompressionDuration = 0.18f;
    [Min(0f)]
    [SerializeField] private float devastatingSecondaryWaveDelay = 0.045f;
    [Min(0.05f)]
    [SerializeField] private float devastatingSecondaryWaveDuration = 0.2f;

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
    [Min(0f)]
    [SerializeField] private float defeatSilhouetteHoldDuration = 0.055f;
    [Min(0.05f)]
    [SerializeField] private float defeatSilhouetteDuration = 0.26f;
    [Min(1f)]
    [SerializeField] private float finalDefeatDurationMultiplier = 1.45f;
    [SerializeField] private Color defeatDustColor =
        new Color(0.72f, 0.35f, 0.12f, 1f);

    private readonly List<GameObject> spawnedEffects = new List<GameObject>();
    private Sprite whiteSprite;
    private Material runtimeMuzzleMaterial;
    private CombatFeedbackController combatFeedback;
    private CombatImpactSignaturePresenter impactSignaturePresenter;

    private float ScaledIntensity => Mathf.Max(0f, intensity);

    internal static ImpactSignature ResolveImpactSignature(
        CombatImpactTier impactTier,
        bool wasFinalEnemy)
    {
        return impactTier switch
        {
            CombatImpactTier.Normal => new ImpactSignature(
                true,
                false,
                false,
                false,
                false),
            CombatImpactTier.Critical => new ImpactSignature(
                false,
                true,
                false,
                false,
                false),
            CombatImpactTier.Devastating => new ImpactSignature(
                false,
                false,
                true,
                false,
                false),
            CombatImpactTier.Defeat => new ImpactSignature(
                false,
                false,
                true,
                true,
                wasFinalEnemy),
            _ => default
        };
    }

    internal static Color ResolveImpactWaveColor(
        Color primaryLineColor,
        Color secondaryLineColor)
    {
        primaryLineColor.a = 1f;
        secondaryLineColor.a = 1f;
        Color waveColor = Color.Lerp(
            primaryLineColor,
            secondaryLineColor,
            0.68f);
        waveColor = Color.Lerp(waveColor, Color.white, 0.12f);
        waveColor.a = 1f;
        return waveColor;
    }

    private void Awake()
    {
        combatFeedback = GetComponent<CombatFeedbackController>();
        EnsureRuntimeResources();
        EnsureImpactSignaturePresenter();
    }

    private void OnDisable()
    {
        impactSignaturePresenter?.Clear();
        ClearSpawnedEffects();

    }

    private void OnDestroy()
    {
        if (whiteSprite != null)
        {
            Texture2D texture = whiteSprite.texture;
            Destroy(whiteSprite);

            if (texture != null)
            {
                Destroy(texture);
            }
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

        EnemySnapshot snapshot = new EnemySnapshot
        {
            Position = enemy.transform.position,
            Rotation = enemy.transform.rotation,
            Scale = Vector3.one,
            Color = Color.white,
            Captured = true
        };
        SpriteRenderer renderer = FindSnapshotRenderer(enemy);

        if (renderer == null)
        {
            return snapshot;
        }

        snapshot.Sprite = renderer.sprite;
        snapshot.Material = renderer.sharedMaterial;
        snapshot.Position = renderer.transform.position;
        snapshot.Rotation = renderer.transform.rotation;
        snapshot.Scale = renderer.transform.lossyScale;
        snapshot.Color = renderer.color;
        snapshot.SortingLayerId = renderer.sortingLayerID;
        snapshot.SortingOrder = renderer.sortingOrder;
        return snapshot;
    }

    private static SpriteRenderer FindSnapshotRenderer(EnemyController enemy)
    {
        SpriteRenderer selectedRenderer = null;
        float selectedArea = -1f;

        foreach (SpriteRenderer renderer in
                 enemy.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            Vector3 spriteSize = renderer.sprite.bounds.size;
            Vector3 scale = renderer.transform.lossyScale;
            float area = Mathf.Abs(spriteSize.x * scale.x)
                * Mathf.Abs(spriteSize.y * scale.y);

            if (area > selectedArea)
            {
                selectedRenderer = renderer;
                selectedArea = area;
            }
        }

        return selectedRenderer;
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
        float criticalScale = isCritical ? 1.15f : 1f;
        int direction = horizontalDirection == 0 ? 1 : horizontalDirection;
        SpawnMuzzleFlash(
            firePoint.position,
            accent,
            muzzleFlashSize * ScaledIntensity * criticalScale * 0.84f,
            direction,
            isCritical);
        SpawnMuzzleEmbers(
            firePoint.position,
            direction,
            accent,
            Mathf.RoundToInt(muzzleEmberCount * criticalScale * 0.72f));
        combatFeedback ??= GetComponent<CombatFeedbackController>();
        combatFeedback?.PlayShotOpticalKick(
            firePoint.position,
            direction,
            accent,
            isCritical);
        PlayHitStop(shotHitStopDuration * criticalScale);
    }

    public void PlayImpact(
        EnemySnapshot snapshot,
        int horizontalDirection,
        BulletInstance bullet,
        CombatImpactTier impactTier,
        float feedbackMultiplier = 1f,
        float presentationDelay = 0f,
        bool wasFinalEnemy = false)
    {
        if (!presentationEnabled || !snapshot.IsValid)
        {
            return;
        }

        if (impactTier == CombatImpactTier.Defeat
            && presentationDelay > 0f)
        {
            StartCoroutine(PlayImpactAfterDelay(
                snapshot,
                horizontalDirection,
                bullet,
                feedbackMultiplier,
                presentationDelay,
                wasFinalEnemy));
            return;
        }

        EnsureRuntimeResources();
        Color accent = GetAccentColor(bullet);
        Color waveAccent = ResolveImpactWaveColor(
            accent,
            GetSecondaryAccentColor(bullet));
        float impactMultiplier = impactTier == CombatImpactTier.Defeat
            ? Mathf.Max(0f, feedbackMultiplier)
            : 1f;
        combatFeedback ??= GetComponent<CombatFeedbackController>();
        combatFeedback?.PlayOpticalImpact(
            snapshot.Position,
            horizontalDirection,
            impactTier,
            accent,
            impactMultiplier,
            wasFinalEnemy);
        SpawnHitContactFlare(
            snapshot.Position,
            horizontalDirection,
            accent,
            waveAccent,
            snapshot.SortingLayerId,
            snapshot.SortingOrder + 4,
            impactTier,
            impactMultiplier);
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

        if (impactTier == CombatImpactTier.Normal)
        {
            SpawnNormalOpticalGlints(
                snapshot.Position,
                horizontalDirection,
                accent,
                snapshot.SortingLayerId,
                snapshot.SortingOrder + 3);
        }

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

        ImpactSignature signature = ResolveImpactSignature(
            impactTier,
            wasFinalEnemy);
        EnsureImpactSignaturePresenter();
        impactSignaturePresenter?.Play(
            signature,
            snapshot,
            horizontalDirection,
            accent,
            waveAccent,
            impactMultiplier,
            wasFinalEnemy,
            CreateImpactSignatureSettings());

        if (impactTier == CombatImpactTier.Defeat)
        {
            PlayHitStop(defeatHitStopDuration * impactMultiplier);
        }
        else if (impactTier == CombatImpactTier.Devastating)
        {
            PlayHitStop(devastatingHitStopDuration);
        }
        else
        {
            PlayHitStop(impactTier == CombatImpactTier.Critical
                ? criticalHitStopDuration
                : hitStopDuration);
        }
    }

    private IEnumerator PlayImpactAfterDelay(
        EnemySnapshot snapshot,
        int horizontalDirection,
        BulletInstance bullet,
        float feedbackMultiplier,
        float delay,
        bool wasFinalEnemy)
    {
        float remaining = Mathf.Max(0f, delay);

        while (remaining > 0f)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                remaining -= Time.unscaledDeltaTime;
            }
        }

        PlayImpact(
            snapshot,
            horizontalDirection,
            bullet,
            CombatImpactTier.Defeat,
            feedbackMultiplier,
            0f,
            wasFinalEnemy);
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

    }

    private void EnsureImpactSignaturePresenter()
    {
        if (impactSignaturePresenter == null && whiteSprite != null)
        {
            impactSignaturePresenter = new CombatImpactSignaturePresenter(
                this,
                whiteSprite);
        }
    }

    private CombatImpactSignaturePresenter.Settings
        CreateImpactSignatureSettings()
    {
        return new CombatImpactSignaturePresenter.Settings(
            ScaledIntensity,
            normalSnapDuration,
            criticalPrecisionDuration,
            devastatingCompressionDuration,
            devastatingSecondaryWaveDelay,
            devastatingSecondaryWaveDuration,
            defeatAfterimageDuration,
            defeatKnockbackDistance,
            defeatLiftHeight,
            defeatSilhouetteHoldDuration,
            defeatSilhouetteDuration,
            finalDefeatDurationMultiplier,
            defeatDustColor);
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
        Light2D muzzleLight = root.AddComponent<Light2D>();
        muzzleLight.lightType = Light2D.LightType.Point;
        muzzleLight.color = Color.Lerp(accent, Color.white, 0.08f);
        muzzleLight.intensity = isCritical ? 1.15f : 0.85f;
        muzzleLight.pointLightInnerRadius = size * 0.18f;
        muzzleLight.pointLightOuterRadius = size
            * (isCritical ? 1.65f : 1.4f);
        muzzleLight.falloffIntensity = 0.68f;

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
            isCritical ? 2.35f : 1.9f,
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
            isCritical ? 1.45f : 1.05f,
            muzzleRayCount + 2);
        renderers.Add(echoRenderer);

        StartCoroutine(AnimateShaderMuzzle(
            root,
            renderers,
            muzzleFlashDuration,
            direction,
            muzzleLight));
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
        propertyBlock.SetFloat(EffectModeId, 0f);
        renderer.SetPropertyBlock(propertyBlock);
    }

    private void SpawnHitContactFlare(
        Vector3 position,
        int horizontalDirection,
        Color accent,
        Color waveAccent,
        int sortingLayerId,
        int sortingOrder,
        CombatImpactTier impactTier,
        float feedbackMultiplier)
    {
        Material material = ResolveMuzzleMaterial();

        if (material == null)
        {
            return;
        }

        int direction = horizontalDirection == 0 ? 1 : horizontalDirection;
        float tier = (float)impactTier;
        float effectMultiplier = impactTier == CombatImpactTier.Defeat
            ? Mathf.Max(0f, feedbackMultiplier)
            : 1f;
        float size = 0.27f
            * (1f + tier * 0.22f)
            * ScaledIntensity
            * effectMultiplier;
        float duration = (0.09f + tier * 0.025f)
            * Mathf.Lerp(1f, 1.14f, Mathf.Clamp01(effectMultiplier - 1f));
        GameObject root = CreateEffectRoot("Localized Hit Flare", position);
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();

        GameObject contact = CreateSpriteObject(
            "Contact Burst",
            root.transform,
            Color.white,
            sortingOrder);
        contact.transform.localScale = new Vector3(
            size * 2.05f,
            size * 1.5f,
            1f);
        SpriteRenderer contactRenderer = contact.GetComponent<SpriteRenderer>();
        contactRenderer.sortingLayerID = sortingLayerId;
        ConfigureHitRenderer(
            contactRenderer,
            material,
            accent,
            waveAccent,
            direction,
            1.55f + tier * 0.42f,
            5 + Mathf.RoundToInt(tier * 2f));
        renderers.Add(contactRenderer);

        if (impactTier >= CombatImpactTier.Critical)
        {
            GameObject echo = CreateSpriteObject(
                "Contact Echo",
                root.transform,
                Color.white,
                sortingOrder - 1);
            echo.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                direction * (12f + tier * 3f));
            echo.transform.localScale = new Vector3(
                size * 1.62f,
                size * 1.18f,
                1f);
            SpriteRenderer echoRenderer = echo.GetComponent<SpriteRenderer>();
            echoRenderer.sortingLayerID = sortingLayerId;
            ConfigureHitRenderer(
                echoRenderer,
                material,
                Color.Lerp(accent, Color.white, 0.16f),
                waveAccent,
                direction,
                1.08f + tier * 0.3f,
                7 + Mathf.RoundToInt(tier * 2f));
            renderers.Add(echoRenderer);
        }

        StartCoroutine(AnimateShaderImpact(
            root,
            renderers,
            duration,
            direction));
    }

    private static void ConfigureHitRenderer(
        SpriteRenderer renderer,
        Material material,
        Color accent,
        Color waveAccent,
        int horizontalDirection,
        float effectIntensity,
        int rayCount)
    {
        renderer.sharedMaterial = material;
        Color hotColor = Color.Lerp(
            waveAccent,
            new Color(1f, 0.78f, 0.24f, 1f),
            0.24f);
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetColor(PrimaryColorId, accent);
        propertyBlock.SetColor(SecondaryColorId, hotColor);
        propertyBlock.SetFloat(ProgressId, 0f);
        propertyBlock.SetFloat(IntensityId, effectIntensity);
        propertyBlock.SetFloat(
            DirectionId,
            horizontalDirection == 0 ? 1f : horizontalDirection);
        propertyBlock.SetFloat(RayCountId, Mathf.Max(3, rayCount));
        propertyBlock.SetFloat(EffectModeId, 1f);
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
            * 0.58f
            * effectMultiplier));
        Color heatDust = new Color(0.44f, 0.26f, 0.12f, 0.42f);
        Color warmGlint = Color.Lerp(
            new Color(1f, 0.72f, 0.3f, 0.88f),
            accent,
            0.38f);

        for (int sparkIndex = 0; sparkIndex < scaledCount; sparkIndex++)
        {
            bool isHeatDust = sparkIndex % 3 == 0;
            Color color = isHeatDust
                ? Color.Lerp(
                    heatDust,
                    defeatDustColor,
                    Random.Range(0.06f, 0.2f))
                : Color.Lerp(warmGlint, accent, Random.Range(0.08f, 0.3f));
            color.a = isHeatDust
                ? Random.Range(0.22f, 0.42f)
                : Random.Range(0.58f, 0.9f);
            GameObject spark = CreateSpriteObject(
                isHeatDust ? "Heat Dust Mote" : "Optical Impact Glint",
                null,
                color,
                sortingOrder);
            SpriteRenderer renderer = spark.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = sortingLayerId;
            spark.transform.position = position
                + (Vector3)Random.insideUnitCircle * 0.05f;

            float forwardSpeed = Random.Range(0.45f, isDefeated ? 1.8f : 1.2f);
            Vector2 velocity = new Vector2(
                direction * forwardSpeed,
                Random.Range(-0.5f, isDefeated ? 1.15f : 0.72f));

            if (sparkIndex % 4 == 0)
            {
                velocity.x *= -0.35f;
            }

            velocity *= effectMultiplier;

            spark.transform.localScale = new Vector3(
                isHeatDust
                    ? Random.Range(0.025f, 0.065f)
                    : Random.Range(0.07f, 0.16f),
                isHeatDust
                    ? Random.Range(0.018f, 0.05f)
                    : Random.Range(0.006f, 0.018f),
                1f) * Mathf.Lerp(0.75f, 1.25f, ScaledIntensity * 0.5f)
                * effectMultiplier;
            spark.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            StartCoroutine(AnimateOpticalMote(
                spark,
                renderer,
                velocity,
                isDefeated
                    ? Random.Range(0.24f, 0.4f)
                    : Random.Range(0.13f, 0.25f)));
        }
    }

    private void SpawnNormalOpticalGlints(
        Vector3 position,
        int horizontalDirection,
        Color accent,
        int sortingLayerId,
        int sortingOrder)
    {
        int direction = horizontalDirection == 0 ? 1 : horizontalDirection;
        int glintCount = Mathf.Max(
            3,
            Mathf.RoundToInt(
                4f * CombatAccessibilitySettings.ParticleDensityMultiplier));
        Color warmGlint = Color.Lerp(
            new Color(1f, 0.76f, 0.34f, 0.86f),
            accent,
            0.36f);

        for (int glintIndex = 0; glintIndex < glintCount; glintIndex++)
        {
            float angle = 360f * glintIndex / glintCount
                + Random.Range(-24f, 24f);
            Vector2 radial = Quaternion.Euler(0f, 0f, angle)
                * Vector2.right;
            Color color = Color.Lerp(
                warmGlint,
                Color.white,
                Random.Range(0.04f, 0.18f));
            color.a = Random.Range(0.46f, 0.82f);
            GameObject glint = CreateSpriteObject(
                "Local Lens Glint",
                null,
                color,
                sortingOrder);
            SpriteRenderer renderer = glint.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = sortingLayerId;
            glint.transform.position = position
                + (Vector3)(radial * Random.Range(0.025f, 0.11f));
            glint.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                angle + Random.Range(-12f, 12f));
            glint.transform.localScale = new Vector3(
                Random.Range(0.055f, 0.13f),
                Random.Range(0.006f, 0.016f),
                1f) * Mathf.Lerp(0.85f, 1.2f, ScaledIntensity * 0.5f);
            Vector2 velocity = radial * Random.Range(0.22f, 0.52f)
                + Vector2.right * direction * 0.16f;
            StartCoroutine(AnimateOpticalMote(
                glint,
                renderer,
                velocity,
                Random.Range(0.13f, 0.22f)));
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
        float tierScale = 0.82f + (int)impactTier * 0.15f;
        float effectMultiplier = impactTier == CombatImpactTier.Defeat
            ? Mathf.Max(0f, feedbackMultiplier)
            : 1f;
        int scaledCount = Mathf.Max(1, Mathf.RoundToInt(
            count * 0.78f
            * CombatAccessibilitySettings.ParticleDensityMultiplier
            * effectMultiplier));

        for (int streakIndex = 0; streakIndex < scaledCount; streakIndex++)
        {
            Color refractionColor = Color.Lerp(
                new Color(1f, 0.72f, 0.3f, 0.72f),
                accent,
                Random.Range(0.22f, 0.46f));
            refractionColor.a = Random.Range(0.34f, 0.68f);
            GameObject streak = CreateSpriteObject(
                $"{impactTier} Heat Refraction Streak",
                null,
                refractionColor,
                sortingOrder);
            SpriteRenderer renderer = streak.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = sortingLayerId;
            streak.transform.position = position + new Vector3(
                -direction * Random.Range(0.02f, 0.16f),
                Random.Range(-0.16f, 0.16f),
                0f);
            streak.transform.localScale = new Vector3(
                Random.Range(0.22f, 0.52f) * tierScale * effectMultiplier,
                Random.Range(0.006f, 0.018f) * tierScale * effectMultiplier,
                1f);
            streak.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Random.Range(-9f, 9f));
            Vector2 velocity = new Vector2(
                direction * Random.Range(1.2f, 2.6f) * tierScale,
                Random.Range(-0.22f, 0.22f)) * effectMultiplier;
            StartCoroutine(AnimateOpticalMote(
                streak,
                renderer,
                velocity,
                Random.Range(0.12f, 0.22f) * tierScale));
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

            if (GamePauseController.IsPaused)
            {
                continue;
            }

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
        int horizontalDirection,
        Light2D muzzleLight)
    {
        float elapsed = 0f;
        Vector3 startPosition = root.transform.position;
        Vector3 baseScale = root.transform.localScale;
        float baseLightIntensity = muzzleLight == null
            ? 0f
            : muzzleLight.intensity;
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        while (elapsed < duration && root != null)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

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

            if (muzzleLight != null)
            {
                muzzleLight.intensity = baseLightIntensity
                    * attack
                    * decay;
            }

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

    private IEnumerator AnimateShaderImpact(
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

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float attack = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(progress / 0.1f));
            float release = 1f - Mathf.SmoothStep(0.08f, 1f, progress);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            root.transform.localScale = baseScale
                * Mathf.Lerp(0.38f, 1.18f, attack)
                * Mathf.Lerp(0.74f, 1f, release);
            root.transform.position = startPosition
                + Vector3.right
                * horizontalDirection
                * 0.045f
                * pulse;
            root.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                horizontalDirection * pulse * 4.5f);

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

    private IEnumerator AnimateOpticalMote(
        GameObject mote,
        SpriteRenderer renderer,
        Vector2 velocity,
        float duration)
    {
        float elapsed = 0f;
        Vector3 initialScale = mote.transform.localScale;
        float initialAlpha = renderer.color.a;

        while (elapsed < duration && mote != null)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            float deltaTime = Time.unscaledDeltaTime;
            elapsed += deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            mote.transform.position += (Vector3)(velocity * deltaTime);
            velocity *= Mathf.Exp(-2.8f * deltaTime);
            mote.transform.localScale = new Vector3(
                initialScale.x * Mathf.Lerp(0.45f, 1.4f, pulse),
                initialScale.y * Mathf.Lerp(0.35f, 1.15f, pulse)
                    * (1f - progress * 0.72f),
                initialScale.z);
            Color color = renderer.color;
            float attack = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(progress / 0.12f));
            float release = 1f - Mathf.SmoothStep(0.18f, 1f, progress);
            color.a = initialAlpha * attack * release;
            renderer.color = color;
        }

        DestroyEffect(mote);
    }

    private void PlayHitStop(float duration)
    {
        duration *= ScaledIntensity;

        if (duration <= 0f)
        {
            return;
        }

        combatFeedback ??= GetComponent<CombatFeedbackController>();
        combatFeedback?.RequestHitStop(duration);
    }

    public void CancelHitStopForPause()
    {
        combatFeedback ??= GetComponent<CombatFeedbackController>();
        combatFeedback?.CancelPresentationForPause();
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
        Color accent = bullet == null
            ? new Color(1f, 0.3f, 0.06f, 1f)
            : bullet.PrimaryLineColor;
        accent.a = 1f;
        return accent;
    }

    private static Color GetSecondaryAccentColor(BulletInstance bullet)
    {
        Color accent = bullet == null
            ? GetAccentColor(null)
            : bullet.SecondaryLineColor;
        accent.a = 1f;
        return accent;
    }
}
