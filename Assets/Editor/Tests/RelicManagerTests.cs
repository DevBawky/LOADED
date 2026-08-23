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
    public void RequestedRelics_AreAuthoredWithActiveAbilities()
    {
        string[] activeRelicIds =
        {
            "predator_holster",
            "closed_circuit",
            "infectious_incubator",
            "running_spur",
            "carriage",
            "gold_panner",
            "cracked_primer",
            "scale",
            "family_will"
        };

        foreach (string relicId in activeRelicIds)
        {
            RelicData relic = manager.ResolveRelicData(relicId);

            Assert.That(relic, Is.Not.Null, relicId);
            Assert.That(relic.Effects, Is.Not.Empty, relicId);
            Assert.That(relic.Description, Does.Not.Contain("효과 없음"), relicId);
        }
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
            manager.GetRelicStatusText(manager.OwnedRelics[0]),
            Is.EqualTo("5"));
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
            effectType: RelicEffectType.FamilyWill);
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
    public void Carriage_IncreasesKickDamageByFiftyPercent()
    {
        RelicData carriage = CreateRelic(
            "carriage",
            effectType: RelicEffectType.Carriage);
        SetEffectDouble(carriage, "finalDamageMultiplier", 1.5d);
        manager.TryAcquire(carriage);
        int activationCount = 0;
        manager.RelicTriggered += (_, effect) =>
        {
            if (effect.EffectType == RelicEffectType.Carriage)
            {
                activationCount++;
            }
        };

        Assert.That(
            manager.GetKickDamageMultiplier(),
            Is.EqualTo(1.5d).Within(0.000001d));
        Assert.That(activationCount, Is.EqualTo(1));
    }

    [Test]
    public void PredatorHolster_BuffsNextReloadedBullet()
    {
        GameObject enemyObject = new GameObject("Holster Enemy Test");

        try
        {
            EnemyController enemy = enemyObject.AddComponent<EnemyController>();
            RelicData holster = CreateRelic(
                "predator-holster",
                effectType: RelicEffectType.PredatorHolster);
            SetEffectDouble(holster, "finalDamageMultiplier", 1.12d);
            manager.TryAcquire(holster);
            BulletInstance reloadedBullet = new BulletInstance(null, 17);

            manager.NotifyEnemyDefeated(enemy, null, null, null);
            Assert.That(manager.ShouldReloadConsumeTurn(
                reloadedBullet,
                true), Is.True);
            Assert.That(
                manager.OwnedRelics[0].HasTrackedBullet(
                    reloadedBullet.AcquisitionOrder),
                Is.True);

            manager.NotifyCylinderStarted(1, null, 100, 100);
            manager.NotifyShotStarted(
                true,
                false,
                0,
                100,
                100,
                true,
                true,
                reloadedBullet.AcquisitionOrder);
            Assert.That(
                manager.GetOutgoingAttackDamageMultiplier(),
                Is.EqualTo(1.12d).Within(0.000001d));
        }
        finally
        {
            Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void PredatorHolster_ConsecutiveKillsQueueOnlyOneBullet()
    {
        GameObject firstEnemyObject = new GameObject("First Holster Enemy");
        GameObject secondEnemyObject = new GameObject("Second Holster Enemy");

        try
        {
            RelicData holster = CreateRelic(
                "single-predator-holster",
                effectType: RelicEffectType.PredatorHolster);
            manager.TryAcquire(holster);
            RelicInstance relic = manager.OwnedRelics[0];

            manager.NotifyEnemyDefeated(
                firstEnemyObject.AddComponent<EnemyController>(),
                null,
                null,
                null);
            manager.NotifyEnemyDefeated(
                secondEnemyObject.AddComponent<EnemyController>(),
                null,
                null,
                null);

            Assert.That(relic.PrimaryCounter, Is.EqualTo(1));
            Assert.That(manager.GetRelicStatusText(relic), Is.Empty);

            BulletInstance firstReloaded = new BulletInstance(null, 31);
            BulletInstance secondReloaded = new BulletInstance(null, 32);
            manager.ShouldReloadConsumeTurn(firstReloaded, true);
            manager.ShouldReloadConsumeTurn(secondReloaded, false);

            Assert.That(relic.HasTrackedBullet(31), Is.True);
            Assert.That(relic.HasTrackedBullet(32), Is.False);
            Assert.That(
                relic.TrackedBulletAcquisitionOrders.Count,
                Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(firstEnemyObject);
            Object.DestroyImmediate(secondEnemyObject);
        }
    }

    [Test]
    public void PredatorHolster_RestoreNormalizesLegacyQueuedStacks()
    {
        RelicData holster = CreateRelic(
            "legacy-predator-holster",
            effectType: RelicEffectType.PredatorHolster);
        manager.TryAcquire(holster);
        RelicInstance relic = manager.OwnedRelics[0];
        relic.SetPrimaryCounter(7);
        relic.AddTrackedBullet(11);
        relic.AddTrackedBullet(23);

        List<RunRelicSaveData> saved = new List<RunRelicSaveData>();
        manager.CaptureRunState(saved);

        Assert.That(manager.RestoreRunState(
            saved,
            id => id == holster.Id ? holster : null), Is.True);
        RelicInstance restored = manager.OwnedRelics[0];
        Assert.That(restored.PrimaryCounter, Is.Zero);
        Assert.That(
            restored.TrackedBulletAcquisitionOrders,
            Is.EqualTo(new[] { 11 }));
    }

    [Test]
    public void GoldPanner_MultipliesEnemyGoldDrop()
    {
        RelicData panner = CreateRelic(
            "gold-panner",
            effectType: RelicEffectType.GoldPanner);
        SetEffectDouble(panner, "goldNuggetChance", 100d);
        SetEffectInt(panner, "nuggetsRequired", 7);
        manager.TryAcquire(panner);
        double evaluatedChance = -1d;
        manager.RelicProbabilityEvaluated += (_, chance) =>
            evaluatedChance = chance;

        Assert.That(manager.GetEnemyGoldDropMultiplier(), Is.EqualTo(7));
        Assert.That(evaluatedChance, Is.EqualTo(100d));
    }

    [Test]
    public void Scale_UsesLivingEnemyCountFormula()
    {
        RelicData scale = CreateRelic(
            "scale",
            effectType: RelicEffectType.Scale);
        SetEffectDouble(scale, "scaleMaximumDamagePercent", 15d);
        SetEffectDouble(scale, "primerFailureChanceBonus", 3d);
        manager.TryAcquire(scale);

        Assert.That(
            manager.GetPreviewTargetConditionalDamageMultiplier(0, 1),
            Is.EqualTo(1.12d).Within(0.000001d));
        Assert.That(
            manager.GetPreviewTargetConditionalDamageMultiplier(0, 5),
            Is.EqualTo(1d).Within(0.000001d));
    }

    [Test]
    public void FamilyWill_AddsFivePercentPerBossDefeated()
    {
        RelicData will = CreateRelic(
            "family-will",
            effectType: RelicEffectType.FamilyWill);
        SetEffectDouble(will, "memorialDamagePercentPerBullet", 5d);
        manager.TryAcquire(will);
        manager.OwnedRelics[0].SetPrimaryCounter(2);

        Assert.That(
            manager.GetOutgoingAttackDamageMultiplier(),
            Is.EqualTo(1.1d).Within(0.000001d));
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

        Assert.That(manager.TryGetLoadedBulletRelicModifiers(
            5,
            6,
            6,
            out double firstMultiplier,
            out _), Is.True);
        Assert.That(firstMultiplier, Is.EqualTo(2d).Within(0.000001d));
        Assert.That(manager.TryGetLoadedBulletRelicModifiers(
            0,
            6,
            6,
            out double lastMultiplier,
            out _), Is.True);
        Assert.That(lastMultiplier, Is.EqualTo(2.5d).Within(0.000001d));
    }

    [Test]
    public void ConditionalDamageEffects_RaiseShotActivationEventsOnce()
    {
        manager.TryAcquire(CreateRelic(
            "first-activation",
            effectType: RelicEffectType.FirstShotFinalMultiplier,
            amount: 2d));
        manager.TryAcquire(CreateRelic(
            "last-activation",
            effectType: RelicEffectType.LastShotFinalMultiplier,
            amount: 2.5d));
        Dictionary<RelicEffectType, int> activations =
            new Dictionary<RelicEffectType, int>();
        manager.RelicTriggered += (_, effect) =>
        {
            activations.TryGetValue(effect.EffectType, out int count);
            activations[effect.EffectType] = count + 1;
        };

        manager.NotifyCylinderStarted(1, null, 100, 100);
        manager.NotifyShotStarted(true, false, 0, 100, 100, true, true);

        Assert.That(
            activations[RelicEffectType.FirstShotFinalMultiplier],
            Is.EqualTo(1));
        Assert.That(
            activations[RelicEffectType.LastShotFinalMultiplier],
            Is.EqualTo(1));
    }

    [Test]
    public void ExecutionersOath_RaisesActivationWhenStreakAddsDamage()
    {
        manager.TryAcquire(CreateRelic(
            "execution-oath",
            effectType: RelicEffectType.ExecutionersOath,
            amount: 1.15d));
        manager.OwnedRelics[0].SetPrimaryCounter(1);
        Assert.That(
            manager.GetRelicStatusText(manager.OwnedRelics[0]),
            Is.Empty);
        Assert.That(manager.TryGetLoadedBulletRelicModifiers(
            5,
            6,
            6,
            out double oathMultiplier,
            out _), Is.True);
        Assert.That(oathMultiplier, Is.EqualTo(1.15d).Within(0.000001d));
        int activationCount = 0;
        manager.RelicTriggered += (_, effect) =>
        {
            if (effect.EffectType == RelicEffectType.ExecutionersOath)
            {
                activationCount++;
            }
        };

        manager.NotifyCylinderStarted(1, null, 100, 100);
        manager.NotifyShotStarted(true, false, 0, 100, 100, true, true);

        Assert.That(activationCount, Is.EqualTo(1));
    }

    [Test]
    public void ExecutionersOath_FinalBattleKillStillRaisesStage()
    {
        manager.TryAcquire(CreateRelic(
            "execution-final-kill",
            effectType: RelicEffectType.ExecutionersOath));
        GameObject enemyObject = new GameObject("Defeated Enemy");
        EnemyController enemy = enemyObject.AddComponent<EnemyController>();

        try
        {
            manager.NotifyCylinderStarted(1, null, 100, 100);
            manager.NotifyShotStarted(true, false, 0, 100, 100, true, true);
            manager.NotifyEnemyDefeated(enemy, null, null, null);
            manager.EndBattle();
            manager.NotifyShotCompleted();

            Assert.That(manager.OwnedRelics[0].PrimaryCounter, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void BattleBoundary_PreservesPersistentRelicCounters()
    {
        RelicData spur = CreateRelic(
            "persistent-spur",
            effectType: RelicEffectType.MovementDamageMultiplier);
        manager.TryAcquire(spur);
        manager.RecordPlayerMovement(new PlayerMovementContext(
            0,
            4,
            4,
            PlayerMovementSource.NormalMove));
        RelicData oath = CreateRelic(
            "persistent-oath",
            effectType: RelicEffectType.ExecutionersOath);
        manager.TryAcquire(oath);
        manager.OwnedRelics[1].SetPrimaryCounter(2);

        manager.EndBattle();
        manager.BeginBattle();

        Assert.That(manager.OwnedRelics[0].MovementStacks, Is.EqualTo(4));
        Assert.That(manager.OwnedRelics[1].PrimaryCounter, Is.EqualTo(2));
    }

    [Test]
    public void NonStackingRelicStatus_DoesNotShowPrimerChance()
    {
        RelicData primer = CreateRelic(
            "primer-status",
            effectType: RelicEffectType.CrackedPrimer);
        SetEffectDouble(primer, "primerBaseChance", 10d);
        manager.TryAcquire(primer);
        manager.OwnedRelics[0].SetPrimaryCounter(3);

        Assert.That(
            manager.GetRelicStatusText(manager.OwnedRelics[0]),
            Is.Empty);
    }

    [Test]
    public void AdvancedScope_AddsOneToEveryBulletRange()
    {
        RelicData scope = CreateRelic(
            "advanced-scope",
            effectType: RelicEffectType.AdvancedScope);
        SetEffectInt(scope, "shotRangeBonus", 1);
        manager.TryAcquire(scope);

        Assert.That(
            manager.GetShotRange(new BulletInstance(null, 1)),
            Is.EqualTo(2));
    }

    [Test]
    public void FreeTurnRelics_UseAuthoredPercentages()
    {
        RelicData emptyBeat = CreateRelic(
            "empty-beat",
            effectType: RelicEffectType.EmptyBeat);
        SetEffectDouble(emptyBeat, "primerBaseChance", 100d);
        manager.TryAcquire(emptyBeat);

        Assert.That(
            manager.ShouldReloadConsumeTurn(
                new BulletInstance(null, 1),
                false),
            Is.False);

        manager.TryRemoveAt(0);
        RelicData spur = CreateRelic(
            "running-spur",
            effectType: RelicEffectType.RunningSpur);
        SetEffectDouble(spur, "primerBaseChance", 100d);
        manager.TryAcquire(spur);

        Assert.That(manager.ShouldMovementConsumeTurn(), Is.False);
    }

    [Test]
    public void ClosedCircuit_TransfersTwentyPercentDamage()
    {
        RelicData circuit = CreateRelic(
            "closed-circuit",
            effectType: RelicEffectType.ClosedCircuit);
        SetEffectDouble(circuit, "debuffTransferPercent", 20d);
        manager.TryAcquire(circuit);

        Assert.That(
            manager.TryGetPreviewClosedCircuitTransferDamage(
                100,
                out int damage),
            Is.True);
        Assert.That(damage, Is.EqualTo(20));
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

    [Test]
    public void LuckyChamber_ExposesSelectedCylinderIndexAndTooltip()
    {
        RelicData luckyChamber = CreateRelic(
            "lucky-chamber",
            effectType: RelicEffectType.LuckyChamber);
        manager.TryAcquire(luckyChamber);
        Random.State previousState = Random.state;

        try
        {
            Random.InitState(20260816);
            int selectionChangedCount = 0;
            manager.LuckyChamberSelectionChanged += () =>
                selectionChangedCount++;
            manager.NotifyCylinderStarted(6, null, 100, 100);

            int selectedIndex = manager.LuckyChamberBulletIndex;
            Assert.That(selectedIndex, Is.InRange(0, 5));
            Assert.That(manager.IsLuckyChamberShot(selectedIndex), Is.True);
            Assert.That(
                manager.IsLuckyChamberLoadedBullet(5 - selectedIndex, 6),
                Is.True);
            Assert.That(
                manager.GetLuckyChamberBulletTooltip(),
                Does.Contain("행운의 약실"));
            Assert.That(
                manager.GetRelicStatusText(manager.OwnedRelics[0]),
                Is.Empty);
            Assert.That(manager.TryGetLoadedBulletRelicModifiers(
                5 - selectedIndex,
                6,
                6,
                out double luckyMultiplier,
                out _), Is.True);
            Assert.That(luckyMultiplier, Is.EqualTo(2d).Within(0.000001d));
            Assert.That(selectionChangedCount, Is.EqualTo(1));
        }
        finally
        {
            Random.state = previousState;
        }
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
                    || effectType == RelicEffectType.ExecutionersOath
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

            if (effectType == RelicEffectType.ExecutionersOath)
            {
                SerializedProperty multipliers = effect.FindPropertyRelative(
                    "executionDamageMultipliers");
                double[] defaultMultipliers = { 1.5d, 2d, 3d, 5d };
                multipliers.arraySize = defaultMultipliers.Length;

                for (int index = 0;
                     index < defaultMultipliers.Length;
                     index++)
                {
                    multipliers.GetArrayElementAtIndex(index).doubleValue =
                        defaultMultipliers[index];
                }
            }
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
