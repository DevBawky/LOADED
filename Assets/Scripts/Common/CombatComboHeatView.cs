using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal enum ComboHeatTier
{
    None = 0,
    Spark = 1,
    Glow = 2,
    Blaze = 3,
    Inferno = 4,
    Maximum = 5
}

[DisallowMultipleComponent]
public sealed class CombatComboHeatView : MonoBehaviour
{
    private enum FlameEdge
    {
        Bottom = 0,
        Top = 1,
        Left = 2,
        Right = 3
    }

    internal const int IgnitionCombo = 5;
    internal const int MaximumCombo = 40;
    internal const int MaximumEmbers = 24;
    internal const float HeatFadeDuration = 5f;
    private const float IgnitionHeat = 0.16f;

    private sealed class EmberState
    {
        public Image Image;
        public RectTransform Rect;
        public Vector2 Velocity;
        public float Age;
        public float Duration;
        public bool Active;
    }

    private readonly Image[] edgeFlames = new Image[8];
    private readonly Vector3[] edgeBaseScales = new Vector3[8];
    private readonly Texture2D[] flameTextures = new Texture2D[4];
    private readonly Sprite[] flameSprites = new Sprite[4];
    private readonly EmberState[] embers = new EmberState[MaximumEmbers];
    private readonly System.Random visualRandom = new System.Random(4017);

    private TMP_Text comboSource;
    private TMP_Text damageSource;
    private TextMeshProUGUI comboOuterGlow;
    private TextMeshProUGUI comboInnerGlow;
    private TextMeshProUGUI damageOuterGlow;
    private TextMeshProUGUI damageInnerGlow;
    private RectTransform effectRoot;
    private CanvasGroup effectCanvasGroup;
    private float visualTime;
    private float pulseStrength;
    private float heatFadeRemaining;
    private bool heatActivated;
    private int activatedComboCount;
    private int emberSpawnFrame = -1;
    private int embersSpawnedThisFrame;

    internal float CurrentHeat { get; private set; }
    internal int ActiveEmberCount
    {
        get
        {
            int count = 0;
            for (int index = 0; index < embers.Length; index++)
            {
                if (embers[index] != null && embers[index].Active)
                {
                    count++;
                }
            }

            return count;
        }
    }

    internal static float CalculateHeat(int comboCount)
    {
        if (comboCount < IgnitionCombo)
        {
            return 0f;
        }

        float progress = Mathf.InverseLerp(
            IgnitionCombo,
            MaximumCombo,
            comboCount);
        return Mathf.Lerp(IgnitionHeat, 1f, progress);
    }

    internal static float CalculateFadeMultiplier(float elapsedSinceDefeat)
    {
        float remaining = 1f - Mathf.Clamp01(
            elapsedSinceDefeat / HeatFadeDuration);
        return remaining * remaining * (3f - 2f * remaining);
    }

    internal static ComboHeatTier ResolveTier(int comboCount)
    {
        if (comboCount >= MaximumCombo)
        {
            return ComboHeatTier.Maximum;
        }

        if (comboCount >= 30)
        {
            return ComboHeatTier.Inferno;
        }

        if (comboCount >= 20)
        {
            return ComboHeatTier.Blaze;
        }

        if (comboCount >= 10)
        {
            return ComboHeatTier.Glow;
        }

        return comboCount >= IgnitionCombo
            ? ComboHeatTier.Spark
            : ComboHeatTier.None;
    }

    internal static CombatComboHeatView GetOrCreate(
        Transform feedbackPanel,
        TMP_Text comboText,
        TMP_Text damageText)
    {
        if (feedbackPanel == null || comboText == null || damageText == null)
        {
            return null;
        }

        CombatComboHeatView view = feedbackPanel
            .GetComponent<CombatComboHeatView>();
        if (view == null)
        {
            view = feedbackPanel.gameObject.AddComponent<
                CombatComboHeatView>();
        }

        view.Configure(comboText, damageText);
        return view;
    }

    internal void Configure(TMP_Text comboText, TMP_Text damageText)
    {
        EnsureEffectRoot();

        if (comboSource == comboText && damageSource == damageText
            && comboOuterGlow != null && damageOuterGlow != null)
        {
            return;
        }

        DestroyTextLayers();
        comboSource = comboText;
        damageSource = damageText;
        comboOuterGlow = CreateGlowText("Combo Heat Outer", comboSource);
        comboInnerGlow = CreateGlowText("Combo Heat Inner", comboSource);
        damageOuterGlow = CreateGlowText("Damage Heat Outer", damageSource);
        damageInnerGlow = CreateGlowText("Damage Heat Inner", damageSource);
    }

