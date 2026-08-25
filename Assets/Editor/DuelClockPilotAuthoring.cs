#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DuelClockPilotAuthoring
{
    private const string BattleAssetRoot =
        "Assets/Scripts/Manager/Battle SO";

    [MenuItem("Tools/LOADED/Author Duel Clock All Battles")]
    public static void ApplyAllBattleSettings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before authoring Duel Clock battle assets.");
        }

        string[] assetGuids = AssetDatabase.FindAssets(
            "t:BattleData",
            new[] { BattleAssetRoot });
        List<string> assetPaths = new List<string>(assetGuids.Length);

        foreach (string assetGuid in assetGuids)
        {
            assetPaths.Add(AssetDatabase.GUIDToAssetPath(assetGuid));
        }

        assetPaths.Sort(StringComparer.Ordinal);

        if (assetPaths.Count == 0)
        {
            throw new InvalidOperationException(
                $"No BattleData assets were found below '{BattleAssetRoot}'.");
        }

        int changedAssetCount = 0;

        foreach (string assetPath in assetPaths)
        {
            BattleData battle = AssetDatabase.LoadAssetAtPath<BattleData>(
                assetPath);

            if (battle == null)
            {
                throw new InvalidOperationException(
                    $"BattleData asset was not found at '{assetPath}'.");
            }

            if (ApplySettings(battle))
            {
                AssetDatabase.SaveAssetIfDirty(battle);
                changedAssetCount++;
            }
        }

        Debug.Log(
            $"Duel Clock all-battle authoring complete. Battle assets: "
            + $"{assetPaths.Count}, changed assets: {changedAssetCount}.");
    }

    public static void ApplyFromCommandLine()
    {
        ApplyAllBattleSettings();
    }

    private static bool ApplySettings(BattleData battle)
    {
        SerializedObject serializedBattle = new SerializedObject(battle);
        serializedBattle.Update();
        SerializedProperty pacingMode = serializedBattle.FindProperty(
            "combatPacingMode");
        SerializedProperty enemySpawnCount = serializedBattle.FindProperty(
            "duelClockEnemySpawnCount");
        SerializedProperty enemySpawnEntries = serializedBattle.FindProperty(
            "duelClockEnemySpawnEntries");
        SerializedProperty enemyPool = serializedBattle.FindProperty(
            "duelClockEnemyPool");

        if (pacingMode == null || enemySpawnCount == null
            || enemySpawnEntries == null || enemyPool == null)
        {
            throw new InvalidOperationException(
                $"BattleData '{battle.name}' is missing Duel Clock settings.");
        }

        bool changed = false;

        if (pacingMode.intValue != (int)CombatPacingMode.DuelClock)
        {
            pacingMode.intValue = (int)CombatPacingMode.DuelClock;
            changed = true;
        }

        List<EnemyData> flattenedEnemies = FlattenEnemies(battle);

        if (flattenedEnemies.Count == 0)
        {
            throw new InvalidOperationException(
                $"BattleData '{battle.name}' has no valid authored enemies.");
        }

        if (enemySpawnEntries.arraySize == 0)
        {
            List<WeightedEnemyEntry> weightedEnemies = GroupEnemies(
                flattenedEnemies);
            enemySpawnCount.intValue = flattenedEnemies.Count;
            enemySpawnEntries.arraySize = weightedEnemies.Count;

            for (int index = 0; index < weightedEnemies.Count; index++)
            {
                SerializedProperty entry =
                    enemySpawnEntries.GetArrayElementAtIndex(index);
                WeightedEnemyEntry expected = weightedEnemies[index];
                entry.FindPropertyRelative("enemyData").objectReferenceValue =
                    expected.Enemy;
                entry.FindPropertyRelative("weight").floatValue =
                    expected.Weight;
                entry.FindPropertyRelative("minimumSpawnCount").intValue = 0;
                entry.FindPropertyRelative(
                    "missedSpawnWeightIncrease").floatValue = 0.25f;
                entry.FindPropertyRelative(
                    "previousSpawnWeightMultiplier").floatValue = 0.35f;
            }

            changed = true;
        }

        // Retain the old concrete pool so version-3 saves made before the
        // weighted selector can still infer their already-spawned counts.
        if (!PoolMatches(enemyPool, flattenedEnemies))
        {
            enemyPool.arraySize = flattenedEnemies.Count;

            for (int index = 0; index < flattenedEnemies.Count; index++)
            {
                enemyPool.GetArrayElementAtIndex(index).objectReferenceValue =
                    flattenedEnemies[index];
            }

            changed = true;
        }

        if (changed)
        {
            serializedBattle.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(battle);
        }

        return changed;
    }

    private static List<EnemyData> FlattenEnemies(BattleData battle)
    {
        List<EnemyData> flattenedEnemies = new List<EnemyData>();

        foreach (EnemyWave wave in battle.Waves)
        {
            if (wave == null)
            {
                continue;
            }

            foreach (EnemyWaveEntry entry in wave.Enemies)
            {
                if (entry?.EnemyData == null || entry.Count <= 0)
                {
                    throw new InvalidOperationException(
                        $"BattleData '{battle.name}' contains an invalid enemy entry.");
                }

                for (int count = 0; count < entry.Count; count++)
                {
                    flattenedEnemies.Add(entry.EnemyData);
                }
            }
        }

        return flattenedEnemies;
    }

    private static List<WeightedEnemyEntry> GroupEnemies(
        IReadOnlyList<EnemyData> flattenedEnemies)
    {
        List<WeightedEnemyEntry> groupedEnemies =
            new List<WeightedEnemyEntry>();

        foreach (EnemyData enemy in flattenedEnemies)
        {
            int existingIndex = groupedEnemies.FindIndex(
                entry => entry.Enemy == enemy);

            if (existingIndex >= 0)
            {
                WeightedEnemyEntry existing = groupedEnemies[existingIndex];
                groupedEnemies[existingIndex] = new WeightedEnemyEntry(
                    enemy,
                    existing.Weight + 1f);
            }
            else
            {
                groupedEnemies.Add(new WeightedEnemyEntry(enemy, 1f));
            }
        }

        return groupedEnemies;
    }

    private static bool PoolMatches(
        SerializedProperty enemyPool,
        IReadOnlyList<EnemyData> expectedEnemies)
    {
        if (enemyPool.arraySize != expectedEnemies.Count)
        {
            return false;
        }

        for (int index = 0; index < expectedEnemies.Count; index++)
        {
            if (enemyPool.GetArrayElementAtIndex(index).objectReferenceValue
                != expectedEnemies[index])
            {
                return false;
            }
        }

        return true;
    }

    private readonly struct WeightedEnemyEntry
    {
        public WeightedEnemyEntry(EnemyData enemy, float weight)
        {
            Enemy = enemy;
            Weight = weight;
        }

        public EnemyData Enemy { get; }
        public float Weight { get; }
    }
}
#endif
