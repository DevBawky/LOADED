using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class BulletUsageStatistic
{
    public string key;
    public string displayName;
    public long count;
}

[Serializable]
public sealed class GameStatisticsData
{
    public long totalPlays;
    public long wins;
    public long totalKills;
    public long totalDamage;
    public long totalBulletsFired;
    public long highestCylinderDamage;
    public long highestSingleHitDamage;
    public long highestComboKills;
    public long goldSpent;
    public List<BulletUsageStatistic> bulletUsage =
        new List<BulletUsageStatistic>();
}

[Serializable]
public sealed class RunBulletSaveData
{
    public string assetName;
    public string bulletId;
    public int level;
    public int acquisitionOrder;
    public int abilityStacks;
    public int permanentStacks;
    public float storedDamageBonus;
    public float temporaryCriticalChanceBonus;
    public float temporaryDamageBonus;
    public int shotsObservedWhileLoaded;
    public int location;
    public int locationIndex;
}

[Serializable]
public sealed class RunStatusEffectSaveData
{
    public int markStacks;
    public int poisonStacks;
    public int stunStacks;
    public int weaknessStacks;
    public bool poisonCreditedToPlayer;
}

[Serializable]
public sealed class RunEnemySaveData
{
    public string enemyAssetName;
    public int tileIndex;
    public bool facingRight;
    public int currentHealth;
    public int currentShield;
    public int remainingSupportCharges;
    public int recoveryTurnsRemaining;
    public List<string> queuedActionAssetNames = new List<string>();
    public bool isQueueCreated;
    public bool isAttackPrepared;
    public bool isRetreating;
    public int preparedTargetTileIndex;
    public int preparedSupportTargetIndex = -1;
    public int preparedSupportType;
    public int lastTurnAction;
    public int bigBarrelStep;
    public bool isBigBarrelPhaseTwo;
    public bool bigBarrelActionUsesPhaseTwo;
    public int preparedBigBarrelFuse;
    public int bigBarrelReloadTurnsRemaining;
    public List<int> preparedBombTargetTileIndices = new List<int>();
    public List<int> preparedShotgunTileIndices = new List<int>();
    public RunStatusEffectSaveData statusEffects =
        new RunStatusEffectSaveData();
}

[Serializable]
public sealed class RunBombSaveData
{
    public string sourceEnemyAssetName;
    public int tileIndex;
    public int remainingFuse;
    public int createdTurnCycle;
}

[Serializable]
public sealed class RunDroppedItemSaveData
{
    public string itemAssetName;
    public int tileIndex;
}

[Serializable]
public sealed class RunCombatReportSaveData
{
    public int cumulativeDamage;
    public int highestCumulativeDamage;
    public int currentTurnDamage;
    public int highestSingleDamage;
    public int damageTaken;
    public int healingReceived;
    public int totalShots;
    public int startingTurnCount;
    public int startingGold;
    public int stageMaxCombo;
    public int stageMaxCylinderKills;
    public float stageMaxOverkillPercent;
    public int lastPlayerHealth;
}

[Serializable]
public sealed class RunShopSaveData
{
    public List<string> bulletOfferAssetNames = new List<string>();
    public List<bool> purchasedBulletOffers = new List<bool>();
    public List<string> itemOfferAssetNames = new List<string>();
    public List<bool> purchasedItemOffers = new List<bool>();
}

