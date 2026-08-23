using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EventChoiceAvailabilityTests
{
    private readonly List<ScriptableObject> createdAssets =
        new List<ScriptableObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (ScriptableObject asset in createdAssets)
        {
            if (asset != null)
            {
                Object.DestroyImmediate(asset);
            }
        }

        createdAssets.Clear();
    }

    [Test]
    public void Evaluate_RejectsSelectionLimitBeforeResourceRequirements()
    {
        EventChoiceData choice = new EventChoiceData
        {
            maximumSelections = 1,
            selectionLimitReason = "선택 횟수를 모두 사용했습니다.",
            requirements = new[]
            {
                new EventChoiceRequirement
                {
                    type = EventChoiceRequirementType.MoneyAtLeast,
                    amount = 100,
                    unavailableReason = "골드가 필요합니다."
                }
            }
        };

        EventChoiceAvailabilityResult result =
            EventChoiceAvailabilityEvaluator.Evaluate(
                choice,
                0,
                1,
                CreateContext(currentMoney: 0));

        Assert.That(result.IsAvailable, Is.False);
        Assert.That(result.UnavailableReason,
            Is.EqualTo("선택 횟수를 모두 사용했습니다."));
    }

    [Test]
    public void Evaluate_ScalesRepeatedAttemptCost()
    {
        EventChoiceData choice = new EventChoiceData
        {
            attemptEffects = new[]
            {
                new EventEffect
                {
                    type = EventEffectType.LoseMoney,
                    amount = 5,
                    amountPerPreviousSelection = 3
                }
            }
        };

        EventChoiceAvailabilityResult insufficient =
            EventChoiceAvailabilityEvaluator.Evaluate(
                choice,
                0,
                2,
                CreateContext(currentMoney: 10));
        EventChoiceAvailabilityResult exact =
            EventChoiceAvailabilityEvaluator.Evaluate(
                choice,
                0,
                2,
                CreateContext(currentMoney: 11));

        Assert.That(insufficient.IsAvailable, Is.False);
        Assert.That(insufficient.UnavailableReason,
            Is.EqualTo("골드가 부족합니다."));
        Assert.That(exact.IsAvailable, Is.True);
    }

    [Test]
    public void Evaluate_ValidatesFailureBranchForChanceChoice()
    {
        EventChoiceData choice = new EventChoiceData
        {
            useSuccessChance = true,
            failureEffects = new[]
            {
                new EventEffect
                {
                    type = EventEffectType.AddItem
                }
            }
        };

        EventChoiceAvailabilityResult result =
            EventChoiceAvailabilityEvaluator.Evaluate(
                choice,
                0,
                0,
                CreateContext(emptyItemSlotCount: 1));

        Assert.That(result.IsAvailable, Is.False);
        Assert.That(result.UnavailableReason,
            Is.EqualTo("획득할 아이템이 설정되지 않았습니다."));
    }

    [Test]
    public void Evaluate_RequiresDistinctSameGradeBulletTargets()
    {
        BulletData firstData = CreateBullet(BulletGrade.Rare);
        BulletData secondData = CreateBullet(BulletGrade.Rare);
        BulletInstance first = new BulletInstance(firstData, 0);
        BulletInstance duplicate = new BulletInstance(firstData, 1);
        BulletInstance second = new BulletInstance(secondData, 2);
        EventChoiceData choice = new EventChoiceData
        {
            bulletSelectionCount = 2,
            requireDistinctBulletTypes = true,
            requireSameBulletGrade = true,
            effects = new[]
            {
                new EventEffect
                {
                    type = EventEffectType.UpgradeChosenBullet
                }
            }
        };

        EventChoiceAvailabilityResult duplicateResult =
            EventChoiceAvailabilityEvaluator.Evaluate(
                choice,
                0,
                0,
                CreateContext(
                    ownedBullets: new[] { first, duplicate },
                    ownedBulletCount: 2,
                    canRemoveOwnedBullet: true));
        EventChoiceAvailabilityResult distinctResult =
            EventChoiceAvailabilityEvaluator.Evaluate(
                choice,
                0,
                0,
                CreateContext(
                    ownedBullets: new[] { first, second },
                    ownedBulletCount: 2,
                    canRemoveOwnedBullet: true));

        Assert.That(duplicateResult.IsAvailable, Is.False);
        Assert.That(duplicateResult.UnavailableReason,
            Is.EqualTo("조건에 맞는 탄환 2개가 필요합니다."));
        Assert.That(distinctResult.IsAvailable, Is.True);
    }

    [Test]
    public void Evaluate_SlotMachineRequiresReachableReward()
    {
        EventChoiceData choice = new EventChoiceData
        {
            specialAction = EventSpecialAction.SlotMachine
        };

        EventChoiceAvailabilityResult bulletOnly =
            EventChoiceAvailabilityEvaluator.Evaluate(
                choice,
                0,
                0,
                CreateContext(
                    ownedBulletCount: 19,
                    hasNonJackpotBulletReward: true));
        EventChoiceAvailabilityResult itemAvailable =
            EventChoiceAvailabilityEvaluator.Evaluate(
                choice,
                0,
                0,
                CreateContext(
                    ownedBulletCount: 19,
                    emptyItemSlotCount: 1,
                    hasNonJackpotBulletReward: true,
                    hasItemReward: true));

        Assert.That(bulletOnly.IsAvailable, Is.False);
        Assert.That(bulletOnly.UnavailableReason,
            Is.EqualTo("잭팟 탄환을 받을 공간이 필요합니다."));
        Assert.That(itemAvailable.IsAvailable, Is.True);
    }

    [Test]
    public void RuntimeRules_FilterRangesAndClampSuccessChance()
    {
        EventEffect early = new EventEffect
        {
            useSelectionRange = true,
            minimumPreviousSelections = 0,
            maximumPreviousSelections = 1
        };
        EventEffect late = new EventEffect
        {
            useSelectionRange = true,
            minimumPreviousSelections = 2,
            maximumPreviousSelections = -1
        };
        EventChoiceData choice = new EventChoiceData
        {
            useSuccessChance = true,
            baseSuccessChancePercent = 80f,
            successChanceIncreaseOnFailurePercent = 15f
        };

        Assert.That(EventRuntimeRules.GetActiveEffects(
            new[] { early, late }, 2), Is.EqualTo(new[] { late }));
        Assert.That(EventRuntimeRules.GetSuccessChance(choice, 2),
            Is.EqualTo(100f));
        Assert.That(EventRuntimeRules.GetChoiceProgress(
            new[] { -3, 4 }, 0), Is.Zero);
    }

    private EventChoiceAvailabilityContext CreateContext(
        int currentMoney = 0,
        bool canRemoveOwnedBullet = false,
        int ownedBulletCount = 0,
        IReadOnlyList<BulletInstance> ownedBullets = null,
        int ownedItemCount = 0,
        int emptyItemSlotCount = 0,
        int ownedRelicCount = 0,
        int bulletCatalogCount = 0,
        bool hasNonJackpotBulletReward = false,
        bool hasItemReward = false)
    {
        return new EventChoiceAvailabilityContext(
            currentMoney,
            canRemoveOwnedBullet,
            ownedBulletCount,
            ownedBullets,
            ownedItemCount,
            emptyItemSlotCount,
            ownedRelicCount,
            bulletCatalogCount,
            hasNonJackpotBulletReward,
            hasItemReward);
    }

    private BulletData CreateBullet(BulletGrade grade)
    {
        BulletData bullet = ScriptableObject.CreateInstance<BulletData>();
        createdAssets.Add(bullet);
        SerializedObject serialized = new SerializedObject(bullet);
        serialized.FindProperty("grade").enumValueIndex = (int)grade;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return bullet;
    }
}
