using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyActionQueueUI : MonoBehaviour
{
    private const string ReadyImageName = "Image | Queue Ready";

    [Header("References")]
    [SerializeField] private Image queueImage;
    [SerializeField] private RectTransform iconParent;
    [SerializeField] private Image attackIconPrefab;
    [SerializeField] private Image queueReadyImage;

    [Header("Queue State Sprites")]
    [SerializeField] private Sprite normalQueueSprite;
    [SerializeField] private Sprite preparedQueueSprite;

    [Header("Ready Emphasis")]
    [SerializeField] private Material queueReadyMaterial;

    [Header("Stunned Emphasis")]
    [SerializeField, ColorUsage(true, true)] private Color stunnedEmberColor =
        new Color(0.015f, 0.08f, 0.55f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color stunnedFlameColor =
        new Color(0.04f, 0.55f, 1.2f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color stunnedHotColor =
        new Color(0.55f, 0.95f, 1.4f, 1f);
    [SerializeField, Range(0f, 8f)] private float stunnedFlameSpeed = 0.45f;

    [Header("Fallback")]
    [SerializeField] private Color missingIconColor = Color.red;

    [Header("Lane Layout")]
    [SerializeField, Min(0f)] private float laneVerticalSeparation = 2f;
    [SerializeField, Min(0f)] private float laneLayoutTransitionDuration = 0.18f;

    private readonly List<Image> spawnedIcons = new List<Image>();
    private Material stunnedQueueMaterial;
    private bool isPrepared;
    private bool isStunned;
    private int displayRevision;
    private bool hasCapturedQueuePosition;
    private Vector2 baseQueuePosition;
    private Vector3 baseQueueLocalPosition;
    private RectTransform laneHealthPanel;
    private RectTransform laneStatusPanel;
    private Vector3 baseHealthPosition;
    private Vector3 baseStatusPosition;
    private Coroutine laneLayoutTransitionCoroutine;
    private int appliedLaneIndex;
    private Outline hoverOutline;
    private bool isHovered;
    private bool originalOutlineEnabled;
    private Color originalOutlineColor;
    private Vector2 originalOutlineDistance;

    public int IconCount => spawnedIcons.Count;
    public Sprite NormalQueueSprite => normalQueueSprite;
    public Sprite PreparedQueueSprite => preparedQueueSprite;
    public Material QueueReadyMaterial => queueReadyMaterial;

    private void Awake()
    {
        CaptureBaseQueuePosition();
        EnsureReadyImage();
        ResetDisplay();
    }

    private void OnDestroy()
    {
        if (stunnedQueueMaterial != null)
        {
            Destroy(stunnedQueueMaterial);
        }
    }

    internal void SetHovered(bool hovered)
    {
        if (hovered == isHovered)
        {
            return;
        }
        isHovered = hovered;
        if (queueImage == null)
        {
            return;
        }
        if (hovered)
        {
            hoverOutline = queueImage.GetComponent<Outline>();
            if (hoverOutline == null)
            {
                hoverOutline = queueImage.gameObject.AddComponent<Outline>();
                hoverOutline.enabled = false;
            }
            originalOutlineEnabled = hoverOutline.enabled;
            originalOutlineColor = hoverOutline.effectColor;
            originalOutlineDistance = hoverOutline.effectDistance;
            hoverOutline.effectColor = new Color(1f, 0.94f, 0.72f, 1f);
            Vector2 size = queueImage.rectTransform.rect.size;
            float thickness = Mathf.Max(0.001f, Mathf.Min(size.x, size.y) * 0.05f);
            hoverOutline.effectDistance = new Vector2(thickness, -thickness);
            hoverOutline.enabled = true;
        }
        else if (hoverOutline != null)
        {
            hoverOutline.effectColor = originalOutlineColor;
            hoverOutline.effectDistance = originalOutlineDistance;
            hoverOutline.enabled = originalOutlineEnabled;
        }
    }

    private void OnDisable()
    {
        SetHovered(false);

        if (laneLayoutTransitionCoroutine != null)
        {
            StopCoroutine(laneLayoutTransitionCoroutine);
            laneLayoutTransitionCoroutine = null;
            ApplyLaneLayout(appliedLaneIndex, false);
        }
    }

    public void ShowQueue()
    {
        if (queueImage == null)
        {
            return;
        }

        displayRevision++;
        ApplyQueueSprite(normalQueueSprite);
        queueImage.gameObject.SetActive(true);
        RefreshEmphasis();
        RefreshQueueWidth();
    }

    public void ApplyLaneLayout(int laneIndex)
    {
        ApplyLaneLayout(laneIndex, false);
    }

    public void ApplyLaneLayout(int laneIndex, bool animate)
    {
        if (queueImage == null)
        {
            return;
        }

        appliedLaneIndex = laneIndex;
        CaptureBaseQueuePosition();
        RectTransform queueRect = queueImage.rectTransform;
        if (laneHealthPanel == null && queueRect.parent != null)
        {
            laneHealthPanel = queueRect.parent.Find(
                "Panel | HP_BG") as RectTransform;
            laneStatusPanel = queueRect.parent.Find(
                "Image | Status") as RectTransform;
            if (laneHealthPanel != null)
            {
                baseHealthPosition = laneHealthPanel.localPosition;
            }
            if (laneStatusPanel != null)
            {
                baseStatusPosition = laneStatusPanel.localPosition;
            }
        }
        if (laneHealthPanel != null)
        {
            const float gap = 0.6f;
            float healthHalf = laneHealthPanel.rect.height * 0.5f;
            float queueHalf = queueRect.rect.height * 0.5f;
            bool lowerLane = laneIndex <= 0;
            float healthY = lowerLane
                ? baseQueueLocalPosition.y
                : baseHealthPosition.y;
            Vector3 targetHealthPosition = new Vector3(
                baseHealthPosition.x,
                healthY,
                baseHealthPosition.z);
            Vector3 targetQueuePosition = new Vector3(
                baseQueueLocalPosition.x,
                healthY + (lowerLane ? -1f : 1f)
                    * (healthHalf + gap + queueHalf),
                baseQueueLocalPosition.z);
            if (laneStatusPanel != null)
            {
                laneStatusPanel.localPosition = baseStatusPosition;
            }
            ApplyLaneLayoutPositions(
                queueRect,
                targetQueuePosition,
                targetHealthPosition,
                animate);
            return;
        }

        Vector2 targetAnchoredPosition = baseQueuePosition
            + Vector2.up * Mathf.Max(0, laneIndex)
                * laneVerticalSeparation;
        ApplyFallbackLaneLayout(
            queueImage.rectTransform,
            targetAnchoredPosition,
            animate);
    }

    private void ApplyLaneLayoutPositions(
        RectTransform queueRect,
        Vector3 targetQueuePosition,
        Vector3 targetHealthPosition,
        bool animate)
    {
        StopLaneLayoutTransition();

        if (!animate || !isActiveAndEnabled
            || laneLayoutTransitionDuration <= 0f)
        {
            queueRect.localPosition = targetQueuePosition;
            laneHealthPanel.localPosition = targetHealthPosition;
            SyncReadyImageRect();
            return;
        }

        laneLayoutTransitionCoroutine = StartCoroutine(
            AnimateLaneLayout(
                queueRect,
                targetQueuePosition,
                laneHealthPanel,
                targetHealthPosition));
    }

    private void ApplyFallbackLaneLayout(
        RectTransform queueRect,
        Vector2 targetPosition,
        bool animate)
    {
        StopLaneLayoutTransition();

        if (!animate || !isActiveAndEnabled
            || laneLayoutTransitionDuration <= 0f)
        {
            queueRect.anchoredPosition = targetPosition;
            SyncReadyImageRect();
            return;
        }

        laneLayoutTransitionCoroutine = StartCoroutine(
            AnimateFallbackLaneLayout(queueRect, targetPosition));
    }

    private IEnumerator AnimateLaneLayout(
        RectTransform queueRect,
        Vector3 targetQueuePosition,
        RectTransform healthRect,
        Vector3 targetHealthPosition)
    {
        Vector3 startQueuePosition = queueRect.localPosition;
        Vector3 startHealthPosition = healthRect.localPosition;
        float elapsedTime = 0f;

        while (elapsedTime < laneLayoutTransitionDuration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(
                elapsedTime / laneLayoutTransitionDuration);
            queueRect.localPosition = Vector3.Lerp(
                startQueuePosition,
                targetQueuePosition,
                progress);
            healthRect.localPosition = Vector3.Lerp(
                startHealthPosition,
                targetHealthPosition,
                progress);
            SyncReadyImageRect();
        }

        queueRect.localPosition = targetQueuePosition;
        healthRect.localPosition = targetHealthPosition;
        SyncReadyImageRect();
        laneLayoutTransitionCoroutine = null;
    }

    private IEnumerator AnimateFallbackLaneLayout(
        RectTransform queueRect,
        Vector2 targetPosition)
    {
        Vector2 startPosition = queueRect.anchoredPosition;
        float elapsedTime = 0f;

        while (elapsedTime < laneLayoutTransitionDuration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(
                elapsedTime / laneLayoutTransitionDuration);
            queueRect.anchoredPosition = Vector2.Lerp(
                startPosition,
                targetPosition,
                progress);
            SyncReadyImageRect();
        }

        queueRect.anchoredPosition = targetPosition;
        SyncReadyImageRect();
        laneLayoutTransitionCoroutine = null;
    }

    private void StopLaneLayoutTransition()
    {
        if (laneLayoutTransitionCoroutine == null)
        {
            return;
        }

        StopCoroutine(laneLayoutTransitionCoroutine);
        laneLayoutTransitionCoroutine = null;
    }

    public bool AddAttackIcon(EnemyActionData actionData)
    {
        return AddAttackIcon(actionData, out _);
    }

    public bool AddAttackIcon(
        EnemyActionData actionData,
        out Image attackIcon)
    {
        attackIcon = null;

        if (queueImage == null || iconParent == null
            || attackIconPrefab == null || actionData == null)
        {
            return false;
        }

        ShowQueue();
        attackIcon = Instantiate(attackIconPrefab, iconParent);
        attackIcon.sprite = actionData.Icon;
        attackIcon.color = actionData.Icon == null
            ? missingIconColor
            : Color.white;
        attackIcon.preserveAspect = true;
        EnemyActionTooltipTrigger tooltipTrigger =
            attackIcon.GetComponent<EnemyActionTooltipTrigger>();
        if (tooltipTrigger == null)
        {
            tooltipTrigger = attackIcon.gameObject.AddComponent<
                EnemyActionTooltipTrigger>();
        }

        tooltipTrigger.Configure(actionData);
        spawnedIcons.Add(attackIcon);
        RefreshQueueWidth();
        return true;
    }

    public IEnumerator RevealQueue(float duration)
    {
        ShowQueue();

        if (queueImage != null)
        {
            yield return RevealGraphic(queueImage.gameObject, duration);
        }
    }

    public IEnumerator RevealIcon(Image icon, float duration)
    {
        if (icon != null)
        {
            yield return RevealGraphic(
                icon.gameObject,
                duration,
                true);
        }
    }

    public IEnumerator RevealQueueAndIcon(Image icon, float duration)
    {
        if (icon != null)
        {
            CanvasGroup iconCanvasGroup = icon.GetComponent<CanvasGroup>();
            if (iconCanvasGroup == null)
            {
                iconCanvasGroup = icon.gameObject.AddComponent<CanvasGroup>();
            }

            iconCanvasGroup.alpha = 0f;
        }

        yield return RevealQueue(duration);
        yield return RevealIcon(icon, duration);
    }

    public void SetPrepared(bool prepared)
    {
        isPrepared = prepared;
        ApplyQueueSprite(prepared
            ? preparedQueueSprite
            : normalQueueSprite);
        RefreshEmphasis();
        RefreshQueueWidth();
    }

    public void SetStunned(bool stunned)
    {
        isStunned = stunned;

        // Stun can be applied while an action icon is still fading in.
        // Complete that reveal immediately so a stopped/interrupted reveal
        // can never leave only the blue queue frame visible.
        if (isStunned)
        {
            EnsureSpawnedIconsVisible();
        }

        RefreshEmphasis();
    }

    public void RemoveFirstIcon()
    {
        if (spawnedIcons.Count == 0)
        {
            return;
        }

        Image icon = spawnedIcons[0];
        spawnedIcons.RemoveAt(0);

        if (icon != null)
        {
            icon.gameObject.SetActive(false);
            Destroy(icon.gameObject);
        }

        RefreshQueueWidth();
    }

    public void HideQueue()
    {
        displayRevision++;

        if (queueImage != null)
        {
            queueImage.gameObject.SetActive(false);
        }

        RefreshEmphasis();
    }

    public void ResetDisplay()
    {
        displayRevision++;
        foreach (Image icon in spawnedIcons)
        {
            if (icon != null)
            {
                icon.gameObject.SetActive(false);
                Destroy(icon.gameObject);
            }
        }

        spawnedIcons.Clear();
        isPrepared = false;

        if (queueImage != null)
        {
            ApplyQueueSprite(normalQueueSprite);
            queueImage.gameObject.SetActive(false);
        }

        RefreshEmphasis();
        RefreshQueueWidth();
    }

    public IEnumerator PlayPhaseTransition(float duration, Color accentColor)
    {
        if (queueImage == null)
        {
            yield break;
        }

        bool wasQueueVisible = queueImage.gameObject.activeSelf;
        ShowQueue();
        int phaseTransitionRevision = displayRevision;
        Color originalColor = queueImage.color;
        queueImage.color = accentColor;
        yield return RevealGraphic(
            queueImage.gameObject,
            Mathf.Max(0.1f, duration));
        queueImage.color = originalColor;

        // A phase change can overlap the turn that creates the next attack queue.
        // Do not let the transition coroutine erase that newly-created queue.
        if (!wasQueueVisible && displayRevision == phaseTransitionRevision)
        {
            ResetDisplay();
            yield break;
        }

        ApplyQueueSprite(isPrepared ? preparedQueueSprite : normalQueueSprite);
        RefreshEmphasis();
        RefreshQueueWidth();
    }

    private void ApplyQueueSprite(Sprite stateSprite)
    {
        if (queueImage == null)
        {
            return;
        }

        if (stateSprite != null)
        {
            queueImage.sprite = stateSprite;
        }

        queueImage.color = Color.white;
    }

    private void CaptureBaseQueuePosition()
    {
        if (hasCapturedQueuePosition || queueImage == null)
        {
            return;
        }

        baseQueuePosition = queueImage.rectTransform.anchoredPosition;
        baseQueueLocalPosition = queueImage.rectTransform.localPosition;
        hasCapturedQueuePosition = true;
    }

    private void EnsureReadyImage()
    {
        if (queueReadyImage == null)
        {
            Transform readyTransform = transform.Find(ReadyImageName);
            if (readyTransform != null)
            {
                queueReadyImage = readyTransform.GetComponent<Image>();
            }
        }

        if (queueReadyImage == null && queueImage != null)
        {
            GameObject readyObject = new GameObject(
                ReadyImageName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            readyObject.layer = queueImage.gameObject.layer;

            RectTransform readyRect =
                readyObject.GetComponent<RectTransform>();
            RectTransform queueRect = queueImage.rectTransform;
            readyRect.SetParent(queueRect.parent, false);
            readyRect.anchorMin = queueRect.anchorMin;
            readyRect.anchorMax = queueRect.anchorMax;
            readyRect.anchoredPosition = queueRect.anchoredPosition;
            readyRect.sizeDelta = queueRect.sizeDelta;
            readyRect.pivot = queueRect.pivot;
            readyRect.SetSiblingIndex(queueRect.GetSiblingIndex() + 1);

            queueReadyImage = readyObject.GetComponent<Image>();
        }

        if (queueReadyImage == null)
        {
            return;
        }

        queueReadyImage.sprite = null;
        queueReadyImage.color = Color.white;
        queueReadyImage.raycastTarget = false;
        queueReadyImage.material = queueReadyMaterial;
        PlaceReadyImageBehindIcons();
        SyncReadyImageRect();
    }

    private void PlaceReadyImageBehindIcons()
    {
        if (queueImage == null || queueReadyImage == null)
        {
            return;
        }

        RectTransform readyRect = queueReadyImage.rectTransform;
        RectTransform queueRect = queueImage.rectTransform;

        if (readyRect.parent != queueRect)
        {
            readyRect.SetParent(queueRect, false);
        }

        LayoutElement layoutElement =
            queueReadyImage.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = queueReadyImage.gameObject.AddComponent<
                LayoutElement>();
        }

        layoutElement.ignoreLayout = true;
        readyRect.SetAsFirstSibling();
    }

    private void RefreshEmphasis()
    {
        EnsureReadyImage();

        if (queueReadyImage == null)
        {
            return;
        }

        Material emphasisMaterial = isStunned
            ? GetOrCreateStunnedMaterial()
            : isPrepared ? queueReadyMaterial : null;
        queueReadyImage.material = emphasisMaterial;
        queueReadyImage.gameObject.SetActive(
            emphasisMaterial != null
            && queueImage != null
            && queueImage.gameObject.activeSelf);
    }

    private Material GetOrCreateStunnedMaterial()
    {
        if (stunnedQueueMaterial != null || queueReadyMaterial == null)
        {
            return stunnedQueueMaterial;
        }

        stunnedQueueMaterial = new Material(queueReadyMaterial)
        {
            name = $"{queueReadyMaterial.name} (Stunned)"
        };
        stunnedQueueMaterial.SetColor("_EmberColor", stunnedEmberColor);
        stunnedQueueMaterial.SetColor("_FlameColor", stunnedFlameColor);
        stunnedQueueMaterial.SetColor("_HotColor", stunnedHotColor);
        stunnedQueueMaterial.SetFloat("_Speed", stunnedFlameSpeed);
        stunnedQueueMaterial.SetFloat("_PulseAmount", 0.1f);
        return stunnedQueueMaterial;
    }

    private void RefreshQueueWidth()
    {
        if (queueImage == null || iconParent == null)
        {
            return;
        }

        HorizontalLayoutGroup layoutGroup =
            iconParent.GetComponent<HorizontalLayoutGroup>();
        float spacing = layoutGroup != null
            ? layoutGroup.spacing
            : 0f;
        float width = layoutGroup != null
            ? layoutGroup.padding.left + layoutGroup.padding.right
            : 0f;
        int activeChildCount = 0;

        for (int i = 0; i < iconParent.childCount; i++)
        {
            RectTransform child =
                iconParent.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
            {
                continue;
            }

            if (queueReadyImage != null
                && child == queueReadyImage.rectTransform)
            {
                continue;
            }

            if (activeChildCount > 0)
            {
                width += spacing;
            }

            width += child.rect.width;
            activeChildCount++;
        }

        if (activeChildCount == 0)
        {
            width += GetEmptyQueueWidth();
        }

        queueImage.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width);
        SyncReadyImageRect();
    }

    private float GetEmptyQueueWidth()
    {
        if (attackIconPrefab == null)
        {
            return queueImage.rectTransform.rect.height;
        }

        float prefabWidth = attackIconPrefab.rectTransform.rect.width;
        return prefabWidth > 0f
            ? prefabWidth
            : queueImage.rectTransform.rect.height;
    }

    private void SyncReadyImageRect()
    {
        if (queueImage == null || queueReadyImage == null)
        {
            return;
        }

        RectTransform queueRect = queueImage.rectTransform;
        RectTransform readyRect = queueReadyImage.rectTransform;

        if (readyRect.parent == queueRect)
        {
            readyRect.anchorMin = Vector2.zero;
            readyRect.anchorMax = Vector2.one;
            readyRect.anchoredPosition = Vector2.zero;
            readyRect.sizeDelta = Vector2.zero;
            readyRect.pivot = queueRect.pivot;
            return;
        }

        readyRect.anchorMin = queueRect.anchorMin;
        readyRect.anchorMax = queueRect.anchorMax;
        readyRect.anchoredPosition = queueRect.anchoredPosition;
        readyRect.sizeDelta = queueRect.sizeDelta;
        readyRect.pivot = queueRect.pivot;
    }

    private IEnumerator RevealGraphic(
        GameObject target,
        float duration,
        bool finishWhenStunned = false)
    {
        if (target == null)
        {
            yield break;
        }

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = target.AddComponent<CanvasGroup>();
        }

        Transform targetTransform = target.transform;
        Vector3 finalScale = targetTransform.localScale;

        if (duration <= 0f || finishWhenStunned && isStunned)
        {
            canvasGroup.alpha = 1f;
            targetTransform.localScale = finalScale;
            yield break;
        }

        canvasGroup.alpha = 0f;
        targetTransform.localScale = finalScale * 0.75f;
        float elapsedTime = 0f;

        while (elapsedTime < duration && target != null)
        {
            yield return null;

            if (finishWhenStunned && isStunned)
            {
                break;
            }

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            canvasGroup.alpha = easedProgress;
            targetTransform.localScale = Vector3.LerpUnclamped(
                finalScale * 0.75f,
                finalScale,
                easedProgress);
        }

        if (target != null)
        {
            canvasGroup.alpha = 1f;
            targetTransform.localScale = finalScale;
        }
    }

    private void EnsureSpawnedIconsVisible()
    {
        foreach (Image icon in spawnedIcons)
        {
            if (icon == null)
            {
                continue;
            }

            icon.enabled = true;
            CanvasGroup canvasGroup = icon.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            Color iconColor = icon.color;
            iconColor.a = 1f;
            icon.color = iconColor;
        }
    }
}
