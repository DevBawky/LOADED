using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class EnemyWaveEntry
{
    [SerializeField] private EnemyData enemyData;
    [Min(1)]
    [SerializeField] private int count = 1;

    public EnemyData EnemyData => enemyData;
    public int Count => count;
}

[Serializable]
public class EnemyWave
{
    [SerializeField] private EnemyWaveEntry[] enemies =
        Array.Empty<EnemyWaveEntry>();

    public IReadOnlyList<EnemyWaveEntry> Enemies =>
        enemies ?? Array.Empty<EnemyWaveEntry>();
}

internal readonly struct EnemyBattleProgress
{
    public EnemyBattleProgress(long defeatedCount, long totalCount)
    {
        DefeatedCount = defeatedCount;
        TotalCount = totalCount;
    }

    public long DefeatedCount { get; }
    public long TotalCount { get; }
    public long RemainingCount => Math.Max(0L, TotalCount - DefeatedCount);
}

public class WaveManager : MonoBehaviour
{
    private const int EnemyCapacityPercentage = 40;

    private readonly struct SpawnCell
    {
        public SpawnCell(int tileIndex, int laneIndex, int cellKey)
        {
            TileIndex = tileIndex;
            LaneIndex = laneIndex;
            CellKey = cellKey;
        }

        public int TileIndex { get; }
        public int LaneIndex { get; }
        public int CellKey { get; }
    }

    [Header("Battle Settings")]
    [SerializeField] private Vector3 spawnPositionOffset =
        new Vector3(0f, 0.3f, 0f);

    [Header("COUNT Timing")]
    [Min(0f)]
    [Tooltip("모든 적이 즉시 행동을 마칠 때 적 전체가 공유하는 기본 COUNT 연출 시간입니다.")]
    [SerializeField] private float enemyTurnDelay = 0.35f;
    [Min(0f)]
    [Tooltip("실제 공격 행동 뒤에만 추가하는 간격입니다.")]
    [SerializeField] private float enemyActionInterval = 0.15f;

    [Header("References")]
    [Tooltip("모든 EnemyData를 실행하는 공용 적 템플릿 프리팹입니다.")]
    [SerializeField] private EnemyController enemyPrefabTemplate;
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Transform enemyParent;
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private BossBombManager bossBombManager;

    [Header("Runtime State")]
    [SerializeField] private List<EnemyController> activeEnemies =
        new List<EnemyController>();
    [Tooltip("레거시 필드명을 유지하지만 값은 lane * boardCount + tile 형식의 셀 키입니다.")]
    [SerializeField] private List<int> reservedSpawnTileIndices =
        new List<int>();
    [SerializeField] private int currentWaveIndex = -1;
    [SerializeField] private int remainingSpawnTurns;
    [SerializeField] private bool isWaitingForNextWave;
    [FormerlySerializedAs("isStageCleared")]
    [SerializeField] private bool isBattleCompleted;
    [SerializeField] private bool isBattleCompletionPending;
    [SerializeField] private CombatPacingMode combatPacingMode =
        CombatPacingMode.Legacy;

    private EnemyWave[] waves = Array.Empty<EnemyWave>();
    private int spawnTerm;
    private bool isResolvingTurn;
    private Coroutine enemyTurnCoroutine;
    private int currentEnemyTurnCycle;
    private long pendingEnemyTurnCycles;
    private int pendingDetachedEnemyAttacks;
    private bool isCancellingManagedCoroutines;
    private readonly List<GameObject> detachedEnemyAttackVisuals =
        new List<GameObject>();
    private int maximumActiveEnemyCount = 1;
    private bool finalDefeatPresented;
    private readonly DuelClockEnemySpawnPool duelClockEnemySpawnPool =
        new DuelClockEnemySpawnPool();
    private DuelClockEnemySpawnEntry[] duelClockSpawnEntries =
        Array.Empty<DuelClockEnemySpawnEntry>();
    private EnemyData[] duelClockAuthoredEnemies = Array.Empty<EnemyData>();
    private EnemyData[] duelClockLegacyAuthoredEnemies =
        Array.Empty<EnemyData>();
    private int duelClockEnemySpawnCount;
    private int pendingDuelClockEnemySpawns;
    private bool isDuelClockEnemyPoolConfigured;
    private DuelClockController duelClockController;
    private readonly List<EnemyTargetData> enemyTargetBuffer =
        new List<EnemyTargetData>();
    private readonly Dictionary<int, Component> movementTileReservations =
        new Dictionary<int, Component>();
    private readonly List<int> movementReservationCleanupBuffer =
        new List<int>();

    public event Action StateChanged;
    public event Action BattleCompleted;
    public event Action BattleFailed;
    public event Action<long> DuelClockBeatsCommitted;
    public event Action<int> EnemyTurnCycleCompleted;
    public event Action<EnemyController> EnemyDefeated;
    public event Action<EnemyController> FinalEnemyDefeated;
    // TODO: A future persistent unlock service can subscribe and add
    // ExplosiveBullet.asset on the first Big Barrel defeat.
    public event Action<EnemyData> BigBarrelDefeated;

    public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies;
    public int MaximumActiveEnemyCount => maximumActiveEnemyCount;
    internal int LivingEnemyCount => GetLivingEnemyCount();
    public bool IsActiveEnemyLimitReached =>
        CalculateAvailableEnemySlots(
            GetLivingEnemyCount(),
            maximumActiveEnemyCount) <= 0;
    public bool IsDuelClockEnemySpawnPoolExhausted =>
        isDuelClockEnemyPoolConfigured
        && duelClockEnemySpawnPool.RemainingCount <= 0;
    public bool IsSpawnGaugePaused => ShouldPauseSpawnGauge(
        isDuelClockEnemyPoolConfigured,
        duelClockEnemySpawnPool.RemainingCount,
        IsActiveEnemyLimitReached);
    public IReadOnlyList<EnemyWave> Waves => waves ?? Array.Empty<EnemyWave>();
    public int CurrentWaveIndex => currentWaveIndex;
    public Vector3 SpawnPositionOffset => spawnPositionOffset;
    public int SpawnTerm => spawnTerm;
    public int RemainingSpawnTurns => remainingSpawnTurns;
    public float EnemyTurnDelay => enemyTurnDelay;
    public float EnemyActionInterval => enemyActionInterval;
    public bool IsWaitingForNextWave => isWaitingForNextWave;
    public bool IsBattleCompleted => isBattleCompleted;
    public bool IsBattleCompletionPending => isBattleCompletionPending;
    public bool IsStageCleared => isBattleCompleted;
    public bool IsResolvingTurn => isResolvingTurn
        || pendingDetachedEnemyAttacks > 0;
    public int CurrentEnemyTurnCycle => currentEnemyTurnCycle;
    public CombatPacingMode PacingMode => combatPacingMode;
    public bool HasRemainingEnemiesToSpawn =>
        combatPacingMode == CombatPacingMode.DuelClock
        && isDuelClockEnemyPoolConfigured
        && duelClockEnemySpawnPool.RemainingCount > 0;
    public int RemainingUnspawnedEnemyCount =>
        HasRemainingEnemiesToSpawn
            ? duelClockEnemySpawnPool.RemainingCount
            : 0;
    public BossBombManager BombManager => bossBombManager;
    internal EnemyBattleProgress EnemyProgress =>
        combatPacingMode == CombatPacingMode.DuelClock
            && isDuelClockEnemyPoolConfigured
            ? CalculateDuelClockEnemyProgress(
                duelClockEnemySpawnPool.InitialCount,
                duelClockEnemySpawnPool.RemainingCount,
                GetLivingEnemyCount())
            : CalculateEnemyProgress(
                waves,
                currentWaveIndex,
                GetLivingEnemyCount());
    internal long PendingEnemyTurnCycles => pendingEnemyTurnCycles;
    internal int PendingDetachedEnemyAttackCount =>
        pendingDetachedEnemyAttacks;

    private readonly EnemyAttackHoverPresenter attackHover = new EnemyAttackHoverPresenter();

    private void LateUpdate()
    {
        attackHover.Update(boardManager, activeEnemies, isBattleCompleted);
    }

    private void Awake()
    {
        activeEnemies.Clear();
        reservedSpawnTileIndices.Clear();
        movementTileReservations.Clear();
        currentWaveIndex = -1;
        remainingSpawnTurns = 0;
        isWaitingForNextWave = false;
        isBattleCompleted = false;
        isBattleCompletionPending = false;
        pendingDetachedEnemyAttacks = 0;
        detachedEnemyAttackVisuals.Clear();
        duelClockController = GetComponent<DuelClockController>();
    }

    private void OnEnable()
    {
        if (playerMove != null)
        {
            playerMove.TurnCompleted += HandlePlayerTurnCompleted;
        }

        SubscribeToActiveEnemies();
    }

    private void Start()
    {
        if (ValidateReferences())
        {
            playerMove.SetWaveManager(this);
            EnsureBossBombManager();
        }
    }

    private void OnDisable()
    {
        attackHover.Clear();
        ClearSpawnWarnings();

        CancelManagedCoroutines();

        isResolvingTurn = false;
        pendingEnemyTurnCycles = 0;
        movementTileReservations.Clear();
        DeactivateCombatPacing();

        if (playerMove != null)
        {
            playerMove.TurnCompleted -= HandlePlayerTurnCompleted;
            playerMove.SetEnemyTurnResolving(false);
        }

        UnsubscribeFromActiveEnemies();
    }

    public bool BeginBattle(
        IReadOnlyList<EnemyWave> configuredWaves,
        int configuredSpawnTerm)
    {
        return BeginBattleInternal(
            configuredWaves,
            configuredSpawnTerm,
            null,
            CombatPacingMode.Legacy);
    }

    public bool BeginBattle(BattleData battleData)
    {
        return battleData != null && BeginBattleInternal(
            battleData.Waves,
            battleData.SpawnTerm,
            battleData,
            battleData.PacingMode);
    }

