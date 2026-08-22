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
    ItemSpaceExists,
    OwnedBulletCountAtLeast,
    BulletGradeCountAtLeast,
    BulletIdExists,
    ItemExists,
    RelicCountAtLeast
}

[Serializable]
public sealed class EventChoiceRequirement
{
    public EventChoiceRequirementType type;
    [Min(0)] public int amount;
    public BulletGrade bulletGrade;
    public string bulletId;
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
    AddItem,
    IncreaseMaxHealthPercent,
    LoseCurrentHealthPercent,
    AddPendingStatusEffect,
    RemoveChosenItem,
    RemoveChosenRelic
}

public enum EventRandomBulletGradeMode
{
    Weighted = 0,
    Fixed = 1,
    MatchSelected = 2,
    MatchSelectedOrOneHigher = 3
}

public enum EventSpecialAction
{
    None = 0,
    RandomBulletOffer = 1,
    SlotMachine = 2,
    BulletQuiz = 3
}

public enum EventFollowUpDestination
{
    NodeMap = 0,
    NormalBattle = 1,
    EliteBattle = 2,
    Shop = 3
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
    public EventRandomBulletGradeMode randomBulletGradeMode;
    public BulletGrade fixedBulletGrade;
    [Range(0f, 100f)] public float oneGradeHigherChancePercent = 50f;
    [Range(0, BulletData.MaximumUpgradeLevel)] public int bulletLevel;
    [Tooltip("Add Item 효과에서 획득할 아이템입니다.")]
    public ItemData item;
    public StatusEffectType statusEffectType;
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

    [Header("Target Selection")]
    [Min(1)] public int bulletSelectionCount = 1;
    public bool requireDistinctBulletTypes;
    public bool requireSameBulletGrade;
    public bool restrictBulletGrade;
    public BulletGrade requiredBulletGrade;
    public string requiredBulletId;
    [Min(1)] public int itemSelectionCount = 1;
    [Min(1)] public int relicSelectionCount = 1;

    [Header("Staged Interaction")]
    public EventSpecialAction specialAction;
    [Range(1, 3)] public int randomBulletOfferCount = 3;
    public EventRandomBulletGradeMode offerGradeMode;
    public BulletGrade fixedOfferGrade;
    [Range(0f, 100f)] public float offerOneGradeHigherChancePercent = 50f;
    [Range(0, BulletData.MaximumUpgradeLevel)] public int offeredBulletLevel;

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
    [Tooltip("기존 이벤트 에셋 호환용입니다. 완료한 이벤트는 이 값과 관계없이 같은 런에서 다시 나오지 않습니다.")]
    public bool oncePerRun = true;
    [Tooltip("위에서 아래 순서로 가중치 연산을 적용합니다.")]
    public EventWeightRule[] weightRules = Array.Empty<EventWeightRule>();

    [Header("Follow-up Encounter Chance")]
    [Range(0f, 100f)] public float normalBattleChancePercent;
    [Range(0f, 100f)] public float eliteBattleChancePercent;
    [Range(0f, 100f)] public float shopChancePercent;

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
        normalBattleChancePercent = Mathf.Clamp(
            normalBattleChancePercent,
            0f,
            100f);
        eliteBattleChancePercent = Mathf.Clamp(
            eliteBattleChancePercent,
            0f,
            100f);
        shopChancePercent = Mathf.Clamp(shopChancePercent, 0f, 100f);
        float followUpTotal = normalBattleChancePercent
            + eliteBattleChancePercent
            + shopChancePercent;
        if (followUpTotal > 100f)
        {
            Debug.LogWarning(
                $"Event '{name}' follow-up chances total {followUpTotal:0.#}%.",
                this);
        }
        if (choices != null && choices.Length > 3)
        {
            Debug.LogWarning(
                $"Event '{name}' has {choices.Length} choices. Only the first 3 are displayed.",
                this);
        }
    }
}

