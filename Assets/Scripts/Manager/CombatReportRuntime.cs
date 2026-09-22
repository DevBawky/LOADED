using System;

internal readonly struct CombatReportSnapshot
{
    private readonly RunCombatReportSaveData source;

    private CombatReportSnapshot(
        RunCombatReportSaveData source,
        int completedCount,
        int stageEarnedGold)
    {
        this.source = source ?? new RunCombatReportSaveData();
        CompletedCount = Math.Max(0, completedCount);
        StageEarnedGold = Math.Max(0, stageEarnedGold);
    }

    public int CumulativeDamage => source?.cumulativeDamage ?? 0;
    public int HighestCumulativeDamage =>
        source?.highestCumulativeDamage ?? 0;
    public int HighestSingleDamage => source?.highestSingleDamage ?? 0;
    public int DamageTaken => source?.damageTaken ?? 0;
    public int HealingReceived => source?.healingReceived ?? 0;
    public int TotalShots => source?.totalShots ?? 0;
    public int StageMaxCombo => source?.stageMaxCombo ?? 0;
    public int StageMaxCylinderKills =>
        source?.stageMaxCylinderKills ?? 0;
    public float StageMaxOverkillPercent =>
        source?.stageMaxOverkillPercent ?? 0f;
    public int CompletedCount { get; }
    public int StageEarnedGold { get; }

    public float AverageDamagePerCount => CompletedCount <= 0
        ? 0f
        : (float)CumulativeDamage / CompletedCount;
    public float AverageDamagePerShot => TotalShots <= 0
        ? 0f
        : (float)CumulativeDamage / TotalShots;

    public RunCombatReportSaveData ToSaveData()
    {
        return CloneAndNormalize(source);
    }

    public static CombatReportSnapshot Create(
        RunCombatReportSaveData state,
        int currentCount,
        int currentMoney)
    {
        RunCombatReportSaveData normalized = CloneAndNormalize(state);
        return new CombatReportSnapshot(
            normalized,
            SaturatingDifference(
                currentCount,
                normalized.startingTurnCount),
            SaturatingDifference(
                currentMoney,
                normalized.startingGold));
    }

    private static RunCombatReportSaveData CloneAndNormalize(
        RunCombatReportSaveData state)
    {
        state ??= new RunCombatReportSaveData();
        return new RunCombatReportSaveData
        {
            cumulativeDamage = Math.Max(0, state.cumulativeDamage),
            highestCumulativeDamage = Math.Max(
                0,
                state.highestCumulativeDamage),
            currentTurnDamage = Math.Max(0, state.currentTurnDamage),
            highestSingleDamage = Math.Max(0, state.highestSingleDamage),
            damageTaken = Math.Max(0, state.damageTaken),
            healingReceived = Math.Max(0, state.healingReceived),
            totalShots = Math.Max(0, state.totalShots),
            startingTurnCount = Math.Max(0, state.startingTurnCount),
            startingGold = Math.Max(0, state.startingGold),
            stageMaxCombo = Math.Max(0, state.stageMaxCombo),
            stageMaxCylinderKills = Math.Max(
                0,
                state.stageMaxCylinderKills),
            stageMaxOverkillPercent = IsFinite(
                state.stageMaxOverkillPercent)
                    ? Math.Max(0f, state.stageMaxOverkillPercent)
                    : 0f,
            lastPlayerHealth = Math.Max(0, state.lastPlayerHealth)
        };
    }

    private static int SaturatingDifference(int current, int starting)
    {
        long difference = (long)Math.Max(0, current) - Math.Max(0, starting);
        return difference <= 0L
            ? 0
            : difference >= int.MaxValue ? int.MaxValue : (int)difference;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

internal sealed class CombatReportRuntime
{
    private RunCombatReportSaveData state = new RunCombatReportSaveData();
    private bool isCollecting;

    public void Begin(int currentCount, int currentMoney, int currentHealth)
    {
        state = new RunCombatReportSaveData
        {
            startingTurnCount = Math.Max(0, currentCount),
            startingGold = Math.Max(0, currentMoney),
            lastPlayerHealth = Math.Max(0, currentHealth)
        };
        isCollecting = true;
    }

    public void Restore(RunCombatReportSaveData restoredState)
    {
        state = CombatReportSnapshot.Create(
            restoredState,
            0,
            0).ToSaveData();
        isCollecting = true;
    }

    public RunCombatReportSaveData CaptureRunState()
    {
        return CombatReportSnapshot.Create(state, 0, 0).ToSaveData();
    }

    public void RecordShot()
    {
        if (isCollecting)
        {
            state.totalShots = SaturatingAdd(state.totalShots, 1);
        }
    }

    public void RecordDamage(int damage)
    {
        if (!isCollecting || damage <= 0)
        {
            return;
        }

        state.cumulativeDamage = SaturatingAdd(
            state.cumulativeDamage,
            damage);
        state.currentTurnDamage = SaturatingAdd(
            state.currentTurnDamage,
            damage);
        state.highestSingleDamage = Math.Max(
            state.highestSingleDamage,
            damage);
    }

    public void CompleteCount()
    {
        if (isCollecting)
        {
            CommitCurrentCountDamage();
        }
    }

    public void RecordDefeatPerformance(
        int comboKills,
        int cylinderKills,
        float overkillPercent)
    {
        if (!isCollecting)
        {
            return;
        }

        state.stageMaxCombo = Math.Max(
            state.stageMaxCombo,
            Math.Max(0, comboKills));
        state.stageMaxCylinderKills = Math.Max(
            state.stageMaxCylinderKills,
            Math.Max(0, cylinderKills));

        if (!float.IsNaN(overkillPercent)
            && !float.IsInfinity(overkillPercent))
        {
            state.stageMaxOverkillPercent = Math.Max(
                state.stageMaxOverkillPercent,
                Math.Max(0f, overkillPercent));
        }
    }

    public void RecordHealthChanged(int currentHealth)
    {
        int normalizedHealth = Math.Max(0, currentHealth);

        if (!isCollecting)
        {
            state.lastPlayerHealth = normalizedHealth;
            return;
        }

        if (normalizedHealth < state.lastPlayerHealth)
        {
            state.damageTaken = SaturatingAdd(
                state.damageTaken,
                state.lastPlayerHealth - normalizedHealth);
        }
        else if (normalizedHealth > state.lastPlayerHealth)
        {
            state.healingReceived = SaturatingAdd(
                state.healingReceived,
                normalizedHealth - state.lastPlayerHealth);
        }

        state.lastPlayerHealth = normalizedHealth;
    }

    public CombatReportSnapshot Complete(int currentCount, int currentMoney)
    {
        if (isCollecting)
        {
            CommitCurrentCountDamage();
            isCollecting = false;
        }

        return CombatReportSnapshot.Create(
            state,
            currentCount,
            currentMoney);
    }

    private void CommitCurrentCountDamage()
    {
        state.highestCumulativeDamage = Math.Max(
            state.highestCumulativeDamage,
            state.currentTurnDamage);
        state.currentTurnDamage = 0;
    }

    private static int SaturatingAdd(int current, int amount)
    {
        long result = (long)Math.Max(0, current) + Math.Max(0, amount);
        return result >= int.MaxValue ? int.MaxValue : (int)result;
    }
}

internal sealed class BattleClearSettlement
{
    public BattleClearSettlement(
        CombatReportSnapshot report,
        int comboBronzeThreshold,
        int comboSilverThreshold,
        int comboGoldThreshold,
        int comboMedalScore,
        int cylinderMedalScore,
        int executorMedalScore,
        float bonusGoldRate,
        int bonusGold)
    {
        Report = report;
        ComboBronzeThreshold = comboBronzeThreshold;
        ComboSilverThreshold = comboSilverThreshold;
        ComboGoldThreshold = comboGoldThreshold;
        ComboMedalScore = Math.Clamp(comboMedalScore, 0, 3);
        CylinderMedalScore = Math.Clamp(cylinderMedalScore, 0, 3);
        ExecutorMedalScore = Math.Clamp(executorMedalScore, 0, 3);
        TotalMedalScore = Math.Clamp(
            ComboMedalScore + CylinderMedalScore + ExecutorMedalScore,
            0,
            9);
        BonusGoldRate = Math.Max(0f, bonusGoldRate);
        BonusGold = Math.Max(0, bonusGold);
    }

    public CombatReportSnapshot Report { get; }
    public int ComboBronzeThreshold { get; }
    public int ComboSilverThreshold { get; }
    public int ComboGoldThreshold { get; }
    public int ComboMedalScore { get; }
    public int CylinderMedalScore { get; }
    public int ExecutorMedalScore { get; }
    public int TotalMedalScore { get; }
    public float BonusGoldRate { get; }
    public int BonusGold { get; }
    public bool IsCommitted { get; private set; }

    public bool TryCommit(Func<int, bool> grantGold)
    {
        if (IsCommitted)
        {
            return false;
        }

        if (BonusGold > 0 && (grantGold == null || !grantGold(BonusGold)))
        {
            return false;
        }

        IsCommitted = true;
        return true;
    }
}

internal static class BattleClearRewardCalculator
{
    public static BattleClearSettlement Calculate(
        CombatReportSnapshot report,
        BattleData battleData)
    {
        return Calculate(report, GetTotalEnemyCount(battleData));
    }

    internal static BattleClearSettlement Calculate(
        CombatReportSnapshot report,
        int totalEnemyCount)
    {
        int comboBronzeThreshold = GetPercentageThreshold(
            totalEnemyCount,
            25);
        int comboSilverThreshold = GetPercentageThreshold(
            totalEnemyCount,
            50);
        int comboGoldThreshold = GetPercentageThreshold(
            totalEnemyCount,
            70);
        int comboMedalScore = GetComboMedalScore(
            report.StageMaxCombo,
            comboBronzeThreshold,
            comboSilverThreshold,
            comboGoldThreshold);
        int cylinderMedalScore = GetCylinderMedalScore(
            report.StageMaxCylinderKills);
        int executorMedalScore = GetExecutorMedalScore(
            report.StageMaxOverkillPercent);
        int totalMedalScore = comboMedalScore
            + cylinderMedalScore
            + executorMedalScore;
        float bonusGoldRate = GetBonusGoldRate(totalMedalScore);
        int bonusGold = SaturatingFloorMultiply(
            report.StageEarnedGold,
            bonusGoldRate);

        return new BattleClearSettlement(
            report,
            comboBronzeThreshold,
            comboSilverThreshold,
            comboGoldThreshold,
            comboMedalScore,
            cylinderMedalScore,
            executorMedalScore,
            bonusGoldRate,
            bonusGold);
    }

    private static int GetTotalEnemyCount(BattleData battleData)
    {
        if (battleData?.Waves == null)
        {
            return 0;
        }

        long totalEnemyCount = 0L;

        foreach (EnemyWave wave in battleData.Waves)
        {
            if (wave?.Enemies == null)
            {
                continue;
            }

            foreach (EnemyWaveEntry entry in wave.Enemies)
            {
                if (entry?.EnemyData == null || entry.Count <= 0)
                {
                    continue;
                }

                totalEnemyCount += entry.Count;

                if (totalEnemyCount >= int.MaxValue)
                {
                    return int.MaxValue;
                }
            }
        }

        return (int)totalEnemyCount;
    }

    private static int GetPercentageThreshold(
        int totalEnemyCount,
        int percentage)
    {
        if (totalEnemyCount <= 0)
        {
            return int.MaxValue;
        }

        long scaledCount = (long)totalEnemyCount
            * Math.Clamp(percentage, 0, 100);
        return Math.Max(
            1,
            (int)Math.Min(int.MaxValue, (scaledCount + 99L) / 100L));
    }

    private static int GetComboMedalScore(
        int comboKills,
        int bronzeThreshold,
        int silverThreshold,
        int goldThreshold)
    {
        if (goldThreshold == int.MaxValue)
        {
            return 0;
        }

        return comboKills >= goldThreshold
            ? 3
            : comboKills >= silverThreshold
                ? 2
                : comboKills >= bronzeThreshold ? 1 : 0;
    }

    private static int GetCylinderMedalScore(int cylinderKills)
    {
        return cylinderKills >= 4
            ? 3
            : cylinderKills >= 3 ? 2 : cylinderKills >= 2 ? 1 : 0;
    }

    private static int GetExecutorMedalScore(float overkillPercent)
    {
        const float thresholdTolerance = 0.0001f;
        float inclusivePercent = Math.Max(0f, overkillPercent)
            + thresholdTolerance;
        return inclusivePercent >= 150f
            ? 3
            : inclusivePercent >= 75f
                ? 2
                : inclusivePercent >= 25f ? 1 : 0;
    }

    private static float GetBonusGoldRate(int score)
    {
        if (score >= 9)
        {
            return 0.3f;
        }

        if (score >= 6)
        {
            return 0.2f;
        }

        if (score >= 3)
        {
            return 0.1f;
        }

        return score >= 1 ? 0.05f : 0f;
    }

    private static int SaturatingFloorMultiply(int amount, float multiplier)
    {
        if (amount <= 0 || multiplier <= 0f)
        {
            return 0;
        }

        double result = Math.Floor(amount * multiplier);
        return result >= int.MaxValue ? int.MaxValue : (int)result;
    }
}