    private bool BeginBattleInternal(
        IReadOnlyList<EnemyWave> configuredWaves,
        int configuredSpawnTerm,
        BattleData battleData,
        CombatPacingMode configuredPacingMode)
    {
        if (!ValidateReferences())
        {
            return false;
        }

        ResetBattleRuntime();
        ConfigureMaximumActiveEnemyCount();
        playerMove.SetWaveManager(this);
        playerMove.ResetKickCooldownForBattle();
        EnsureBossBombManager();
        bossBombManager.ResumeForBattle();
        spawnTerm = Mathf.Max(0, configuredSpawnTerm);

        int waveCount = configuredWaves == null ? 0 : configuredWaves.Count;
        waves = new EnemyWave[waveCount];

        for (int waveIndex = 0; waveIndex < waveCount; waveIndex++)
        {
            waves[waveIndex] = configuredWaves[waveIndex];
        }

        bool usesDuelClock = battleData != null
            && configuredPacingMode == CombatPacingMode.DuelClock;

        if (usesDuelClock)
        {
            if (!ConfigureDuelClockEnemyPoolFresh(battleData)
                || !TrySpawnOneDuelClockEnemy())
            {
                ResetBattleRuntime();
                return false;
            }

            currentWaveIndex = 0;
        }
        else
        {
            if (!ValidateConfiguredWaves() || !TrySpawnNextWave())
            {
                return false;
            }
        }

        ConfigureCombatPacingFresh(battleData, configuredPacingMode);
        return true;
    }

