using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EventRunStatistic
{
    EliteClears,
    ShopVisits,
    EventClears,
    Money,
    OwnedBullets,
    CurrentHealthPercent,
    CumulativeBattleTurns
}

public enum EventComparison
{
    LessThan,
    LessThanOrEqual,
    Equal,
    GreaterThanOrEqual,
    GreaterThan
}

public enum EventWeightOperation
{
    Add,
    Multiply
}

[Serializable]
public sealed class EventWeightRule
{
    [Tooltip("확인할 현재 런 기록입니다.")]
    public EventRunStatistic statistic;
    public EventComparison comparison = EventComparison.GreaterThanOrEqual;
    [Tooltip("체력 비율은 0~100 사이의 값으로 입력합니다.")]
    public float threshold;
    [Tooltip("조건을 만족했을 때 기본 가중치에 더하거나 곱합니다.")]
    public EventWeightOperation operation = EventWeightOperation.Add;
    [Tooltip("음수 Add 또는 0~1 Multiply를 사용하면 등장 확률을 낮출 수 있습니다.")]
    public float value = 1f;

    public bool Matches(EventRunContext context)
    {
        float current = context.GetValue(statistic);
        return comparison switch
        {
            EventComparison.LessThan => current < threshold,
            EventComparison.LessThanOrEqual => current <= threshold,
            EventComparison.Equal => Mathf.Approximately(current, threshold),
            EventComparison.GreaterThanOrEqual => current >= threshold,
            EventComparison.GreaterThan => current > threshold,
            _ => false
        };
    }
}

public enum EventChoiceRequirementType
{
    None,
    MoneyAtLeast,
    RemovableBulletExists,
    UpgradableBulletExists,
    BulletSpaceExists,
    ItemSpaceExists
}

[Serializable]
public sealed class EventChoiceRequirement
{
    public EventChoiceRequirementType type;
    [Min(0)] public int amount;
    [TextArea] public string unavailableReason;
}

public enum EventEffectType
{
    GainMoney,
    LoseMoney,
    Heal,
    LoseHealth,
    AddBullet,
    RemoveChosenBullet,
    UpgradeChosenBullet,
    AddItem
}

[Serializable]
public sealed class EventEffect
{
    public EventEffectType type;
    [Tooltip("돈/체력 변화량입니다. 탄환 효과에서는 사용하지 않습니다.")]
    [Min(0)] public int amount;
    [Tooltip("같은 선택지를 이전에 선택한 횟수마다 Amount에 더할 값입니다.")]
    [Min(0)] public int amountPerPreviousSelection;
    [Tooltip("활성화하면 이전 선택 횟수에 따라 이 효과의 적용 구간을 제한합니다.")]
    public bool useSelectionRange;
    [Tooltip("효과가 적용되는 최소 이전 선택 횟수입니다. 첫 선택은 0입니다.")]
    [Min(0)] public int minimumPreviousSelections;
    [Tooltip("효과가 적용되는 최대 이전 선택 횟수입니다. -1은 제한 없음입니다.")]
    [Min(-1)] public int maximumPreviousSelections = -1;
    [Tooltip("Add Bullet 효과에서 획득할 탄환입니다.")]
    public BulletData bullet;
    [Tooltip("Add Item 효과에서 획득할 아이템입니다.")]
    public ItemData item;
}

[Serializable]
public sealed class EventChoiceData
{
    [TextArea] public string buttonText;
    [TextArea(2, 6)] public string outcomeText;
    public EventChoiceRequirement[] requirements =
        Array.Empty<EventChoiceRequirement>();
    [Tooltip("성공 여부를 판정하기 전에 항상 적용되는 비용/효과입니다.")]
    public EventEffect[] attemptEffects = Array.Empty<EventEffect>();
    [Tooltip("성공했을 때 적용되는 효과입니다. 기존 이벤트 효과도 이 배열을 사용합니다.")]
    public EventEffect[] effects = Array.Empty<EventEffect>();

    [Header("Repeat / Chance")]
    [Tooltip("활성화하면 선택할 때 성공 확률을 판정합니다.")]
    public bool useSuccessChance;
    [Range(0f, 100f)] public float baseSuccessChancePercent = 50f;
    [Tooltip("실패할 때마다 다음 시도의 성공 확률에 더해지는 값입니다.")]
    [Min(0f)] public float successChanceIncreaseOnFailurePercent = 10f;
    [TextArea(2, 6)] public string failureOutcomeText;
    [Tooltip("실패했을 때만 적용되는 효과입니다.")]
    public EventEffect[] failureEffects = Array.Empty<EventEffect>();
    [Tooltip("성공 후 이벤트를 끝내지 않고 같은 선택지 화면으로 돌아갑니다.")]
    public bool continueAfterSuccess;
    [Tooltip("실패 후 이벤트를 끝내지 않고 같은 선택지 화면으로 돌아갑니다.")]
    public bool continueAfterFailure = true;
    [Tooltip("이 선택지의 최대 선택 횟수입니다. 0은 제한 없음입니다.")]
    [Min(0)] public int maximumSelections;
    [TextArea] public string selectionLimitReason =
        "더 이상 이 선택지를 고를 수 없습니다.";
}

