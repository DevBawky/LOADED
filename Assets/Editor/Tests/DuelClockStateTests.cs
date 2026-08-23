using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DuelClockStateTests
{
    [Test]
    public void NewStateStartsAtZero()
    {
        DuelClockState state = new DuelClockState();

        Assert.That(state.Snapshot.Progress, Is.Zero);
        Assert.That(state.Snapshot.CumulativeBeats, Is.Zero);
    }

    [Test]
    public void CommitCarriesOverflowIntoCumulativeBeats()
    {
        DuelClockState state = new DuelClockState();
        state.Commit(80d);

        DuelClockAdvanceResult result = state.Commit(50d);

        Assert.That(result.Before.Progress, Is.EqualTo(80d));
        Assert.That(result.TriggeredBeatCount, Is.EqualTo(1));
        Assert.That(result.After.Progress, Is.EqualTo(30d));
        Assert.That(result.After.CumulativeBeats, Is.EqualTo(1));
        Assert.That(state.Snapshot.Progress, Is.EqualTo(30d));
    }

    [Test]
    public void CommitAtExactCycleBoundaryTriggersOneBeat()
    {
        DuelClockState state = new DuelClockState();

        DuelClockAdvanceResult result = state.Commit(
            DuelClockState.CycleLength);

        Assert.That(result.TriggeredBeatCount, Is.EqualTo(1));
        Assert.That(result.After.Progress, Is.Zero);
        Assert.That(result.After.CumulativeBeats, Is.EqualTo(1));
    }

    [Test]
    public void CommitCanTriggerMultipleBeatsAtOnce()
    {
        DuelClockState state = new DuelClockState();
        state.Commit(80d);

        DuelClockAdvanceResult result = state.Commit(250d);

        Assert.That(result.TriggeredBeatCount, Is.EqualTo(3));
        Assert.That(result.After.Progress, Is.EqualTo(30d));
        Assert.That(result.After.CumulativeBeats, Is.EqualTo(3));
    }

    [Test]
    public void LargeFiniteCommitKeepsProgressWithinCycle()
    {
        DuelClockState state = new DuelClockState();

        DuelClockAdvanceResult result = state.Commit(
            1_000_000_000_000_000_100d);

        Assert.That(result.After.Progress, Is.GreaterThanOrEqualTo(0d));
        Assert.That(result.After.Progress,
            Is.LessThan(DuelClockState.CycleLength));
        Assert.That(state.Snapshot.Progress,
            Is.EqualTo(result.After.Progress));
    }

    [Test]
    public void PreviewMatchesCommitWithoutMutatingState()
    {
        DuelClockState state = new DuelClockState();
        state.Commit(80d);

        DuelClockAdvanceResult preview = state.Preview(50d);

        Assert.That(state.Snapshot.Progress, Is.EqualTo(80d));
        Assert.That(state.Snapshot.CumulativeBeats, Is.Zero);

        DuelClockAdvanceResult committed = state.Commit(50d);

        Assert.That(committed.After.Progress,
            Is.EqualTo(preview.After.Progress));
        Assert.That(committed.After.CumulativeBeats,
            Is.EqualTo(preview.After.CumulativeBeats));
        Assert.That(committed.TriggeredBeatCount,
            Is.EqualTo(preview.TriggeredBeatCount));
    }

    [Test]
    public void RestoreNormalizesProgressBeyondOneCycle()
    {
        DuelClockState state = DuelClockState.Restore(250d, 7);

        Assert.That(state.Snapshot.Progress, Is.EqualTo(50d));
        Assert.That(state.Snapshot.CumulativeBeats, Is.EqualTo(9));
    }

    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void ProgressOperationsRejectInvalidInput(double invalidProgress)
    {
        DuelClockState state = new DuelClockState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => state.Preview(invalidProgress));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => state.Commit(invalidProgress));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DuelClockState.Restore(invalidProgress, 0));
    }

    [Test]
    public void RestoreRejectsNegativeCumulativeBeats()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DuelClockState.Restore(0d, -1));
    }

    [Test]
    public void CommitRejectsCumulativeBeatOverflow()
    {
        DuelClockState state = DuelClockState.Restore(0d, long.MaxValue);

        Assert.Throws<OverflowException>(
            () => state.Commit(DuelClockState.CycleLength));
        Assert.That(state.Snapshot.Progress, Is.Zero);
        Assert.That(state.Snapshot.CumulativeBeats,
            Is.EqualTo(long.MaxValue));
    }
}

public sealed class BattleDataCombatPacingTests
{
    private BattleData battleData;

    [SetUp]
    public void SetUp()
    {
        battleData = ScriptableObject.CreateInstance<BattleData>();
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(battleData);
    }

    [Test]
    public void NewBattleDefaultsToLegacyPacing()
    {
        Assert.That((int)CombatPacingMode.Legacy, Is.Zero);
        Assert.That((int)CombatPacingMode.DuelClock, Is.EqualTo(1));
        Assert.That(battleData.PacingMode,
            Is.EqualTo(CombatPacingMode.Legacy));
    }

    [Test]
    public void NewBattleUsesPrototypeDuelClockValues()
    {
        Assert.That(battleData.DuelClockNaturalProgressPerSecond,
            Is.EqualTo(4f));
        Assert.That(battleData.DuelClockPaidActionProgress,
            Is.EqualTo(45f));
        Assert.That(battleData.DuelClockEnemyWaveCount,
            Is.EqualTo(5));
        Assert.That(DuelClockState.CycleLength, Is.EqualTo(100d));
    }
}