    internal void Pulse(int comboCount)
    {
        float heat = CalculateHeat(comboCount);
        if (heat <= 0f)
        {
            return;
        }

        heatActivated = true;
        activatedComboCount = comboCount;
        heatFadeRemaining = HeatFadeDuration;
        pulseStrength = Mathf.Max(pulseStrength, 0.35f + heat * 0.65f);
        if (emberSpawnFrame != Time.frameCount)
        {
            emberSpawnFrame = Time.frameCount;
            embersSpawnedThisFrame = 0;
        }

        int emberCount = Mathf.Clamp(
            Mathf.RoundToInt((1f + heat * 4f)
                * CombatAccessibilitySettings.ParticleDensityMultiplier),
            0,
            6);
        emberCount = Mathf.Min(
            emberCount,
            MaximumEmbers - embersSpawnedThisFrame);

        for (int index = 0; index < emberCount; index++)
        {
            SpawnEmber(index % 2 == 0 ? comboSource : damageSource, heat);
        }

        embersSpawnedThisFrame += emberCount;
    }

    internal void Render(
        int comboCount,
        float comboAlpha,
        float damageAlpha,
        float deltaTime,
        bool paused)
    {
        EnsureEffectRoot();
        float baseHeat = CalculateHeat(comboCount);
        if (baseHeat <= 0f)
        {
            heatActivated = false;
            activatedComboCount = 0;
            heatFadeRemaining = 0f;
            CurrentHeat = 0f;
        }
        else
        {
            if (!heatActivated || activatedComboCount != comboCount)
            {
                heatActivated = true;
                activatedComboCount = comboCount;
                heatFadeRemaining = HeatFadeDuration;
            }

            if (!paused)
            {
                heatFadeRemaining = Mathf.Max(
                    0f,
                    heatFadeRemaining - Mathf.Max(0f, deltaTime));
            }

            float elapsedSinceDefeat = HeatFadeDuration
                - heatFadeRemaining;
            CurrentHeat = baseHeat * CalculateFadeMultiplier(
                elapsedSinceDefeat);
        }

        if (!paused)
        {
            visualTime += Mathf.Max(0f, deltaTime);
            pulseStrength = Mathf.MoveTowards(
                pulseStrength,
                0f,
                Mathf.Max(0f, deltaTime) * 3.5f);
            UpdateEmbers(deltaTime, CurrentHeat);
        }

        float presentationIntensity = Mathf.Clamp01(
            CombatAccessibilitySettings.PresentationIntensity);
        float flashIntensity = Mathf.Clamp01(
            CombatAccessibilitySettings.FlashMultiplier);
        float visibleHeat = CurrentHeat * presentationIntensity;
        float visiblePulse = pulseStrength * flashIntensity
            * presentationIntensity;
        ComboHeatTier tier = ResolveTier(comboCount);

        UpdateEdgeFlames(visibleHeat, visiblePulse, tier);
        UpdateTextGlow(
            comboSource,
            comboOuterGlow,
            comboInnerGlow,
            comboAlpha,
            visibleHeat,
            visiblePulse,
            0f);
        UpdateTextGlow(
            damageSource,
            damageOuterGlow,
            damageInnerGlow,
            damageAlpha,
            visibleHeat,
            visiblePulse,
            1.7f);
        effectCanvasGroup.alpha = visibleHeat > 0f ? 1f : 0f;
    }

