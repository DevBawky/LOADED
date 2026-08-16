using System.Collections;
using System.Collections.Generic;
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

    private readonly List<GameObject> spawnedRelics = new List<GameObject>();
    private readonly Dictionary<RelicInstance, RectTransform> relicIcons =
        new Dictionary<RelicInstance, RectTransform>();
    private readonly Dictionary<RelicInstance, Coroutine> pulseAnimations =
        new Dictionary<RelicInstance, Coroutine>();
    private RelicTooltipUI tooltip;
    private RelicInstance hoveredRelic;

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
        if (relicManager != null)
        {
            relicManager.InventoryChanged -= Refresh;
            relicManager.RelicTriggered -= HandleRelicTriggered;
        }

        HideTooltip();
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

    private static void ConfigureRelic(
        GameObject relicObject,
        RelicInstance relic)
    {
        Image icon = relicObject.GetComponent<Image>();
        if (icon != null)
        {
            icon.sprite = relic.Data.Icon;
            icon.preserveAspect = true;
            icon.enabled = relic.Data.Icon != null;
        }

        TMP_Text stackText = FindStackText(relicObject.transform);
        if (stackText != null)
        {
            bool showStack = relic.StackCount > 1;
            stackText.text = relic.StackCount.ToString();
            stackText.gameObject.SetActive(showStack);
        }
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
    IPointerMoveHandler
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
}
