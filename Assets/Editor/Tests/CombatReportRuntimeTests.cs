using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatReportRuntimeTests
{
    [Test]
    public void RuntimeTracksReportAndPreservesLegacySaveFields()
    {
        CombatReportRuntime runtime = new CombatReportRuntime();
        runtime.Begin(3, 100, 80);

        runtime.RecordShot();
        runtime.RecordDamage(12);
        runtime.RecordDamage(8);
        runtime.RecordHealthChanged(70);
        runtime.RecordHealthChanged(75);
        runtime.RecordDefeatPerformance(4, 2, 49.99f);

        RunCombatReportSaveData saved = runtime.CaptureRunState();

        Assert.That(saved.cumulativeDamage, Is.EqualTo(20));
        Assert.That(saved.currentTurnDamage, Is.EqualTo(20));
        Assert.That(saved.highestSingleDamage, Is.EqualTo(12));
        Assert.That(saved.damageTaken, Is.EqualTo(10));
        Assert.That(saved.healingReceived, Is.EqualTo(5));
        Assert.That(saved.totalShots, Is.EqualTo(1));
        Assert.That(saved.startingTurnCount, Is.EqualTo(3));
        Assert.That(saved.startingGold, Is.EqualTo(100));
        Assert.That(saved.stageMaxCombo, Is.EqualTo(4));
        Assert.That(saved.stageMaxCylinderKills, Is.EqualTo(2));
        Assert.That(saved.stageMaxOverkillPercent, Is.EqualTo(49.99f));
        Assert.That(saved.lastPlayerHealth, Is.EqualTo(75));
    }

    [Test]
    public void CompleteCommitsLastCountAndIgnoresLaterEvents()
    {
        CombatReportRuntime runtime = new CombatReportRuntime();
        runtime.Begin(10, 100, 80);
        runtime.RecordDamage(25);
        runtime.CompleteCount();
        runtime.RecordDamage(7);

        CombatReportSnapshot report = runtime.Complete(12, 145);
        runtime.RecordDamage(100);
        runtime.RecordShot();
        CombatReportSnapshot repeated = runtime.Complete(12, 145);

        Assert.That(report.CumulativeDamage, Is.EqualTo(32));
        Assert.That(report.HighestCumulativeDamage, Is.EqualTo(25));
        Assert.That(report.CompletedCount, Is.EqualTo(2));
        Assert.That(report.StageEarnedGold, Is.EqualTo(45));
        Assert.That(repeated.CumulativeDamage, Is.EqualTo(32));
        Assert.That(repeated.TotalShots, Is.Zero);
    }

    [Test]
    public void RestoreContinuesActiveBattleCollection()
    {
        CombatReportRuntime runtime = new CombatReportRuntime();
        runtime.Restore(new RunCombatReportSaveData
        {
            cumulativeDamage = 20,
            currentTurnDamage = 12,
            highestSingleDamage = 10,
            startingTurnCount = 5,
            startingGold = 30,
            lastPlayerHealth = 40
        });

        runtime.RecordDamage(8);
        runtime.RecordHealthChanged(35);
        CombatReportSnapshot report = runtime.Complete(8, 70);

        Assert.That(report.CumulativeDamage, Is.EqualTo(28));
        Assert.That(report.HighestCumulativeDamage, Is.EqualTo(20));
        Assert.That(report.DamageTaken, Is.EqualTo(5));
        Assert.That(report.CompletedCount, Is.EqualTo(3));
        Assert.That(report.StageEarnedGold, Is.EqualTo(40));
    }

    [Test]
    public void SettlementMatchesExistingMedalAndBonusRules()
    {
        CombatReportRuntime runtime = new CombatReportRuntime();
        runtime.Begin(0, 0, 10);
        runtime.RecordDefeatPerformance(7, 4, 150f);
        CombatReportSnapshot report = runtime.Complete(1, 100);

        BattleClearSettlement settlement =
            BattleClearRewardCalculator.Calculate(report, 10);

        Assert.That(settlement.ComboBronzeThreshold, Is.EqualTo(3));
        Assert.That(settlement.ComboSilverThreshold, Is.EqualTo(5));
        Assert.That(settlement.ComboGoldThreshold, Is.EqualTo(7));
        Assert.That(settlement.ComboMedalScore, Is.EqualTo(3));
        Assert.That(settlement.CylinderMedalScore, Is.EqualTo(3));
        Assert.That(settlement.ExecutorMedalScore, Is.EqualTo(3));
        Assert.That(settlement.TotalMedalScore, Is.EqualTo(9));
        Assert.That(settlement.BonusGoldRate, Is.EqualTo(0.3f));
        Assert.That(settlement.BonusGold, Is.EqualTo(30));
    }

    [Test]
    public void SettlementCommitsBonusAtMostOnce()
    {
        CombatReportRuntime runtime = new CombatReportRuntime();
        runtime.Begin(0, 0, 10);
        runtime.RecordDefeatPerformance(7, 4, 150f);
        BattleClearSettlement settlement =
            BattleClearRewardCalculator.Calculate(
                runtime.Complete(1, 100),
                10);
        int granted = 0;

        bool first = settlement.TryCommit(amount =>
        {
            granted += amount;
            return true;
        });
        bool second = settlement.TryCommit(amount =>
        {
            granted += amount;
            return true;
        });

        Assert.That(first, Is.True);
        Assert.That(second, Is.False);
        Assert.That(granted, Is.EqualTo(30));
        Assert.That(settlement.IsCommitted, Is.True);
    }

    [TestCase(GameFlowState.Battle, false, true)]
    [TestCase(GameFlowState.Battle, true, false)]
    [TestCase(GameFlowState.BattleClear, false, false)]
    [TestCase(GameFlowState.RunFailed, false, false)]
    public void BulletDepletionFailureRespectsBattleClearPriority(
        GameFlowState currentState,
        bool battleCompleted,
        bool expected)
    {
        Assert.That(
            BattleOutcomeRules.ShouldFailFromBulletDepletion(
                currentState,
                battleCompleted),
            Is.EqualTo(expected));
    }

    [TestCase(GameFlowState.Battle, false, true)]
    [TestCase(GameFlowState.Battle, true, false)]
    [TestCase(GameFlowState.BattleClear, true, false)]
    [TestCase(GameFlowState.BattleClear, false, false)]
    [TestCase(GameFlowState.RunFailed, false, true)]
    [TestCase(GameFlowState.RunComplete, true, false)]
    public void GameOverPresentationRespectsVictoryAndEitherSubscriberOrder(
        GameFlowState state, bool completed, bool expected)
    {
        Assert.That(
            BattleOutcomeRules.ShouldShowBulletDepletionGameOver(state, completed),
            Is.EqualTo(expected));
    }

    [UnityTest]
    public IEnumerator CompletedBattleIgnoresRealDeckDepletionEvent()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;

        GameObject owners = new GameObject("Completed battle owners");
        owners.SetActive(false);
        StateManager state = owners.AddComponent<StateManager>();
        WaveManager waves = owners.AddComponent<WaveManager>();
        DeckManager deck = owners.AddComponent<DeckManager>();
        BulletData bullet = ScriptableObject.CreateInstance<BulletData>();
        try
        {
            state.ConfigureExternalSceneState(0, 0, GameFlowState.Battle);
            typeof(WaveManager).GetField("isBattleCompleted",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(waves, true);
            GameOverController gameOver = Object.FindFirstObjectByType<GameOverController>();
            Assert.That(gameOver, Is.Not.Null);
            typeof(GameOverController).GetMethod("BindGameOverSources",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gameOver, null);

            Assert.That(deck.TryAddBullet(bullet), Is.True);
            Assert.That(deck.TryDestroyBullet(deck.PeekNextBullet()), Is.True);
            deck.CompleteFiringSequence();

            Assert.That(GameOverController.IsGameOver, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            state.ConfigureExternalSceneState(0, 0, GameFlowState.BattleClear);
            deck.CompleteFiringSequence();
            Assert.That(GameOverController.IsGameOver, Is.False);
        }
        finally
        {
            Object.Destroy(owners);
            Object.Destroy(bullet);
        }

        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (EditorApplication.isPlaying)
        {
            yield return new ExitPlayMode();
        }
    }
}

public sealed class EmptyDeckRunTests
{
    [Test]
    public void EmptyRestoreClearsAllLocationsWithoutFailingAndAllowsAcquisition()
    {
        GameObject owner = new GameObject("Empty deck restore");
        owner.SetActive(false);
        DeckManager deck = owner.AddComponent<DeckManager>();
        BulletData bullet = ScriptableObject.CreateInstance<BulletData>();
        try
        {
            Assert.That(deck.RestoreRunState(new[]
            {
                new RunBulletSaveData { location = 0, acquisitionOrder = 0 },
                new RunBulletSaveData { location = 1, acquisitionOrder = 1 },
                new RunBulletSaveData { location = 2, acquisitionOrder = 2 }
            }, _ => bullet, 2, new[] { 1, 2 }), Is.True);
            int depleted = 0;
            deck.BulletsDepleted += () => depleted++;

            Assert.That(deck.RestoreRunState(
                System.Array.Empty<RunBulletSaveData>(), _ => bullet,
                2, System.Array.Empty<int>()), Is.True);
            Assert.That(deck.TotalBulletCount, Is.Zero);
            Assert.That(deck.NextCycleOrder, Is.Empty);
            Assert.That(deck.PaidBulletRemovalCount, Is.EqualTo(2));
            Assert.That(depleted, Is.Zero);
            Assert.That(deck.TryReload(), Is.False);

            deck.NotifyBulletDepletion();
            Assert.That(depleted, Is.EqualTo(1));
            Assert.That(deck.TryAddBullet(bullet), Is.True);
            Assert.That(deck.TryReload(), Is.True);
            deck.NotifyBulletDepletion();
            Assert.That(depleted, Is.EqualTo(1));
            Assert.That(deck.TotalBulletCount, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(bullet);
        }
    }

    [Test]
    public void UnresolvedNonemptySaveStillFailsWithoutClearingDeck()
    {
        GameObject owner = new GameObject("Invalid deck restore");
        owner.SetActive(false);
        DeckManager deck = owner.AddComponent<DeckManager>();
        BulletData bullet = ScriptableObject.CreateInstance<BulletData>();
        try
        {
            deck.TryAddBullet(bullet);
            LogAssert.Expect(LogType.Warning, "Saved bullet 'Missing' could not be resolved.");
            Assert.That(deck.RestoreRunState(new[]
            {
                new RunBulletSaveData { assetName = "Missing" }
            }, _ => null, 0, null), Is.False);
            Assert.That(deck.TotalBulletCount, Is.EqualTo(1));
            Assert.That(deck.RestoreRunState(null, _ => bullet, 0, null), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(bullet);
        }
    }

    [TestCase(GameFlowState.BattleClear)]
    [TestCase(GameFlowState.Shop)]
    [TestCase(GameFlowState.Event)]
    [TestCase(GameFlowState.Battle)]
    public void EmptyDeckSaveRemainsValid(GameFlowState flow)
    {
        RunSaveData save = new RunSaveData { flowState = (int)flow };
        RunSaveData restored = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(save));
        RunSaveSystem.NormalizeSaveData(restored);
        MethodInfo validate = typeof(RunSaveSystem).GetMethod(
            "IsValidSaveData", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(validate.Invoke(null, new object[] { restored }), Is.EqualTo(true));
        restored.version = -1;
        Assert.That(validate.Invoke(null, new object[] { restored }), Is.EqualTo(false));
    }
}
