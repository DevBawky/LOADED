using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class EnemyActionTooltipTrigger : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler
{
    private EnemyActionData actionData;

    public void Configure(EnemyActionData configuredActionData)
    {
        actionData = configuredActionData;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnemyActionTooltipView.Show(actionData, eventData.position, this);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        EnemyActionTooltipView.Move(eventData.position, this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        EnemyActionTooltipView.Hide(this);
    }

    private void OnDisable()
    {
        EnemyActionTooltipView.Hide(this);
    }
}

internal static class EnemyActionTooltipView
{
    private const string TooltipName = "Panel | Action Tooltip";
    private const string ActionNameTextName = "Text | Action Name";
    private const string ActionDescriptionTextName =
        "Text | Action Description";
    private const string ActionDamageRangeBackgroundName =
        "BG | Action Damage Range";
    private const float PointerGap = 12f;
    private const float ScreenPadding = 8f;
    private static readonly Vector3[] WorldCorners = new Vector3[4];

    private static RectTransform tooltip;
    private static TextMeshProUGUI actionNameText;
    private static TextMeshProUGUI actionDescriptionText;
    private static GameObject actionDamageRangeBackground;
    private static TextMeshProUGUI actionDamageRangeText;
    private static Canvas rootCanvas;
    private static object owner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void HideAfterSceneLoad()
    {
        ClearCachedReferences();

        if (TryResolveReferences())
        {
            tooltip.gameObject.SetActive(false);
        }
    }

    public static void Show(
        EnemyActionData actionData,
        Vector2 pointerPosition,
        EnemyActionTooltipTrigger requestedOwner)
    {
        if (actionData == null || !TryResolveReferences())
        {
            return;
        }

        owner = requestedOwner;
        EnemyAttackData attackData = actionData.AttackData;
        actionNameText.text = actionData.DisplayName;
        actionDescriptionText.text = actionData.TooltipDescription;
        actionDamageRangeBackground.SetActive(attackData != null);

        if (attackData != null)
        {
            actionDamageRangeText.richText = true;
            actionDamageRangeText.text =
                $"대미지: <color=red> {attackData.Damage} </color> "
                + $"사거리: <color=yellow>{attackData.Range}</color>";
        }

        tooltip.gameObject.SetActive(true);
        PositionInsideScreen(pointerPosition);
    }

    public static void Move(
        Vector2 pointerPosition,
        EnemyActionTooltipTrigger requestedOwner)
    {
        if (ReferenceEquals(owner, requestedOwner) && tooltip != null
            && tooltip.gameObject.activeSelf)
        {
            PositionInsideScreen(pointerPosition);
        }
    }

    public static void ShowStatus(
        string displayName,
        string description,
        Vector2 pointerPosition,
        DebuffIconUI requestedOwner)
    {
        if (requestedOwner == null || !TryResolveReferences())
        {
            return;
        }

        owner = requestedOwner;
        actionNameText.richText = true;
        actionDescriptionText.richText = true;
        actionNameText.text = displayName;
        actionDescriptionText.text = description;
        actionDamageRangeBackground.SetActive(false);
        tooltip.gameObject.SetActive(true);
        PositionInsideScreen(pointerPosition);
    }

    public static void MoveStatus(
        Vector2 pointerPosition,
        DebuffIconUI requestedOwner)
    {
        if (ReferenceEquals(owner, requestedOwner) && tooltip != null
            && tooltip.gameObject.activeSelf)
        {
            PositionInsideScreen(pointerPosition);
        }
    }

    public static void HideStatus(DebuffIconUI requestedOwner)
    {
        if (!ReferenceEquals(owner, requestedOwner))
        {
            return;
        }

        owner = null;

        if (tooltip != null)
        {
            tooltip.gameObject.SetActive(false);
        }
    }

    public static void Hide(EnemyActionTooltipTrigger requestedOwner)
    {
        if (!ReferenceEquals(owner, requestedOwner))
        {
            return;
        }

        owner = null;

        if (tooltip != null)
        {
            tooltip.gameObject.SetActive(false);
        }
    }

    private static bool TryResolveReferences()
    {
        if (tooltip != null && actionNameText != null
            && actionDescriptionText != null
            && actionDamageRangeBackground != null
            && actionDamageRangeText != null
            && rootCanvas != null)
        {
            return true;
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || !canvas.isRootCanvas)
            {
                continue;
            }

            RectTransform[] rectTransforms =
                canvas.GetComponentsInChildren<RectTransform>(true);

            foreach (RectTransform candidate in rectTransforms)
            {
                if (candidate != null && candidate.name == TooltipName)
                {
                    tooltip = candidate;
                    rootCanvas = canvas;
                    break;
                }
            }

            if (tooltip != null)
            {
                break;
            }
        }

        if (tooltip == null || rootCanvas == null)
        {
            return false;
        }

        TextMeshProUGUI[] texts =
            tooltip.GetComponentsInChildren<TextMeshProUGUI>(true);

        RectTransform[] tooltipRects =
            tooltip.GetComponentsInChildren<RectTransform>(true);

        foreach (RectTransform candidate in tooltipRects)
        {
            if (candidate.name == ActionDamageRangeBackgroundName)
            {
                actionDamageRangeBackground = candidate.gameObject;
                break;
            }
        }

        foreach (TextMeshProUGUI text in texts)
        {
            if (text.name == ActionNameTextName)
            {
                actionNameText = text;
            }
            else if (text.name == ActionDescriptionTextName)
            {
                if (actionDamageRangeBackground != null
                    && text.transform.IsChildOf(
                        actionDamageRangeBackground.transform))
                {
                    actionDamageRangeText = text;
                }
                else
                {
                    actionDescriptionText = text;
                }
            }
        }

        foreach (Graphic graphic in tooltip.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }

        return actionNameText != null
            && actionDescriptionText != null
            && actionDamageRangeBackground != null
            && actionDamageRangeText != null;
    }

    private static void PositionInsideScreen(Vector2 pointerPosition)
    {
        if (tooltip == null || rootCanvas == null)
        {
            return;
        }

        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera;
        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        tooltip.GetWorldCorners(WorldCorners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(
            eventCamera,
            WorldCorners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(
            eventCamera,
            WorldCorners[2]);
        Vector2 tooltipSize = topRight - bottomLeft;
        Rect screenRect = rootCanvas.pixelRect;
        float minimumX = screenRect.xMin + ScreenPadding;
        float minimumY = screenRect.yMin + ScreenPadding;
        float maximumX = Mathf.Max(
            minimumX,
            screenRect.xMax - ScreenPadding - tooltipSize.x);
        float maximumY = Mathf.Max(
            minimumY,
            screenRect.yMax - ScreenPadding - tooltipSize.y);
        float availableRight = screenRect.xMax - ScreenPadding
            - pointerPosition.x - PointerGap;
        float availableLeft = pointerPosition.x - PointerGap
            - screenRect.xMin - ScreenPadding;
        bool placeOnRight = tooltipSize.x <= availableRight
            || tooltipSize.x > availableLeft && availableRight >= availableLeft;
        float preferredX = placeOnRight
            ? pointerPosition.x + PointerGap
            : pointerPosition.x - PointerGap - tooltipSize.x;
        Vector2 desiredBottomLeft = new Vector2(
            Mathf.Clamp(preferredX, minimumX, maximumX),
            Mathf.Clamp(
                pointerPosition.y + PointerGap,
                minimumY,
                maximumY));
        Vector2 targetPivotPosition = desiredBottomLeft + new Vector2(
            tooltipSize.x * tooltip.pivot.x,
            tooltipSize.y * tooltip.pivot.y);

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                targetPivotPosition,
                eventCamera,
                out Vector3 worldPosition))
        {
            tooltip.position = worldPosition;
        }
    }

    private static void ClearCachedReferences()
    {
        tooltip = null;
        actionNameText = null;
        actionDescriptionText = null;
        actionDamageRangeBackground = null;
        actionDamageRangeText = null;
        rootCanvas = null;
        owner = null;
    }
}