    public void CaptureRunState(RunSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        saveData.currentWaveIndex = currentWaveIndex;
        saveData.remainingSpawnTurns = remainingSpawnTurns;
        saveData.isWaitingForNextWave = isWaitingForNextWave;
        saveData.isBattleCompletionPending = isBattleCompletionPending;
        saveData.currentEnemyTurnCycle = currentEnemyTurnCycle;
        CaptureCombatPacing(saveData);
        CaptureDuelClockEnemyPool(saveData);
        saveData.reservedSpawnTileIndices.Clear();
        saveData.reservedSpawnTileIndices.AddRange(
            reservedSpawnTileIndices);
        saveData.enemies.Clear();

        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy != null && enemy.CurrentHealth > 0)
            {
                saveData.enemies.Add(enemy.CaptureRunState(activeEnemies));
            }
        }

        bossBombManager?.CaptureRunState(saveData.bombs);
    }

    public bool RestoreBattle(
        IReadOnlyList<EnemyWave> configuredWaves,
        int configuredSpawnTerm,
        RunSaveData saveData)
    {
        return RestoreBattleInternal(
            configuredWaves,
            configuredSpawnTerm,
            null,
            CombatPacingMode.Legacy,
            saveData);
    }

    public bool RestoreBattle(
        BattleData battleData,
        RunSaveData saveData)
    {
        if (battleData == null || saveData == null)
        {
            return false;
        }

        CombatPacingMode savedPacingMode =
            saveData.combatPacingMode == (int)CombatPacingMode.DuelClock
                ? CombatPacingMode.DuelClock
                : CombatPacingMode.Legacy;
        return RestoreBattleInternal(
            battleData.Waves,
            battleData.SpawnTerm,
            battleData,
            savedPacingMode,
            saveData);
    }

    private bool RestoreBattleInternal(
        IReadOnlyList<EnemyWave> configuredWaves,
        int configuredSpawnTerm,
        BattleData battleData,
        CombatPacingMode configuredPacingMode,
        RunSaveData saveData)
    {
        if (!ValidateReferences() || saveData == null)
        {
            return false;
        }

        ResetBattleRuntime();
        ConfigureMaximumActiveEnemyCount();
        playerMove.SetWaveManager(this);
        EnsureBossBombManager();
        bossBombManager.ResumeForBattle();
        spawnTerm = Mathf.Max(0, configuredSpawnTerm);
        int waveCount = configuredWaves == null ? 0 : configuredWaves.Count;
        waves = new EnemyWave[waveCount];

        for (int index = 0; index < waveCount; index++)
        {
            waves[index] = configuredWaves[index];
        }

        bool usesDuelClock = battleData != null
            && configuredPacingMode == CombatPacingMode.DuelClock;

        bool invalidLegacyState = !usesDuelClock
            && (!ValidateConfiguredWaves()
                || saveData.currentWaveIndex < 0
                || saveData.currentWaveIndex >= waves.Length);
        bool invalidDuelClockState = usesDuelClock
            && (saveData.currentWaveIndex != 0
                || !RestoreDuelClockEnemyPool(battleData, saveData));

        if (invalidLegacyState || invalidDuelClockState)
        {
            return false;
        }

        currentWaveIndex = saveData.currentWaveIndex;
        remainingSpawnTurns = Mathf.Max(0, saveData.remainingSpawnTurns);
        isWaitingForNextWave = saveData.isWaitingForNextWave;
        isBattleCompletionPending = saveData.isBattleCompletionPending;
        isBattleCompleted = false;
        isResolvingTurn = false;
        currentEnemyTurnCycle = Mathf.Max(
            0,
            saveData.currentEnemyTurnCycle);
        reservedSpawnTileIndices.Clear();

        if (saveData.reservedSpawnTileIndices != null)
        {
            foreach (int cellKey in saveData.reservedSpawnTileIndices)
            {
                if (TryGetSpawnCell(cellKey, out SpawnCell cell)
                    && !reservedSpawnTileIndices.Contains(cellKey))
                {
                    reservedSpawnTileIndices.Add(cellKey);
                    boardManager.SetTileWarningActive(
                        cell.TileIndex,
                        cell.LaneIndex,
                        true);
                }
            }
        }

        List<RunEnemySaveData> savedEnemies = saveData.enemies
            ?? new List<RunEnemySaveData>();

        if (savedEnemies.Count > maximumActiveEnemyCount)
        {
            Debug.LogError(
                $"A saved battle cannot restore more than {maximumActiveEnemyCount} active enemies.",
                this);
            ResetBattleRuntime();
            return false;
        }

        foreach (RunEnemySaveData savedEnemy in savedEnemies)
        {
            EnemyData enemyData = ResolveSavedEnemy(
                savedEnemy == null ? string.Empty : savedEnemy.enemyAssetName);

            if (savedEnemy == null || enemyData == null
                || !TrySpawnEnemy(
                    enemyData,
                    savedEnemy.tileIndex,
                    savedEnemy.laneIndex,
                    out _))
            {
                ResetBattleRuntime();
                return false;
            }
        }

        for (int index = 0; index < savedEnemies.Count; index++)
        {
            RunEnemySaveData savedEnemy = savedEnemies[index];
            EnemyController supportTarget = savedEnemy != null
                && savedEnemy.preparedSupportTargetIndex >= 0
                && savedEnemy.preparedSupportTargetIndex < activeEnemies.Count
                    ? activeEnemies[savedEnemy.preparedSupportTargetIndex]
                    : null;
            activeEnemies[index].RestoreRunState(
                savedEnemy,
                supportTarget);
        }

        if (bossBombManager != null
            && !bossBombManager.RestoreRunState(
                saveData.bombs,
                ResolveSavedEnemy))
        {
            ResetBattleRuntime();
            return false;
        }

        bool hasRestoredBattle = activeEnemies.Count > 0
            || isWaitingForNextWave
            || usesDuelClock
            && duelClockEnemySpawnPool.RemainingCount > 0;

        if (!hasRestoredBattle)
        {
            ResetBattleRuntime();
            return false;
        }

        playerMove.SetEnemyTurnResolving(false);
        ConfigureCombatPacingRestored(
            battleData,
            configuredPacingMode,
            saveData);

        if (usesDuelClock)
        {
            ResolveEmptyDuelClockBattle();
        }

        StateChanged?.Invoke();
        return true;
    }

    private EnemyData ResolveSavedEnemy(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return null;
        }

        foreach (EnemyData enemy in duelClockAuthoredEnemies)
        {
            if (enemy != null && string.Equals(
                    enemy.name,
                    assetName,
                    StringComparison.Ordinal))
            {
                return enemy;
            }
        }

        if (waves == null)
        {
            return null;
        }

        foreach (EnemyWave wave in waves)
        {
            if (wave == null)
            {
                continue;
            }

            foreach (EnemyWaveEntry entry in wave.Enemies)
            {
                if (entry?.EnemyData != null && string.Equals(
                        entry.EnemyData.name,
                        assetName,
                        StringComparison.Ordinal))
                {
                    return entry.EnemyData;
                }
            }
        }

        return null;
    }

    public void StopBattle()
    {
        ResetBattleRuntime();
    }

    public bool IsTileOccupied(int tileIndex, EnemyController ignoredEnemy = null)
    {
        return IsTileOccupied(tileIndex, 0, ignoredEnemy);
    }

    public bool IsTileOccupied(
        int tileIndex,
        int laneIndex,
        EnemyController ignoredEnemy = null)
    {
        return TryGetEnemyAtTile(
            tileIndex,
            laneIndex,
            out _,
            ignoredEnemy);
    }

    private bool IsPlayerAtTile(int tileIndex)
    {
        return IsPlayerAtTile(tileIndex, 0);
    }

    private bool IsPlayerAtTile(int tileIndex, int laneIndex)
    {
        return playerMove != null && boardManager != null
            && playerMove.CurrentLaneIndex == laneIndex
            && boardManager.TryGetTileIndex(
                playerMove.transform.position,
                playerMove.CurrentLaneIndex,
                out int playerTileIndex)
            && playerTileIndex == tileIndex;
    }

    public bool IsTileReservedForMovement(
        int tileIndex,
        Component ignoredOwner = null)
    {
        return IsTileReservedForMovement(tileIndex, 0, ignoredOwner);
    }

    public bool IsTileReservedForMovement(
        int tileIndex,
        int laneIndex,
        Component ignoredOwner = null)
    {
        RemoveStaleMovementReservations();
        return movementTileReservations.TryGetValue(
                GetBoardCellKey(tileIndex, laneIndex),
                out Component owner)
            && owner != ignoredOwner;
    }

    internal bool HasMovementReservation(Component owner)
    {
        if (owner == null)
        {
            return false;
        }

        RemoveStaleMovementReservations();

        foreach (Component reservedOwner in movementTileReservations.Values)
        {
            if (reservedOwner == owner)
            {
                return true;
            }
        }

        return false;
    }

    internal bool TryReserveMovementTile(Component owner, int tileIndex)
    {
        return TryReserveMovementTile(owner, tileIndex, 0);
    }

    internal bool TryReserveMovementTile(
        Component owner,
        int tileIndex,
        int laneIndex)
    {
        return TryReserveMovementTiles(owner, new[] { tileIndex }, laneIndex);
    }

    internal bool TryReserveMovementTiles(
        Component owner,
        IReadOnlyList<int> tileIndices)
    {
        return TryReserveMovementTiles(owner, tileIndices, 0);
    }

    internal bool TryReserveMovementTiles(
        Component owner,
        IReadOnlyList<int> tileIndices,
        int laneIndex)
    {
        if (owner == null || tileIndices == null || tileIndices.Count == 0)
        {
            return false;
        }

        RemoveStaleMovementReservations();

        for (int index = 0; index < tileIndices.Count; index++)
        {
            int tileIndex = tileIndices[index];
            int cellKey = GetBoardCellKey(tileIndex, laneIndex);

            if (cellKey < 0
                || movementTileReservations.TryGetValue(
                    cellKey,
                    out Component reservedOwner)
                && reservedOwner != owner)
            {
                return false;
            }
        }

        ReleaseMovementTiles(owner);

        for (int index = 0; index < tileIndices.Count; index++)
        {
            movementTileReservations[
                GetBoardCellKey(tileIndices[index], laneIndex)] = owner;
        }

        return true;
    }

    internal bool TryReserveMovementSwap(
        Component firstOwner,
        int firstTargetTileIndex,
        Component secondOwner,
        int secondTargetTileIndex)
    {
        return TryReserveMovementSwap(
            firstOwner,
            firstTargetTileIndex,
            0,
            secondOwner,
            secondTargetTileIndex,
            0);
    }

    internal bool TryReserveMovementSwap(
        Component firstOwner,
        int firstTargetTileIndex,
        int firstTargetLaneIndex,
        Component secondOwner,
        int secondTargetTileIndex,
        int secondTargetLaneIndex)
    {
        int firstCellKey = GetBoardCellKey(
            firstTargetTileIndex,
            firstTargetLaneIndex);
        int secondCellKey = GetBoardCellKey(
            secondTargetTileIndex,
            secondTargetLaneIndex);

        if (firstOwner == null || secondOwner == null
            || firstOwner == secondOwner
            || firstCellKey < 0 || secondCellKey < 0
            || firstCellKey == secondCellKey)
        {
            return false;
        }

        RemoveStaleMovementReservations();

        if (IsReservedByAnotherOwner(firstCellKey, firstOwner, secondOwner)
            || IsReservedByAnotherOwner(
                secondCellKey,
                firstOwner,
                secondOwner))
        {
            return false;
        }

        ReleaseMovementTiles(firstOwner);
        ReleaseMovementTiles(secondOwner);
        movementTileReservations[firstCellKey] = firstOwner;
        movementTileReservations[secondCellKey] = secondOwner;
        return true;
    }

    internal void ReleaseMovementTiles(Component owner)
    {
        if (owner == null)
        {
            RemoveStaleMovementReservations();
            return;
        }

        movementReservationCleanupBuffer.Clear();

        foreach (KeyValuePair<int, Component> reservation
                 in movementTileReservations)
        {
            if (reservation.Value == null || reservation.Value == owner)
            {
                movementReservationCleanupBuffer.Add(reservation.Key);
            }
        }

        RemoveBufferedMovementReservations();
    }

    private bool IsReservedByAnotherOwner(
        int tileIndex,
        Component firstAllowedOwner,
        Component secondAllowedOwner)
    {
        return movementTileReservations.TryGetValue(
                tileIndex,
                out Component reservedOwner)
            && reservedOwner != firstAllowedOwner
            && reservedOwner != secondAllowedOwner;
    }

    private void RemoveStaleMovementReservations()
    {
        movementReservationCleanupBuffer.Clear();

        foreach (KeyValuePair<int, Component> reservation
                 in movementTileReservations)
        {
            if (reservation.Value == null)
            {
                movementReservationCleanupBuffer.Add(reservation.Key);
            }
        }

        RemoveBufferedMovementReservations();
    }

    private void RemoveBufferedMovementReservations()
    {
        foreach (int tileIndex in movementReservationCleanupBuffer)
        {
            movementTileReservations.Remove(tileIndex);
        }

        movementReservationCleanupBuffer.Clear();
    }

    public bool TryGetFirstBulletBlocker(
        Vector3 originWorldPosition,
        int direction,
        int maxRange,
        out IPlayerBulletBlocker blocker)
    {
        blocker = null;
        return false;
    }

    public bool TryGetEnemyAtTile(
        int tileIndex,
        out EnemyController foundEnemy,
        EnemyController ignoredEnemy = null)
    {
        return TryGetEnemyAtTile(
            tileIndex,
            0,
            out foundEnemy,
            ignoredEnemy);
    }

    public bool TryGetEnemyAtTile(
        int tileIndex,
        int laneIndex,
        out EnemyController foundEnemy,
        EnemyController ignoredEnemy = null)
    {
        foundEnemy = null;

        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy == null || enemy == ignoredEnemy)
            {
                continue;
            }

            if (enemy.CurrentLaneIndex == laneIndex
                && boardManager.TryGetTileIndex(
                    enemy.transform.position,
                    enemy.CurrentLaneIndex,
                    out int enemyIndex)
                && enemyIndex == tileIndex)
            {
                foundEnemy = enemy;
                return true;
            }
        }

        return false;
    }

    public bool IsTileReservedForSpawn(int tileIndex)
    {
        return IsTileReservedForSpawn(tileIndex, 0);
    }

    public bool IsTileReservedForSpawn(int tileIndex, int laneIndex)
    {
        int cellKey = GetBoardCellKey(tileIndex, laneIndex);
        return cellKey >= 0 && reservedSpawnTileIndices.Contains(cellKey);
    }

    public void GetEnemiesInDirection(
        Vector3 originWorldPosition,
        int direction,
        int maxRange,
        List<EnemyController> results)
    {
        GetEnemiesInDirection(
            originWorldPosition,
            0,
            direction,
            maxRange,
            results);
    }

    public void GetEnemiesInDirection(
        Vector3 originWorldPosition,
        int laneIndex,
        int direction,
        int maxRange,
        List<EnemyController> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        enemyTargetBuffer.Clear();

        if (boardManager == null || direction == 0 || maxRange <= 0
            || !boardManager.TryGetTileIndex(originWorldPosition, laneIndex, out int originIndex))
        {
            return;
        }

        int normalizedDirection = direction > 0 ? 1 : -1;

        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy == null || enemy.CurrentHealth <= 0
                || enemy.CurrentLaneIndex != laneIndex
                || !boardManager.TryGetTileIndex(
                    enemy.transform.position,
                    enemy.CurrentLaneIndex,
                    out int enemyIndex))
            {
                continue;
            }

            int offset = enemyIndex - originIndex;

            if (offset * normalizedDirection > 0 && Mathf.Abs(offset) <= maxRange)
            {
                enemyTargetBuffer.Add(new EnemyTargetData(
                    enemy,
                    Mathf.Abs(offset),
                    enemyIndex));
            }
        }

        enemyTargetBuffer.Sort(CompareEnemyTargets);

        foreach (EnemyTargetData targetData in enemyTargetBuffer)
        {
            results.Add(targetData.Enemy);
        }
    }

    private int GetBoardCellKey(int tileIndex, int laneIndex)
    {
        int boardCount = boardManager == null
            ? 1000000
            : boardManager.BoardCount;
        int laneCount = boardManager == null
            ? int.MaxValue / boardCount
            : boardManager.LaneCount;
        return EncodeBoardCellKey(
            tileIndex,
            laneIndex,
            boardCount,
            laneCount);
    }

    internal static int EncodeBoardCellKey(
        int tileIndex,
        int laneIndex,
        int boardCount,
        int laneCount)
    {
        if (boardCount <= 0 || laneCount <= 0 || tileIndex < 0
            || tileIndex >= boardCount || laneIndex < 0
            || laneIndex >= laneCount)
        {
            return -1;
        }

        return laneIndex * boardCount + tileIndex;
    }

    internal static bool TryDecodeBoardCellKey(
        int cellKey,
        int boardCount,
        int laneCount,
        out int tileIndex,
        out int laneIndex)
    {
        tileIndex = -1;
        laneIndex = -1;

        if (boardCount <= 0 || laneCount <= 0 || cellKey < 0
            || cellKey >= boardCount * laneCount)
        {
            return false;
        }

        laneIndex = cellKey / boardCount;
        tileIndex = cellKey % boardCount;
        return true;
    }

    private bool TryGetSpawnCell(int cellKey, out SpawnCell cell)
    {
        cell = default;

        if (boardManager == null
            || !TryDecodeBoardCellKey(
                cellKey,
                boardManager.BoardCount,
                boardManager.LaneCount,
                out int tileIndex,
                out int laneIndex))
        {
            return false;
        }

        cell = new SpawnCell(tileIndex, laneIndex, cellKey);
        return true;
    }

    private void HandlePlayerTurnCompleted()
    {
        if (combatPacingMode == CombatPacingMode.DuelClock
            || isResolvingTurn || isBattleCompleted || !ValidateReferences()
            || playerHealth.IsDefeated)
        {
            return;
        }

        QueueEnemyTurnCycles(1);
    }

    private void HandleDuelClockBeatsCommitted(long beatCount)
    {
        if (combatPacingMode == CombatPacingMode.DuelClock)
        {
            long queuedBeatCount = QueueEnemyTurnCycles(beatCount);

            if (queuedBeatCount > 0)
            {
                DuelClockBeatsCommitted?.Invoke(queuedBeatCount);
            }
        }
    }

    internal void QueueDuelClockBeats(long beatCount)
    {
        HandleDuelClockBeatsCommitted(beatCount);
    }

    private long QueueEnemyTurnCycles(long cycleCount)
    {
        if (cycleCount <= 0 || isBattleCompleted || !ValidateReferences()
            || playerHealth.IsDefeated)
        {
            return 0L;
        }

        if (combatPacingMode == CombatPacingMode.DuelClock)
        {
            cycleCount = Math.Min(
                cycleCount,
                Math.Max(0L, 1L - pendingEnemyTurnCycles));
        }

        if (cycleCount > long.MaxValue - pendingEnemyTurnCycles)
        {
            cycleCount = long.MaxValue - pendingEnemyTurnCycles;
        }

        if (cycleCount <= 0)
        {
            return 0L;
        }

        pendingEnemyTurnCycles += cycleCount;

        if (enemyTurnCoroutine == null)
        {
            enemyTurnCoroutine = StartCoroutine(ResolveEnemyTurnCycles());
        }

        return cycleCount;
    }

    private IEnumerator ResolveEnemyTurnCycles()
    {
        isResolvingTurn = true;
        playerMove.SetEnemyTurnResolving(true);
        StateChanged?.Invoke();

        bool usesDuelClock =
            combatPacingMode == CombatPacingMode.DuelClock;

        while (pendingEnemyTurnCycles > 0
               && !isBattleCompleted && !playerHealth.IsDefeated)
        {
            if (usesDuelClock || GamePauseController.IsPaused)
            {
                yield return WaitForPlayerActionToSettle(usesDuelClock);
            }

            if (isBattleCompleted || playerHealth.IsDefeated)
            {
                break;
            }

            pendingEnemyTurnCycles--;

            if (usesDuelClock)
            {
                duelClockController?.HandleEnemyCycleStarted();
                playerMove.ProcessDuelClockStatusBeat();
            }

            currentEnemyTurnCycle++;
            yield return ResolveOneEnemyTurnCycle();
        }

        pendingEnemyTurnCycles = 0;
        playerMove.SetEnemyTurnResolving(false);
        isResolvingTurn = false;
        enemyTurnCoroutine = null;
        StateChanged?.Invoke();

        if (!usesDuelClock && !isBattleCompleted && !playerHealth.IsDefeated)
        {
            playerMove.TrySkipStunnedTurn();
        }
    }

    private IEnumerator WaitForPlayerActionToSettle(
        bool waitForPlayerAction)
    {
        while (GamePauseController.IsPaused
               || ShouldWaitForPlayerAction(
                   waitForPlayerAction,
                   playerMove != null && playerMove.IsShooting,
                   playerMove != null && playerMove.IsActing))
        {
            yield return null;
        }
    }

    internal static bool ShouldWaitForPlayerAction(
        bool usesDuelClock,
        bool isShooting,
        bool isActing)
    {
        return usesDuelClock && (isShooting || isActing);
    }

    internal bool TryStartDetachedEnemyAttack(
        IEnumerator attackRoutine,
        GameObject attackVisual)
    {
        if (attackRoutine == null || !isActiveAndEnabled
            || isBattleCompleted)
        {
            return false;
        }

        pendingDetachedEnemyAttacks++;

        if (attackVisual != null)
        {
            detachedEnemyAttackVisuals.Add(attackVisual);
        }

        StartCoroutine(ResolveDetachedEnemyAttack(
            attackRoutine,
            attackVisual));
        StateChanged?.Invoke();
        return true;
    }

    private IEnumerator ResolveDetachedEnemyAttack(
        IEnumerator attackRoutine,
        GameObject attackVisual)
    {
        try
        {
            while (attackRoutine.MoveNext())
            {
                yield return attackRoutine.Current;
            }
        }
        finally
        {
            (attackRoutine as IDisposable)?.Dispose();
            detachedEnemyAttackVisuals.Remove(attackVisual);

            if (attackVisual != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(attackVisual);
                }
                else
                {
                    DestroyImmediate(attackVisual);
                }
            }

            pendingDetachedEnemyAttacks = Mathf.Max(
                0,
                pendingDetachedEnemyAttacks - 1);

            if (!isCancellingManagedCoroutines)
            {
                ResolveBattleAfterDetachedEnemyAttacks();
                StateChanged?.Invoke();
            }
        }
    }

    private void ResolveBattleAfterDetachedEnemyAttacks()
    {
        if (pendingDetachedEnemyAttacks > 0 || isBattleCompleted)
        {
            return;
        }

        if (playerHealth != null && playerHealth.IsDefeated)
        {
            isBattleCompletionPending = false;
            return;
        }

        if (activeEnemies.Count > 0)
        {
            isBattleCompletionPending = false;
            return;
        }

        if (combatPacingMode == CombatPacingMode.DuelClock)
        {
            ResolveEmptyDuelClockBattle();
        }
        else
        {
            HandleWaveCleared();
        }
    }

    private IEnumerator ResolveOneEnemyTurnCycle()
    {
        RemoveMissingEnemies();

        EnemyController[] enemiesThisTurn = activeEnemies.ToArray();
        List<EnemyController> concurrentActions = new List<EnemyController>();
        float turnStartedAt = Time.time;

        for (int enemyIndex = 0;
             enemyIndex < enemiesThisTurn.Length;
             enemyIndex++)
        {
            EnemyController enemy = enemiesThisTurn[enemyIndex];

            if (enemy != null && activeEnemies.Contains(enemy))
            {
                bool usesDedicatedMotion =
                    enemy.WillExecuteDedicatedTurnMotion;

                if (usesDedicatedMotion)
                {
                    yield return WaitForEnemyActions(concurrentActions);
                    concurrentActions.Clear();
                }

                enemy.TakeTurn();

                if (!usesDedicatedMotion)
                {
                    if (enemy != null && enemy.IsActing)
                    {
                        concurrentActions.Add(enemy);
                    }

                    continue;
                }

                yield return WaitForEnemyAction(enemy);

                if (playerHealth.IsDefeated)
                {
                    break;
                }

                bool performedAttack = enemy != null
                    && enemy.LastTurnAction == EnemyTurnActionType.Fire;

                if (performedAttack
                    && enemyIndex < enemiesThisTurn.Length - 1)
                {
                    yield return WaitForTurnTime(enemyActionInterval);
                }
            }
        }

        yield return WaitForEnemyActions(concurrentActions);
        yield return WaitForDetachedEnemyAttacks();

        float remainingTurnDelay = Mathf.Max(
            0f,
            enemyTurnDelay - (Time.time - turnStartedAt));
        yield return WaitForTurnTime(remainingTurnDelay);

        RemoveMissingEnemies();
        bossBombManager?.ProcessEnemyTurnCycleEnd(currentEnemyTurnCycle);

        while (bossBombManager != null
               && bossBombManager.IsResolvingExplosions
               && !isBattleCompleted && !playerHealth.IsDefeated)
        {
            yield return null;
        }

        RemoveMissingEnemies();
        EnemyTurnCycleCompleted?.Invoke(currentEnemyTurnCycle);
        AdvanceWaveCountdown();
        StateChanged?.Invoke();
    }

    private IEnumerator WaitForTurnTime(float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                elapsedTime += Time.deltaTime;
            }
        }
    }

    private static IEnumerator WaitForEnemyAction(EnemyController enemy)
    {
        while (enemy != null && enemy.IsActing)
        {
            yield return null;
        }
    }

    private static IEnumerator WaitForEnemyActions(
        IReadOnlyList<EnemyController> enemies)
    {
        if (enemies == null || enemies.Count == 0)
        {
            yield break;
        }

        bool hasRunningAction = true;

        while (hasRunningAction)
        {
            hasRunningAction = false;

            for (int index = 0; index < enemies.Count; index++)
            {
                EnemyController enemy = enemies[index];

                if (enemy != null && enemy.IsActing)
                {
                    hasRunningAction = true;
                    break;
                }
            }

            if (hasRunningAction)
            {
                yield return null;
            }
        }
    }

    private bool TrySpawnNextWave()
    {
        int nextWaveIndex = currentWaveIndex + 1;

        if (waves == null || nextWaveIndex < 0 || nextWaveIndex >= waves.Length)
        {
            CompleteBattle();
            return false;
        }

        EnemyWave nextWave = waves[nextWaveIndex];

        if (!TryGetWaveEnemyCount(nextWave, out int enemyCount)
            || enemyCount > GetAvailableSpawnTileCount())
        {
            Debug.LogError(
                $"Wave {nextWaveIndex + 1} must contain valid EnemyData and fit on the available board tiles.",
                this);
            return false;
        }

        List<SpawnCell> spawnCells;

        if (reservedSpawnTileIndices.Count == enemyCount)
        {
            spawnCells = new List<SpawnCell>(enemyCount);

            foreach (int cellKey in reservedSpawnTileIndices)
            {
                if (!TryGetSpawnCell(cellKey, out SpawnCell cell))
                {
                    spawnCells.Clear();
                    break;
                }

                spawnCells.Add(cell);
            }
        }
        else
        {
            spawnCells = null;
        }

        if (spawnCells == null || spawnCells.Count != enemyCount)
        {
            if (!TrySelectSpawnCells(enemyCount, out spawnCells))
            {
                Debug.LogError(
                    $"Wave {nextWaveIndex + 1} spawn cells could not be selected.",
                    this);
                return false;
            }
        }

        List<EnemyController> spawnedEnemies = new List<EnemyController>();
        int spawnTileListIndex = 0;

        foreach (EnemyWaveEntry entry in nextWave.Enemies)
        {
            for (int count = 0; count < entry.Count; count++)
            {
                SpawnCell spawnCell = spawnCells[spawnTileListIndex];
                spawnTileListIndex++;

                if (!TrySpawnEnemy(
                        entry.EnemyData,
                        spawnCell.TileIndex,
                        spawnCell.LaneIndex,
                        out EnemyController enemy))
                {
                    RollBackWaveSpawn(spawnedEnemies);
                    Debug.LogError(
                        $"Wave {nextWaveIndex + 1} could not be spawned completely.",
                        this);
                    return false;
                }

                spawnedEnemies.Add(enemy);
            }
        }

        currentWaveIndex = nextWaveIndex;
        remainingSpawnTurns = 0;
        isWaitingForNextWave = false;
        ClearSpawnWarnings();
        StateChanged?.Invoke();
        return true;
    }

    private bool TrySpawnEnemy(
        EnemyData enemyData,
        int spawnTileIndex,
        out EnemyController spawnedEnemy)
    {
        return TrySpawnEnemy(
            enemyData,
            spawnTileIndex,
            0,
            out spawnedEnemy);
    }

    private bool TrySpawnEnemy(
        EnemyData enemyData,
        int spawnTileIndex,
        int spawnLaneIndex,
        out EnemyController spawnedEnemy)
    {
        spawnedEnemy = null;

        if (enemyData == null || enemyPrefabTemplate == null
            || CalculateAvailableEnemySlots(
                GetLivingEnemyCount(),
                maximumActiveEnemyCount) <= 0
            || IsPlayerAtTile(spawnTileIndex, spawnLaneIndex)
            || IsTileOccupied(spawnTileIndex, spawnLaneIndex)
            || IsTileReservedForMovement(spawnTileIndex, spawnLaneIndex)
            || !boardManager.TryGetTilePosition(
                spawnTileIndex,
                spawnLaneIndex,
                out Vector3 spawnPosition))
        {
            return false;
        }

        spawnPosition += spawnPositionOffset;

        EnemyController enemy = Instantiate(
            enemyPrefabTemplate,
            spawnPosition,
            Quaternion.identity,
            enemyParent);

        if (!enemy.Initialize(
                enemyData,
                boardManager,
                playerMove,
                playerHealth,
                this,
                spawnLaneIndex))
        {
            Destroy(enemy.gameObject);
            return false;
        }

        activeEnemies.Add(enemy);
        enemy.Defeated += HandleEnemyDefeated;
        spawnedEnemy = enemy;
        return true;
    }

    private bool TrySpawnOneDuelClockEnemy()
    {
        if (duelClockEnemySpawnPool.IsExhausted
            || !TrySelectSpawnCells(
                1,
                out List<SpawnCell> spawnCells))
        {
            return false;
        }

        if (!duelClockEnemySpawnPool.TrySelect(
                UnityEngine.Random.value,
                out int selectedEntryIndex,
                out EnemyData enemyData)
            || !TrySpawnEnemy(
                enemyData,
                spawnCells[0].TileIndex,
                spawnCells[0].LaneIndex,
                out EnemyController spawnedEnemy))
        {
            return false;
        }

        if (!duelClockEnemySpawnPool.TryCommitSpawn(
                selectedEntryIndex,
                enemyData))
        {
            RollBackWaveSpawn(new List<EnemyController> { spawnedEnemy });
            return false;
        }

        return true;
    }

    private bool TryGetWaveEnemyCount(EnemyWave wave, out int enemyCount)
    {
        enemyCount = 0;

        if (wave == null || wave.Enemies == null || wave.Enemies.Count == 0)
        {
            return false;
        }

        foreach (EnemyWaveEntry entry in wave.Enemies)
        {
            if (entry == null || entry.EnemyData == null || entry.Count <= 0)
            {
                return false;
            }

            if (entry.Count > maximumActiveEnemyCount - enemyCount)
            {
                return false;
            }

            enemyCount += entry.Count;
        }

        return enemyCount > 0;
    }

    private int GetAvailableSpawnTileCount()
    {
        if (boardManager == null || playerMove == null
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                playerMove.CurrentLaneIndex,
                out int playerIndex))
        {
            return 0;
        }

        int availableCount = 0;

        for (int laneIndex = 0;
             laneIndex < boardManager.LaneCount;
             laneIndex++)
        {
            for (int tileIndex = 0;
                 tileIndex < boardManager.BoardCount;
                 tileIndex++)
            {
                if (boardManager.GetColumnIndex(tileIndex, laneIndex)
                        != boardManager.GetColumnIndex(playerIndex, playerMove.CurrentLaneIndex)
                    && !IsTileOccupied(tileIndex, laneIndex)
                    && !IsTileReservedForMovement(tileIndex, laneIndex)
                    && !IsTileReservedForSpawn(tileIndex, laneIndex))
                {
                    availableCount++;
                }
            }
        }

        return Mathf.Min(
            availableCount,
            CalculateAvailableEnemySlots(
                GetLivingEnemyCount(),
                maximumActiveEnemyCount));
    }

    private bool TrySelectSpawnCells(
        int requestedCount,
        out List<SpawnCell> selectedCells)
    {
        selectedCells = new List<SpawnCell>();
        List<SpawnCell> preferredCells = new List<SpawnCell>();
        List<SpawnCell> adjacentFallbackCells = new List<SpawnCell>();

        if (requestedCount <= 0
            || requestedCount > CalculateAvailableEnemySlots(
                GetLivingEnemyCount(),
                maximumActiveEnemyCount)
            || boardManager == null || playerMove == null
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                playerMove.CurrentLaneIndex,
                out int playerIndex))
        {
            return false;
        }

        for (int laneIndex = 0;
             laneIndex < boardManager.LaneCount;
             laneIndex++)
        {
            for (int tileIndex = 0;
                 tileIndex < boardManager.BoardCount;
                 tileIndex++)
            {
                int columnDistance = Mathf.Abs(
                    boardManager.GetColumnIndex(tileIndex, laneIndex)
                    - boardManager.GetColumnIndex(playerIndex, playerMove.CurrentLaneIndex));
                if (columnDistance == 0
                    || IsTileOccupied(tileIndex, laneIndex)
                    || IsTileReservedForMovement(tileIndex, laneIndex)
                    || IsTileReservedForSpawn(tileIndex, laneIndex))
                {
                    continue;
                }

                int cellKey = GetBoardCellKey(tileIndex, laneIndex);
                SpawnCell cell = new SpawnCell(
                    tileIndex,
                    laneIndex,
                    cellKey);

                if (columnDistance == 1)
                {
                    adjacentFallbackCells.Add(cell);
                }
                else
                {
                    preferredCells.Add(cell);
                }
            }
        }

        if (preferredCells.Count
            + adjacentFallbackCells.Count < requestedCount)
        {
            return false;
        }

        int[] projectedLaneCounts = GetLivingEnemyCountByLane();
        SelectBalancedSpawnCells(
            preferredCells,
            requestedCount,
            selectedCells,
            projectedLaneCounts);

        if (selectedCells.Count < requestedCount)
        {
            SelectBalancedSpawnCells(
                adjacentFallbackCells,
                requestedCount,
                selectedCells,
                projectedLaneCounts);
        }

        return selectedCells.Count == requestedCount;
    }

    private int[] GetLivingEnemyCountByLane()
    {
        int laneCount = boardManager == null ? 1 : boardManager.LaneCount;
        int[] counts = new int[Mathf.Max(1, laneCount)];

        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy != null && enemy.CurrentHealth > 0)
            {
                int laneIndex = Mathf.Clamp(
                    enemy.CurrentLaneIndex,
                    0,
                    counts.Length - 1);
                counts[laneIndex]++;
            }
        }

        return counts;
    }

    private void SelectBalancedSpawnCells(
        List<SpawnCell> candidates,
        int requestedTotalCount,
        List<SpawnCell> selectedCells,
        int[] projectedLaneCounts)
    {
        while (selectedCells.Count < requestedTotalCount
               && candidates.Count > 0)
        {
            List<int> candidateLanes = new List<int>();

            foreach (SpawnCell candidate in candidates)
            {
                if (!candidateLanes.Contains(candidate.LaneIndex))
                {
                    candidateLanes.Add(candidate.LaneIndex);
                }
            }

            int selectedLane = SelectLeastPopulatedLane(
                projectedLaneCounts,
                candidateLanes,
                UnityEngine.Random.value);
            List<int> candidateIndices = new List<int>();

            for (int index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].LaneIndex == selectedLane)
                {
                    candidateIndices.Add(index);
                }
            }

            if (candidateIndices.Count == 0)
            {
                break;
            }

            int randomCandidateIndex = candidateIndices[
                UnityEngine.Random.Range(0, candidateIndices.Count)];
            SpawnCell selected = candidates[randomCandidateIndex];
            selectedCells.Add(selected);
            projectedLaneCounts[selected.LaneIndex]++;
            candidates.RemoveAt(randomCandidateIndex);
        }
    }

    internal static int SelectLeastPopulatedLane(
        IReadOnlyList<int> lanePopulations,
        IReadOnlyList<int> candidateLanes,
        float randomValue)
    {
        if (lanePopulations == null || candidateLanes == null
            || candidateLanes.Count == 0)
        {
            return -1;
        }

        int minimumPopulation = int.MaxValue;
        int tiedLaneCount = 0;

        foreach (int laneIndex in candidateLanes)
        {
            if (laneIndex < 0 || laneIndex >= lanePopulations.Count)
            {
                continue;
            }

            int population = Mathf.Max(0, lanePopulations[laneIndex]);

            if (population < minimumPopulation)
            {
                minimumPopulation = population;
                tiedLaneCount = 1;
            }
            else if (population == minimumPopulation)
            {
                tiedLaneCount++;
            }
        }

        if (tiedLaneCount == 0)
        {
            return -1;
        }

        int selectedTieIndex = Mathf.Min(
            tiedLaneCount - 1,
            Mathf.FloorToInt(Mathf.Clamp01(randomValue) * tiedLaneCount));

        foreach (int laneIndex in candidateLanes)
        {
            if (laneIndex < 0 || laneIndex >= lanePopulations.Count
                || Mathf.Max(0, lanePopulations[laneIndex])
                    != minimumPopulation)
            {
                continue;
            }

            if (selectedTieIndex == 0)
            {
                return laneIndex;
            }

            selectedTieIndex--;
        }

        return -1;
    }

    private void RollBackWaveSpawn(List<EnemyController> spawnedEnemies)
    {
        foreach (EnemyController enemy in spawnedEnemies)
        {
            if (enemy == null)
            {
                continue;
            }

            enemy.Defeated -= HandleEnemyDefeated;
            activeEnemies.Remove(enemy);
            Destroy(enemy.gameObject);
        }
    }

    private bool PrepareNextWaveWarnings()
    {
        ClearSpawnWarnings();
        int nextWaveIndex = currentWaveIndex + 1;

        if (waves == null || nextWaveIndex < 0 || nextWaveIndex >= waves.Length
            || !TryGetWaveEnemyCount(waves[nextWaveIndex], out int enemyCount)
            || !TrySelectSpawnCells(
                enemyCount,
                out List<SpawnCell> selectedCells))
        {
            return false;
        }

        foreach (SpawnCell cell in selectedCells)
        {
            reservedSpawnTileIndices.Add(cell.CellKey);
        }

        foreach (int cellKey in reservedSpawnTileIndices)
        {
            if (!TryGetSpawnCell(cellKey, out SpawnCell cell)
                || !boardManager.SetTileWarningActive(
                    cell.TileIndex,
                    cell.LaneIndex,
                    true))
            {
                ClearSpawnWarnings();
                return false;
            }
        }

        return true;
    }

    private void ClearSpawnWarnings()
    {
        if (boardManager != null)
        {
            foreach (int cellKey in reservedSpawnTileIndices)
            {
                if (TryGetSpawnCell(cellKey, out SpawnCell cell))
                {
                    boardManager.SetTileWarningActive(
                        cell.TileIndex,
                        cell.LaneIndex,
                        false);
                }
            }
        }

        reservedSpawnTileIndices.Clear();
    }

    private void HandleEnemyDefeated(EnemyController enemy)
    {
        if (enemy == null)
        {
            return;
        }

        if (rewardManager != null)
        {
            rewardManager.SpawnEnemyDrop(enemy.Data, enemy.transform.position,
                enemy.CurrentLaneIndex);
        }

        enemy.Defeated -= HandleEnemyDefeated;
        ReleaseMovementTiles(enemy);
        activeEnemies.Remove(enemy);
        EnemyDefeated?.Invoke(enemy);
        if (!finalDefeatPresented && IsFinalDefeatForPresentation())
        {
            finalDefeatPresented = true;
            FinalEnemyDefeated?.Invoke(enemy);
        }

        if (activeEnemies.Count == 0 && pendingDetachedEnemyAttacks > 0)
        {
            isBattleCompletionPending = true;
            StateChanged?.Invoke();
            return;
        }

        if (combatPacingMode == CombatPacingMode.DuelClock)
        {
            ResolveEmptyDuelClockBattle();
        }
        else if (activeEnemies.Count == 0)
        {
            HandleWaveCleared();
        }

        StateChanged?.Invoke();
    }

    internal bool IsFinalDefeatForPresentation()
    {
        if (isBattleCompleted || GetLivingEnemyCount() > 0)
        {
            return false;
        }
        return combatPacingMode == CombatPacingMode.DuelClock
            ? isDuelClockEnemyPoolConfigured && duelClockEnemySpawnPool.IsExhausted
            : currentWaveIndex >= 0 && currentWaveIndex == waves.Length - 1;
    }

    private void HandleWaveCleared()
    {
        if (pendingDetachedEnemyAttacks > 0)
        {
            isBattleCompletionPending = true;
            StateChanged?.Invoke();
            return;
        }

        if (combatPacingMode == CombatPacingMode.DuelClock)
        {
            ResolveEmptyDuelClockBattle();
            return;
        }

        if (currentWaveIndex < 0 || isBattleCompleted)
        {
            return;
        }

        if (waves == null || currentWaveIndex >= waves.Length - 1)
        {
            if (playerMove != null && playerMove.IsShooting)
            {
                isBattleCompletionPending = true;
                isWaitingForNextWave = false;
                remainingSpawnTurns = 0;
                ClearSpawnWarnings();
                StateChanged?.Invoke();
                return;
            }

            CompleteBattle();
            return;
        }

        isWaitingForNextWave = true;
        remainingSpawnTurns = Mathf.Max(0, spawnTerm);

        if (!PrepareNextWaveWarnings())
        {
            FailBattle(
                $"Wave {currentWaveIndex + 2} spawn warnings could not be prepared.");
            return;
        }

        if (remainingSpawnTurns == 0)
        {
            if (!TrySpawnNextWave() && !isBattleCompleted)
            {
                FailBattle($"Wave {currentWaveIndex + 2} could not be spawned.");
            }
        }
    }

    private void AdvanceWaveCountdown()
    {
        if (combatPacingMode == CombatPacingMode.DuelClock)
        {
            AdvanceDuelClockEnemySpawns();
            return;
        }

        if (!isWaitingForNextWave || isBattleCompleted || activeEnemies.Count > 0)
        {
            return;
        }

        if (remainingSpawnTurns > 0)
        {
            remainingSpawnTurns--;
            StateChanged?.Invoke();
        }

        if (remainingSpawnTurns == 0)
        {
            if (!TrySpawnNextWave() && !isBattleCompleted)
            {
                FailBattle($"Wave {currentWaveIndex + 2} could not be spawned.");
            }
        }
    }

    private void CompleteBattle()
    {
        if (isBattleCompleted)
        {
            return;
        }

        if (pendingDetachedEnemyAttacks > 0)
        {
            isBattleCompletionPending = true;
            StateChanged?.Invoke();
            return;
        }

        isBattleCompletionPending = false;
        isBattleCompleted = true;
        bossBombManager?.ClearAll();
        rewardManager?.CollectAndDestroyAllDroppedItems();
        isWaitingForNextWave = false;
        remainingSpawnTurns = 0;
        ClearSpawnWarnings();
        DeactivateCombatPacing();
        StateChanged?.Invoke();
        BattleCompleted?.Invoke();
    }

    public void NotifyFiringSequenceCompleted()
    {
        if (combatPacingMode == CombatPacingMode.DuelClock
            && !isBattleCompleted)
        {
            ResolveEmptyDuelClockBattle();
        }

        if (!isBattleCompletionPending || isBattleCompleted
            || pendingDetachedEnemyAttacks > 0)
        {
            return;
        }

        if (activeEnemies.Count > 0)
        {
            isBattleCompletionPending = false;
            return;
        }

        CompleteBattle();
    }

    private void FailBattle(string message)
    {
        if (isBattleCompleted)
        {
            return;
        }

        Debug.LogError(message, this);
        isBattleCompletionPending = false;
        isBattleCompleted = true;
        bossBombManager?.ClearAll();
        isWaitingForNextWave = false;
        remainingSpawnTurns = 0;
        ClearSpawnWarnings();
        DeactivateCombatPacing();
        StateChanged?.Invoke();
        BattleFailed?.Invoke();
    }

    private void CancelManagedCoroutines()
    {
        isCancellingManagedCoroutines = true;
        StopAllCoroutines();
        enemyTurnCoroutine = null;

        foreach (GameObject attackVisual in detachedEnemyAttackVisuals)
        {
            if (attackVisual != null)
            {
                boardManager?.ReleaseTileWarnings(attackVisual.transform);
                Destroy(attackVisual);
            }
        }

        detachedEnemyAttackVisuals.Clear();
        pendingDetachedEnemyAttacks = 0;
        isCancellingManagedCoroutines = false;
    }

    private void ResetBattleRuntime()
    {
        finalDefeatPresented = false;
        attackHover.Clear();
        ClearSpawnWarnings();
        CancelManagedCoroutines();

        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy == null)
            {
                continue;
            }

            enemy.Defeated -= HandleEnemyDefeated;
            enemy.gameObject.SetActive(false);
            Destroy(enemy.gameObject);
        }

        activeEnemies.Clear();
        bossBombManager?.ClearAll();
        reservedSpawnTileIndices.Clear();
        movementTileReservations.Clear();
        currentWaveIndex = -1;
        remainingSpawnTurns = 0;
        isWaitingForNextWave = false;
        isBattleCompletionPending = false;
        isBattleCompleted = false;
        isResolvingTurn = false;
        currentEnemyTurnCycle = 0;
        pendingEnemyTurnCycles = 0;
        pendingDetachedEnemyAttacks = 0;
        duelClockEnemySpawnPool.Clear();
        duelClockSpawnEntries = Array.Empty<DuelClockEnemySpawnEntry>();
        duelClockAuthoredEnemies = Array.Empty<EnemyData>();
        duelClockLegacyAuthoredEnemies = Array.Empty<EnemyData>();
        duelClockEnemySpawnCount = 0;
        pendingDuelClockEnemySpawns = 0;
        isDuelClockEnemyPoolConfigured = false;
        DeactivateCombatPacing();
        playerMove.SetEnemyTurnResolving(false);
        StateChanged?.Invoke();
    }

    private bool ValidateConfiguredWaves()
    {
        if (waves == null || waves.Length == 0)
        {
            Debug.LogError("A battle must contain at least one wave.", this);
            return false;
        }

        int availableSpawnTileCount = GetAvailableSpawnTileCount();

        for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
        {
            if (!TryGetWaveEnemyCount(waves[waveIndex], out int enemyCount)
                || enemyCount > availableSpawnTileCount)
            {
                Debug.LogError(
                    $"Wave {waveIndex + 1} must contain valid EnemyData and fit on the available board tiles.",
                    this);
                return false;
            }
        }

        return true;
    }

    private bool ConfigureDuelClockEnemyPoolFresh(BattleData battleData)
    {
        if (!TryBuildDuelClockEnemyConfiguration(battleData))
        {
            Debug.LogError(
                "A Duel Clock battle must contain a valid weighted enemy pool whose minimum counts fit its total spawn count.",
                this);
            return false;
        }

        if (!duelClockEnemySpawnPool.ConfigureFresh(
                duelClockSpawnEntries,
                duelClockEnemySpawnCount))
        {
            Debug.LogError(
                "A Duel Clock weighted enemy pool could not be initialized.",
                this);
            return false;
        }

        isDuelClockEnemyPoolConfigured = true;
        pendingDuelClockEnemySpawns = 0;
        return true;
    }

    private bool RestoreDuelClockEnemyPool(
        BattleData battleData,
        RunSaveData saveData)
    {
        if (!TryBuildDuelClockEnemyConfiguration(battleData))
        {
            return false;
        }

        bool restored = saveData.duelClockWeightedSpawnStateInitialized
            ? duelClockEnemySpawnPool.Restore(
                duelClockSpawnEntries,
                duelClockEnemySpawnCount,
                saveData.duelClockRemainingEnemySpawnCount,
                saveData.duelClockEnemySpawnCounts,
                saveData.duelClockEnemyMissedSpawnCounts,
                saveData.duelClockLastSpawnedEnemyAssetName,
                ResolveSavedEnemy)
            : TryRestoreLegacyDuelClockEnemyPool(saveData);

        if (restored)
        {
            isDuelClockEnemyPoolConfigured = true;
            pendingDuelClockEnemySpawns = Mathf.Clamp(
                saveData.duelClockPendingEnemySpawns,
                0,
                duelClockEnemySpawnPool.RemainingCount);
            return true;
        }

        Debug.LogError(
            "Saved Duel Clock enemy pool could not be restored from the current BattleData.",
            this);
        return false;
    }

    private void CaptureDuelClockEnemyPool(RunSaveData saveData)
    {
        saveData.duelClockRemainingEnemyAssetNames.Clear();
        saveData.duelClockEnemySpawnCounts.Clear();
        saveData.duelClockEnemyMissedSpawnCounts.Clear();
        saveData.duelClockRemainingEnemySpawnCount = 0;
        saveData.duelClockLastSpawnedEnemyAssetName = string.Empty;

        if (combatPacingMode != CombatPacingMode.DuelClock)
        {
            saveData.duelClockSpawnPoolInitialized = false;
            saveData.duelClockWeightedSpawnStateInitialized = false;
            saveData.duelClockPendingEnemySpawns = 0;
            return;
        }

        saveData.duelClockSpawnPoolInitialized = true;
        saveData.duelClockWeightedSpawnStateInitialized = true;
        saveData.duelClockPendingEnemySpawns =
            pendingDuelClockEnemySpawns;
        duelClockEnemySpawnPool.Capture(
            saveData.duelClockEnemySpawnCounts,
            saveData.duelClockEnemyMissedSpawnCounts,
            out saveData.duelClockRemainingEnemySpawnCount,
            out saveData.duelClockLastSpawnedEnemyAssetName);
    }

    private bool TryBuildDuelClockEnemyConfiguration(BattleData battleData)
    {
        duelClockLegacyAuthoredEnemies = BuildLegacyDuelClockEnemyPool(
            battleData);

        if (battleData != null
            && battleData.DuelClockEnemySpawnEntries.Count > 0)
        {
            duelClockSpawnEntries = new DuelClockEnemySpawnEntry[
                battleData.DuelClockEnemySpawnEntries.Count];

            for (int index = 0; index < duelClockSpawnEntries.Length; index++)
            {
                duelClockSpawnEntries[index] =
                    battleData.DuelClockEnemySpawnEntries[index];
            }

            duelClockEnemySpawnCount = battleData.DuelClockEnemySpawnCount;
        }
        else
        {
            duelClockSpawnEntries = BuildWeightedEntriesFromLegacyPool(
                duelClockLegacyAuthoredEnemies);
            duelClockEnemySpawnCount = duelClockLegacyAuthoredEnemies.Length;
        }

        duelClockAuthoredEnemies = BuildAuthoredEnemyList(
            duelClockSpawnEntries);
        return duelClockSpawnEntries.Length > 0
            && duelClockEnemySpawnCount > 0;
    }

    private EnemyData[] BuildLegacyDuelClockEnemyPool(BattleData battleData)
    {
        if (battleData != null && battleData.DuelClockEnemyPool.Count > 0)
        {
            EnemyData[] authoredPool = new EnemyData[
                battleData.DuelClockEnemyPool.Count];

            for (int index = 0; index < authoredPool.Length; index++)
            {
                authoredPool[index] = battleData.DuelClockEnemyPool[index];
            }

            return authoredPool;
        }

        List<EnemyData> flattenedEnemies = new List<EnemyData>();

        foreach (EnemyWave wave in waves)
        {
            if (wave == null)
            {
                continue;
            }

            foreach (EnemyWaveEntry entry in wave.Enemies)
            {
                if (entry?.EnemyData == null || entry.Count <= 0)
                {
                    continue;
                }

                for (int count = 0; count < entry.Count; count++)
                {
                    flattenedEnemies.Add(entry.EnemyData);
                }
            }
        }

        return flattenedEnemies.ToArray();
    }

    private static DuelClockEnemySpawnEntry[]
        BuildWeightedEntriesFromLegacyPool(
            IReadOnlyList<EnemyData> legacyEnemies)
    {
        List<EnemyData> uniqueEnemies = new List<EnemyData>();
        List<int> counts = new List<int>();

        if (legacyEnemies == null)
        {
            return Array.Empty<DuelClockEnemySpawnEntry>();
        }

        for (int index = 0; index < legacyEnemies.Count; index++)
        {
            EnemyData enemy = legacyEnemies[index];

            if (enemy == null)
            {
                return Array.Empty<DuelClockEnemySpawnEntry>();
            }

            int existingIndex = uniqueEnemies.IndexOf(enemy);

            if (existingIndex >= 0)
            {
                counts[existingIndex]++;
            }
            else
            {
                uniqueEnemies.Add(enemy);
                counts.Add(1);
            }
        }

        DuelClockEnemySpawnEntry[] weightedEntries =
            new DuelClockEnemySpawnEntry[uniqueEnemies.Count];

        for (int index = 0; index < weightedEntries.Length; index++)
        {
            weightedEntries[index] = new DuelClockEnemySpawnEntry(
                uniqueEnemies[index],
                counts[index]);
        }

        return weightedEntries;
    }

    private static EnemyData[] BuildAuthoredEnemyList(
        IReadOnlyList<DuelClockEnemySpawnEntry> entries)
    {
        if (entries == null)
        {
            return Array.Empty<EnemyData>();
        }

        EnemyData[] authoredEnemies = new EnemyData[entries.Count];

        for (int index = 0; index < entries.Count; index++)
        {
            authoredEnemies[index] = entries[index]?.EnemyData;
        }

        return authoredEnemies;
    }

    private bool TryRestoreLegacyDuelClockEnemyPool(RunSaveData saveData)
    {
        IReadOnlyList<string> remainingEnemyNames =
            saveData.duelClockSpawnPoolInitialized
                ? saveData.duelClockRemainingEnemyAssetNames
                : BuildLegacyRemainingEnemyNames(
                    saveData.currentWaveIndex);

        if (remainingEnemyNames == null
            || duelClockLegacyAuthoredEnemies.Length
                != duelClockEnemySpawnCount)
        {
            return false;
        }

        int[] remainingCounts = new int[duelClockSpawnEntries.Length];

        foreach (string enemyName in remainingEnemyNames)
        {
            EnemyData enemy = ResolveSavedEnemy(enemyName);
            int entryIndex = FindDuelClockSpawnEntryIndex(enemy);

            if (entryIndex < 0)
            {
                return false;
            }

            remainingCounts[entryIndex]++;
        }

        int[] initialCounts = new int[duelClockSpawnEntries.Length];

        foreach (EnemyData enemy in duelClockLegacyAuthoredEnemies)
        {
            int entryIndex = FindDuelClockSpawnEntryIndex(enemy);

            if (entryIndex < 0)
            {
                return false;
            }

            initialCounts[entryIndex]++;
        }

        List<int> spawnedCounts = new List<int>(initialCounts.Length);
        List<int> missedSpawnCounts = new List<int>(initialCounts.Length);

        for (int index = 0; index < initialCounts.Length; index++)
        {
            if (remainingCounts[index] > initialCounts[index])
            {
                return false;
            }

            spawnedCounts.Add(initialCounts[index] - remainingCounts[index]);
            missedSpawnCounts.Add(0);
        }

        return duelClockEnemySpawnPool.Restore(
            duelClockSpawnEntries,
            duelClockEnemySpawnCount,
            remainingEnemyNames.Count,
            spawnedCounts,
            missedSpawnCounts,
            ResolveLegacyLastSpawnedEnemyName(saveData),
            ResolveSavedEnemy);
    }

    private int FindDuelClockSpawnEntryIndex(EnemyData enemy)
    {
        if (enemy == null)
        {
            return -1;
        }

        for (int index = 0; index < duelClockSpawnEntries.Length; index++)
        {
            if (duelClockSpawnEntries[index]?.EnemyData == enemy)
            {
                return index;
            }
        }

        return -1;
    }

    private static string ResolveLegacyLastSpawnedEnemyName(
        RunSaveData saveData)
    {
        if (saveData?.enemies == null)
        {
            return string.Empty;
        }

        for (int index = saveData.enemies.Count - 1; index >= 0; index--)
        {
            string enemyName = saveData.enemies[index]?.enemyAssetName;

            if (!string.IsNullOrWhiteSpace(enemyName))
            {
                return enemyName;
            }
        }

        return string.Empty;
    }

    private List<string> BuildLegacyRemainingEnemyNames(
        int savedCurrentWaveIndex)
    {
        List<string> remainingNames = new List<string>();

        for (int waveIndex = Mathf.Max(0, savedCurrentWaveIndex + 1);
             waveIndex < waves.Length;
             waveIndex++)
        {
            EnemyWave wave = waves[waveIndex];

            if (wave == null)
            {
                continue;
            }

            foreach (EnemyWaveEntry entry in wave.Enemies)
            {
                if (entry?.EnemyData == null || entry.Count <= 0)
                {
                    continue;
                }

                for (int count = 0; count < entry.Count; count++)
                {
                    remainingNames.Add(entry.EnemyData.name);
                }
            }
        }

        return remainingNames;
    }

    private void RemoveMissingEnemies()
    {
        if (activeEnemies.RemoveAll(enemy => enemy == null) > 0)
        {
            if (combatPacingMode == CombatPacingMode.DuelClock)
            {
                ResolveEmptyDuelClockBattle();
            }
            else if (activeEnemies.Count == 0)
            {
                HandleWaveCleared();
            }

            StateChanged?.Invoke();
        }
    }

    private IEnumerator WaitForDetachedEnemyAttacks()
    {
        while (pendingDetachedEnemyAttacks > 0
               && !isBattleCompleted && !playerHealth.IsDefeated)
        {
            yield return null;
        }
    }

    private void AdvanceDuelClockEnemySpawns()
    {
        if (isBattleCompleted || !isDuelClockEnemyPoolConfigured)
        {
            return;
        }

        if (!TryResolvePendingDuelClockEnemySpawns())
        {
            return;
        }

        ResolveEmptyDuelClockBattle();
    }

    private void HandleDuelClockSpawnCyclesCommitted(long spawnCycleCount)
    {
        if (combatPacingMode != CombatPacingMode.DuelClock
            || spawnCycleCount <= 0L
            || !isDuelClockEnemyPoolConfigured
            || isBattleCompleted)
        {
            return;
        }

        pendingDuelClockEnemySpawns = CalculatePendingDuelClockSpawns(
            pendingDuelClockEnemySpawns,
            duelClockEnemySpawnPool.RemainingCount,
            spawnCycleCount);

        if (duelClockController == null
            || !duelClockController.HasReservedBeat)
        {
            TryResolvePendingDuelClockEnemySpawns();
        }

        StateChanged?.Invoke();
    }

    private bool TryResolvePendingDuelClockEnemySpawns()
    {
        while (pendingDuelClockEnemySpawns > 0
               && duelClockEnemySpawnPool.RemainingCount > 0
               && CalculateAvailableEnemySlots(
                   GetLivingEnemyCount(),
                   maximumActiveEnemyCount) > 0
               && GetAvailableSpawnTileCount() > 0)
        {
            if (!TrySpawnOneDuelClockEnemy())
            {
                FailBattle(
                    "A Duel Clock gauge reinforcement could not be spawned.");
                return false;
            }

            pendingDuelClockEnemySpawns--;
            StateChanged?.Invoke();
        }

        return true;
    }

    private void ResolveEmptyDuelClockBattle()
    {
        if (combatPacingMode != CombatPacingMode.DuelClock
            || !isDuelClockEnemyPoolConfigured || isBattleCompleted)
        {
            return;
        }

        if (pendingDetachedEnemyAttacks > 0)
        {
            isBattleCompletionPending = true;
            StateChanged?.Invoke();
            return;
        }

        int livingEnemyCount = GetLivingEnemyCount();

        if (!TryResolvePendingDuelClockEnemySpawns())
        {
            return;
        }

        livingEnemyCount = GetLivingEnemyCount();

        if (ShouldImmediatelySpawnDuelClockEnemy(
                duelClockEnemySpawnPool.RemainingCount,
                livingEnemyCount,
                maximumActiveEnemyCount))
        {
            if (GetAvailableSpawnTileCount() <= 0)
            {
                return;
            }

            if (!TrySpawnOneDuelClockEnemy())
            {
                FailBattle(
                    "An immediate Duel Clock enemy reinforcement could not be spawned.");
            }

            if (pendingDuelClockEnemySpawns > 0)
            {
                pendingDuelClockEnemySpawns--;
            }

            return;
        }

        TryCompleteDuelClockBattle();
    }

    internal static bool ShouldImmediatelySpawnDuelClockEnemy(
        int remainingSpawnCount,
        int livingEnemyCount,
        int configuredMaximumEnemyCount)
    {
        return remainingSpawnCount > 0
            && livingEnemyCount <= 0
            && CalculateAvailableEnemySlots(
                livingEnemyCount,
                configuredMaximumEnemyCount) > 0;
    }

    internal static int CalculatePendingDuelClockSpawns(
        int currentPendingCount,
        int remainingSpawnCount,
        long completedSpawnCycles)
    {
        int sanitizedRemaining = Mathf.Max(0, remainingSpawnCount);
        int sanitizedPending = Mathf.Clamp(
            currentPendingCount,
            0,
            sanitizedRemaining);
        long sanitizedCompleted = Math.Max(0L, completedSpawnCycles);
        long availableRequests = sanitizedRemaining - sanitizedPending;
        long acceptedRequests = Math.Min(
            availableRequests,
            sanitizedCompleted);
        return sanitizedPending + (int)acceptedRequests;
    }

    internal static int CalculateMaximumActiveEnemyCount(int totalTileCount)
    {
        long scaledCapacity = (long)Mathf.Max(0, totalTileCount)
            * EnemyCapacityPercentage;
        // Round down so the live enemy count never exceeds 40% of existing cells.
        return (int)(scaledCapacity / 100L);
    }

    internal static int CalculateAvailableEnemySlots(
        int livingEnemyCount,
        int configuredMaximumEnemyCount)
    {
        return Mathf.Max(
            0,
            Mathf.Max(0, configuredMaximumEnemyCount)
            - Mathf.Max(0, livingEnemyCount));
    }

    internal static bool ShouldPauseSpawnGauge(
        bool isEnemySpawnPoolConfigured,
        int remainingEnemySpawnCount,
        bool isActiveEnemyLimitReached)
    {
        return isActiveEnemyLimitReached
            || (isEnemySpawnPoolConfigured
                && remainingEnemySpawnCount <= 0);
    }

    private void ConfigureMaximumActiveEnemyCount()
    {
        int totalTileCount = boardManager == null ? 0 : boardManager.TotalTileCount;
        maximumActiveEnemyCount = CalculateMaximumActiveEnemyCount(totalTileCount);
    }

    private void TryCompleteDuelClockBattle()
    {
        if (combatPacingMode != CombatPacingMode.DuelClock
            || !isDuelClockEnemyPoolConfigured
            || isBattleCompleted || activeEnemies.Count > 0
            || pendingDetachedEnemyAttacks > 0
            || pendingDuelClockEnemySpawns > 0
            || !duelClockEnemySpawnPool.IsExhausted)
        {
            return;
        }

        if (playerMove != null && playerMove.IsShooting)
        {
            isBattleCompletionPending = true;
            StateChanged?.Invoke();
            return;
        }

        CompleteBattle();
    }

    internal static EnemyBattleProgress CalculateEnemyProgress(
        IReadOnlyList<EnemyWave> configuredWaves,
        int currentWaveIndex,
        int livingEnemyCount)
    {
        long totalCount = CountAuthoredEnemies(configuredWaves, 0);
        long futureCount = CountAuthoredEnemies(
            configuredWaves,
            Mathf.Max(0, currentWaveIndex + 1));
        long undefeatedCount = Math.Max(0, livingEnemyCount) + futureCount;
        long defeatedCount = Math.Max(0L, totalCount - undefeatedCount);
        return new EnemyBattleProgress(
            Math.Min(defeatedCount, totalCount),
            totalCount);
    }

    internal static EnemyBattleProgress CalculateDuelClockEnemyProgress(
        int authoredEnemyCount,
        int remainingSpawnCount,
        int livingEnemyCount)
    {
        long totalCount = Math.Max(0, authoredEnemyCount);
        long remainingCount = Math.Min(
            totalCount,
            Math.Max(0, remainingSpawnCount)
            + Math.Max(0, livingEnemyCount));
        return new EnemyBattleProgress(
            totalCount - remainingCount,
            totalCount);
    }

    private static long CountAuthoredEnemies(
        IReadOnlyList<EnemyWave> configuredWaves,
        int startWaveIndex)
    {
        if (configuredWaves == null)
        {
            return 0L;
        }

        long count = 0L;

        for (int waveIndex = Mathf.Max(0, startWaveIndex);
             waveIndex < configuredWaves.Count;
             waveIndex++)
        {
            EnemyWave wave = configuredWaves[waveIndex];

            if (wave == null)
            {
                continue;
            }

            foreach (EnemyWaveEntry entry in wave.Enemies)
            {
                if (entry != null && entry.Count > 0)
                {
                    count += entry.Count;
                }
            }
        }

        return count;
    }

    private int GetLivingEnemyCount()
    {
        int livingEnemyCount = 0;

        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy != null && enemy.CurrentHealth > 0)
            {
                livingEnemyCount++;
            }
        }

        return livingEnemyCount;
    }

    private void SubscribeToActiveEnemies()
    {
        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy != null)
            {
                enemy.Defeated -= HandleEnemyDefeated;
                enemy.Defeated += HandleEnemyDefeated;
            }
        }
    }

    private void UnsubscribeFromActiveEnemies()
    {
        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy != null)
            {
                enemy.Defeated -= HandleEnemyDefeated;
            }
        }
    }

    private int CompareEnemyTargets(
        EnemyTargetData first,
        EnemyTargetData second)
    {
        int distanceComparison = first.Distance.CompareTo(second.Distance);

        if (distanceComparison != 0)
        {
            return distanceComparison;
        }

        return first.TileIndex.CompareTo(second.TileIndex);
    }

    private bool ValidateReferences()
    {
        if (enemyPrefabTemplate != null && boardManager != null
            && playerMove != null && playerHealth != null)
        {
            return true;
        }

        Debug.LogError(
            "Enemy Prefab Template, Board Manager, Player Move, and Player Health must be assigned in the Inspector.",
            this);
        return false;
    }

    public void NotifyBigBarrelDefeated(EnemyController boss)
    {
        if (boss != null
            && boss.Data != null
            && boss.Data.BehaviorType == EnemyBehaviorType.BigBarrel)
        {
            bossBombManager?.PauseForBossDefeat();
            BigBarrelDefeated?.Invoke(boss.Data);
        }
    }

    private void ConfigureCombatPacingFresh(
        BattleData battleData,
        CombatPacingMode configuredMode)
    {
        combatPacingMode = battleData != null
            && configuredMode == CombatPacingMode.DuelClock
                ? CombatPacingMode.DuelClock
                : CombatPacingMode.Legacy;

        if (combatPacingMode != CombatPacingMode.DuelClock)
        {
            DeactivateCombatPacing();
            return;
        }

        EnsureDuelClockController();
        duelClockController.ConfigureFresh(battleData, combatPacingMode);
    }

    private void ConfigureCombatPacingRestored(
        BattleData battleData,
        CombatPacingMode configuredMode,
        RunSaveData saveData)
    {
        combatPacingMode = battleData != null
            && configuredMode == CombatPacingMode.DuelClock
                ? CombatPacingMode.DuelClock
                : CombatPacingMode.Legacy;

        if (combatPacingMode != CombatPacingMode.DuelClock)
        {
            DeactivateCombatPacing();
            return;
        }

        EnsureDuelClockController();
        duelClockController.ConfigureRestored(
            battleData,
            combatPacingMode,
            saveData);
    }

    private void CaptureCombatPacing(RunSaveData saveData)
    {
        if (combatPacingMode == CombatPacingMode.DuelClock
            && duelClockController != null
            && duelClockController.IsActive)
        {
            duelClockController.CaptureRunState(saveData);
            return;
        }

        saveData.combatPacingMode = (int)CombatPacingMode.Legacy;
        saveData.duelClockProgress = 0d;
        saveData.duelClockCumulativeBeats = 0;
        saveData.duelClockSpawnProgress = 0d;
        saveData.duelClockCumulativeSpawns = 0;
    }

    private void EnsureDuelClockController()
    {
        if (duelClockController == null)
        {
            duelClockController = GetComponent<DuelClockController>();
        }

        if (duelClockController == null)
        {
            duelClockController = gameObject.AddComponent<
                DuelClockController>();
        }

        duelClockController.Initialize(playerMove, this);
        duelClockController.BeatsCommitted -= HandleDuelClockBeatsCommitted;
        duelClockController.BeatsCommitted += HandleDuelClockBeatsCommitted;
        duelClockController.SpawnCyclesCommitted -=
            HandleDuelClockSpawnCyclesCommitted;
        duelClockController.SpawnCyclesCommitted +=
            HandleDuelClockSpawnCyclesCommitted;
    }

    private void DeactivateCombatPacing()
    {
        combatPacingMode = CombatPacingMode.Legacy;
        pendingEnemyTurnCycles = 0;
        pendingDuelClockEnemySpawns = 0;

        if (duelClockController != null)
        {
            duelClockController.BeatsCommitted -= HandleDuelClockBeatsCommitted;
            duelClockController.SpawnCyclesCommitted -=
                HandleDuelClockSpawnCyclesCommitted;
            duelClockController.Deactivate();
        }
        else
        {
            playerMove?.SetDuelClockActive(false);
        }
    }

    private void EnsureBossBombManager()
    {
        if (bossBombManager == null)
        {
            bossBombManager = GetComponent<BossBombManager>();
        }

        if (bossBombManager == null)
        {
            bossBombManager = gameObject.AddComponent<BossBombManager>();
        }

        bossBombManager.Initialize(
            this,
            boardManager,
            playerMove,
            playerHealth);
    }

    private readonly struct EnemyTargetData
    {
        public EnemyTargetData(
            EnemyController enemy,
            int distance,
            int tileIndex)
        {
            Enemy = enemy;
            Distance = distance;
            TileIndex = tileIndex;
        }

        public EnemyController Enemy { get; }
        public int Distance { get; }
        public int TileIndex { get; }
    }
}
