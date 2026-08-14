#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RelicManagerTests
{
    private GameObject gameObject;
    private RelicManager manager;
    private readonly List<RelicData> createdRelics =
        new List<RelicData>();

    [SetUp]
    public void SetUp()
    {
        gameObject = new GameObject("Relic Manager Test");
        manager = gameObject.AddComponent<RelicManager>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (RelicData relic in createdRelics)
        {
            Object.DestroyImmediate(relic);
        }

        createdRelics.Clear();
        Object.DestroyImmediate(gameObject);
    }

    [Test]
    public void Inventory_AllowsEightRelicsAndRejectsNinth()
    {
        for (int index = 0; index < RelicManager.MaximumRelicCount; index++)
        {
            RelicData relic = CreateRelic($"relic-{index}");
            Assert.That(
                manager.TryAcquire(relic),
                Is.EqualTo(RelicAcquireResult.Acquired));
        }

        Assert.That(manager.Count, Is.EqualTo(8));
        Assert.That(
            manager.TryAcquire(CreateRelic("relic-ninth")),
            Is.EqualTo(RelicAcquireResult.InventoryFull));
    }

    [Test]
    public void ConsumableLethalGuard_PreventsOneDeathThenIsDestroyed()
    {
        RelicData guard = CreateRelic(
            "last-chance",
            RelicLifetimeType.Consumable,
            RelicEffectType.PreventLethalDamage);
        manager.TryAcquire(guard);

        Assert.That(
            manager.TryPreventLethalDamage(100, 30, out int health),
            Is.True);
        Assert.That(health, Is.EqualTo(1));
        Assert.That(manager.Count, Is.Zero);
        Assert.That(
            manager.TryPreventLethalDamage(100, 1, out _),
            Is.False);
    }

    [Test]
    public void MovementMultiplier_CountsNormalAndPositionSwapDistance()
    {
        RelicData movementRelic = CreateRelic(
            "running-spur",
            RelicLifetimeType.RunPersistent,
            RelicEffectType.MovementDamageMultiplier,
            1.1d);
        manager.TryAcquire(movementRelic);

        manager.RecordPlayerMovement(new PlayerMovementContext(
            0,
            2,
            2,
            PlayerMovementSource.NormalMove));
        manager.RecordPlayerMovement(new PlayerMovementContext(
            2,
            5,
            3,
            PlayerMovementSource.BulletPositionSwap));

        Assert.That(manager.OwnedRelics[0].MovementStacks, Is.EqualTo(5));
        Assert.That(
            manager.GetOutgoingAttackDamageMultiplier(),
            Is.EqualTo(System.Math.Pow(1.1d, 5)).Within(0.000001d));

        manager.NotifyShotStarted();
        manager.RecordPlayerMovement(new PlayerMovementContext(
            5,
            7,
            2,
            PlayerMovementSource.BulletPositionSwap));
        manager.NotifyShotCompleted();
        Assert.That(manager.OwnedRelics[0].MovementStacks, Is.EqualTo(2));
    }

    [Test]
    public void SaveRestore_PreservesChargesMovementAndOrder()
    {
        RelicData movementRelic = CreateRelic(
            "saved-movement",
            RelicLifetimeType.RunPersistent,
            RelicEffectType.MovementDamageMultiplier,
            1.2d,
            RelicMovementStackReset.Never);
        manager.TryAcquire(movementRelic);
        manager.RecordPlayerMovement(new PlayerMovementContext(
            1,
            4,
            3,
            PlayerMovementSource.BulletPositionSwap));

        List<RunRelicSaveData> saved = new List<RunRelicSaveData>();
        manager.CaptureRunState(saved);

        Assert.That(manager.RestoreRunState(
            saved,
            id => id == movementRelic.Id ? movementRelic : null), Is.True);
        Assert.That(manager.Count, Is.EqualTo(1));
        Assert.That(manager.OwnedRelics[0].MovementStacks, Is.EqualTo(3));
        Assert.That(manager.OwnedRelics[0].AcquisitionOrder, Is.Zero);
    }

    [Test]
    public void SaveRestore_PreservesExtendedRelicRuntimeState()
    {
        RelicData relicData = CreateRelic(
            "runtime-state",
            effectType: RelicEffectType.PredatorHolster);
        manager.TryAcquire(relicData);
        RelicInstance relic = manager.OwnedRelics[0];
        relic.SetPrimaryCounter(7);
        relic.SetSecondaryCounter(2);
        relic.SetStoredValue(37.5d);
        relic.SetRuntimeFlag(true);
        relic.AddTrackedBullet(11);
        relic.AddTrackedBullet(23);

        List<RunRelicSaveData> saved = new List<RunRelicSaveData>();
        manager.CaptureRunState(saved);

        Assert.That(manager.RestoreRunState(
            saved,
            id => id == relicData.Id ? relicData : null), Is.True);
        RelicInstance restored = manager.OwnedRelics[0];
        Assert.That(restored.PrimaryCounter, Is.EqualTo(7));
        Assert.That(restored.SecondaryCounter, Is.EqualTo(2));
        Assert.That(restored.StoredValue, Is.EqualTo(37.5d));
        Assert.That(restored.RuntimeFlag, Is.True);
        Assert.That(restored.RemoveTrackedBullet(11), Is.True);
        Assert.That(restored.RemoveTrackedBullet(23), Is.True);
    }

    [Test]
    public void Carriage_EarnsAndConsumesStoredFreeReloads()
    {
        RelicData carriage = CreateRelic(
            "carriage",
            effectType: RelicEffectType.Carriage);
        SetEffectInt(carriage, "movementTilesPerFreeReload", 5);
        SetEffectInt(carriage, "freeReloadStorageLimit", 2);
        manager.TryAcquire(carriage);
        manager.BeginBattle();

        manager.RecordPlayerMovement(new PlayerMovementContext(
            0,
            12,
            12,
            PlayerMovementSource.BulletPositionSwap));

        RelicInstance instance = manager.OwnedRelics[0];
        Assert.That(instance.PrimaryCounter, Is.EqualTo(2));
        Assert.That(instance.SecondaryCounter, Is.EqualTo(2));
        Assert.That(manager.ShouldReloadConsumeTurn(
            new BulletInstance(null, 1),
            false), Is.False);
        Assert.That(instance.SecondaryCounter, Is.EqualTo(1));
    }

    [Test]
    public void GoldPanner_ConsumesNuggetsForCriticalFiveTimesShot()
    {
        RelicData panner = CreateRelic(
            "gold-panner",
            effectType: RelicEffectType.GoldPanner);
        SetEffectDouble(panner, "goldNuggetChance", 100d);
        SetEffectInt(panner, "nuggetsRequired", 3);
        SetEffectDouble(panner, "finalDamageMultiplier", 5d);
        manager.TryAcquire(panner);
        manager.BeginBattle();
        manager.NotifyGoldGained(3);
        manager.NotifyCylinderStarted(1, null, 100, 100);

        manager.NotifyShotStarted(true, false, 0, 100, 100);

        Assert.That(manager.CurrentShotForcesCritical, Is.True);
        Assert.That(manager.GetOutgoingAttackDamageMultiplier(),
            Is.EqualTo(5d));
        Assert.That(manager.OwnedRelics[0].PrimaryCounter, Is.Zero);
    }

    [Test]
    public void Scale_AppliesPreviousCylinderHealthLossToNextCylinder()
    {
        RelicData scale = CreateRelic(
            "scale",
            effectType: RelicEffectType.Scale);
        SetEffectDouble(scale, "scaleMaximumDamagePercent", 100d);
        manager.TryAcquire(scale);
        manager.BeginBattle();
        manager.NotifyCylinderStarted(1, null, 100, 100);
        manager.NotifyPlayerHealthLost(25, 100);
        manager.NotifyCylinderCompleted();

        manager.NotifyCylinderStarted(1, null, 75, 100);
        manager.NotifyShotStarted(true, false, 0, 75, 100);

        Assert.That(manager.GetOutgoingAttackDamageMultiplier(),
            Is.EqualTo(1.25d).Within(0.000001d));
    }

    [Test]
    public void FamilyWill_AddsMemorialShotFromDestroyedBullets()
    {
        RelicData will = CreateRelic(
            "family-will",
            effectType: RelicEffectType.FamilyWill);
        SetEffectDouble(will, "memorialDamagePercentPerBullet", 20d);
        SetEffectDouble(will, "memorialMaximumDamagePercent", 100d);
        manager.TryAcquire(will);
        manager.NotifyBulletDestroyed(new BulletInstance(null, 4));
        manager.NotifyBulletDestroyed(new BulletInstance(null, 8));
        manager.BeginBattle();

        manager.NotifyCylinderStarted(1, null, 100, 100);

        Assert.That(manager.GetMemorialExtraShotMultiplier(),
            Is.EqualTo(0.4d).Within(0.000001d));
    }

    [Test]
    public void ConditionalDamageEffects_ApplyOnlyToFirstOrLastShot()
    {
        manager.TryAcquire(CreateRelic(
            "first",
            effectType: RelicEffectType.FirstShotFinalMultiplier,
            amount: 2d));
        manager.TryAcquire(CreateRelic(
            "last",
            effectType: RelicEffectType.LastShotFinalMultiplier,
            amount: 2.5d));

        Assert.That(
            manager.GetConditionalFinalDamageMultiplier(true, false),
            Is.EqualTo(2d).Within(0.000001d));
        Assert.That(
            manager.GetConditionalFinalDamageMultiplier(false, true),
            Is.EqualTo(2.5d).Within(0.000001d));
        Assert.That(
            manager.GetConditionalFinalDamageMultiplier(true, true),
            Is.EqualTo(5d).Within(0.000001d));
        Assert.That(
            manager.OwnedRelics[0].Data.BuildEffectSummary(),
            Does.Contain("첫 사격 최종 피해"));
    }

    [Test]
    public void UniformRewardChoices_ExcludeOwnedAndNeverRepeat()
    {
        RelicData owned = CreateRelic("owned");
        RelicData availableA = CreateRelic("available-a");
        RelicData availableB = CreateRelic("available-b");
        SerializedObject serializedManager = new SerializedObject(manager);
        SerializedProperty catalog =
            serializedManager.FindProperty("relicCatalog");
        catalog.arraySize = 3;
        catalog.GetArrayElementAtIndex(0).objectReferenceValue = owned;
        catalog.GetArrayElementAtIndex(1).objectReferenceValue = availableA;
        catalog.GetArrayElementAtIndex(2).objectReferenceValue = availableB;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
        manager.TryAcquire(owned);

        List<RelicData> choices = new List<RelicData>();
        manager.GetUniformRewardChoices(100, choices);
        HashSet<string> ids = new HashSet<string>();

        foreach (RelicData choice in choices)
        {
            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.Id, Is.Not.EqualTo(owned.Id));
            Assert.That(ids.Add(choice.Id), Is.True);
        }

        Assert.That(ids, Does.Contain(availableA.Id));
        Assert.That(ids, Does.Contain(availableB.Id));
    }

    private RelicData CreateRelic(
        string id,
        RelicLifetimeType lifetime = RelicLifetimeType.RunPersistent,
        RelicEffectType effectType = RelicEffectType.None,
        double amount = 1.1d,
        RelicMovementStackReset reset = RelicMovementStackReset.AfterShot)
    {
        RelicData relic = ScriptableObject.CreateInstance<RelicData>();
        relic.name = id;
        createdRelics.Add(relic);

        SerializedObject serialized = new SerializedObject(relic);
        serialized.FindProperty("relicId").stringValue = id;
        serialized.FindProperty("lifetimeType").enumValueIndex = (int)lifetime;
        serialized.FindProperty("initialCharges").intValue = 1;

        if (effectType != RelicEffectType.None)
        {
            SerializedProperty effects = serialized.FindProperty("effects");
            effects.arraySize = 1;
            SerializedProperty effect = effects.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("effectType").intValue =
                (int)effectType;
            effect.FindPropertyRelative("finalDamageMultiplier").doubleValue =
                effectType == RelicEffectType.FirstShotFinalMultiplier
                    || effectType == RelicEffectType.LastShotFinalMultiplier
                        ? amount
                        : 2d;
            effect.FindPropertyRelative(
                "movementDamageMultiplierPerStack").doubleValue =
                    effectType == RelicEffectType.MovementDamageMultiplier
                        ? amount
                        : 1.1d;
            effect.FindPropertyRelative("survivingHealth").intValue = 1;
            effect.FindPropertyRelative("movementSources").intValue =
                (int)(PlayerMovementSource.NormalMove
                    | PlayerMovementSource.BulletPositionSwap);
            effect.FindPropertyRelative("movementStackReset").enumValueIndex =
                (int)reset;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return relic;
    }

    private static void SetEffectInt(
        RelicData relic,
        string propertyName,
        int value)
    {
        SerializedObject serialized = new SerializedObject(relic);
        serialized.FindProperty("effects")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative(propertyName)
            .intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEffectDouble(
        RelicData relic,
        string propertyName,
        double value)
    {
        SerializedObject serialized = new SerializedObject(relic);
        serialized.FindProperty("effects")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative(propertyName)
            .doubleValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
