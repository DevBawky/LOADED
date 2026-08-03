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
    private static readonly Vector2 PointerOffset = new Vector2(18f, -18f);
    private static readonly Vector3[] WorldCorners = new Vector3[4];

    private static RectTransform tooltip;
    private static TextMeshProUGUI actionNameText;
    private static TextMeshProUGUI actionDescriptionText;
    private static Canvas rootCanvas;
    private static EnemyActionTooltipTrigger owner;

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
        actionNameText.text = actionData.DisplayName;
        actionDescriptionText.text = actionData.TooltipDescription;
        tooltip.gameObject.SetActive(true);
        tooltip.SetAsLastSibling();
        PositionInsideScreen(pointerPosition);
    }

    public static void Move(
        Vector2 pointerPosition,
        EnemyActionTooltipTrigger requestedOwner)
    {
        if (owner == requestedOwner && tooltip != null
            && tooltip.gameObject.activeSelf)
        {
            PositionInsideScreen(pointerPosition);
        }
    }

    public static void Hide(EnemyActionTooltipTrigger requestedOwner)
    {
        if (owner != requestedOwner)
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
            && actionDescriptionText != null && rootCanvas != null)
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

        foreach (TextMeshProUGUI text in texts)
        {
            if (text.name == ActionNameTextName)
            {
                actionNameText = text;
            }
            else if (text.name == ActionDescriptionTextName)
            {
                actionDescriptionText = text;
            }
        }

        foreach (Graphic graphic in tooltip.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }

        return actionNameText != null && actionDescriptionText != null;
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
        Vector2 targetPosition = pointerPosition + PointerOffset;

        if (canvasRect == null
            || !RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                targetPosition,
                eventCamera,
                out Vector3 worldPosition))
        {
            return;
        }

        tooltip.position = worldPosition;
        tooltip.GetWorldCorners(WorldCorners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(
            eventCamera,
            WorldCorners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(
            eventCamera,
            WorldCorners[2]);
        Vector2 correction = Vector2.zero;

        if (bottomLeft.x < 0f)
        {
            correction.x -= bottomLeft.x;
        }
        else if (topRight.x > Screen.width)
        {
            correction.x += Screen.width - topRight.x;
        }

        if (bottomLeft.y < 0f)
        {
            correction.y -= bottomLeft.y;
        }
        else if (topRight.y > Screen.height)
        {
            correction.y += Screen.height - topRight.y;
        }

        if (correction != Vector2.zero
            && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                targetPosition + correction,
                eventCamera,
                out worldPosition))
        {
            tooltip.position = worldPosition;
        }
    }

    private static void ClearCachedReferences()
    {
        tooltip = null;
        actionNameText = null;
        actionDescriptionText = null;
        rootCanvas = null;
        owner = null;
    }
}
