#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DuelClockPilotAuthoring
{
    private const string BattleAssetRoot =
        "Assets/Scripts/Manager/Battle SO";

    [MenuItem("Tools/LOADED/Validate Battle Enemy Pools")]
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
        if (assetGuids.Length == 0)
        {
            throw new InvalidOperationException(
                $"No BattleData assets were found below '{BattleAssetRoot}'.");
        }

        foreach (string assetGuid in assetGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            BattleData battle = AssetDatabase.LoadAssetAtPath<BattleData>(
                assetPath);

            if (battle == null)
            {
                throw new InvalidOperationException(
                    $"BattleData asset was not found at '{assetPath}'.");
            }

            ValidateSettings(battle);
        }

        Debug.Log(
            $"Battle enemy-pool validation complete. Battle assets: "
            + $"{assetGuids.Length}.");
    }

    public static void ApplyFromCommandLine()
    {
        ApplyAllBattleSettings();
    }

    private static void ValidateSettings(BattleData battle)
    {
        if (battle.DuelClockEnemySpawnEntries.Count == 0)
        {
            throw new InvalidOperationException(
                $"BattleData '{battle.name}' has no authored enemy entries.");
        }

        int minimumSpawnCount = 0;
        HashSet<EnemyData> authoredEnemies = new HashSet<EnemyData>();

        foreach (DuelClockEnemySpawnEntry entry in
                 battle.DuelClockEnemySpawnEntries)
        {
            if (entry?.EnemyData == null || entry.Weight <= 0f
                || !authoredEnemies.Add(entry.EnemyData))
            {
                throw new InvalidOperationException(
                    $"BattleData '{battle.name}' contains an invalid enemy entry.");
            }

            minimumSpawnCount += entry.MinimumSpawnCount;
        }

        if (minimumSpawnCount > battle.DuelClockEnemySpawnCount)
        {
            throw new InvalidOperationException(
                $"BattleData '{battle.name}' minimum spawn counts exceed its total spawn count.");
        }
    }
}
#endif
