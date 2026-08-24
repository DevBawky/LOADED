using System;
using System.Collections.Generic;

internal sealed class DuelClockEnemySpawnPool
{
    private readonly List<DuelClockEnemySpawnEntry> entries =
        new List<DuelClockEnemySpawnEntry>();
    private readonly List<int> spawnedCounts = new List<int>();
    private readonly List<int> missedSpawnCounts = new List<int>();
    private EnemyData lastSpawnedEnemy;

    public int InitialCount { get; private set; }
    public int RemainingCount { get; private set; }
    public bool IsExhausted => RemainingCount == 0;

    public bool ConfigureFresh(
        IReadOnlyList<DuelClockEnemySpawnEntry> authoredEntries,
        int totalSpawnCount)
    {
        Clear();

        if (!TryConfigureEntries(authoredEntries, totalSpawnCount))
        {
            Clear();
            return false;
        }

        InitialCount = totalSpawnCount;
        RemainingCount = totalSpawnCount;
        return true;
    }

    public bool Restore(
        IReadOnlyList<DuelClockEnemySpawnEntry> authoredEntries,
        int totalSpawnCount,
        int remainingSpawnCount,
        IReadOnlyList<int> savedSpawnedCounts,
        IReadOnlyList<int> savedMissedSpawnCounts,
        string lastSpawnedEnemyAssetName,
        Func<string, EnemyData> resolver)
    {
        Clear();

        if (!TryConfigureEntries(authoredEntries, totalSpawnCount)
            || remainingSpawnCount < 0
            || remainingSpawnCount > totalSpawnCount
            || savedSpawnedCounts == null
            || savedSpawnedCounts.Count != entries.Count
            || savedMissedSpawnCounts == null
            || savedMissedSpawnCounts.Count != entries.Count)
        {
            Clear();
            return false;
        }

        long spawnedTotal = 0L;

        for (int index = 0; index < savedSpawnedCounts.Count; index++)
        {
            int count = savedSpawnedCounts[index];
            int missedCount = savedMissedSpawnCounts[index];

            if (count < 0 || missedCount < 0)
            {
                Clear();
                return false;
            }

            spawnedCounts[index] = count;
            missedSpawnCounts[index] = missedCount;
            spawnedTotal += count;
        }

        if (spawnedTotal != totalSpawnCount - remainingSpawnCount
            || GetOutstandingMinimumCount() > remainingSpawnCount)
        {
            Clear();
            return false;
        }

        if (!string.IsNullOrWhiteSpace(lastSpawnedEnemyAssetName))
        {
            if (resolver == null)
            {
                Clear();
                return false;
            }

            lastSpawnedEnemy = resolver(lastSpawnedEnemyAssetName);

            if (lastSpawnedEnemy == null
                || FindEntryIndex(lastSpawnedEnemy) < 0)
            {
                Clear();
                return false;
            }
        }

        if (!IsVariationStateValid(spawnedTotal))
        {
            Clear();
            return false;
        }

        InitialCount = totalSpawnCount;
        RemainingCount = remainingSpawnCount;
        return true;
    }

    public bool TrySelect(
        float normalizedRoll,
        out int selectedIndex,
        out EnemyData selectedEnemy)
    {
        selectedIndex = -1;
        selectedEnemy = null;

        if (RemainingCount <= 0 || entries.Count == 0)
        {
            return false;
        }

        bool requireMinimum =
            RemainingCount <= GetOutstandingMinimumCount();
        double totalWeight = CalculateTotalWeight(
            requireMinimum,
            true);
        bool applyPreviousPenalty = totalWeight > 0d;

        if (!applyPreviousPenalty)
        {
            totalWeight = CalculateTotalWeight(requireMinimum, false);
        }

        if (totalWeight <= 0d)
        {
            return false;
        }

        double roll = float.IsNaN(normalizedRoll)
            || float.IsInfinity(normalizedRoll)
                ? 0d
                : Math.Max(0d, Math.Min(0.999999999d, normalizedRoll));
        double threshold = roll * totalWeight;
        double cumulativeWeight = 0d;

        for (int index = 0; index < entries.Count; index++)
        {
            double weight = GetEffectiveWeight(
                index,
                requireMinimum,
                applyPreviousPenalty);

            if (weight <= 0d)
            {
                continue;
            }

            cumulativeWeight += weight;

            if (threshold < cumulativeWeight)
            {
                selectedIndex = index;
                selectedEnemy = entries[index].EnemyData;
                return selectedEnemy != null;
            }
        }

        for (int index = entries.Count - 1; index >= 0; index--)
        {
            if (GetEffectiveWeight(
                    index,
                    requireMinimum,
                    applyPreviousPenalty) > 0d)
            {
                selectedIndex = index;
                selectedEnemy = entries[index].EnemyData;
                return selectedEnemy != null;
            }
        }

        return false;
    }