public sealed class WaveManagerActiveEnemyLimitTests
{
    [Test]
    public void ActiveEnemyLimitIsSix()
    {
        Assert.That(WaveManager.MaximumActiveEnemyCount, Is.EqualTo(6));
    }

    [TestCase(-1, 6)]
    [TestCase(0, 6)]
    [TestCase(5, 1)]
    [TestCase(6, 0)]
    [TestCase(9, 0)]
    public void AvailableSlotsNeverExceedConfiguredLimit(
        int livingEnemyCount,
        int expectedSlots)
    {
        Assert.That(
            WaveManager.CalculateAvailableEnemySlots(livingEnemyCount),
            Is.EqualTo(expectedSlots));
    }
}

public sealed class DuelClockBattleAssetTests
{
    [Test]
    public void EveryBattleUsesDuelClockAndFlattenedEnemyPool()
    {
        string[] battleGuids = AssetDatabase.FindAssets(
            "t:BattleData",
            new[] { "Assets/Scripts/Manager/Battle SO" });

        Assert.That(battleGuids, Has.Length.GreaterThan(0));

        foreach (string battleGuid in battleGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(battleGuid);
            BattleData battle = AssetDatabase.LoadAssetAtPath<BattleData>(
                assetPath);
            Assert.That(battle, Is.Not.Null, assetPath);
            int flattenedEnemyCount = 0;

            foreach (EnemyWave wave in battle.Waves)
            {
                foreach (EnemyWaveEntry entry in wave.Enemies)
                {
                    flattenedEnemyCount += entry.Count;
                }
            }

            Assert.That(battle.PacingMode,
                Is.EqualTo(CombatPacingMode.DuelClock), assetPath);
            Assert.That(battle.DuelClockEnemyPool.Count,
                Is.EqualTo(flattenedEnemyCount), assetPath);
            Assert.That(battle.DuelClockEnemyPool,
                Has.None.Null, assetPath);
        }
    }
}

