using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

internal sealed class RelicChanceEffectHandlerTests
{
    private readonly List<RelicData> createdRelics = new List<RelicData>();

    [TearDown]
    public void TearDown()
    {
        foreach (RelicData relic in createdRelics)
        {
            Object.DestroyImmediate(relic);
        }

        createdRelics.Clear();
    }

    [Test]
    public void EmptyBeat_PublishesProbabilityBeforeRollAndTrigger()
    {
        RelicInstance relic = CreateInstance(
            "empty-beat-order",
            RelicEffectType.EmptyBeat,
            primerChance: 37d);
        List<string> calls = new List<string>();
        RelicChanceEffectHandler handler = CreateHandler(
            new[] { relic },
            calls,
            chance =>
            {
                calls.Add($"roll:{chance}");
                return true;
            });

        Assert.That(handler.TryWaiveReloadTurn(), Is.True);
        Assert.That(calls, Is.EqualTo(new[]
        {
            "probability:37",
            "roll:37",
            "trigger:EmptyBeat"
        }));
    }

    [Test]
    public void FailedChance_ReturnsDefaultWithoutTriggering()
    {
        RelicInstance spur = CreateInstance(
            "running-spur-failure",
            RelicEffectType.RunningSpur,
            primerChance: 25d);
        List<string> calls = new List<string>();
        RelicChanceEffectHandler handler = CreateHandler(
            new[] { spur },
            calls,
            _ => false);

        Assert.That(handler.TryWaiveMovementTurn(), Is.False);
        Assert.That(calls, Is.EqualTo(new[] { "probability:25" }));
    }

    [Test]
    public void GoldPanner_UsesGoldChanceAndAuthoredMultiplier()
    {
        RelicInstance panner = CreateInstance(
            "gold-panner-handler",
            RelicEffectType.GoldPanner,
            goldChance: 64d,
            nuggetsRequired: 7);
        List<string> calls = new List<string>();
        RelicChanceEffectHandler handler = CreateHandler(
            new[] { panner },
            calls,
            chance => chance == 64d);

        Assert.That(handler.GetEnemyGoldDropMultiplier(), Is.EqualTo(7));
        Assert.That(calls, Is.EqualTo(new[]
        {
            "probability:64",
            "trigger:GoldPanner"
        }));
    }

    [Test]
    public void CrackedPrimer_SkipsSpentRelicAndUsesFirstActiveMatch()
    {
        RelicInstance spent = CreateInstance(
            "spent-primer",
            RelicEffectType.CrackedPrimer,
            RelicLifetimeType.Consumable,
            primerChance: 10d);
        Assert.That(spent.TryConsumeCharge(), Is.True);
        RelicInstance active = CreateInstance(
            "active-primer",
            RelicEffectType.CrackedPrimer,
            primerChance: 80d);
        RelicInstance later = CreateInstance(
            "later-primer",
            RelicEffectType.CrackedPrimer,
            primerChance: 100d);
        List<string> calls = new List<string>();
        RelicChanceEffectHandler handler = CreateHandler(
            new[] { spent, active, later },
            calls,
            chance => chance == 80d);

        Assert.That(handler.TryReuseFiredBullet(), Is.True);
        Assert.That(calls, Is.EqualTo(new[]
        {
            "probability:80",
            "trigger:CrackedPrimer"
        }));
    }

    private RelicChanceEffectHandler CreateHandler(
        IReadOnlyList<RelicInstance> relics,
        List<string> calls,
        System.Func<double, bool> roll)
    {
        return new RelicChanceEffectHandler(
            relics,
            (_, chance) => calls.Add($"probability:{chance}"),
            roll,
            (_, effect) => calls.Add($"trigger:{effect.EffectType}"));
    }

    private RelicInstance CreateInstance(
        string id,
        RelicEffectType effectType,
        RelicLifetimeType lifetime = RelicLifetimeType.RunPersistent,
        double primerChance = 0d,
        double goldChance = 0d,
        int nuggetsRequired = 1)
    {
        RelicData data = ScriptableObject.CreateInstance<RelicData>();
        data.name = id;
        createdRelics.Add(data);

        SerializedObject serialized = new SerializedObject(data);
        serialized.FindProperty("relicId").stringValue = id;
        serialized.FindProperty("lifetimeType").enumValueIndex =
            (int)lifetime;
        serialized.FindProperty("initialCharges").intValue = 1;
        SerializedProperty effects = serialized.FindProperty("effects");
        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("effectType").intValue = (int)effectType;
        effect.FindPropertyRelative("primerBaseChance").doubleValue =
            primerChance;
        effect.FindPropertyRelative("goldNuggetChance").doubleValue =
            goldChance;
        effect.FindPropertyRelative("nuggetsRequired").intValue =
            nuggetsRequired;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return new RelicInstance(data, createdRelics.Count - 1);
    }
}
