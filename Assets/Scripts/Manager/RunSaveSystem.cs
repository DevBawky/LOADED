using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum RunStartMode
{
    None,
    New,
    Continue
}

/// <summary>
/// Stores the resumable run separately from aggregate statistics. The scene
/// reload starts the saved battle again while restoring the player's run
/// progression.
/// </summary>
public static class RunSaveSystem
{
    private const int CurrentVersion = 3;
    private const string SaveFileName = "loaded_run_save.json";
    private const string WebSaveKey = "loaded.run.save.v3";
    private static RunStartMode requestedStartMode;

    public static string SavePath => Path.Combine(
        Application.persistentDataPath,
        SaveFileName);

    public static bool HasValidSave
    {
        get
        {
            bool valid = TryLoad(out _);

            if (!valid && HasStoredSave())
            {
                DeleteSave();
            }

            return valid;
        }
    }

    public static void RequestStart(RunStartMode mode)
    {
        requestedStartMode = mode;
    }

    public static bool PrepareForSelectedBattle(int stageIndex, int battleIndex)
    {
        if (!TryLoad(out RunSaveData saveData))
        {
            return false;
        }

        saveData.stageIndex = Mathf.Max(0, stageIndex);
        saveData.battleIndex = Mathf.Max(0, battleIndex);
        saveData.flowState = (int)GameFlowState.Battle;
        saveData.startSelectedBattleFresh = true;
        saveData.cumulativeBattleTurnCount = Mathf.Max(
            saveData.cumulativeBattleTurnCount,
            saveData.playerTurnCount);
        saveData.playerTurnCount = 0;
        saveData.nextPushAvailableTurn = 0;
        saveData.currentWaveIndex = 0;
        saveData.remainingSpawnTurns = 0;
        saveData.isWaitingForNextWave = false;
        saveData.isBattleCompletionPending = false;
        saveData.currentEnemyTurnCycle = 0;
        saveData.playerStatusEffects =
            saveData.pendingNextBattlePlayerStatusEffects
            ?? new RunStatusEffectSaveData();
        saveData.pendingNextBattlePlayerStatusEffects =
            new RunStatusEffectSaveData();
        saveData.reservedSpawnTileIndices.Clear();
        saveData.enemies.Clear();
        saveData.bombs.Clear();
        saveData.droppedItems.Clear();
        return Save(saveData);
    }

    public static RunStartMode ConsumeRequestedStartMode()
    {
        RunStartMode mode = requestedStartMode;
        requestedStartMode = RunStartMode.None;
        return mode;
    }

    public static bool Save(RunSaveData saveData)
    {
        if (saveData == null)
        {
            return false;
        }

        try
        {
            saveData.version = CurrentVersion;
            string json = JsonUtility.ToJson(saveData, true);

#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(WebSaveKey, json);
            PlayerPrefs.Save();
#else
            string directory = Path.GetDirectoryName(SavePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(SavePath, json);
#endif
            RunSession.Instance.SetSnapshot(saveData);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Run save could not be written: {exception.Message}");
            return false;
        }
    }

    public static bool TryLoad(out RunSaveData saveData)
    {
        saveData = null;

        try
        {
            if (RunSession.Instance.TryGetSnapshot(out saveData))
            {
                NormalizeSaveData(saveData);
                return IsValidSaveData(saveData);
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            string json = PlayerPrefs.GetString(WebSaveKey, string.Empty);

            // Import a save made by an older WebGL build if its virtual file
            // system happened to persist successfully.
            if (string.IsNullOrWhiteSpace(json) && File.Exists(SavePath))
            {
                json = File.ReadAllText(SavePath);
                PlayerPrefs.SetString(WebSaveKey, json);
                PlayerPrefs.Save();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }
#else
            if (!File.Exists(SavePath))
            {
                return false;
            }

            string json = File.ReadAllText(SavePath);
#endif
            saveData = JsonUtility.FromJson<RunSaveData>(json);

            if (saveData == null || saveData.version != CurrentVersion
                || saveData.stageIndex < 0 || saveData.battleIndex < 0)
            {
                saveData = null;
                return false;
            }

            NormalizeSaveData(saveData);

            if (!IsValidSaveData(saveData))
            {
                saveData = null;
                return false;
            }

            RunSession.Instance.SetSnapshot(saveData);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Run save could not be loaded: {exception.Message}");
            saveData = null;
            return false;
        }
    }

    public static void DeleteSave()
    {
        RunSession.Instance.Clear();

        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.DeleteKey(WebSaveKey);
            PlayerPrefs.Save();
#endif
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Run save could not be deleted: {exception.Message}");
        }
    }

    private static bool HasStoredSave()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return PlayerPrefs.HasKey(WebSaveKey) || File.Exists(SavePath);
#else
        return File.Exists(SavePath);
#endif
    }