public sealed class DuelClockControllerTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private readonly List<ScriptableObject> createdAssets =
        new List<ScriptableObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
        }

        foreach (ScriptableObject createdAsset in createdAssets)
        {
            if (createdAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(createdAsset);
            }
        }

        createdObjects.Clear();
        createdAssets.Clear();
    }

    [Test]
    public void PaidActionCommitsExactlyOnceAfterRepeatedInitialization()
    {
        PlayerMove playerMove = CreateComponent<PlayerMove>("Player");
        WaveManager waveManager = CreateComponent<WaveManager>("Wave");
        DuelClockController controller =
            waveManager.gameObject.AddComponent<DuelClockController>();
        BattleData battle = CreateDuelBattle(0f, 100f);
        int notificationCount = 0;
        long notifiedBeats = 0;
        controller.BeatsCommitted += beats =>
        {
            notificationCount++;
            notifiedBeats += beats;
        };
        controller.Initialize(playerMove, waveManager);
        controller.Initialize(playerMove, waveManager);
        controller.ConfigureFresh(battle, CombatPacingMode.DuelClock);

        playerMove.Wait();

        Assert.That(playerMove.TurnCount, Is.EqualTo(1));
        Assert.That(controller.Progress, Is.Zero);
        Assert.That(controller.CumulativeBeats, Is.EqualTo(1));
        Assert.That(notificationCount, Is.EqualTo(1));
        Assert.That(notifiedBeats, Is.EqualTo(1));
    }

    [Test]
    public void LegacyConfigurationDoesNotCommitPlayerActions()
    {
        PlayerMove playerMove = CreateComponent<PlayerMove>("Player");
        WaveManager waveManager = CreateComponent<WaveManager>("Wave");
        DuelClockController controller =
            waveManager.gameObject.AddComponent<DuelClockController>();
        BattleData battle = CreateAsset<BattleData>();
        int notificationCount = 0;
        controller.BeatsCommitted += _ => notificationCount++;
        controller.Initialize(playerMove, waveManager);
        controller.ConfigureFresh(battle, CombatPacingMode.Legacy);

        playerMove.Wait();

        Assert.That(playerMove.TurnCount, Is.EqualTo(1));
        Assert.That(controller.Progress, Is.Zero);
        Assert.That(controller.CumulativeBeats, Is.Zero);
        Assert.That(notificationCount, Is.Zero);
    }

    [Test]
    public void EmptyBoardUsesFullNaturalClockSpeed()
    {
        PlayerMove playerMove = CreateComponent<PlayerMove>("Player");
        WaveManager waveManager = CreateComponent<WaveManager>("Wave");
        DuelClockController controller =
            waveManager.gameObject.AddComponent<DuelClockController>();
        BattleData battle = CreateDuelBattle(4f, 45f);
        controller.Initialize(playerMove, waveManager);
        controller.ConfigureFresh(battle, CombatPacingMode.DuelClock);

        bool advanced = controller.TryAdvanceNaturalTime(1d);

        Assert.That(waveManager.ActiveEnemies, Is.Empty);
        Assert.That(advanced, Is.True);
        Assert.That(controller.Progress, Is.EqualTo(4d));
    }

    [Test]
    public void EnemyWaveProgressWrapsAtAuthoredBeatCount()
    {
        PlayerMove playerMove = CreateComponent<PlayerMove>("Player");
        WaveManager waveManager = CreateComponent<WaveManager>("Wave");
        DuelClockController controller =
            waveManager.gameObject.AddComponent<DuelClockController>();
        BattleData battle = CreateDuelBattle(0f, 100f, 5);
        controller.Initialize(playerMove, waveManager);
        controller.ConfigureFresh(battle, CombatPacingMode.DuelClock);

        for (int actionIndex = 0; actionIndex < 4; actionIndex++)
        {
            playerMove.Wait();
        }

        Assert.That(controller.EnemyWaveProgress, Is.EqualTo(4));
        Assert.That(controller.EnemyWaveCount, Is.EqualTo(5));

        playerMove.Wait();

        Assert.That(controller.EnemyWaveProgress, Is.Zero);
        Assert.That(controller.CumulativeBeats, Is.EqualTo(5));
    }

    [Test]
    public void CaptureRunStateCopiesTheReadOnlyClockSnapshot()
    {
        PlayerMove playerMove = CreateComponent<PlayerMove>("Player");
        WaveManager waveManager = CreateComponent<WaveManager>("Wave");
        DuelClockController controller =
            waveManager.gameObject.AddComponent<DuelClockController>();
        BattleData battle = CreateDuelBattle(4f, 45f);
        RunSaveData saveData = new RunSaveData();
        controller.Initialize(playerMove, waveManager);
        controller.ConfigureFresh(battle, CombatPacingMode.DuelClock);
        controller.TryAdvanceNaturalTime(1d);

        DuelClockSnapshot snapshot = controller.Snapshot;
        controller.CaptureRunState(saveData);

        Assert.That(saveData.combatPacingMode,
            Is.EqualTo((int)CombatPacingMode.DuelClock));
        Assert.That(saveData.duelClockProgress,
            Is.EqualTo(snapshot.Progress));
        Assert.That(saveData.duelClockCumulativeBeats,
            Is.EqualTo(snapshot.CumulativeBeats));
    }

    [Test]
    public void FreeActionPreviewAlwaysAddsZeroAndDoesNotMutateState()
    {
        PlayerMove playerMove = CreateComponent<PlayerMove>("Player");
        WaveManager waveManager = CreateComponent<WaveManager>("Wave");
        DuelClockController controller =
            waveManager.gameObject.AddComponent<DuelClockController>();
        BattleData battle = CreateDuelBattle(4f, 45f);
        controller.Initialize(playerMove, waveManager);
        controller.ConfigureFresh(battle, CombatPacingMode.DuelClock);
        controller.TryAdvanceNaturalTime(1d);

        DuelClockAdvanceResult preview = controller.PreviewFreeAction();

        Assert.That(preview.AddedProgress, Is.Zero);
        Assert.That(preview.Before.Progress, Is.EqualTo(4d));
        Assert.That(preview.After.Progress, Is.EqualTo(4d));
        Assert.That(controller.Progress, Is.EqualTo(4d));
    }

    [Test]
    public void NaturalClockPolicyStopsForGamePauseOrGuidePanel()
    {
        Assert.That(DuelClockController.ShouldAdvanceNaturalClock(
            true, true, false, true, true, false), Is.True);
        Assert.That(DuelClockController.ShouldAdvanceNaturalClock(
            true, true, true, true, true, false), Is.False);
        Assert.That(DuelClockController.ShouldAdvanceNaturalClock(
            true, true, false, true, true, false, true), Is.False);
    }

    [Test]
    public void PlayerBusyFlagsDoNotPauseNaturalClock()
    {
        PlayerMove playerMove = CreateComponent<PlayerMove>("Player");
        WaveManager waveManager = CreateComponent<WaveManager>("Wave");
        DuelClockController controller =
            waveManager.gameObject.AddComponent<DuelClockController>();
        BattleData battle = CreateDuelBattle(4f, 45f);
        controller.Initialize(playerMove, waveManager);
        controller.ConfigureFresh(battle, CombatPacingMode.DuelClock);

        playerMove.SetInputLocked(true);
        bool advancedWhileInputLocked =
            controller.TryAdvanceNaturalTime(1d);
        playerMove.SetInputLocked(false);

        playerMove.SetShooting(true);
        bool advancedWhileShooting =
            controller.TryAdvanceNaturalTime(1d);
        playerMove.SetShooting(false);

        playerMove.SetEnemyTurnResolving(true);
        bool advancedWhileEnemyResolving =
            controller.TryAdvanceNaturalTime(1d);
        playerMove.SetEnemyTurnResolving(false);

        System.Reflection.FieldInfo actingField = typeof(PlayerMove).GetField(
            "isActing",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        Assert.That(actingField, Is.Not.Null);
        actingField.SetValue(playerMove, true);
        bool advancedWhileActing = controller.TryAdvanceNaturalTime(1d);
        actingField.SetValue(playerMove, false);

        Assert.That(advancedWhileInputLocked, Is.True);
        Assert.That(advancedWhileShooting, Is.True);
        Assert.That(advancedWhileEnemyResolving, Is.True);
        Assert.That(advancedWhileActing, Is.True);
        Assert.That(controller.Progress, Is.EqualTo(16d));
    }

    [Test]
    public void StunDoesNotPauseNaturalClock()
    {
        GameObject playerObject = new GameObject("Stunned Player");
        createdObjects.Add(playerObject);
        StatusEffectController statusEffects =
            playerObject.AddComponent<StatusEffectController>();
        PlayerMove playerMove = playerObject.AddComponent<PlayerMove>();
        InvokePrivateLifecycle(playerMove, "Awake");
        statusEffects.Add(StatusEffectType.Stun, 1);
        WaveManager waveManager = CreateComponent<WaveManager>("Wave");
        DuelClockController controller =
            waveManager.gameObject.AddComponent<DuelClockController>();
        BattleData battle = CreateDuelBattle(4f, 45f);
        controller.Initialize(playerMove, waveManager);
        controller.ConfigureFresh(battle, CombatPacingMode.DuelClock);

        bool advanced = controller.TryAdvanceNaturalTime(1d);

        Assert.That(playerMove.CanStartAction, Is.False);
        Assert.That(advanced, Is.True);
        Assert.That(controller.Progress, Is.EqualTo(4d));
    }

    [Test]
    public void DuelClockEnemyResolutionDoesNotBlockPlayerActions()
    {
        PlayerMove playerMove = CreateComponent<PlayerMove>("Player");
        playerMove.SetDuelClockActive(true);
        playerMove.SetEnemyTurnResolving(true);

        Assert.That(playerMove.CanStartAction, Is.True);

        playerMove.SetDuelClockActive(false);

        Assert.That(playerMove.CanStartAction, Is.False);
    }

    [Test]
    public void RestoredSavedDuelModeOverridesCurrentAuthoredLegacyMode()
    {
        PlayerMove playerMove = CreateComponent<PlayerMove>("Player");
        WaveManager waveManager = CreateComponent<WaveManager>("Wave");
        DuelClockController controller =
            waveManager.gameObject.AddComponent<DuelClockController>();
        BattleData currentlyLegacyBattle = CreateAsset<BattleData>();
        RunSaveData saveData = new RunSaveData
        {
            combatPacingMode = (int)CombatPacingMode.DuelClock,
            duelClockProgress = 75d,
            duelClockCumulativeBeats = 3
        };
        controller.Initialize(playerMove, waveManager);

        controller.ConfigureRestored(
            currentlyLegacyBattle,
            CombatPacingMode.DuelClock,
            saveData);

        Assert.That(controller.IsActive, Is.True);
        Assert.That(controller.Progress, Is.EqualTo(75d));
        Assert.That(controller.CumulativeBeats, Is.EqualTo(3));
        Assert.That(controller.EnemyWaveProgress, Is.EqualTo(3));
        Assert.That(controller.EnemyWaveCount, Is.EqualTo(5));
        Assert.That(playerMove.CanStartAction, Is.True);
    }

    private BattleData CreateDuelBattle(
        float naturalProgress,
        float paidProgress,
        int enemyWaveCount = 5)
    {
        BattleData battle = CreateAsset<BattleData>();
        SerializedObject serializedBattle = new SerializedObject(battle);
        serializedBattle.FindProperty("combatPacingMode").enumValueIndex =
            (int)CombatPacingMode.DuelClock;
        serializedBattle.FindProperty("duelClockNaturalProgressPerSecond")
            .floatValue = naturalProgress;
        serializedBattle.FindProperty("duelClockPaidActionProgress")
            .floatValue = paidProgress;
        serializedBattle.FindProperty("duelClockEnemyWaveCount")
            .intValue = enemyWaveCount;
        serializedBattle.ApplyModifiedPropertiesWithoutUndo();
        return battle;
    }

    private T CreateComponent<T>(string objectName) where T : Component
    {
        GameObject gameObject = new GameObject(objectName);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    private T CreateAsset<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        createdAssets.Add(asset);
        return asset;
    }

    private static void InvokePrivateLifecycle(
        object target,
        string methodName)
    {
        System.Reflection.MethodInfo method = target.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
    }
}

