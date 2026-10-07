using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BulletDynamicCombatRulesTests
{
    [Test]
    public void EvaluateCombinesDamageEffectsWithoutMutatingBulletState()
    {
        BulletData data = CreateBullet(
            new EffectDefinition(BulletEffectType.Gilded, 10f, 20),
            new EffectDefinition(BulletEffectType.Heart, 10f, 20));

        try
        {
            BulletInstance bullet = new BulletInstance(data, 0);
            bullet.AddTemporaryDamageBonus(0.1f);
            BulletDynamicCombatContext context = CreateContext(
                currentGold: 100,
                maxHealth: 100,
                initialLoadedCount: 4,
                maxChambers: 6,
                temporaryDamageBonus: bullet.TemporaryDamageBonus);

            BulletDynamicCombatResult result =
                BulletDynamicCombatRules.Evaluate(
                    bullet,
                    bullet,
                    context);

            Assert.That(
                result.DamageMultiplier,
                Is.EqualTo(1.1f * 1.5f * 1.5f)
                    .Within(0.0001f));
            Assert.That(bullet.TemporaryDamageBonus, Is.EqualTo(0.1f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void EvaluateCombinesTemporaryAndFocusCriticalBonus()
    {
        BulletData data = CreateBullet(
            new EffectDefinition(BulletEffectType.Focus, 2f));

        try
        {
            BulletInstance bullet = new BulletInstance(data, 0);
            BulletDynamicCombatContext context = CreateContext(
                currentHealth: 50,
                maxHealth: 100,
                abilityStacks: 3,
                temporaryCriticalChanceBonus: 7f);

            BulletDynamicCombatResult result =
                BulletDynamicCombatRules.Evaluate(
                    bullet,
                    bullet,
                    context);

            Assert.That(result.CriticalChanceBonus, Is.EqualTo(13f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void DamageAndCriticalCalculationsCanUseDifferentHealthSnapshots()
    {
        BulletData data = CreateBullet(
            new EffectDefinition(BulletEffectType.HighRoller, 100f),
            new EffectDefinition(BulletEffectType.Focus, 5f));

        try
        {
            BulletInstance bullet = new BulletInstance(data, 0);
            float damageMultiplier =
                BulletDynamicCombatRules.CalculateDamageMultiplier(
                    bullet,
                    bullet,
                    CreateContext(currentHealth: 100, maxHealth: 100));
            float criticalChanceBonus =
                BulletDynamicCombatRules.CalculateCriticalChanceBonus(
                    bullet,
                    CreateContext(currentHealth: 50, maxHealth: 100));

            Assert.That(damageMultiplier, Is.EqualTo(1f));
            Assert.That(criticalChanceBonus, Is.EqualTo(0f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void RemadeBulletHelpersUseStatusShotsAndRecoveryRules()
    {
        BulletData data = CreateBullet(
            new EffectDefinition(BulletEffectType.Crescendo, 2f, 4),
            new EffectDefinition(BulletEffectType.MassProduced, 0f, 3),
            new EffectDefinition(BulletEffectType.Coagulation, 12f, 4));

        try
        {
            SerializedObject serialized = new SerializedObject(data);
            SerializedProperty effects = serialized.FindProperty("effects");
            effects.GetArrayElementAtIndex(0)
                .FindPropertyRelative("knockbackDistance").intValue = 10;
            effects.GetArrayElementAtIndex(2)
                .FindPropertyRelative("knockbackDistance").intValue = 40;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            BulletInstance bullet = new BulletInstance(data, 0);

            Assert.That(
                BulletEffectUtility.GetCrescendoStatusStacks(
                    bullet.Effects[0]),
                Is.EqualTo(4));
            Assert.That(
                BulletEffectUtility.GetMassProducedAdditionalShots(
                    bullet.Effects[1],
                    8),
                Is.EqualTo(3));
            Assert.That(
                BulletEffectUtility.GetCoagulationRecoveryPercent(
                    bullet.Effects[2],
                    20),
                Is.EqualTo(40f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void MixedGradeMaskExcludesStunAndCrescendoIsResolvedAtRuntime()
    {
        BulletData data = CreateBullet(
            new EffectDefinition(BulletEffectType.Crescendo, 0f, 2),
            new EffectDefinition(BulletEffectType.MixedGrade, 0f, 1));

        try
        {
            BulletInstance bullet = new BulletInstance(data, 0);
            int mask = BulletEffectUtility.GetInflictedStatusMask(bullet);

            Assert.That(mask, Is.EqualTo(BulletEffectUtility.MixedGradeStatusMask));
            Assert.That(
                BulletEffectUtility.IncludesStatus(mask, StatusEffectType.Mark),
                Is.True);
            Assert.That(
                BulletEffectUtility.IncludesStatus(mask, StatusEffectType.Poison),
                Is.True);
            Assert.That(
                BulletEffectUtility.IncludesStatus(mask, StatusEffectType.Weakness),
                Is.True);
            Assert.That(
                BulletEffectUtility.IncludesStatus(mask, StatusEffectType.Stun),
                Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void MasterpieceAndRitualHelpersUseOwnedAndPermanentStacks()
    {
        BulletData data = CreateBullet(
            new EffectDefinition(BulletEffectType.Masterpiece, 0.25f, 4),
            new EffectDefinition(BulletEffectType.Ritual, 5f));

        try
        {
            BulletInstance bullet = new BulletInstance(data, 0);
            bullet.AddPermanentStacks(3);

            Assert.That(
                BulletEffectUtility.GetMasterpieceCriticalDamageBonus(
                    bullet.Effects[0],
                    9),
                Is.EqualTo(1f));
            Assert.That(
                BulletEffectUtility.GetRitualDamageBonus(
                    bullet.Effects[1],
                    bullet.PermanentStacks),
                Is.EqualTo(15));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void TargetMultiplierUsesStatusStacksAndPriorSniperShots()
    {
        BulletData data = CreateBullet(
            new EffectDefinition(BulletEffectType.Rangefinder, 10f),
            new EffectDefinition(BulletEffectType.Judgment, 20f),
            new EffectDefinition(BulletEffectType.Assassination, 50f),
            new EffectDefinition(BulletEffectType.Mastery, 1.2f));

        try
        {
            BulletInstance bullet = new BulletInstance(data, 0);
            float multiplier =
                BulletDynamicCombatRules.CalculateTargetDamageMultiplier(
                    bullet,
                    new BulletTargetDamageContext(3, 2, true, 3));

            Assert.That(
                multiplier,
                Is.EqualTo(1.4f * 2f * Mathf.Pow(1.2f, 3))
                    .Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void CompositionCaptureSharesLoadedAndOwnedCountingRules()
    {
        BulletData firedData = CreateBullet();
        BulletData resonanceData = CreateBullet(
            new EffectDefinition(BulletEffectType.Resonance, 10f));
        BulletData highGradeData = CreateBullet();
        SetGrade(firedData, BulletGrade.Normal);
        SetGrade(resonanceData, BulletGrade.Rare);
        SetGrade(highGradeData, BulletGrade.Legendary);

        try
        {
            BulletInstance fired = new BulletInstance(firedData, 0);
            BulletInstance firstResonance =
                new BulletInstance(resonanceData, 1);
            BulletInstance secondResonance =
                new BulletInstance(resonanceData, 2);
            BulletInstance highGrade =
                new BulletInstance(highGradeData, 3);
            BulletInstance[] loaded =
                { fired, firstResonance, secondResonance, highGrade };

            BulletOwnedCompositionSnapshot snapshot =
                BulletOwnedCompositionSnapshot.Capture(
                    fired,
                    loaded,
                    loaded);

            Assert.That(snapshot.OtherResonanceCount, Is.EqualTo(2));
            Assert.That(snapshot.DistinctOwnedBulletTypeCount, Is.EqualTo(3));
            Assert.That(snapshot.OtherLoadedGradeCount, Is.EqualTo(3));
            Assert.That(snapshot.OwnedHighGradeCount, Is.EqualTo(1));
            Assert.That(snapshot.OwnedLowGradeCount, Is.EqualTo(3));
            Assert.That(snapshot.MostCommonOwnedGradeCount, Is.EqualTo(2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firedData);
            UnityEngine.Object.DestroyImmediate(resonanceData);
            UnityEngine.Object.DestroyImmediate(highGradeData);
        }
    }

    [Test]
    public void TooltipUsesTheSharedDynamicCombatResult()
    {
        BulletData data = CreateBullet(
            new EffectDefinition(BulletEffectType.Gilded, 10f, 20),
            new EffectDefinition(BulletEffectType.Heart, 10f, 20));

        try
        {
            BulletInstance bullet = new BulletInstance(data, 0);
            BulletTooltipContext tooltipContext = new BulletTooltipContext(
                100,
                100,
                100,
                1,
                6,
                0,
                0,
                Array.Empty<BulletInstance>(),
                new[] { bullet },
                Array.Empty<BulletInstance>());
            BulletDynamicCombatContext dynamicContext = CreateContext(
                currentGold: 100,
                currentHealth: 100,
                maxHealth: 100,
                initialLoadedCount: 1,
                maxChambers: 6,
                isLastChamber: true,
                distinctOwnedBulletTypeCount: 1,
                ownedLowGradeCount: 1,
                mostCommonOwnedGradeCount: 1);

            BulletDynamicCombatResult expected =
                BulletDynamicCombatRules.Evaluate(
                    bullet,
                    bullet,
                    dynamicContext);
            BulletRuntimeTooltipStats actual =
                bullet.GetRuntimeTooltipStats(tooltipContext);

            Assert.That(
                actual.DamageMultiplier,
                Is.EqualTo(expected.DamageMultiplier).Within(0.0001f));
            Assert.That(
                actual.CriticalChanceBonus,
                Is.EqualTo(expected.CriticalChanceBonus).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static BulletDynamicCombatContext CreateContext(
        int currentGold = 0,
        int currentHealth = 0,
        int maxHealth = 0,
        int initialLoadedCount = 0,
        int maxChambers = 0,
        bool isLastChamber = false,
        int abilityStacks = 0,
        int permanentStacks = 0,
        int shotsObservedWhileLoaded = 0,
        float temporaryDamageBonus = 0f,
        float temporaryCriticalChanceBonus = 0f,
        int otherResonanceCount = 0,
        int distinctOwnedBulletTypeCount = 0,
        int otherLoadedGradeCount = 0,
        int ownedHighGradeCount = 0,
        int ownedLowGradeCount = 0,
        int mostCommonOwnedGradeCount = 0)
    {
        return new BulletDynamicCombatContext(
            new BulletCombatResourceSnapshot(
                currentGold,
                currentHealth,
                maxHealth),
            new BulletChamberSnapshot(
                initialLoadedCount,
                maxChambers,
                true,
                isLastChamber,
                false),
            new BulletRuntimeCombatSnapshot(
                abilityStacks,
                permanentStacks,
                shotsObservedWhileLoaded,
                temporaryDamageBonus,
                temporaryCriticalChanceBonus),
            new BulletOwnedCompositionSnapshot(
                otherResonanceCount,
                distinctOwnedBulletTypeCount,
                otherLoadedGradeCount,
                ownedHighGradeCount,
                ownedLowGradeCount,
                mostCommonOwnedGradeCount));
    }

    private static BulletData CreateBullet(
        params EffectDefinition[] definitions)
    {
        BulletData data = ScriptableObject.CreateInstance<BulletData>();
        SerializedObject serialized = new SerializedObject(data);
        SerializedProperty effects = serialized.FindProperty("effects");
        effects.arraySize = definitions.Length;

        for (int index = 0; index < definitions.Length; index++)
        {
            EffectDefinition definition = definitions[index];
            SerializedProperty effect = effects.GetArrayElementAtIndex(index);
            effect.FindPropertyRelative("effectType").enumValueIndex =
                (int)definition.Type;
            effect.FindPropertyRelative("target").enumValueIndex =
                (int)BulletEffectTarget.FiringPlayer;
            effect.FindPropertyRelative("activationChance").floatValue = 100f;
            effect.FindPropertyRelative("stackCount").intValue =
                definition.StackCount;
            effect.FindPropertyRelative("amount").floatValue =
                definition.Amount;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return data;
    }

    private static void SetGrade(BulletData data, BulletGrade grade)
    {
        SerializedObject serialized = new SerializedObject(data);
        serialized.FindProperty("grade").enumValueIndex = (int)grade;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private readonly struct EffectDefinition
    {
        public EffectDefinition(
            BulletEffectType type,
            float amount,
            int stackCount = 1)
        {
            Type = type;
            Amount = amount;
            StackCount = stackCount;
        }

        public BulletEffectType Type { get; }
        public float Amount { get; }
        public int StackCount { get; }
    }
}