[CreateAssetMenu(fileName = "Event_", menuName = "LOADED/Event Definition")]
public sealed class EventDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("세이브에 기록되는 고유 ID입니다. 변경하지 않는 것을 권장합니다.")]
    public string eventId;
    public string displayName;
    public Sprite artwork;

    [Header("Dialogue")]
    [TextArea(5, 14)] public string dialogue;
    [Tooltip("화면에는 앞에서부터 최대 3개만 표시됩니다.")]
    public EventChoiceData[] choices = Array.Empty<EventChoiceData>();

    [Header("Appearance Weight")]
    [Min(0f)] public float baseWeight = 10f;
    [Tooltip("선택을 마친 이벤트가 같은 런에서 다시 나오지 않게 합니다.")]
    public bool oncePerRun = true;
    [Tooltip("위에서 아래 순서로 가중치 연산을 적용합니다.")]
    public EventWeightRule[] weightRules = Array.Empty<EventWeightRule>();

    public string StableId => string.IsNullOrWhiteSpace(eventId)
        ? name
        : eventId.Trim();

    public float EvaluateWeight(EventRunContext context)
    {
        float weight = Mathf.Max(0f, baseWeight);

        if (weightRules == null)
        {
            return weight;
        }

        foreach (EventWeightRule rule in weightRules)
        {
            if (rule == null || !rule.Matches(context))
            {
                continue;
            }

            weight = rule.operation == EventWeightOperation.Multiply
                ? weight * rule.value
                : weight + rule.value;
            weight = Mathf.Max(0f, weight);
        }

        return weight;
    }

    private void OnValidate()
    {
        baseWeight = Mathf.Max(0f, baseWeight);
        if (choices != null && choices.Length > 3)
        {
            Debug.LogWarning(
                $"Event '{name}' has {choices.Length} choices. Only the first 3 are displayed.",
                this);
        }
    }
}

public readonly struct EventRunContext
{
    public EventRunContext(
        int eliteClears,
        int shopVisits,
        int eventClears,
        int money,
        int ownedBullets,
        float currentHealthPercent,
        int cumulativeBattleTurns)
    {
        EliteClears = Mathf.Max(0, eliteClears);
        ShopVisits = Mathf.Max(0, shopVisits);
        EventClears = Mathf.Max(0, eventClears);
        Money = Mathf.Max(0, money);
        OwnedBullets = Mathf.Max(0, ownedBullets);
        CurrentHealthPercent = Mathf.Clamp(currentHealthPercent, 0f, 100f);
        CumulativeBattleTurns = Mathf.Max(0, cumulativeBattleTurns);
    }

    public int EliteClears { get; }
    public int ShopVisits { get; }
    public int EventClears { get; }
    public int Money { get; }
    public int OwnedBullets { get; }
    public float CurrentHealthPercent { get; }
    public int CumulativeBattleTurns { get; }

    public float GetValue(EventRunStatistic statistic)
    {
        return statistic switch
        {
            EventRunStatistic.EliteClears => EliteClears,
            EventRunStatistic.ShopVisits => ShopVisits,
            EventRunStatistic.EventClears => EventClears,
            EventRunStatistic.Money => Money,
            EventRunStatistic.OwnedBullets => OwnedBullets,
            EventRunStatistic.CurrentHealthPercent => CurrentHealthPercent,
            EventRunStatistic.CumulativeBattleTurns => CumulativeBattleTurns,
            _ => 0f
        };
    }
}

public static class EventSelector
{
    public static EventDefinition Select(
        IReadOnlyList<EventDefinition> events,
        EventRunContext context,
        IReadOnlyCollection<string> completedEventIds)
    {
        if (events == null || events.Count == 0)
        {
            return null;
        }

        List<(EventDefinition Event, float Weight)> candidates =
            new List<(EventDefinition, float)>();
        float totalWeight = 0f;

        foreach (EventDefinition definition in events)
        {
            if (definition == null || definition.oncePerRun
                && completedEventIds != null
                && completedEventIds.Contains(definition.StableId))
            {
                continue;
            }

            float weight = definition.EvaluateWeight(context);
            if (weight <= 0f)
            {
                continue;
            }

            candidates.Add((definition, weight));
            totalWeight += weight;
        }

        if (candidates.Count == 0 || totalWeight <= 0f)
        {
            return null;
        }

        float roll = UnityEngine.Random.value * totalWeight;
        foreach ((EventDefinition definition, float weight) in candidates)
        {
            roll -= weight;
            if (roll <= 0f)
            {
                return definition;
            }
        }

        return candidates[candidates.Count - 1].Event;
    }
}