[Serializable]
public sealed class RunSaveData
{
    public int version = 3;
    public int flowState = (int)GameFlowState.Battle;
    public int stageIndex;
    public int battleIndex;
    public bool startSelectedBattleFresh;
    public int currentHealth;
    public int maxHealth;
    public int money;
    public int paidBulletRemovalCount;
    public int shopRefreshCost;
    public List<RunBulletSaveData> bullets = new List<RunBulletSaveData>();
    public List<int> nextCycleAcquisitionOrders = new List<int>();
    public List<string> inventoryItemAssetNames = new List<string>();
    public int playerTileIndex;
    public bool playerFacingRight;
    public int playerTurnCount;
    public int nextPushAvailableTurn;
    public RunStatusEffectSaveData playerStatusEffects =
        new RunStatusEffectSaveData();
    public int currentWaveIndex;
    public int remainingSpawnTurns;
    public bool isWaitingForNextWave;
    public bool isBattleCompletionPending;
    public int currentEnemyTurnCycle;
    public List<int> reservedSpawnTileIndices = new List<int>();
    public List<RunEnemySaveData> enemies = new List<RunEnemySaveData>();
    public List<RunBombSaveData> bombs = new List<RunBombSaveData>();
    public List<RunDroppedItemSaveData> droppedItems =
        new List<RunDroppedItemSaveData>();
    public int comboCount;
    public int comboTurnsRemaining;
    public bool comboResetSinceLastTurn;
    public int cylinderDamage;
    public int firingSequenceDefeatCount;
    public bool cylinderActive;
    public RunCombatReportSaveData combatReport =
        new RunCombatReportSaveData();
    public string randomStateJson;
    public bool statisticsCylinderActive;
    public long statisticsCurrentCylinderDamage;
    public RunShopSaveData shop = new RunShopSaveData();
}

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
        saveData.currentWaveIndex = 0;
        saveData.remainingSpawnTurns = 0;
        saveData.isWaitingForNextWave = false;
        saveData.isBattleCompletionPending = false;
        saveData.currentEnemyTurnCycle = 0;
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

            saveData.bullets ??= new List<RunBulletSaveData>();
            saveData.nextCycleAcquisitionOrders ??= new List<int>();
            saveData.inventoryItemAssetNames ??= new List<string>();
            saveData.playerStatusEffects ??= new RunStatusEffectSaveData();
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
            return saveData.bullets.Count > 0;
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
}

/// <summary>
/// Keeps the current run's statistics in memory and persists the aggregate
/// as one small JSON value. WebGL stores PlayerPrefs in the browser.
/// </summary>
public static class GameStatistics
{
    private const string SaveKey = "loaded.statistics.v1";

    private static GameStatisticsData data;
    private static bool runActive;
    private static bool cylinderActive;
    private static long currentCylinderDamage;
    private static bool dirty;

    public static GameStatisticsData Data
    {
        get
        {
            EnsureLoaded();
            return data;
        }
    }

    public static void BeginRun()
    {
        EnsureLoaded();

        if (runActive)
        {
            return;
        }

        runActive = true;
        cylinderActive = false;
        currentCylinderDamage = 0;
        data.totalPlays = SaturatingAdd(data.totalPlays, 1);
        dirty = true;
    }

    public static void BeginFreshRun()
    {
        EnsureLoaded();
        runActive = false;
        cylinderActive = false;
        currentCylinderDamage = 0;
        BeginRun();
    }

    public static void ResumeRun()
    {
        EnsureLoaded();
        runActive = true;
        cylinderActive = false;
        currentCylinderDamage = 0;
    }

    public static void ResumeRun(RunSaveData saveData)
    {
        ResumeRun();

        if (saveData != null)
        {
            cylinderActive = saveData.statisticsCylinderActive;
            currentCylinderDamage = Math.Max(
                0,
                saveData.statisticsCurrentCylinderDamage);
        }
    }

    public static void CaptureRunState(RunSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        saveData.statisticsCylinderActive = cylinderActive;
        saveData.statisticsCurrentCylinderDamage = Math.Max(
            0,
            currentCylinderDamage);
    }

    public static void EndRun(bool won)
    {
        EnsureLoaded();

        if (!runActive)
        {
            return;
        }

        if (won)
        {
            data.wins = SaturatingAdd(data.wins, 1);
        }

        runActive = false;
        cylinderActive = false;
        currentCylinderDamage = 0;
        dirty = true;
        SaveCheckpoint();
    }

