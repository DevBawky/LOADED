using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal readonly struct EventChoiceAvailabilityContext
{
    public EventChoiceAvailabilityContext(
        int currentMoney,
        bool canRemoveOwnedBullet,
        int ownedBulletCount,
        IReadOnlyList<BulletInstance> ownedBullets,
        int ownedItemCount,
        int emptyItemSlotCount,
        int ownedRelicCount,
        int bulletCatalogCount,
        bool hasNonJackpotBulletReward,
        bool hasItemReward)
    {
        CurrentMoney = currentMoney;
        CanRemoveOwnedBullet = canRemoveOwnedBullet;
        OwnedBulletCount = ownedBulletCount;
        OwnedBullets = ownedBullets ?? Array.Empty<BulletInstance>();
        OwnedItemCount = ownedItemCount;
        EmptyItemSlotCount = emptyItemSlotCount;
        OwnedRelicCount = ownedRelicCount;
        BulletCatalogCount = bulletCatalogCount;
        HasNonJackpotBulletReward = hasNonJackpotBulletReward;
        HasItemReward = hasItemReward;
    }

    public int CurrentMoney { get; }
    public bool CanRemoveOwnedBullet { get; }
    public int OwnedBulletCount { get; }
    public IReadOnlyList<BulletInstance> OwnedBullets { get; }
    public int OwnedItemCount { get; }
    public int EmptyItemSlotCount { get; }
    public int OwnedRelicCount { get; }
    public int BulletCatalogCount { get; }
    public bool HasNonJackpotBulletReward { get; }
    public bool HasItemReward { get; }
}

internal readonly struct EventChoiceAvailabilityResult
{
    private EventChoiceAvailabilityResult(
        bool isAvailable,
        string unavailableReason)
    {
        IsAvailable = isAvailable;
        UnavailableReason = unavailableReason ?? string.Empty;
    }

    public bool IsAvailable { get; }
    public string UnavailableReason { get; }

    public static EventChoiceAvailabilityResult Available()
    {
        return new EventChoiceAvailabilityResult(true, string.Empty);
    }

    public static EventChoiceAvailabilityResult Unavailable(string reason)
    {
        return new EventChoiceAvailabilityResult(false, reason);
    }
}

internal static class EventChoiceAvailabilityEvaluator
{
    public static EventChoiceAvailabilityResult Evaluate(
        EventChoiceData choice,
        int choiceIndex,
        int previousSelections,
        EventChoiceAvailabilityContext context)
    {
        if (choice == null)
        {
            return EventChoiceAvailabilityResult.Unavailable(string.Empty);
        }

        if (choice.maximumSelections > 0
            && previousSelections >= choice.maximumSelections)
        {
            return EventChoiceAvailabilityResult.Unavailable(
                string.IsNullOrWhiteSpace(choice.selectionLimitReason)
                    ? "더 이상 이 선택지를 고를 수 없습니다."
                    : choice.selectionLimitReason);
        }

        EventChoiceAvailabilityResult requirements =
            ValidateRequirements(choice, context);
        if (!requirements.IsAvailable)
        {
            return requirements;
        }

        EventChoiceAvailabilityResult targetSelection =
            ValidateTargetSelection(choice, context);
        if (!targetSelection.IsAvailable)
        {
            return targetSelection;
        }

        EventChoiceAvailabilityResult specialAction =
            ValidateSpecialAction(choice, context);
        if (!specialAction.IsAvailable)
        {
            return specialAction;
        }

        return ValidateEffects(
            choice,
            choiceIndex,
            previousSelections,
            context);
    }

    public static bool IsBulletEligibleForChoice(
        BulletInstance bullet,
        EventChoiceData choice,
        bool requireUpgrade)
    {
        return bullet?.Data != null
            && (!requireUpgrade || bullet.CanUpgrade)
            && (!choice.restrictBulletGrade
                || bullet.Grade == choice.requiredBulletGrade)
            && (string.IsNullOrWhiteSpace(choice.requiredBulletId)
                || bullet.Data.BulletId == choice.requiredBulletId);
    }

