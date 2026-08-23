using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DebuffIconUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;

    private readonly List<EnemyHealthBarFeedback.DamagePreviewSegment>
        poisonPreviewSegments = new();
    private StatusEffectType effectType;
    private int currentStacks;
    private EnemyController enemyTarget;

    public void Initialize(
        Sprite sprite,
        int stacks,
        StatusEffectType configuredEffectType,
        EnemyController configuredEnemyTarget)
    {
        ResolveReferences();
        effectType = configuredEffectType;
        enemyTarget = configuredEnemyTarget;

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            iconImage.raycastTarget = true;
        }

        SetStacks(stacks);
    }

    private void ResolveReferences()
    {
        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }

        if (stackText == null)
        {
            stackText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    public void SetStacks(int stacks)
    {
        currentStacks = Mathf.Max(0, stacks);

        if (stackText != null)
        {
            stackText.text = currentStacks.ToString();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnemyActionTooltipView.ShowStatus(
            GetDisplayName(effectType),
            GetDescription(effectType, currentStacks),
            eventData.position,
            this);
        ShowDamagePreview();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        EnemyActionTooltipView.MoveStatus(eventData.position, this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltipAndPreview();
    }

    private void ShowDamagePreview()
    {
        if (effectType != StatusEffectType.Poison
            || currentStacks <= 0 || enemyTarget == null)
        {
            return;
        }

        poisonPreviewSegments.Clear();
        poisonPreviewSegments.Add(
            new EnemyHealthBarFeedback.DamagePreviewSegment(
                currentStacks,
                new Color(0.45f, 1f, 0.18f, 1f),
                true));
        enemyTarget.ShowDamagePreview(poisonPreviewSegments);
    }

    private void HideTooltipAndPreview()
    {
        EnemyActionTooltipView.HideStatus(this);

        if (effectType == StatusEffectType.Poison && enemyTarget != null)
        {
            enemyTarget.ClearDamagePreview();
        }
    }

    private static string GetDisplayName(StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.Mark => Highlight(
                "표식",
                TooltipTextFormatter.MarkColor),
            StatusEffectType.Poison => Highlight(
                "독",
                TooltipTextFormatter.PoisonColor),
            StatusEffectType.Stun => Highlight(
                "기절",
                TooltipTextFormatter.StunColor),
            StatusEffectType.Weakness => Highlight(
                "약화",
                TooltipTextFormatter.WeaknessColor),
            _ => type.ToString()
        };
    }

    private static string GetDescription(StatusEffectType type, int stacks)
    {
        int safeStacks = Mathf.Max(0, stacks);
        string stackText = Highlight(
            $"{safeStacks}스택",
            TooltipTextFormatter.StackColor);
        string countText = Highlight(
            $"{safeStacks} COUNT",
            TooltipTextFormatter.RangeColor);

        return type switch
        {
            StatusEffectType.Mark =>
                $"{Highlight("받는 피해", TooltipTextFormatter.DamageColor)}가 "
                + $"{Highlight("50% 증가", TooltipTextFormatter.MarkColor)}합니다.\n\n"
                + $"현재 {stackText}: {countText} 동안 "
                + $"{Highlight("50%의 추가 대미지", TooltipTextFormatter.DamageColor)}를 "
                + "받습니다.",
            StatusEffectType.Poison =>
                $"COUNT 종료 시 현재 스택만큼 {Highlight("피해", TooltipTextFormatter.DamageColor)}를 "
                + $"받고 {Highlight("1스택 감소", TooltipTextFormatter.StackColor)}합니다.\n\n"
                + $"현재 {stackText}: {countText} 동안 독의 대미지를 받습니다. "
                + Highlight(
                    $"이번 COUNT 피해량: {safeStacks}",
                    TooltipTextFormatter.DamageColor),
            StatusEffectType.Stun =>
                $"{Highlight("행동 불가", TooltipTextFormatter.StunColor)} 상태이며 "
                + $"COUNT마다 {Highlight("1스택 감소", TooltipTextFormatter.StackColor)}합니다.\n\n"
                + $"현재 {stackText}: {countText} 동안 "
                + $"{Highlight("행동 불가", TooltipTextFormatter.StunColor)}.",
            StatusEffectType.Weakness =>
                $"{Highlight("공격력", TooltipTextFormatter.WeaknessColor)}이 "
                + $"{Highlight("30% 감소", TooltipTextFormatter.DebuffColor)}합니다.\n\n"
                + $"현재 {stackText}: {countText} 동안 "
                + $"{Highlight("공격력", TooltipTextFormatter.WeaknessColor)}이 "
                + $"{Highlight("30% 감소", TooltipTextFormatter.DebuffColor)}합니다.",
            _ => string.Empty
        };
    }

    private static string Highlight(string text, string color)
    {
        return $"<color={color}>{text}</color>";
    }

    private void OnDisable()
    {
        HideTooltipAndPreview();
    }
}