    internal void ResetPresentation()
    {
        CurrentHeat = 0f;
        pulseStrength = 0f;
        heatFadeRemaining = 0f;
        heatActivated = false;
        activatedComboCount = 0;
        emberSpawnFrame = -1;
        embersSpawnedThisFrame = 0;
        if (effectCanvasGroup != null)
        {
            effectCanvasGroup.gameObject.SetActive(true);
        }

        for (int index = 0; index < edgeFlames.Length; index++)
        {
            if (edgeFlames[index] != null)
            {
                edgeFlames[index].color = Color.clear;
            }
        }

        SetTextAlpha(comboOuterGlow, 0f);
        SetTextAlpha(comboInnerGlow, 0f);
        SetTextAlpha(damageOuterGlow, 0f);
        SetTextAlpha(damageInnerGlow, 0f);

        for (int index = 0; index < embers.Length; index++)
        {
            DeactivateEmber(embers[index]);
        }

        if (effectCanvasGroup != null)
        {
            effectCanvasGroup.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        for (int index = 0; index < flameSprites.Length; index++)
        {
            DestroyGeneratedObject(flameSprites[index]);
            DestroyGeneratedObject(flameTextures[index]);
        }
    }

    private void EnsureEffectRoot()
    {
        if (effectRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject(
            "Combo Heat Presentation",
            typeof(RectTransform),
            typeof(CanvasGroup));
        rootObject.layer = gameObject.layer;
        effectRoot = rootObject.GetComponent<RectTransform>();
        effectRoot.SetParent(transform, false);
        effectRoot.anchorMin = Vector2.zero;
        effectRoot.anchorMax = Vector2.one;
        effectRoot.offsetMin = Vector2.zero;
        effectRoot.offsetMax = Vector2.zero;
        effectRoot.SetAsFirstSibling();

        effectCanvasGroup = rootObject.GetComponent<CanvasGroup>();
        effectCanvasGroup.alpha = 0f;
        effectCanvasGroup.interactable = false;
        effectCanvasGroup.blocksRaycasts = false;

        CreateFlameSprites();
        CreateEdgeFlames();
        CreateEmberPool();
    }

    private void CreateFlameSprites()
    {
        for (int index = 0; index < flameSprites.Length; index++)
        {
            CreateFlameSprite((FlameEdge)index);
        }
    }

    private void CreateFlameSprite(FlameEdge edge)
    {
        bool horizontal = edge == FlameEdge.Bottom
            || edge == FlameEdge.Top;
        int width = horizontal ? 128 : 64;
        int height = horizontal ? 64 : 128;
        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = $"Runtime Combo Flame {edge}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float normalizedX = (float)x / (width - 1);
                float normalizedY = (float)y / (height - 1);
                float along = horizontal ? normalizedX : normalizedY;
                float inward = edge switch
                {
                    FlameEdge.Bottom => normalizedY,
                    FlameEdge.Top => 1f - normalizedY,
                    FlameEdge.Left => normalizedX,
                    _ => 1f - normalizedX
                };
                float flameHeight = 0.32f
                    + Mathf.PerlinNoise(
                        along * 9.6f,
                        0.37f + (int)edge * 0.29f) * 0.48f
                    + Mathf.Sin(
                        along * 39.7f + (int)edge * 1.13f) * 0.055f;
                float alpha = inward >= flameHeight
                    ? 0f
                    : Mathf.Pow(
                        1f - inward / Mathf.Max(0.01f, flameHeight),
                        0.55f);
                pixels[y * width + x] = new Color32(
                    255,
                    255,
                    255,
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = $"Runtime Combo Flame Sprite {edge}";
        sprite.hideFlags = HideFlags.DontSave;
        flameTextures[(int)edge] = texture;
        flameSprites[(int)edge] = sprite;
    }

    private void CreateEdgeFlames()
    {
        edgeFlames[0] = CreateHorizontalEdge(
            "Bottom Heat", FlameEdge.Bottom, 140f);
        edgeFlames[1] = CreateHorizontalEdge(
            "Bottom Heat Inner", FlameEdge.Bottom, 96f);
        edgeFlames[2] = CreateHorizontalEdge(
            "Top Heat", FlameEdge.Top, 140f);
        edgeFlames[3] = CreateHorizontalEdge(
            "Top Heat Inner", FlameEdge.Top, 96f);
        edgeFlames[4] = CreateVerticalEdge(
            "Left Heat", FlameEdge.Left, 140f);
        edgeFlames[5] = CreateVerticalEdge(
            "Left Heat Inner", FlameEdge.Left, 96f);
        edgeFlames[6] = CreateVerticalEdge(
            "Right Heat", FlameEdge.Right, 140f);
        edgeFlames[7] = CreateVerticalEdge(
            "Right Heat Inner", FlameEdge.Right, 96f);

        for (int index = 0; index < edgeFlames.Length; index++)
        {
            edgeBaseScales[index] = edgeFlames[index].rectTransform.localScale;
        }
    }

    private Image CreateHorizontalEdge(
        string objectName,
        FlameEdge edge,
        float edgeHeight)
    {
        Image image = CreateImage(objectName, effectRoot);
        image.sprite = flameSprites[(int)edge];
        image.type = Image.Type.Simple;
        RectTransform rect = image.rectTransform;
        float verticalAnchor = edge == FlameEdge.Bottom ? 0f : 1f;
        rect.anchorMin = new Vector2(0f, verticalAnchor);
        rect.anchorMax = new Vector2(1f, verticalAnchor);
        rect.pivot = new Vector2(0.5f, verticalAnchor);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, edgeHeight);
        return image;
    }

    private Image CreateVerticalEdge(
        string objectName,
        FlameEdge edge,
        float edgeWidth)
    {
        Image image = CreateImage(objectName, effectRoot);
        image.sprite = flameSprites[(int)edge];
        image.type = Image.Type.Simple;
        RectTransform rect = image.rectTransform;
        float horizontalAnchor = edge == FlameEdge.Left ? 0f : 1f;
        rect.anchorMin = new Vector2(horizontalAnchor, 0f);
        rect.anchorMax = new Vector2(horizontalAnchor, 1f);
        rect.pivot = new Vector2(horizontalAnchor, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(edgeWidth, 0f);
        return image;
    }

    private void CreateEmberPool()
    {
        for (int index = 0; index < embers.Length; index++)
        {
            Image image = CreateImage($"Combo Ember {index + 1}", effectRoot);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            image.gameObject.SetActive(false);
            embers[index] = new EmberState
            {
                Image = image,
                Rect = rect
            };
        }
    }

    private TextMeshProUGUI CreateGlowText(string objectName, TMP_Text source)
    {
        if (source == null)
        {
            return null;
        }

        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = source.gameObject.layer;
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(source.transform.parent, false);
        rect.SetSiblingIndex(source.transform.GetSiblingIndex());

        TextMeshProUGUI glow = textObject.GetComponent<TextMeshProUGUI>();
        glow.font = source.font;
        glow.fontSharedMaterial = source.fontSharedMaterial;
        glow.alignment = source.alignment;
        glow.enableAutoSizing = source.enableAutoSizing;
        glow.fontSize = source.fontSize;
        glow.fontSizeMin = source.fontSizeMin;
        glow.fontSizeMax = source.fontSizeMax;
        glow.fontStyle = source.fontStyle | FontStyles.Bold;
        glow.richText = source.richText;
        glow.raycastTarget = false;
        glow.color = Color.clear;
        CopyRectTransform(source.rectTransform, rect);
        return glow;
    }

    private void UpdateEdgeFlames(
        float heat,
        float pulse,
        ComboHeatTier tier)
    {
        for (int index = 0; index < edgeFlames.Length; index++)
        {
            Image image = edgeFlames[index];
            if (image == null)
            {
                continue;
            }

            float flicker = 0.78f + Mathf.Sin(
                visualTime * (5.2f + index * 0.37f) + index * 1.91f)
                * 0.14f
                + Mathf.Sin(visualTime * 9.7f + index) * 0.08f;
            bool innerLayer = index % 2 == 1;
            Color color = innerLayer
                ? new Color(1f, 0.66f, 0.12f, 1f)
                : new Color(0.92f, 0.11f, 0.025f, 1f);
            float maximumMultiplier = tier == ComboHeatTier.Maximum
                ? 1.25f
                : 1f;
            float baseAlpha = innerLayer ? 0.18f : 0.27f;
            color.a = Mathf.Clamp01(
                (heat * baseAlpha * flicker + pulse * 0.055f)
                * maximumMultiplier);
            image.color = color;
            float scalePulse = 1f
                + Mathf.Sin(visualTime * 6.3f + index) * heat * 0.025f
                + pulse * 0.035f;
            image.rectTransform.localScale = edgeBaseScales[index]
                * scalePulse;
        }
    }

    private void UpdateTextGlow(
        TMP_Text source,
        TextMeshProUGUI outer,
        TextMeshProUGUI inner,
        float sourceAlpha,
        float heat,
        float pulse,
        float phase)
    {
        if (source == null || outer == null || inner == null)
        {
            return;
        }

        float flicker = 0.82f
            + Mathf.Sin(visualTime * 7.1f + phase) * 0.12f
            + Mathf.Sin(visualTime * 12.7f + phase * 0.7f) * 0.06f;
        float glow = Mathf.Clamp01((heat * flicker + pulse * 0.4f)
            * Mathf.Clamp01(sourceAlpha));
        float rise = Mathf.Sin(visualTime * 8.4f + phase) * heat * 1.8f;

        SyncGlowText(
            source,
            outer,
            new Color(0.82f, 0.04f, 0.01f, glow * 0.48f),
            1f + heat * 0.085f + pulse * 0.035f,
            new Vector2(0f, -2.5f + rise));
        SyncGlowText(
            source,
            inner,
            new Color(1f, 0.55f, 0.08f, glow * 0.62f),
            1f + heat * 0.035f + pulse * 0.02f,
            new Vector2(0f, 1f + rise * 0.5f));
    }

    private void SpawnEmber(TMP_Text source, float heat)
    {
        if (source == null || effectRoot == null)
        {
            return;
        }

        EmberState ember = FindAvailableEmber();
        Bounds sourceBounds = RectTransformUtility
            .CalculateRelativeRectTransformBounds(
                effectRoot,
                source.rectTransform);
        float x = Mathf.Lerp(
            sourceBounds.min.x,
            sourceBounds.max.x,
            NextRandom());
        float y = Mathf.Lerp(
            sourceBounds.min.y,
            sourceBounds.max.y,
            0.2f + NextRandom() * 0.55f);
        float size = Mathf.Lerp(2.5f, 7f, heat)
            * Mathf.Lerp(0.75f, 1.2f, NextRandom());

        ember.Active = true;
        ember.Age = 0f;
        ember.Duration = Mathf.Lerp(0.45f, 0.85f, NextRandom());
        ember.Velocity = new Vector2(
            Mathf.Lerp(-22f, 22f, NextRandom()),
            Mathf.Lerp(75f, 150f, NextRandom()));
        ember.Rect.anchoredPosition = new Vector2(x, y);
        ember.Rect.sizeDelta = new Vector2(size * 0.55f, size);
        ember.Image.color = new Color(1f, 0.45f, 0.05f, 0.9f);
        ember.Image.gameObject.SetActive(true);
    }

    private EmberState FindAvailableEmber()
    {
        EmberState oldest = embers[0];
        float oldestProgress = -1f;

        for (int index = 0; index < embers.Length; index++)
        {
            EmberState ember = embers[index];
            if (!ember.Active)
            {
                return ember;
            }

            float progress = ember.Duration <= 0f
                ? 1f
                : ember.Age / ember.Duration;
            if (progress > oldestProgress)
            {
                oldest = ember;
                oldestProgress = progress;
            }
        }

        return oldest;
    }

    private void UpdateEmbers(float deltaTime, float heat)
    {
        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        for (int index = 0; index < embers.Length; index++)
        {
            EmberState ember = embers[index];
            if (ember == null || !ember.Active)
            {
                continue;
            }

            ember.Age += safeDeltaTime;
            if (ember.Age >= ember.Duration || heat <= 0f)
            {
                DeactivateEmber(ember);
                continue;
            }

            ember.Rect.anchoredPosition += ember.Velocity * safeDeltaTime;
            float progress = Mathf.Clamp01(ember.Age / ember.Duration);
            Color color = Color.Lerp(
                new Color(1f, 0.75f, 0.14f, 1f),
                new Color(0.9f, 0.08f, 0.01f, 0f),
                progress);
            color.a *= (1f - progress) * Mathf.Clamp01(
                CombatAccessibilitySettings.PresentationIntensity);
            ember.Image.color = color;
        }
    }

    private static void SyncGlowText(
        TMP_Text source,
        TextMeshProUGUI glow,
        Color color,
        float scale,
        Vector2 offset)
    {
        RectTransform sourceRect = source.rectTransform;
        RectTransform glowRect = glow.rectTransform;
        CopyRectTransform(sourceRect, glowRect);
        glowRect.anchoredPosition += offset;
        glowRect.localScale = sourceRect.localScale * scale;
        glowRect.localRotation = sourceRect.localRotation;
        glow.text = source.text;
        glow.fontSize = source.fontSize;
        glow.color = color;
    }

    private static void CopyRectTransform(
        RectTransform source,
        RectTransform destination)
    {
        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.pivot = source.pivot;
        destination.anchoredPosition = source.anchoredPosition;
        destination.sizeDelta = source.sizeDelta;
        destination.localScale = source.localScale;
        destination.localRotation = source.localRotation;
    }

    private static Image CreateImage(string objectName, Transform parent)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.layer = parent.gameObject.layer;
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.clear;
        return image;
    }

    private float NextRandom()
    {
        return (float)visualRandom.NextDouble();
    }

    private static void SetTextAlpha(TMP_Text text, float alpha)
    {
        if (text == null)
        {
            return;
        }

        Color color = text.color;
        color.a = alpha;
        text.color = color;
    }

    private static void DeactivateEmber(EmberState ember)
    {
        if (ember == null)
        {
            return;
        }

        ember.Active = false;
        ember.Age = 0f;
        if (ember.Image != null)
        {
            ember.Image.gameObject.SetActive(false);
        }
    }

    private void DestroyTextLayers()
    {
        DestroyGlowText(comboOuterGlow);
        DestroyGlowText(comboInnerGlow);
        DestroyGlowText(damageOuterGlow);
        DestroyGlowText(damageInnerGlow);
        comboOuterGlow = null;
        comboInnerGlow = null;
        damageOuterGlow = null;
        damageInnerGlow = null;
    }

    private static void DestroyGlowText(TextMeshProUGUI target)
    {
        if (target != null)
        {
            DestroyGeneratedObject(target.gameObject);
        }
    }

    private static void DestroyGeneratedObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