    public static void RecordDamage(int damage)
    {
        if (!CanRecord() || damage <= 0)
        {
            return;
        }

        data.totalDamage = SaturatingAdd(data.totalDamage, damage);
        data.highestSingleHitDamage = Math.Max(
            data.highestSingleHitDamage,
            damage);

        if (cylinderActive)
        {
            currentCylinderDamage = SaturatingAdd(
                currentCylinderDamage,
                damage);
        }

        dirty = true;
    }

    public static void RecordKill()
    {
        if (!CanRecord())
        {
            return;
        }

        data.totalKills = SaturatingAdd(data.totalKills, 1);
        dirty = true;
    }

    public static void BeginCylinder()
    {
        if (!CanRecord())
        {
            return;
        }

        currentCylinderDamage = 0;
        cylinderActive = true;
    }

    public static void EndCylinder()
    {
        if (!CanRecord() || !cylinderActive)
        {
            return;
        }

        data.highestCylinderDamage = Math.Max(
            data.highestCylinderDamage,
            currentCylinderDamage);
        cylinderActive = false;
        currentCylinderDamage = 0;
    }

    public static void RecordComboKills(int comboKills)
    {
        if (!CanRecord() || comboKills <= 0)
        {
            return;
        }

        data.highestComboKills = Math.Max(
            data.highestComboKills,
            comboKills);
        dirty = true;
    }

    public static void RecordBulletFired(BulletInstance bullet)
    {
        if (!CanRecord() || bullet == null || bullet.Data == null)
        {
            return;
        }

        string displayName = bullet.Data.GetDisplayName(0);
        string bulletId = string.IsNullOrWhiteSpace(bullet.Data.BulletId)
            ? bullet.Data.name
            : bullet.Data.BulletId;
        string key = $"{bulletId}|{displayName}";

        BulletUsageStatistic usage = data.bulletUsage.Find(
            entry => entry != null && entry.key == key);

        if (usage == null)
        {
            usage = new BulletUsageStatistic
            {
                key = key,
                displayName = displayName,
                count = 0
            };
            data.bulletUsage.Add(usage);
        }

        usage.displayName = displayName;
        usage.count = SaturatingAdd(usage.count, 1);
        data.totalBulletsFired = SaturatingAdd(
            data.totalBulletsFired,
            1);
        dirty = true;
    }

    public static void RecordGoldSpent(int amount)
    {
        if (!CanRecord() || amount <= 0)
        {
            return;
        }

        data.goldSpent = SaturatingAdd(data.goldSpent, amount);
        dirty = true;

        // Purchases are infrequent and can happen after the shop-entry save.
        // Persist here so closing the browser while shopping loses no spend.
        SaveCheckpoint();
    }

    public static string GetMostUsedBulletName()
    {
        EnsureLoaded();
        BulletUsageStatistic mostUsed = null;

        foreach (BulletUsageStatistic usage in data.bulletUsage)
        {
            if (usage != null && usage.count > 0
                && (mostUsed == null || usage.count > mostUsed.count))
            {
                mostUsed = usage;
            }
        }

        return mostUsed == null || string.IsNullOrWhiteSpace(
            mostUsed.displayName)
            ? "-"
            : mostUsed.displayName;
    }

    public static void SaveCheckpoint()
    {
        EnsureLoaded();

        if (!dirty)
        {
            return;
        }

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        dirty = false;
    }

    private static bool CanRecord()
    {
        EnsureLoaded();
        return runActive;
    }

    private static void EnsureLoaded()
    {
        if (data != null)
        {
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey, string.Empty);

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                data = JsonUtility.FromJson<GameStatisticsData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Statistics data could not be loaded: {exception.Message}");
            }
        }

        data ??= new GameStatisticsData();
        data.bulletUsage ??= new List<BulletUsageStatistic>();
    }

    private static long SaturatingAdd(long current, long amount)
    {
        if (amount <= 0)
        {
            return current;
        }

        return current > long.MaxValue - amount
            ? long.MaxValue
            : current + amount;
    }
}
