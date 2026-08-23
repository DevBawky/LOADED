using System;
using System.Collections.Generic;

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
public sealed class RunRelicSaveData
{
    public string relicId;
    public int stackCount = 1;
    public int remainingCharges;
    public int movementStacks;
    public long storedDamage;
    public int primaryCounter;
    public int secondaryCounter;
    public double storedValue;
    public bool runtimeFlag;
    public List<int> trackedBulletAcquisitionOrders = new List<int>();
    public int acquisitionOrder;
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
    public List<RunRelicSaveData> relics = new List<RunRelicSaveData>();
    public int playerTileIndex;
    public bool playerFacingRight;
    public int playerTurnCount;
    public int cumulativeBattleTurnCount;
    public int nextPushAvailableTurn;
    public RunStatusEffectSaveData playerStatusEffects =
        new RunStatusEffectSaveData();
    public RunStatusEffectSaveData pendingNextBattlePlayerStatusEffects =
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
    public bool shopVisitActive;
    public string activeEventId;
    public bool eventChoiceResolved;
    public string eventOutcomeText;
    public List<int> eventChoiceSelectionCounts = new List<int>();
    public List<int> eventChoiceFailureCounts = new List<int>();
    public int eventInteractionStage;
    public int eventPendingChoiceIndex = -1;
    public List<string> eventOfferAssetNames = new List<string>();
    public string eventQuizCorrectAssetName;
    public string eventResultText;
    public List<string> eventReelSymbolKeys = new List<string>();
    public int eventFollowUpDestination;
    public int eventFollowUpBattleIndex = -1;
    public List<string> completedEventIds = new List<string>();
    public bool treasureVisitActive;
    public bool treasureChestOpened;
    public bool treasureChoiceResolved;
    public List<string> treasureOfferRelicIds = new List<string>();
}