    private static EventChoiceAvailabilityResult ValidateRequirements(
        EventChoiceData choice,
        EventChoiceAvailabilityContext context)
    {
        IEnumerable<EventChoiceRequirement> requirements =
            choice.requirements ?? Array.Empty<EventChoiceRequirement>();
        foreach (EventChoiceRequirement requirement in requirements)
        {
            if (requirement == null)
            {
                continue;
            }

            bool valid = requirement.type switch
            {
                EventChoiceRequirementType.None => true,
                EventChoiceRequirementType.MoneyAtLeast =>
                    context.CurrentMoney >= requirement.amount,
                EventChoiceRequirementType.RemovableBulletExists =>
                    context.CanRemoveOwnedBullet,
                EventChoiceRequirementType.UpgradableBulletExists =>
                    context.OwnedBullets.Any(bullet =>
                        bullet != null && bullet.CanUpgrade),
                EventChoiceRequirementType.BulletSpaceExists =>
                    context.OwnedBulletCount
                        < DeckManager.MaximumOwnedBulletCount,
                EventChoiceRequirementType.ItemSpaceExists =>
                    context.EmptyItemSlotCount > 0,
                EventChoiceRequirementType.OwnedBulletCountAtLeast =>
                    context.OwnedBulletCount >= requirement.amount,
                EventChoiceRequirementType.BulletGradeCountAtLeast =>
                    context.OwnedBullets.Count(bullet => bullet != null
                        && bullet.Grade == requirement.bulletGrade)
                        >= requirement.amount,
                EventChoiceRequirementType.BulletIdExists =>
                    context.OwnedBullets.Count(bullet => bullet?.Data != null
                        && bullet.Data.BulletId == requirement.bulletId)
                        >= Mathf.Max(1, requirement.amount),
                EventChoiceRequirementType.ItemExists =>
                    context.OwnedItemCount
                        >= Mathf.Max(1, requirement.amount),
                EventChoiceRequirementType.RelicCountAtLeast =>
                    context.OwnedRelicCount >= requirement.amount,
                _ => true
            };

            if (!valid)
            {
                return EventChoiceAvailabilityResult.Unavailable(
                    requirement.unavailableReason);
            }
        }

        return EventChoiceAvailabilityResult.Available();
    }

    private static EventChoiceAvailabilityResult ValidateTargetSelection(
        EventChoiceData choice,
        EventChoiceAvailabilityContext context)
    {
        IEnumerable<EventEffect> allEffects =
            (choice.attemptEffects ?? Array.Empty<EventEffect>())
            .Concat(choice.effects ?? Array.Empty<EventEffect>())
            .Concat(choice.failureEffects ?? Array.Empty<EventEffect>());
        bool removesBullet = allEffects.Any(effect => effect != null
            && effect.type == EventEffectType.RemoveChosenBullet);
        bool upgradesBullet = allEffects.Any(effect => effect != null
            && effect.type == EventEffectType.UpgradeChosenBullet);
        int bulletCount = Mathf.Max(1, choice.bulletSelectionCount);
        if (removesBullet || upgradesBullet)
        {
            if (removesBullet && context.OwnedBulletCount - bulletCount
                < DeckManager.MinimumOwnedBulletCount)
            {
                return EventChoiceAvailabilityResult.Unavailable(
                    "선택 후에도 탄환을 1개 이상 보유해야 합니다.");
            }

            List<BulletInstance> candidates = context.OwnedBullets
                .Where(bullet => IsBulletEligibleForChoice(
                    bullet,
                    choice,
                    upgradesBullet))
                .ToList();
            bool hasGroup = choice.requireSameBulletGrade
                ? candidates.GroupBy(bullet => bullet.Grade).Any(group =>
                    CountSelectableBulletTypes(
                        group,
                        choice.requireDistinctBulletTypes) >= bulletCount)
                : CountSelectableBulletTypes(
                    candidates,
                    choice.requireDistinctBulletTypes) >= bulletCount;
            if (!hasGroup)
            {
                return EventChoiceAvailabilityResult.Unavailable(
                    $"조건에 맞는 탄환 {bulletCount}개가 필요합니다.");
            }
        }

        if (allEffects.Any(effect => effect != null
                && effect.type == EventEffectType.RemoveChosenItem)
            && context.OwnedItemCount
                < Mathf.Max(1, choice.itemSelectionCount))
        {
            return EventChoiceAvailabilityResult.Unavailable(
                "몰수할 아이템이 부족합니다.");
        }

        if (allEffects.Any(effect => effect != null
                && effect.type == EventEffectType.RemoveChosenRelic)
            && context.OwnedRelicCount
                < Mathf.Max(1, choice.relicSelectionCount))
        {
            return EventChoiceAvailabilityResult.Unavailable(
                "넘길 유물이 부족합니다.");
        }

        return EventChoiceAvailabilityResult.Available();
    }

    private static EventChoiceAvailabilityResult ValidateSpecialAction(
        EventChoiceData choice,
        EventChoiceAvailabilityContext context)
    {
        if (choice.specialAction == EventSpecialAction.RandomBulletOffer
            && context.OwnedBulletCount
                >= DeckManager.MaximumOwnedBulletCount)
        {
            return EventChoiceAvailabilityResult.Unavailable(
                "탄환 보유 공간이 부족합니다.");
        }

        if (choice.specialAction == EventSpecialAction.SlotMachine
            && !HasSlotRewardCandidate(context))
        {
            return EventChoiceAvailabilityResult.Unavailable(
                "잭팟 탄환을 받을 공간이 필요합니다.");
        }

        if (choice.specialAction == EventSpecialAction.BulletQuiz
            && (context.OwnedBulletCount == 0
                || context.BulletCatalogCount < 3))
        {
            return EventChoiceAvailabilityResult.Unavailable(
                "퀴즈를 만들 탄환 정보가 부족합니다.");
        }

        return EventChoiceAvailabilityResult.Available();
    }

