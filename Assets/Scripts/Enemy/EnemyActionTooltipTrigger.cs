using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class EnemyActionTooltipTrigger : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IPointerClickHandler
{
    public static event System.Action<EnemyActionData> ActionInspected;

    private EnemyActionData actionData;
    private string intentName;
    private string intentDescription;

    public void Configure(EnemyActionData configuredActionData)
    {
        actionData = configuredActionData;
        intentName = null;
    }

    internal void ConfigureIntent(EnemyTurnActionType action,
        EnemyAttackIconType attackType = EnemyAttackIconType.Melee)
    {
        string previousName = intentName;
        string previousDescription = intentDescription;
        actionData = null;
        (intentName, intentDescription) = action switch
        {
            EnemyTurnActionType.Move => ("이동", "화살표 방향으로 이동합니다. 경로가 막히면 이동하지 못할 수 있습니다."),
            EnemyTurnActionType.Rotate => ("회전", "예고한 방향으로 회전합니다. 플레이어가 이동해도 방향을 다시 고르지 않습니다."),
            EnemyTurnActionType.PrepareAttack => ("공격 준비", "이번 행동에는 조준만 합니다. 준비 후 아이콘 배경이 붉게 점멸하며, 다음 적 행동에 고정된 칸을 공격합니다."),
            EnemyTurnActionType.Fire => ("공격 준비 완료", "다음 적 행동에 붉은 칸을 공격합니다. 지금 이동·밀치기·사격으로 대응하거나 공격 경고음에 맞춰 회피할 수 있습니다."),
            EnemyTurnActionType.Support => ("지원", "아군에게 회복 또는 보호막을 제공합니다."),
            _ => ("대기", "이번 행동을 쉬어 갑니다.")
        };
        if (action == EnemyTurnActionType.PrepareAttack || action == EnemyTurnActionType.Fire)
        {
            (string name, string description) = attackType switch
            {
                EnemyAttackIconType.Ranged => ("원거리 사격", "같은 레인 전방의 첫 대상에게 총을 발사합니다."),
                EnemyAttackIconType.Throw => ("투척 공격", "조준한 칸에 투사체를 던집니다. 다른 레인도 공격할 수 있습니다."),
                EnemyAttackIconType.Shotgun => ("샷건 사격", "빅베럴이 있는 칸을 제외한 같은 레인 전체를 공격합니다. 다른 레인으로 피할 수 있습니다."),
                EnemyAttackIconType.Bomb => ("폭탄 투척", "예고한 칸에 폭탄을 설치합니다. 폭탄은 표시된 남은 행동 수가 0이 되면 폭발합니다."),
                _ => ("근접 공격", "같은 레인 전방의 가까운 대상을 공격합니다.")
            };
            intentName = name + (action == EnemyTurnActionType.Fire ? " · 준비 완료" : " · 준비");
            intentDescription = description + "\n\n" + intentDescription;
        }
        if (previousName != intentName || previousDescription != intentDescription)
            EnemyActionTooltipView.RefreshIntent(intentName, intentDescription, this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (actionData == null && intentName == null)
        {
            return;
        }

        Show(eventData.position);
        ActionInspected?.Invoke(actionData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (actionData == null && intentName == null)
        {
            return;
        }

        Show(eventData.position);
        ActionInspected?.Invoke(actionData);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        EnemyActionTooltipView.Move(eventData.position, this);
    }

    private void Show(Vector2 pointer)
    {
        if (intentName != null)
            EnemyActionTooltipView.ShowIntent(intentName, intentDescription, pointer, this);
        else
            EnemyActionTooltipView.Show(actionData, pointer, this);
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
    private static Canvas tooltipCanvas;
    private static object owner;

    internal static void ShowIntent(string name, string description, Vector2 pointer,
        EnemyActionTooltipTrigger requestedOwner)
    {
        if (!TryResolveReferences()) return;
        owner = requestedOwner;
        SetGuideOverlaySorting(false);
        actionNameText.text = name;
        actionDescriptionText.text = description;
        actionDamageRangeBackground.SetActive(false);
        tooltip.gameObject.SetActive(true);
        PositionInsideScreen(pointer);
    }

    internal static void RefreshIntent(string name, string description, EnemyActionTooltipTrigger requestedOwner)
    {
        if (!ReferenceEquals(owner, requestedOwner) || tooltip == null || !tooltip.gameObject.activeSelf) return;
        actionNameText.text = name;
        actionDescriptionText.text = description;
    }

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
        SetGuideOverlaySorting(false);
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
        SetGuideOverlaySorting(
            FirstRunGuideController.IsGuideElement(
                requestedOwner.transform));
        tooltip.SetAsLastSibling();
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
            // Keep the guide overlay context while hidden. Resetting it on
            // pointer exit can expose the tooltip below the guide during
            // rapid icon transitions. The next Show call configures sorting
            // for its actual owner before making the tooltip visible.
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
            SetGuideOverlaySorting(false);
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

    private static void SetGuideOverlaySorting(bool enabled)
    {
        if (tooltip == null || rootCanvas == null)
        {
            return;
        }

        tooltipCanvas ??= tooltip.GetComponent<Canvas>();
        if (enabled && tooltipCanvas == null)
        {
            tooltipCanvas = tooltip.gameObject.AddComponent<Canvas>();
        }

        if (tooltipCanvas == null)
        {
            return;
        }

        tooltipCanvas.overrideSorting = enabled;
        if (enabled)
        {
            tooltipCanvas.sortingLayerID = rootCanvas.sortingLayerID;
            tooltipCanvas.sortingOrder =
                FirstRunGuideController.GuideTooltipSortingOrder;
        }
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
        tooltipCanvas = null;
        owner = null;
    }
}