    private static bool IsValidSaveData(RunSaveData saveData)
    {
        return saveData != null
            && saveData.version == CurrentVersion
            && saveData.stageIndex >= 0
            && saveData.battleIndex >= 0
            && saveData.bullets != null
            && saveData.bullets.Count > 0;
    }

    private static void NormalizeSaveData(RunSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        saveData.bullets ??= new List<RunBulletSaveData>();
        saveData.nextCycleAcquisitionOrders ??= new List<int>();
        saveData.inventoryItemAssetNames ??= new List<string>();
        saveData.relics ??= new List<RunRelicSaveData>();
        saveData.playerTurnCount = Mathf.Max(0, saveData.playerTurnCount);
        saveData.cumulativeBattleTurnCount = Mathf.Max(
            saveData.playerTurnCount,
            saveData.cumulativeBattleTurnCount);
        saveData.playerStatusEffects ??= new RunStatusEffectSaveData();
        saveData.pendingNextBattlePlayerStatusEffects ??=
            new RunStatusEffectSaveData();
        saveData.reservedSpawnTileIndices ??= new List<int>();
        saveData.enemies ??= new List<RunEnemySaveData>();
        saveData.bombs ??= new List<RunBombSaveData>();
        saveData.droppedItems ??= new List<RunDroppedItemSaveData>();
        saveData.combatReport ??= new RunCombatReportSaveData();
        saveData.shop ??= new RunShopSaveData();
        saveData.shop.bulletOfferAssetNames ??= new List<string>();
        saveData.shop.purchasedBulletOffers ??= new List<bool>();
        saveData.shop.itemOfferAssetNames ??= new List<string>();
        saveData.shop.purchasedItemOffers ??= new List<bool>();
        saveData.activeEventId ??= string.Empty;
        saveData.eventOutcomeText ??= string.Empty;
        saveData.eventChoiceSelectionCounts ??= new List<int>();
        saveData.eventChoiceFailureCounts ??= new List<int>();
        saveData.eventOfferAssetNames ??= new List<string>();
        saveData.eventQuizCorrectAssetName ??= string.Empty;
        saveData.eventResultText ??= string.Empty;
        saveData.eventReelSymbolKeys ??= new List<string>();
        saveData.eventPendingChoiceIndex = Mathf.Max(
            -1,
            saveData.eventPendingChoiceIndex);
        saveData.eventFollowUpBattleIndex = Mathf.Max(
            -1,
            saveData.eventFollowUpBattleIndex);
        for (int index = 0;
             index < saveData.eventChoiceSelectionCounts.Count;
             index++)
        {
            saveData.eventChoiceSelectionCounts[index] = Mathf.Max(
                0,
                saveData.eventChoiceSelectionCounts[index]);
        }
        for (int index = 0;
             index < saveData.eventChoiceFailureCounts.Count;
             index++)
        {
            saveData.eventChoiceFailureCounts[index] = Mathf.Max(
                0,
                saveData.eventChoiceFailureCounts[index]);
        }
        saveData.completedEventIds ??= new List<string>();
        saveData.treasureOfferRelicIds ??= new List<string>();
    }
}

/// <summary>
/// Keeps the current run's statistics in memory and persists the aggregate
/// as one small JSON value. WebGL stores PlayerPrefs in the browser.
/// </summary>
