using System;
using System.Collections.Generic;

internal sealed class DuelClockEnemySpawnPool
{
    private readonly List<EnemyData> remainingEnemies =
        new List<EnemyData>();

    public int InitialCount { get; private set; }
    public int RemainingCount => remainingEnemies.Count;
    public bool IsExhausted => remainingEnemies.Count == 0;

    public bool ConfigureFresh(IReadOnlyList<EnemyData> authoredEnemies)
    {
        remainingEnemies.Clear();

        if (!TryAddEnemies(authoredEnemies))
        {
            InitialCount = 0;
            remainingEnemies.Clear();
            return false;
        }

        InitialCount = remainingEnemies.Count;
        return InitialCount > 0;
    }

    public bool Restore(
        IReadOnlyList<EnemyData> authoredEnemies,
        IReadOnlyList<string> remainingEnemyAssetNames,
        Func<string, EnemyData> resolver)
    {
        remainingEnemies.Clear();

        if (!TryCountAuthoredEnemies(authoredEnemies, out int authoredCount)
            || remainingEnemyAssetNames == null || resolver == null)
        {
            InitialCount = 0;
            return false;
        }

        List<EnemyData> availableAuthoredEnemies =
            new List<EnemyData>(authoredCount);

        for (int index = 0; index < authoredCount; index++)
        {
            availableAuthoredEnemies.Add(authoredEnemies[index]);
        }

        foreach (string assetName in remainingEnemyAssetNames)
        {
            EnemyData enemy = resolver(assetName);
            int authoredIndex = availableAuthoredEnemies.IndexOf(enemy);

            if (enemy == null || authoredIndex < 0)
            {
                remainingEnemies.Clear();
                InitialCount = 0;
                return false;
            }

            remainingEnemies.Add(enemy);
            availableAuthoredEnemies.RemoveAt(authoredIndex);
        }

        InitialCount = authoredCount;
        return remainingEnemies.Count <= InitialCount;
    }

    public bool TryGet(int index, out EnemyData enemy)
    {
        enemy = null;

        if (index < 0 || index >= remainingEnemies.Count)
        {
            return false;
        }

        enemy = remainingEnemies[index];
        return enemy != null;
    }

    public bool TryConsumeAt(int index, EnemyData expectedEnemy)
    {
        if (index < 0 || index >= remainingEnemies.Count
            || remainingEnemies[index] != expectedEnemy)
        {
            return false;
        }

        remainingEnemies.RemoveAt(index);
        return true;
    }

    public void Capture(List<string> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();

        foreach (EnemyData enemy in remainingEnemies)
        {
            destination.Add(enemy == null ? string.Empty : enemy.name);
        }
    }

    public void Clear()
    {
        remainingEnemies.Clear();
        InitialCount = 0;
    }

    private bool TryAddEnemies(IReadOnlyList<EnemyData> enemies)
    {
        if (enemies == null || enemies.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < enemies.Count; index++)
        {
            if (enemies[index] == null)
            {
                return false;
            }

            remainingEnemies.Add(enemies[index]);
        }

        return true;
    }

    private static bool TryCountAuthoredEnemies(
        IReadOnlyList<EnemyData> enemies,
        out int count)
    {
        count = enemies == null ? 0 : enemies.Count;

        if (count == 0)
        {
            return false;
        }

        for (int index = 0; index < count; index++)
        {
            if (enemies[index] == null)
            {
                return false;
            }
        }

        return true;
    }
}