    private static EventChoiceAvailabilityResult ValidateEffects(
        EventChoiceData choice,
        int choiceIndex,
        int previousSelections,
        EventChoiceAvailabilityContext context)
    {
        if (choiceIndex < 0)
        {
            return EventChoiceAvailabilityResult.Unavailable(
                "유효하지 않은 이벤트 선택지입니다.");
        }

        IEnumerable<EventEffect> attemptEffects =
            EventRuntimeRules.GetActiveEffects(
                choice.attemptEffects,
                previousSelections);
        IEnumerable<EventEffect> successEffects =
            EventRuntimeRules.GetActiveEffects(
                choice.effects,
                previousSelections);
        EventChoiceAvailabilityResult success = ValidateEffectSet(
            attemptEffects.Concat(successEffects),
            previousSelections,
            context);
        if (!success.IsAvailable || !choice.useSuccessChance)
        {
            return success;
        }

        IEnumerable<EventEffect> failureEffects =
            EventRuntimeRules.GetActiveEffects(
                choice.failureEffects,
                previousSelections);
        return ValidateEffectSet(
            EventRuntimeRules.GetActiveEffects(
                    choice.attemptEffects,
                    previousSelections)
                .Concat(failureEffects),
            previousSelections,
            context);
    }

    private static EventChoiceAvailabilityResult ValidateEffectSet(
        IEnumerable<EventEffect> effects,
        int previousSelections,
        EventChoiceAvailabilityContext context)
    {
        long moneyCost = 0L;
        int bulletsToAdd = 0;
        int itemsToAdd = 0;

        foreach (EventEffect effect in effects)
        {
            if (effect == null)
            {
                continue;
            }

            switch (effect.type)
            {
                case EventEffectType.LoseMoney:
                    moneyCost += EventRuntimeRules.GetEffectAmount(
                        effect,
                        previousSelections);
                    break;
                case EventEffectType.AddBullet:
                    bulletsToAdd++;
                    break;
                case EventEffectType.AddItem:
                    if (effect.item == null)
                    {
                        return EventChoiceAvailabilityResult.Unavailable(
                            "획득할 아이템이 설정되지 않았습니다.");
                    }

                    itemsToAdd++;
                    break;
                case EventEffectType.RemoveChosenBullet:
                    if (!context.CanRemoveOwnedBullet)
                    {
                        return EventChoiceAvailabilityResult.Unavailable(
                            "제거할 수 있는 탄환이 없습니다.");
                    }

                    break;
                case EventEffectType.UpgradeChosenBullet:
                    if (!context.OwnedBullets.Any(bullet =>
                            bullet != null && bullet.CanUpgrade))
                    {
                        return EventChoiceAvailabilityResult.Unavailable(
                            "강화할 수 있는 탄환이 없습니다.");
                    }

                    break;
                case EventEffectType.RemoveChosenItem:
                    if (context.OwnedItemCount <= 0)
                    {
                        return EventChoiceAvailabilityResult.Unavailable(
                            "몰수할 아이템이 없습니다.");
                    }

                    break;
                case EventEffectType.RemoveChosenRelic:
                    if (context.OwnedRelicCount <= 0)
                    {
                        return EventChoiceAvailabilityResult.Unavailable(
                            "넘길 유물이 없습니다.");
                    }

                    break;
            }
        }

        if (moneyCost > context.CurrentMoney)
        {
            return EventChoiceAvailabilityResult.Unavailable(
                "골드가 부족합니다.");
        }

        if ((long)context.OwnedBulletCount + bulletsToAdd
            > DeckManager.MaximumOwnedBulletCount)
        {
            return EventChoiceAvailabilityResult.Unavailable(
                "탄환 보유 공간이 부족합니다.");
        }

        if (itemsToAdd > context.EmptyItemSlotCount)
        {
            return EventChoiceAvailabilityResult.Unavailable(
                "아이템 보유 공간이 부족합니다.");
        }

        return EventChoiceAvailabilityResult.Available();
    }

    private static bool HasSlotRewardCandidate(
        EventChoiceAvailabilityContext context)
    {
        int bulletSpaces = DeckManager.MaximumOwnedBulletCount
            - context.OwnedBulletCount;
        return bulletSpaces >= 2 && context.HasNonJackpotBulletReward
            || bulletSpaces >= 1 && context.EmptyItemSlotCount > 0
                && context.HasItemReward;
    }

    private static int CountSelectableBulletTypes(
        IEnumerable<BulletInstance> bullets,
        bool distinctTypes)
    {
        return distinctTypes
            ? bullets.Where(bullet => bullet?.Data != null)
                .Select(bullet => bullet.Data)
                .Distinct()
                .Count()
            : bullets.Count();
    }
}
