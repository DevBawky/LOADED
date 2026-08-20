using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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