public sealed class EnemyAttackActiveWindowTimingTests
{
    [Test]
    public void MissingEndEventUsesImmediateFallback()
    {
        AnimationClip clip = CreateOneSecondClip();
        clip.events = new[]
        {
            new AnimationEvent
            {
                functionName =
                    EnemyAttackAnimationEvents.BeginFunctionName,
                time = 0.25f
            }
        };

        try
        {
            Assert.That(
                EnemyAttackActiveWindowTiming.TryCreate(clip, out _),
                Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PairedEventsDefineInclusiveActiveWindow()
    {
        AnimationClip clip = CreateOneSecondClip();
        clip.events = new[]
        {
            new AnimationEvent
            {
                functionName =
                    EnemyAttackAnimationEvents.BeginFunctionName,
                time = 0.25f
            },
            new AnimationEvent
            {
                functionName =
                    EnemyAttackAnimationEvents.EndFunctionName,
                time = 0.5f
            }
        };

        try
        {
            bool created = EnemyAttackActiveWindowTiming.TryCreate(
                clip,
                out EnemyAttackActiveWindowTiming timing);

            Assert.That(created, Is.True);
            Assert.That(timing.Contains(0.24f), Is.False);
            Assert.That(timing.Contains(0.25f), Is.True);
            Assert.That(timing.Contains(0.5f), Is.True);
            Assert.That(timing.Contains(0.51f), Is.False);
            Assert.That(timing.Overlaps(0.2f, 0.3f), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clip);
        }
    }

    private static AnimationClip CreateOneSecondClip()
    {
        AnimationClip clip = new AnimationClip();
        clip.SetCurve(
            string.Empty,
            typeof(Transform),
            "m_LocalPosition.x",
            AnimationCurve.Linear(0f, 0f, 1f, 0f));
        return clip;
    }
}

public sealed class WaveManagerMovementReservationTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void ConflictingPathReservationFailsAtomically()
    {
        WaveManager waveManager = CreateComponent<WaveManager>("Wave Manager");
        BoxCollider2D firstOwner = CreateComponent<BoxCollider2D>("First Owner");
        BoxCollider2D secondOwner = CreateComponent<BoxCollider2D>("Second Owner");

        Assert.That(
            waveManager.TryReserveMovementTiles(firstOwner, new[] { 3, 4 }),
            Is.True);
        Assert.That(
            waveManager.TryReserveMovementTiles(secondOwner, new[] { 4, 5 }),
            Is.False);
        Assert.That(
            waveManager.IsTileReservedForMovement(3, firstOwner),
            Is.False);
        Assert.That(waveManager.IsTileReservedForMovement(4), Is.True);
        Assert.That(waveManager.IsTileReservedForMovement(5), Is.False);

        waveManager.ReleaseMovementTiles(firstOwner);

        Assert.That(
            waveManager.TryReserveMovementTiles(secondOwner, new[] { 4, 5 }),
            Is.True);
    }

    [Test]
    public void SwapReservationClaimsBothDestinationsTogether()
    {
        WaveManager waveManager = CreateComponent<WaveManager>("Wave Manager");
        BoxCollider2D playerOwner = CreateComponent<BoxCollider2D>("Player");
        BoxCollider2D enemyOwner = CreateComponent<BoxCollider2D>("Enemy");
        BoxCollider2D thirdOwner = CreateComponent<BoxCollider2D>("Third Actor");

        Assert.That(
            waveManager.TryReserveMovementSwap(
                playerOwner,
                8,
                enemyOwner,
                2),
            Is.True);
        Assert.That(
            waveManager.TryReserveMovementTile(thirdOwner, 8),
            Is.False);
        Assert.That(
            waveManager.TryReserveMovementTile(thirdOwner, 2),
            Is.False);

        waveManager.ReleaseMovementTiles(playerOwner);
        waveManager.ReleaseMovementTiles(enemyOwner);

        Assert.That(
            waveManager.TryReserveMovementTile(thirdOwner, 8),
            Is.True);
    }

    private T CreateComponent<T>(string objectName) where T : Component
    {
        GameObject gameObject = new GameObject(objectName);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}

public sealed class PlayerMoveDuelClockStatusTests
{
    private GameObject playerObject;

    [TearDown]
    public void TearDown()
    {
        if (playerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void DuelClockStatusBeatDecrementsEveryStatusWithoutCompletingTurn()
    {
        playerObject = new GameObject("Stunned Player");
        StatusEffectController statusEffects =
            playerObject.AddComponent<StatusEffectController>();
        PlayerMove playerMove = playerObject.AddComponent<PlayerMove>();
        InvokePrivateLifecycle(playerMove, "Awake");
        statusEffects.Add(StatusEffectType.Mark, 2);
        statusEffects.Add(StatusEffectType.Poison, 2);
        statusEffects.Add(StatusEffectType.Stun, 2);
        statusEffects.Add(StatusEffectType.Weakness, 2);
        playerMove.SetDuelClockActive(true);
        int completionCount = 0;
        playerMove.TurnCompleted += () => completionCount++;

        playerMove.Wait();
        bool usedLegacySkip = playerMove.TrySkipStunnedTurn();
        bool consumedFirst = playerMove.ProcessDuelClockStatusBeat();

        Assert.That(statusEffects.MarkStacks, Is.EqualTo(1));
        Assert.That(statusEffects.PoisonStacks, Is.EqualTo(1));
        Assert.That(statusEffects.StunStacks, Is.EqualTo(1));
        Assert.That(statusEffects.WeaknessStacks, Is.EqualTo(1));

        bool consumedSecond = playerMove.ProcessDuelClockStatusBeat();

        Assert.That(usedLegacySkip, Is.False);
        Assert.That(consumedFirst, Is.True);
        Assert.That(consumedSecond, Is.True);
        Assert.That(statusEffects.MarkStacks, Is.Zero);
        Assert.That(statusEffects.PoisonStacks, Is.Zero);
        Assert.That(statusEffects.StunStacks, Is.Zero);
        Assert.That(statusEffects.WeaknessStacks, Is.Zero);
        Assert.That(playerMove.TurnCount, Is.Zero);
        Assert.That(completionCount, Is.Zero);

        playerMove.Wait();

        Assert.That(playerMove.TurnCount, Is.EqualTo(1));
        Assert.That(completionCount, Is.EqualTo(1));
    }

    [Test]
    public void DuelClockPaidActionDoesNotDecreaseStatusBeforeCount()
    {
        playerObject = new GameObject("Duel Clock Player");
        StatusEffectController statusEffects =
            playerObject.AddComponent<StatusEffectController>();
        PlayerMove playerMove = playerObject.AddComponent<PlayerMove>();
        InvokePrivateLifecycle(playerMove, "Awake");
        statusEffects.Add(StatusEffectType.Mark, 2);
        statusEffects.Add(StatusEffectType.Poison, 2);
        statusEffects.Add(StatusEffectType.Weakness, 2);
        playerMove.SetDuelClockActive(true);

        playerMove.Wait();

        Assert.That(playerMove.TurnCount, Is.EqualTo(1));
        Assert.That(statusEffects.MarkStacks, Is.EqualTo(2));
        Assert.That(statusEffects.PoisonStacks, Is.EqualTo(2));
        Assert.That(statusEffects.WeaknessStacks, Is.EqualTo(2));

        playerMove.ProcessDuelClockStatusBeat();

        Assert.That(statusEffects.MarkStacks, Is.EqualTo(1));
        Assert.That(statusEffects.PoisonStacks, Is.EqualTo(1));
        Assert.That(statusEffects.WeaknessStacks, Is.EqualTo(1));
    }

    [Test]
    public void LegacyStunSkipStillCompletesOneTurn()
    {
        playerObject = new GameObject("Legacy Stunned Player");
        StatusEffectController statusEffects =
            playerObject.AddComponent<StatusEffectController>();
        PlayerMove playerMove = playerObject.AddComponent<PlayerMove>();
        InvokePrivateLifecycle(playerMove, "Awake");
        statusEffects.Add(StatusEffectType.Stun, 1);
        int completionCount = 0;
        playerMove.TurnCompleted += () => completionCount++;

        bool skipped = playerMove.TrySkipStunnedTurn();

        Assert.That(skipped, Is.True);
        Assert.That(statusEffects.StunStacks, Is.Zero);
        Assert.That(playerMove.TurnCount, Is.EqualTo(1));
        Assert.That(completionCount, Is.EqualTo(1));
    }

    private static void InvokePrivateLifecycle(
        object target,
        string methodName)
    {
        System.Reflection.MethodInfo method = target.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
    }
}

public sealed class WaveManagerPacingDispatchTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private readonly List<ScriptableObject> createdAssets =
        new List<ScriptableObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
        }

        foreach (ScriptableObject createdAsset in createdAssets)
        {
            if (createdAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(createdAsset);
            }
        }

        createdObjects.Clear();
        createdAssets.Clear();
    }

    [Test]
    public void LegacyTurnCompletedDispatchesOneEnemyCycle()
    {
        CreateWaveSetup(
            CombatPacingMode.Legacy,
            out WaveManager waveManager,
            out PlayerMove playerMove);
        int completionCount = 0;
        waveManager.EnemyTurnCycleCompleted += _ => completionCount++;

        playerMove.Wait();

        Assert.That(waveManager.CurrentEnemyTurnCycle, Is.EqualTo(1));
        DrainEnemyTurnCycleBody(waveManager);
        Assert.That(completionCount, Is.EqualTo(1));
    }

    [Test]
    public void DuelClockIgnoresTurnEventAndResolvesQueuedBeatsInOrder()
    {
        CreateWaveSetup(
            CombatPacingMode.DuelClock,
            out WaveManager waveManager,
            out PlayerMove playerMove);
        List<int> completedCycles = new List<int>();
        waveManager.EnemyTurnCycleCompleted +=
            cycle => completedCycles.Add(cycle);

        playerMove.Wait();

        Assert.That(waveManager.CurrentEnemyTurnCycle, Is.Zero);
        waveManager.QueueDuelClockBeats(2);
        DrainEnemyTurnResolver(waveManager);

        Assert.That(waveManager.CurrentEnemyTurnCycle, Is.EqualTo(2));
        Assert.That(completedCycles, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(waveManager.PendingEnemyTurnCycles, Is.Zero);
    }

    [Test]
    public void ShootingBeatsAndPaidCompletionShareResolverQueue()
    {
        CreateWaveSetup(
            CombatPacingMode.DuelClock,
            out WaveManager waveManager,
            out PlayerMove playerMove);
        DuelClockController controller =
            waveManager.gameObject.AddComponent<DuelClockController>();
        BattleData battle = CreateDuelBattle(4f, 100f);
        controller.Initialize(playerMove, waveManager);
        controller.BeatsCommitted += waveManager.QueueDuelClockBeats;
        controller.ConfigureFresh(battle, CombatPacingMode.DuelClock);
        List<int> completedCycles = new List<int>();
        waveManager.EnemyTurnCycleCompleted +=
            cycle => completedCycles.Add(cycle);

        playerMove.SetShooting(true);
        bool naturalBeatCommitted =
            controller.TryAdvanceNaturalTime(25d);
        bool naturalTimeAdvancedDuringResolver =
            controller.TryAdvanceNaturalTime(25d);

        Assert.That(naturalBeatCommitted, Is.True);
        Assert.That(naturalTimeAdvancedDuringResolver, Is.True);
        Assert.That(waveManager.CurrentEnemyTurnCycle, Is.Zero);
        Assert.That(waveManager.PendingEnemyTurnCycles, Is.EqualTo(2));

        playerMove.SetShooting(false);
        playerMove.CompleteTurn();

        Assert.That(waveManager.CurrentEnemyTurnCycle, Is.Zero);
        Assert.That(waveManager.PendingEnemyTurnCycles, Is.EqualTo(3));

        DrainEnemyTurnResolver(waveManager);

        Assert.That(completedCycles, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(waveManager.PendingEnemyTurnCycles, Is.Zero);
    }

    [Test]
    public void OnlyDuelClockWaitsForUnsettledPlayerActions()
    {
        Assert.That(WaveManager.ShouldWaitForPlayerAction(
            true, true, false), Is.True);
        Assert.That(WaveManager.ShouldWaitForPlayerAction(
            true, false, true), Is.True);
        Assert.That(WaveManager.ShouldWaitForPlayerAction(
            false, true, true), Is.False);
        Assert.That(WaveManager.ShouldWaitForPlayerAction(
            true, false, false), Is.False);
    }

    private void CreateWaveSetup(
        CombatPacingMode pacingMode,
        out WaveManager waveManager,
        out PlayerMove playerMove)
    {
        GameObject playerObject = CreateObject("Player");
        playerMove = playerObject.AddComponent<PlayerMove>();
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        SerializedObject serializedHealth = new SerializedObject(playerHealth);
        serializedHealth.FindProperty("currentHealth").intValue = 100;
        serializedHealth.ApplyModifiedPropertiesWithoutUndo();
        BoardManager boardManager =
            CreateObject("Board").AddComponent<BoardManager>();
        EnemyController enemyTemplate =
            CreateObject("Enemy Template").AddComponent<EnemyController>();
        GameObject waveObject = CreateObject("Wave");
        waveObject.SetActive(false);
        waveManager = waveObject.AddComponent<WaveManager>();
        SerializedObject serializedWave = new SerializedObject(waveManager);
        serializedWave.FindProperty("enemyPrefabTemplate")
            .objectReferenceValue = enemyTemplate;
        serializedWave.FindProperty("boardManager").objectReferenceValue =
            boardManager;
        serializedWave.FindProperty("playerMove").objectReferenceValue =
            playerMove;
        serializedWave.FindProperty("playerHealth").objectReferenceValue =
            playerHealth;
        serializedWave.FindProperty("enemyTurnDelay").floatValue = 0f;
        serializedWave.FindProperty("enemyActionInterval").floatValue = 0f;
        serializedWave.FindProperty("combatPacingMode").enumValueIndex =
            (int)pacingMode;
        serializedWave.ApplyModifiedPropertiesWithoutUndo();
        InvokePrivateLifecycle(waveManager, "Awake");
        waveObject.SetActive(true);
        InvokePrivateLifecycle(waveManager, "OnEnable");
    }

    private static void DrainEnemyTurnResolver(WaveManager waveManager)
    {
        IEnumerator root = CreatePrivateRoutine(
            waveManager,
            "ResolveEnemyTurnCycles",
            "Enemy turn resolver");
        DrainRoutine(root, "Enemy turn resolver");
    }

    private static void DrainEnemyTurnCycleBody(WaveManager waveManager)
    {
        IEnumerator routine = CreatePrivateRoutine(
            waveManager,
            "ResolveOneEnemyTurnCycle",
            "Enemy turn cycle");
        DrainRoutine(routine, "Enemy turn cycle");
    }

    private static IEnumerator CreatePrivateRoutine(
        WaveManager waveManager,
        string methodName,
        string displayName)
    {
        System.Reflection.MethodInfo method = typeof(WaveManager).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, displayName);
        IEnumerator routine = method.Invoke(waveManager, null) as IEnumerator;
        Assert.That(routine, Is.Not.Null, displayName);
        return routine;
    }

    private static void DrainRoutine(
        IEnumerator root,
        string displayName)
    {
        Stack<IEnumerator> routines = new Stack<IEnumerator>();
        routines.Push(root);
        int remainingSteps = 1000;

        while (routines.Count > 0 && remainingSteps-- > 0)
        {
            IEnumerator current = routines.Peek();

            if (!current.MoveNext())
            {
                routines.Pop();
                continue;
            }

            if (current.Current is IEnumerator nested)
            {
                routines.Push(nested);
            }
        }

        Assert.That(remainingSteps, Is.GreaterThan(0),
            $"{displayName} did not settle.");
    }

    private static void InvokePrivateLifecycle(
        object target,
        string methodName)
    {
        System.Reflection.MethodInfo method = target.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
    }

    private GameObject CreateObject(string objectName)
    {
        GameObject gameObject = new GameObject(objectName);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private BattleData CreateDuelBattle(
        float naturalProgress,
        float paidProgress)
    {
        BattleData battle = ScriptableObject.CreateInstance<BattleData>();
        createdAssets.Add(battle);
        SerializedObject serializedBattle = new SerializedObject(battle);
        serializedBattle.FindProperty("combatPacingMode").enumValueIndex =
            (int)CombatPacingMode.DuelClock;
        serializedBattle.FindProperty("duelClockNaturalProgressPerSecond")
            .floatValue = naturalProgress;
        serializedBattle.FindProperty("duelClockPaidActionProgress")
            .floatValue = paidProgress;
        serializedBattle.ApplyModifiedPropertiesWithoutUndo();
        return battle;
    }
}

public sealed class DuelClockSaveDataTests
{
    [Test]
    public void MissingFieldsDefaultToLegacyMode()
    {
        RunSaveData saveData = JsonUtility.FromJson<RunSaveData>(
            "{\"version\":3}");

        RunSaveSystem.NormalizeSaveData(saveData);

        Assert.That(saveData.combatPacingMode,
            Is.EqualTo((int)CombatPacingMode.Legacy));
        Assert.That(saveData.duelClockProgress, Is.Zero);
        Assert.That(saveData.duelClockCumulativeBeats, Is.Zero);
    }

    [Test]
    public void DuelClockFieldsRoundTripThroughJson()
    {
        RunSaveData source = new RunSaveData
        {
            combatPacingMode = (int)CombatPacingMode.DuelClock,
            duelClockProgress = 72.5d,
            duelClockCumulativeBeats = 12
        };

        RunSaveData restored = JsonUtility.FromJson<RunSaveData>(
            JsonUtility.ToJson(source));
        RunSaveSystem.NormalizeSaveData(restored);

        Assert.That(restored.combatPacingMode,
            Is.EqualTo((int)CombatPacingMode.DuelClock));
        Assert.That(restored.duelClockProgress, Is.EqualTo(72.5d));
        Assert.That(restored.duelClockCumulativeBeats, Is.EqualTo(12));
    }

    [Test]
    public void NormalizeKeepsCumulativeCountIndependentOfPlayerTurns()
    {
        RunSaveData saveData = new RunSaveData
        {
            playerTurnCount = 41,
            cumulativeBattleTurnCount = 7
        };

        RunSaveSystem.NormalizeSaveData(saveData);

        Assert.That(saveData.playerTurnCount, Is.EqualTo(41));
        Assert.That(saveData.cumulativeBattleTurnCount, Is.EqualTo(7));
    }

    [Test]
    public void CountComboStateRoundTripsThroughLegacyJsonFieldNames()
    {
        RunSaveData source = new RunSaveData
        {
            cumulativeBattleTurnCount = 23,
            comboCount = 4,
            comboTurnsRemaining = 8,
            comboResetSinceLastTurn = true
        };

        RunSaveData restored = JsonUtility.FromJson<RunSaveData>(
            JsonUtility.ToJson(source));

        Assert.That(restored.cumulativeBattleTurnCount, Is.EqualTo(23));
        Assert.That(restored.comboCount, Is.EqualTo(4));
        Assert.That(restored.comboTurnsRemaining, Is.EqualTo(8));
        Assert.That(restored.comboResetSinceLastTurn, Is.True);
    }

    [Test]
    public void CountReportStateRoundTripsThroughLegacyJsonFieldNames()
    {
        RunCombatReportSaveData source = new RunCombatReportSaveData
        {
            currentTurnDamage = 37,
            startingTurnCount = 12
        };

        RunCombatReportSaveData restored =
            JsonUtility.FromJson<RunCombatReportSaveData>(
                JsonUtility.ToJson(source));

        Assert.That(restored.currentTurnDamage, Is.EqualTo(37));
        Assert.That(restored.startingTurnCount, Is.EqualTo(12));
    }

    [Test]
    public void DuelClockEnemyPoolFieldsRoundTripThroughJson()
    {
        RunSaveData source = new RunSaveData
        {
            combatPacingMode = (int)CombatPacingMode.DuelClock,
            duelClockSpawnPoolInitialized = true,
            duelClockPendingEnemySpawns = 1,
            duelClockRemainingEnemyAssetNames =
                new List<string> { "Melee", "Gunner" }
        };

        RunSaveData restored = JsonUtility.FromJson<RunSaveData>(
            JsonUtility.ToJson(source));
        RunSaveSystem.NormalizeSaveData(restored);

        Assert.That(restored.duelClockSpawnPoolInitialized, Is.True);
        Assert.That(restored.duelClockPendingEnemySpawns, Is.EqualTo(1));
        Assert.That(restored.duelClockRemainingEnemyAssetNames,
            Is.EqualTo(new[] { "Melee", "Gunner" }));
    }

    [Test]
    public void NormalizeCarriesRestoredClockOverflow()
    {
        RunSaveData saveData = new RunSaveData
        {
            combatPacingMode = (int)CombatPacingMode.DuelClock,
            duelClockProgress = 250d,
            duelClockCumulativeBeats = 7
        };

        RunSaveSystem.NormalizeSaveData(saveData);

        Assert.That(saveData.duelClockProgress, Is.EqualTo(50d));
        Assert.That(saveData.duelClockCumulativeBeats, Is.EqualTo(9));
    }

    [TestCase(double.NaN, 0L)]
    [TestCase(double.PositiveInfinity, 0L)]
    [TestCase(-1d, 0L)]
    [TestCase(100d, long.MaxValue)]
    public void NormalizeResetsInvalidDuelClockState(
        double progress,
        long cumulativeBeats)
    {
        RunSaveData saveData = new RunSaveData
        {
            combatPacingMode = (int)CombatPacingMode.DuelClock,
            duelClockProgress = progress,
            duelClockCumulativeBeats = cumulativeBeats
        };

        RunSaveSystem.NormalizeSaveData(saveData);

        Assert.That(saveData.combatPacingMode,
            Is.EqualTo((int)CombatPacingMode.DuelClock));
        Assert.That(saveData.duelClockProgress, Is.Zero);
        Assert.That(saveData.duelClockCumulativeBeats, Is.Zero);
    }

    [Test]
    public void NormalizeInvalidModeFallsBackToLegacy()
    {
        RunSaveData saveData = new RunSaveData
        {
            combatPacingMode = 99,
            duelClockProgress = 50d,
            duelClockCumulativeBeats = 2
        };

        RunSaveSystem.NormalizeSaveData(saveData);

        Assert.That(saveData.combatPacingMode,
            Is.EqualTo((int)CombatPacingMode.Legacy));
        Assert.That(saveData.duelClockProgress, Is.Zero);
        Assert.That(saveData.duelClockCumulativeBeats, Is.Zero);
    }

    [Test]
    public void PreparingFreshBattleResetsSavedPacingState()
    {
        RunSaveData saveData = new RunSaveData
        {
            combatPacingMode = (int)CombatPacingMode.DuelClock,
            duelClockProgress = 50d,
            duelClockCumulativeBeats = 2
        };

        RunSaveSystem.ResetCombatPacingForFreshBattle(saveData);

        Assert.That(saveData.combatPacingMode,
            Is.EqualTo((int)CombatPacingMode.Legacy));
        Assert.That(saveData.duelClockProgress, Is.Zero);
        Assert.That(saveData.duelClockCumulativeBeats, Is.Zero);
        Assert.That(saveData.duelClockSpawnPoolInitialized, Is.False);
        Assert.That(saveData.duelClockRemainingEnemyAssetNames, Is.Empty);
        Assert.That(saveData.duelClockPendingEnemySpawns, Is.Zero);
    }
}
