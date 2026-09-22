using NUnit.Framework;

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
}
