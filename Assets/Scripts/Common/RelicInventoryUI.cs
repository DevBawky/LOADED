using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RelicInventoryUI : MonoBehaviour
{
    private const float PulseScale = 1.18f;
    private const float PulseUpDuration = 0.08f;
    private const float PulseDownDuration = 0.14f;

    [SerializeField] private RectTransform relicContainer;
    [SerializeField] private GameObject relicPrefab;
    [SerializeField] private RelicManager relicManager;

    [Header("Relic Activation Effect")]
    [Min(0.1f)] [SerializeField] private float activationDuration = 0.48f;
    [Min(1f)] [SerializeField] private float activationWaveScale = 1.65f;
    [SerializeField] private Color attackActivationColor =
        new Color(1f, 0.35f, 0.08f, 1f);
    [SerializeField] private Color defenseActivationColor =
        new Color(0.2f, 1f, 0.42f, 1f);
    [SerializeField] private Color cylinderActivationColor =
        new Color(0.1f, 0.9f, 1f, 1f);
    [SerializeField] private Color rewardActivationColor =
        new Color(1f, 0.78f, 0.08f, 1f);
    [SerializeField] private Color specialActivationColor =
        new Color(0.72f, 0.3f, 1f, 1f);

    private readonly List<GameObject> spawnedRelics = new List<GameObject>();
    private readonly Dictionary<RelicInstance, RectTransform> relicIcons =
        new Dictionary<RelicInstance, RectTransform>();
    private readonly Dictionary<RelicInstance, Coroutine> pulseAnimations =
        new Dictionary<RelicInstance, Coroutine>();
    private readonly List<GameObject> activeActivationEffects =
        new List<GameObject>();
    private Texture2D activationRingTexture;
    private Sprite activationRingSprite;
    private RelicTooltipUI tooltip;
    private RelicInstance hoveredRelic;
    private readonly List<RelicInstance> eventSelectedRelics =
        new List<RelicInstance>();
    private int eventRequiredSelectionCount;
    private Func<RelicInstance, bool> eventSelectionPredicate;
    private Action<IReadOnlyList<RelicInstance>> eventConfirmCallback;
    private Action eventCancelCallback;

    public event Action<int, int> EventSelectionChanged;
    public bool IsEventSelectionActive => eventRequiredSelectionCount > 0;

    private void Awake()
    {
        relicContainer ??= transform as RectTransform;
        ResolveRelicManager();
    }

    private void OnEnable()
    {
        ResolveRelicManager();

        if (relicManager != null)
        {
            relicManager.InventoryChanged -= Refresh;
            relicManager.InventoryChanged += Refresh;
            relicManager.RelicTriggered -= HandleRelicTriggered;
            relicManager.RelicTriggered += HandleRelicTriggered;
        }

        Refresh();
        StartCoroutine(RefreshAfterSceneInitialization());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        pulseAnimations.Clear();
        foreach (RectTransform icon in relicIcons.Values)
        {
            if (icon != null)
            {
                icon.localScale = Vector3.one;
            }
        }
        ClearActivationEffects();
        if (relicManager != null)
        {
            relicManager.InventoryChanged -= Refresh;
            relicManager.RelicTriggered -= HandleRelicTriggered;
        }

        HideTooltip();

        if (IsEventSelectionActive)
        {
            CancelEventSelection();
        }
    }

    private void OnDestroy()
    {
        DestroyRuntimeObject(activationRingSprite);
        DestroyRuntimeObject(activationRingTexture);
        activationRingSprite = null;
        activationRingTexture = null;
    }

    private void ResolveRelicManager()
    {
        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);
    }

    private IEnumerator RefreshAfterSceneInitialization()
    {
        // Dedicated Shop/Event managers and restored run data are prepared by
        // their scene controllers. Retry once after that initialization so the
        // floating inventory is not left empty because of Awake/OnEnable order.
        yield return null;
        ResolveRelicManager();

        if (relicManager != null)
        {
            relicManager.InventoryChanged -= Refresh;
            relicManager.InventoryChanged += Refresh;
            relicManager.RelicTriggered -= HandleRelicTriggered;
            relicManager.RelicTriggered += HandleRelicTriggered;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (relicContainer == null || relicPrefab == null
            || relicManager == null)
        {
            ClearSpawnedRelics();
            return;
        }

        HashSet<RelicInstance> currentRelics =
            new HashSet<RelicInstance>();
        foreach (RelicInstance relic in relicManager.OwnedRelics)
        {
            if (relic?.Data == null)
            {
                continue;
            }

            currentRelics.Add(relic);
            if (relicIcons.TryGetValue(relic, out RectTransform existingIcon)
                && existingIcon != null)
            {
                ConfigureRelic(existingIcon.gameObject, relic);
                continue;
            }

            GameObject relicObject = Instantiate(relicPrefab, relicContainer);
            relicObject.name = relicPrefab.name;
            ConfigureRelic(relicObject, relic);
            RelicInventoryIconUI interaction =
                relicObject.GetComponent<RelicInventoryIconUI>();
            interaction ??= relicObject.AddComponent<RelicInventoryIconUI>();
            interaction.Initialize(this, relic);
            spawnedRelics.Add(relicObject);
            relicIcons[relic] = relicObject.transform as RectTransform;
        }

        List<RelicInstance> removedRelics = new List<RelicInstance>();
        foreach (KeyValuePair<RelicInstance, RectTransform> entry in relicIcons)
        {
            if (!currentRelics.Contains(entry.Key))
            {
                removedRelics.Add(entry.Key);
            }
        }

        foreach (RelicInstance removedRelic in removedRelics)
        {
            if (!relicIcons.TryGetValue(
                    removedRelic,
                    out RectTransform removedIcon))
            {
                continue;
            }

            if (pulseAnimations.TryGetValue(
                    removedRelic,
                    out Coroutine pulse)
                && pulse != null)
            {
                StopCoroutine(pulse);
            }
            pulseAnimations.Remove(removedRelic);
            relicIcons.Remove(removedRelic);
            GameObject removedObject = removedIcon == null
                ? null
                : removedIcon.gameObject;
            spawnedRelics.Remove(removedObject);
            DestroyRelicObject(removedObject);
        }
    }

    private void ClearSpawnedRelics()
    {
        HideTooltip();
        StopAllCoroutines();
        pulseAnimations.Clear();
        ClearActivationEffects();
        relicIcons.Clear();

        foreach (GameObject relicObject in spawnedRelics)
        {
            DestroyRelicObject(relicObject);
        }

        spawnedRelics.Clear();
    }

    private static void DestroyRelicObject(GameObject relicObject)
    {
        if (relicObject == null)
        {
            return;
        }

        relicObject.SetActive(false);
        if (Application.isPlaying)
        {
            Destroy(relicObject);
        }
        else
        {
            DestroyImmediate(relicObject);
        }
    }

    private void HandleRelicTriggered(
        RelicInstance relic,
        RelicEffectData effect)
    {
        if (effect?.EffectType == RelicEffectType.PredatorHolster)
        {
            StartCoroutine(PlayDelayedRelicActivation(relic, effect));
            return;
        }

        PlayRelicActivation(relic, effect);
    }

    private IEnumerator PlayDelayedRelicActivation(
        RelicInstance relic,
        RelicEffectData effect)
    {
        // 처치 처리 직후 실행되는 인벤토리 갱신과 겹치지 않게 한 프레임
        // 미룬 뒤, 해당 탄환으로 적을 처치한 순간의 연출을 보여 준다.
        yield return null;
        PlayRelicActivation(relic, effect);
    }

    private void PlayRelicActivation(
        RelicInstance relic,
        RelicEffectData effect)
    {
        if (relic == null || !relicIcons.TryGetValue(
                relic,
                out RectTransform icon)
            || icon == null)
        {
            return;
        }

        if (pulseAnimations.TryGetValue(relic, out Coroutine running)
            && running != null)
        {
            StopCoroutine(running);
        }

        icon.localScale = Vector3.one;
        pulseAnimations[relic] = StartCoroutine(PulseRelic(relic, icon));
        StartCoroutine(PlayActivationEffect(
            icon,
            GetActivationColor(effect)));
    }

    private IEnumerator PlayActivationEffect(RectTransform icon, Color color)
    {
        if (icon == null)
        {
            yield break;
        }

        Sprite ringSprite = GetActivationRingSprite();
        GameObject effectRoot = new GameObject(
            "Effect | Relic Activation",
            typeof(RectTransform),
            typeof(Canvas));
        effectRoot.layer = icon.gameObject.layer;
        RectTransform rootRect = effectRoot.GetComponent<RectTransform>();
        rootRect.SetParent(icon, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.localScale = Vector3.one;
        rootRect.SetAsLastSibling();

        Canvas effectCanvas = effectRoot.GetComponent<Canvas>();
        Canvas parentCanvas = icon.GetComponentInParent<Canvas>();
        effectCanvas.overrideSorting = true;
        effectCanvas.sortingLayerID = parentCanvas == null
            ? 0
            : parentCanvas.sortingLayerID;
        effectCanvas.sortingOrder = (parentCanvas == null
            ? 0
            : parentCanvas.sortingOrder) + 20;

        Image ring = CreateActivationImage(
            effectRoot.transform,
            "Image | Activation Ring",
            ringSprite);
        Image wave = CreateActivationImage(
            effectRoot.transform,
            "Image | Activation Wave",
            ringSprite);
        activeActivationEffects.Add(effectRoot);

        float duration = Mathf.Max(0.1f, activationDuration);
        float elapsed = 0f;
        while (effectRoot != null && icon != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float entrance = Mathf.Clamp01(progress / 0.16f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, progress);

            RectTransform ringRect = ring.rectTransform;
            ringRect.localScale = Vector3.one * Mathf.Lerp(
                0.78f,
                1.18f,
                EaseOutBack(progress));
            ringRect.localEulerAngles = new Vector3(
                0f,
                0f,
                Mathf.Lerp(-110f, 170f, progress));
            ring.color = WithAlpha(color, entrance * fade);

            float waveProgress = Mathf.SmoothStep(0f, 1f, progress);
            wave.rectTransform.localScale = Vector3.one * Mathf.Lerp(
                0.72f,
                Mathf.Max(1f, activationWaveScale),
                waveProgress);
            wave.color = WithAlpha(color, fade * fade * 0.72f);
            yield return null;
        }

        activeActivationEffects.Remove(effectRoot);
        DestroyRelicObject(effectRoot);
    }

    private static Image CreateActivationImage(
        Transform parent,
        string objectName,
        Sprite sprite)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.layer = parent.gameObject.layer;
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.one * 118f;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private Sprite GetActivationRingSprite()
    {
        if (activationRingSprite != null)
        {
            return activationRingSprite;
        }

        const int size = 128;
        activationRingTexture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false)
        {
            name = "Relic Activation Ring (Runtime)",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        float outerRadius = size * 0.48f;
        float innerRadius = size * 0.37f;
        float antialias = 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                float radius = offset.magnitude;
                float outerFade = 1f - Mathf.InverseLerp(
                    outerRadius - antialias,
                    outerRadius + antialias,
                    radius);
                float innerFade = Mathf.InverseLerp(
                    innerRadius - antialias,
                    innerRadius + antialias,
                    radius);
                float ringAlpha = Mathf.Clamp01(outerFade * innerFade);
                float angle = Mathf.Atan2(offset.y, offset.x)
                    / (Mathf.PI * 2f) + 0.5f;
                float dash = Mathf.Repeat(angle * 8f, 1f) < 0.68f
                    ? 1f
                    : 0.12f;
                float highlight = 0.55f + 0.45f * Mathf.Pow(
                    Mathf.Max(0f, Mathf.Cos(angle * Mathf.PI * 2f)),
                    8f);
                pixels[y * size + x] = new Color(
                    1f,
                    1f,
                    1f,
                    ringAlpha * dash * highlight);
            }
        }

        activationRingTexture.SetPixels(pixels);
        activationRingTexture.Apply(false, true);
        activationRingSprite = Sprite.Create(
            activationRingTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0,
            SpriteMeshType.FullRect);
        activationRingSprite.name = "Relic Activation Ring (Runtime)";
        return activationRingSprite;
    }

    private Color GetActivationColor(RelicEffectData effect)
    {
        return effect?.EffectType switch
        {
            RelicEffectType.PreventLethalDamage
                or RelicEffectType.BrinkTrigger => defenseActivationColor,
            RelicEffectType.GoldPanner => rewardActivationColor,
            RelicEffectType.Carriage
                or RelicEffectType.ClosedCircuit
                or RelicEffectType.EmptyBeat
                or RelicEffectType.LuckyChamber
                or RelicEffectType.PredatorHolster => cylinderActivationColor,
            RelicEffectType.InfectiousIncubator
                or RelicEffectType.MutationCatalyst
                or RelicEffectType.FamilyWill => specialActivationColor,
            _ => attackActivationColor
        };
    }

    private void ClearActivationEffects()
    {
        foreach (GameObject effect in activeActivationEffects)
        {
            DestroyRelicObject(effect);
        }
        activeActivationEffects.Clear();
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a *= Mathf.Clamp01(alpha);
        return color;
    }

    private static float EaseOutBack(float value)
    {
        float shifted = Mathf.Clamp01(value) - 1f;
        return 1f + shifted * shifted * (2.5f * shifted + 1.5f);
    }

    private static void DestroyRuntimeObject(UnityEngine.Object value)
    {
        if (value == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(value);
        }
        else
        {
            DestroyImmediate(value);
        }
    }

    private IEnumerator PulseRelic(RelicInstance relic, RectTransform icon)
    {
        yield return ScaleIcon(
            icon,
            Vector3.one,
            Vector3.one * PulseScale,
            PulseUpDuration);
        yield return ScaleIcon(
            icon,
            Vector3.one * PulseScale,
            Vector3.one,
            PulseDownDuration);

        if (icon != null)
        {
            icon.localScale = Vector3.one;
        }
        pulseAnimations.Remove(relic);
    }

    private static IEnumerator ScaleIcon(
        RectTransform icon,
        Vector3 from,
        Vector3 to,
        float duration)
    {
        float elapsed = 0f;

        while (icon != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            icon.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }
    }

    internal void ShowTooltip(RelicInstance relic, Vector2 pointerPosition)
    {
        if (relic?.Data == null)
        {
            HideTooltip();
            return;
        }

        EnsureTooltip();
        if (tooltip == null)
        {
            return;
        }

        hoveredRelic = relic;
        tooltip.Show(relic.Data, pointerPosition);
    }

    internal void MoveTooltip(RelicInstance relic, Vector2 pointerPosition)
    {
        if (ReferenceEquals(hoveredRelic, relic)
            && tooltip != null)
        {
            tooltip.Move(relic.Data, pointerPosition);
        }
    }

    internal void HideTooltip(RelicInstance relic = null)
    {
        if (relic != null && !ReferenceEquals(hoveredRelic, relic))
        {
            return;
        }

        hoveredRelic = null;
        if (tooltip != null)
        {
            tooltip.Hide(relic?.Data);
        }
    }

    public bool BeginEventSelection(
        int requiredSelectionCount,
        Func<RelicInstance, bool> selectionPredicate,
        Action<IReadOnlyList<RelicInstance>> onConfirm,
        Action onCancel)
    {
        if (relicManager == null || requiredSelectionCount <= 0)
        {
            return false;
        }

        int eligibleCount = 0;
        foreach (RelicInstance relic in relicManager.OwnedRelics)
        {
            if (relic?.Data != null && (selectionPredicate == null
                || selectionPredicate(relic)))
            {
                eligibleCount++;
            }
        }

        if (eligibleCount < requiredSelectionCount)
        {
            return false;
        }

        eventSelectedRelics.Clear();
        eventRequiredSelectionCount = requiredSelectionCount;
        eventSelectionPredicate = selectionPredicate;
        eventConfirmCallback = onConfirm;
        eventCancelCallback = onCancel;
        EventSelectionChanged?.Invoke(0, eventRequiredSelectionCount);
        Refresh();
        return true;
    }

    public bool ConfirmEventSelection()
    {
        if (!IsEventSelectionActive
            || eventSelectedRelics.Count != eventRequiredSelectionCount)
        {
            return false;
        }

        List<RelicInstance> confirmed =
            new List<RelicInstance>(eventSelectedRelics);
        Action<IReadOnlyList<RelicInstance>> callback = eventConfirmCallback;
        ResetEventSelection();
        callback?.Invoke(confirmed);
        return true;
    }

    public void CancelEventSelection()
    {
        if (!IsEventSelectionActive)
        {
            return;
        }

        Action callback = eventCancelCallback;
        ResetEventSelection();
        callback?.Invoke();
    }

    internal void ToggleEventSelection(RelicInstance relic)
    {
        if (!IsEventSelectionActive || relic?.Data == null
            || eventSelectionPredicate != null
            && !eventSelectionPredicate(relic))
        {
            return;
        }

        if (eventSelectedRelics.Contains(relic))
        {
            eventSelectedRelics.Remove(relic);
        }
        else if (eventSelectedRelics.Count < eventRequiredSelectionCount)
        {
            eventSelectedRelics.Add(relic);
        }

        EventSelectionChanged?.Invoke(
            eventSelectedRelics.Count,
            eventRequiredSelectionCount);
        Refresh();
    }

    private void ResetEventSelection()
    {
        eventSelectedRelics.Clear();
        eventRequiredSelectionCount = 0;
        eventSelectionPredicate = null;
        eventConfirmCallback = null;
        eventCancelCallback = null;
        Refresh();
    }

    private void EnsureTooltip()
    {
        if (tooltip != null)
        {
            return;
        }

        TMP_Text fontSource = relicPrefab == null
            ? null
            : relicPrefab.GetComponentInChildren<TMP_Text>(true);
        tooltip = RelicTooltipUI.GetOrCreate(this, fontSource);
    }

    private void ConfigureRelic(
        GameObject relicObject,
        RelicInstance relic)
    {
        Image icon = relicObject.GetComponent<Image>();
        if (icon != null)
        {
            icon.sprite = relic.Data.Icon;
            icon.preserveAspect = true;
            icon.enabled = relic.Data.Icon != null;
            icon.color = IsEventSelectionActive
                && eventSelectedRelics.Contains(relic)
                    ? new Color(1f, 0.72f, 0.2f, 1f)
                    : Color.white;
        }

        TMP_Text stackText = FindStackText(relicObject.transform);
        if (stackText != null)
        {
            string displayText = GetStackDisplayText(relic);
            bool showStack = !string.IsNullOrEmpty(displayText);
            stackText.text = displayText;
            stackText.gameObject.SetActive(showStack);
        }
    }

    private string GetStackDisplayText(RelicInstance relic)
    {
        if (relicManager != null)
        {
            return relicManager.GetRelicStatusText(relic);
        }

        return relic.Data.CanStack && relic.StackCount > 0
            ? relic.StackCount.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static TMP_Text FindStackText(Transform root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "Text | Stack")
            {
                return text;
            }
        }

        return null;
    }
}

[DisallowMultipleComponent]
public sealed class RelicInventoryIconUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IPointerClickHandler
{
    private RelicInventoryUI owner;
    private RelicInstance relic;

    public void Initialize(RelicInventoryUI value, RelicInstance instance)
    {
        owner = value;
        relic = instance;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowTooltip(relic, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideTooltip(relic);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        owner?.MoveTooltip(relic, eventData.position);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            owner?.ToggleEventSelection(relic);
        }
    }
}