internal static class EventRuntimeRules
{
    public static void GenerateBulletOffers(
        IReadOnlyList<BulletData> catalog,
        IReadOnlyList<BulletGradeWeightData> gradeWeights,
        int count,
        EventRandomBulletGradeMode gradeMode,
        BulletGrade fixedGrade,
        float oneGradeHigherChancePercent,
        IReadOnlyList<BulletInstance> selectedBullets,
        List<BulletData> destination)
    {
        destination.Clear();
        BulletGrade? targetGrade = ResolveTargetGrade(
            gradeMode,
            fixedGrade,
            oneGradeHigherChancePercent,
            selectedBullets);

        if (!targetGrade.HasValue)
        {
            ShopOfferGenerator.GenerateBullets(
                catalog,
                gradeWeights,
                count,
                destination);
            return;
        }

        List<BulletData> candidates = catalog == null
            ? new List<BulletData>()
            : catalog.Where(bullet => bullet != null
                    && bullet.Grade == targetGrade.Value)
                .Distinct()
                .ToList();
        int offerCount = Mathf.Min(Mathf.Max(0, count), candidates.Count);
        for (int index = 0; index < offerCount; index++)
        {
            int candidateIndex = UnityEngine.Random.Range(
                0,
                candidates.Count);
            destination.Add(candidates[candidateIndex]);
            candidates.RemoveAt(candidateIndex);
        }
    }

    public static BulletData FindBulletByAssetName(
        IReadOnlyList<BulletData> catalog,
        string assetName)
    {
        return catalog?.FirstOrDefault(bullet => bullet != null
            && bullet.name == assetName);
    }

    public static BulletData FindBulletById(
        IReadOnlyList<BulletData> catalog,
        string bulletId)
    {
        return catalog?.FirstOrDefault(bullet => bullet != null
            && bullet.BulletId == bulletId);
    }

    public static ItemData FindItemByAssetName(
        IReadOnlyList<ItemData> catalog,
        string assetName)
    {
        return catalog?.FirstOrDefault(item => item != null
            && item.name == assetName);
    }

    public static bool IsValidBulletGroup(
        IReadOnlyList<BulletInstance> bullets,
        int requiredCount,
        bool requireDistinctTypes,
        bool requireSameGrade)
    {
        if (bullets == null || bullets.Count != requiredCount
            || bullets.Any(bullet => bullet?.Data == null))
        {
            return false;
        }

        if (requireDistinctTypes
            && bullets.Select(bullet => bullet.Data).Distinct().Count()
                != bullets.Count)
        {
            return false;
        }

        return !requireSameGrade
            || bullets.Select(bullet => bullet.Grade).Distinct().Count() == 1;
    }

    public static EventFollowUpDestination SelectFollowUp(
        float normalChancePercent,
        float eliteChancePercent,
        float shopChancePercent)
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        float cursor = Mathf.Max(0f, normalChancePercent);
        if (roll < cursor)
        {
            return EventFollowUpDestination.NormalBattle;
        }

        cursor += Mathf.Max(0f, eliteChancePercent);
        if (roll < cursor)
        {
            return EventFollowUpDestination.EliteBattle;
        }

        cursor += Mathf.Max(0f, shopChancePercent);
        return roll < cursor
            ? EventFollowUpDestination.Shop
            : EventFollowUpDestination.NodeMap;
    }

    private static BulletGrade? ResolveTargetGrade(
        EventRandomBulletGradeMode mode,
        BulletGrade fixedGrade,
        float oneGradeHigherChancePercent,
        IReadOnlyList<BulletInstance> selectedBullets)
    {
        if (mode == EventRandomBulletGradeMode.Weighted)
        {
            return null;
        }

        BulletGrade grade = mode == EventRandomBulletGradeMode.Fixed
            || selectedBullets == null || selectedBullets.Count == 0
                ? fixedGrade
                : selectedBullets[0].Grade;
        if (mode == EventRandomBulletGradeMode.MatchSelectedOrOneHigher
            && grade < BulletGrade.Legendary
            && UnityEngine.Random.Range(0f, 100f)
                < Mathf.Clamp(oneGradeHigherChancePercent, 0f, 100f))
        {
            grade = (BulletGrade)((int)grade + 1);
        }

        return grade;
    }
}