    public bool TryCommitSpawn(int selectedIndex, EnemyData expectedEnemy)
    {
        if (RemainingCount <= 0 || selectedIndex < 0
            || selectedIndex >= entries.Count
            || entries[selectedIndex].EnemyData != expectedEnemy)
        {
            return false;
        }

        for (int index = 0; index < missedSpawnCounts.Count; index++)
        {
            missedSpawnCounts[index] = index == selectedIndex
                ? 0
                : missedSpawnCounts[index] == int.MaxValue
                    ? int.MaxValue
                    : missedSpawnCounts[index] + 1;
        }

        spawnedCounts[selectedIndex]++;
        RemainingCount--;
        lastSpawnedEnemy = expectedEnemy;
        return true;
    }

    public void Capture(
        List<int> destinationSpawnedCounts,
        List<int> destinationMissedSpawnCounts,
        out int remainingSpawnCount,
        out string lastSpawnedEnemyAssetName)
    {
        remainingSpawnCount = RemainingCount;
        lastSpawnedEnemyAssetName = lastSpawnedEnemy == null
            ? string.Empty
            : lastSpawnedEnemy.name;

        if (destinationSpawnedCounts == null)
        {
            return;
        }

        destinationSpawnedCounts.Clear();
        destinationSpawnedCounts.AddRange(spawnedCounts);

        if (destinationMissedSpawnCounts == null)
        {
            return;
        }

        destinationMissedSpawnCounts.Clear();
        destinationMissedSpawnCounts.AddRange(missedSpawnCounts);
    }

    public void Clear()
    {
        entries.Clear();
        spawnedCounts.Clear();
        missedSpawnCounts.Clear();
        lastSpawnedEnemy = null;
        InitialCount = 0;
        RemainingCount = 0;
    }

    private bool TryConfigureEntries(
        IReadOnlyList<DuelClockEnemySpawnEntry> authoredEntries,
        int totalSpawnCount)
    {
        if (authoredEntries == null || authoredEntries.Count == 0
            || totalSpawnCount <= 0)
        {
            return false;
        }

        long minimumTotal = 0L;

        for (int index = 0; index < authoredEntries.Count; index++)
        {
            DuelClockEnemySpawnEntry entry = authoredEntries[index];

            if (entry == null || entry.EnemyData == null
                || entry.Weight <= 0f
                || FindEntryIndex(entry.EnemyData) >= 0)
            {
                return false;
            }

            entries.Add(entry);
            spawnedCounts.Add(0);
            missedSpawnCounts.Add(0);
            minimumTotal += entry.MinimumSpawnCount;
        }

        return minimumTotal <= totalSpawnCount;
    }

    private int GetOutstandingMinimumCount()
    {
        long outstanding = 0L;

        for (int index = 0; index < entries.Count; index++)
        {
            outstanding += Math.Max(
                0,
                entries[index].MinimumSpawnCount - spawnedCounts[index]);
        }

        return (int)Math.Min(int.MaxValue, outstanding);
    }

    private double CalculateTotalWeight(
        bool requireMinimum,
        bool applyPreviousPenalty)
    {
        double totalWeight = 0d;

        for (int index = 0; index < entries.Count; index++)
        {
            totalWeight += GetEffectiveWeight(
                index,
                requireMinimum,
                applyPreviousPenalty);
        }

        return totalWeight;
    }

    private double GetEffectiveWeight(
        int index,
        bool requireMinimum,
        bool applyPreviousPenalty)
    {
        DuelClockEnemySpawnEntry entry = entries[index];

        if (requireMinimum
            && spawnedCounts[index] >= entry.MinimumSpawnCount)
        {
            return 0d;
        }

        double weight = entry.Weight * (1d
            + missedSpawnCounts[index] * entry.MissedSpawnWeightIncrease);

        if (applyPreviousPenalty && entry.EnemyData == lastSpawnedEnemy)
        {
            weight *= entry.PreviousSpawnWeightMultiplier;
        }

        return weight;
    }

    private int FindEntryIndex(EnemyData enemy)
    {
        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].EnemyData == enemy)
            {
                return index;
            }
        }

        return -1;
    }

    private bool IsVariationStateValid(long spawnedTotal)
    {
        if (spawnedTotal == 0L)
        {
            if (lastSpawnedEnemy != null)
            {
                return false;
            }

            for (int index = 0; index < missedSpawnCounts.Count; index++)
            {
                if (missedSpawnCounts[index] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        if (lastSpawnedEnemy == null)
        {
            for (int index = 0; index < missedSpawnCounts.Count; index++)
            {
                if (missedSpawnCounts[index] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        for (int index = 0; index < missedSpawnCounts.Count; index++)
        {
            if (missedSpawnCounts[index] > spawnedTotal
                || entries[index].EnemyData == lastSpawnedEnemy
                    && missedSpawnCounts[index] != 0)
            {
                return false;
            }
        }

        return true;
    }
}
