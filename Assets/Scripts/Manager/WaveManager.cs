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
    private const int EnemyCapacityPercentage = 35;

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
    private int maximumActiveEnemyCount = 1;
    private readonly DuelClockEnemySpawnPool duelClockEnemySpawnPool =
        new DuelClockEnemySpawnPool();
    private EnemyData[] duelClockAuthoredEnemies = Array.Empty<EnemyData>();
    private int duelClockEnemySpawnInterval = 5;
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
    public event Action<int> EnemyTurnCycleCompleted;
    public event Action<EnemyController> EnemyDefeated;
    // TODO: A future persistent unlock service can subscribe and add
    // ExplosiveBullet.asset on the first Big Barrel defeat.
    public event Action<EnemyData> BigBarrelDefeated;

    public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies;
    public int MaximumActiveEnemyCount => maximumActiveEnemyCount;
    internal int LivingEnemyCount => GetLivingEnemyCount();
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
    public bool IsResolvingTurn => isResolvingTurn;
    public int CurrentEnemyTurnCycle => currentEnemyTurnCycle;
    public CombatPacingMode PacingMode => combatPacingMode;
    public bool HasRemainingEnemiesToSpawn =>
        combatPacingMode == CombatPacingMode.DuelClock
        && isDuelClockEnemyPoolConfigured
        && duelClockEnemySpawnPool.RemainingCount > 0;
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
        ClearSpawnWarnings();

        if (enemyTurnCoroutine != null)
        {
            StopCoroutine(enemyTurnCoroutine);
            enemyTurnCoroutine = null;
        }

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
        ConfigureMaximumActiveEnemyCount(battleData);
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
        ConfigureMaximumActiveEnemyCount(battleData);
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

        if ((!usesDuelClock && !ValidateConfiguredWaves())
            || saveData.currentWaveIndex < 0
            || saveData.currentWaveIndex >= waves.Length
            || usesDuelClock
            && !RestoreDuelClockEnemyPool(battleData, saveData))
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
            foreach (int tileIndex in saveData.reservedSpawnTileIndices)
            {
                if (tileIndex >= 0 && tileIndex < boardManager.BoardCount
                    && !reservedSpawnTileIndices.Contains(tileIndex))
                {
                    reservedSpawnTileIndices.Add(tileIndex);
                    boardManager.SetTileWarningActive(tileIndex, true);
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
        return TryGetEnemyAtTile(tileIndex, out _, ignoredEnemy);
    }

    private bool IsPlayerAtTile(int tileIndex)
    {
        return playerMove != null && boardManager != null
            && boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerTileIndex)
            && playerTileIndex == tileIndex;
    }

    public bool IsTileReservedForMovement(
        int tileIndex,
        Component ignoredOwner = null)
    {
        RemoveStaleMovementReservations();
        return movementTileReservations.TryGetValue(
                tileIndex,
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
        return TryReserveMovementTiles(owner, new[] { tileIndex });
    }

    internal bool TryReserveMovementTiles(
        Component owner,
        IReadOnlyList<int> tileIndices)
    {
        if (owner == null || tileIndices == null || tileIndices.Count == 0)
        {
            return false;
        }

        RemoveStaleMovementReservations();

        for (int index = 0; index < tileIndices.Count; index++)
        {
            int tileIndex = tileIndices[index];

            if (tileIndex < 0
                || movementTileReservations.TryGetValue(
                    tileIndex,
                    out Component reservedOwner)
                && reservedOwner != owner)
            {
                return false;
            }
        }

        ReleaseMovementTiles(owner);

        for (int index = 0; index < tileIndices.Count; index++)
        {
            movementTileReservations[tileIndices[index]] = owner;
        }

        return true;
    }

    internal bool TryReserveMovementSwap(
        Component firstOwner,
        int firstTargetTileIndex,
        Component secondOwner,
        int secondTargetTileIndex)
    {
        if (firstOwner == null || secondOwner == null
            || firstOwner == secondOwner
            || firstTargetTileIndex < 0 || secondTargetTileIndex < 0
            || firstTargetTileIndex == secondTargetTileIndex)
        {
            return false;
        }

        RemoveStaleMovementReservations();

        if (IsReservedByAnotherOwner(firstTargetTileIndex, firstOwner, secondOwner)
            || IsReservedByAnotherOwner(
                secondTargetTileIndex,
                firstOwner,
                secondOwner))
        {
            return false;
        }

        ReleaseMovementTiles(firstOwner);
        ReleaseMovementTiles(secondOwner);
        movementTileReservations[firstTargetTileIndex] = firstOwner;
        movementTileReservations[secondTargetTileIndex] = secondOwner;
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
        foundEnemy = null;

        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy == null || enemy == ignoredEnemy)
            {
                continue;
            }

            if (boardManager.TryGetTileIndex(enemy.transform.position, out int enemyIndex)
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
        return reservedSpawnTileIndices.Contains(tileIndex);
    }

    public void GetEnemiesInDirection(
        Vector3 originWorldPosition,
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
            || !boardManager.TryGetTileIndex(originWorldPosition, out int originIndex))
        {
            return;
        }

        int normalizedDirection = direction > 0 ? 1 : -1;

        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy == null || enemy.CurrentHealth <= 0
                || !boardManager.TryGetTileIndex(
                    enemy.transform.position,
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
            QueueEnemyTurnCycles(beatCount);
        }
    }

    internal void QueueDuelClockBeats(long beatCount)
    {
        HandleDuelClockBeatsCommitted(beatCount);
    }

    private void QueueEnemyTurnCycles(long cycleCount)
    {
        if (cycleCount <= 0 || isBattleCompleted || !ValidateReferences()
            || playerHealth.IsDefeated)
        {
            return;
        }

        if (cycleCount > long.MaxValue - pendingEnemyTurnCycles)
        {
            cycleCount = long.MaxValue - pendingEnemyTurnCycles;
        }

        pendingEnemyTurnCycles += cycleCount;

        if (enemyTurnCoroutine == null)
        {
            enemyTurnCoroutine = StartCoroutine(ResolveEnemyTurnCycles());
        }
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

        float remainingTurnDelay = Mathf.Max(
            0f,
            enemyTurnDelay - (Time.time - turnStartedAt));
        yield return WaitForTurnTime(remainingTurnDelay);

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

        List<int> spawnTileIndices;

        if (reservedSpawnTileIndices.Count == enemyCount)
        {
            spawnTileIndices = new List<int>(reservedSpawnTileIndices);
        }
        else if (!TrySelectSpawnTileIndices(enemyCount, out spawnTileIndices))
        {
            Debug.LogError(
                $"Wave {nextWaveIndex + 1} spawn tiles could not be selected.",
                this);
            return false;
        }

        List<EnemyController> spawnedEnemies = new List<EnemyController>();
        int spawnTileListIndex = 0;

        foreach (EnemyWaveEntry entry in nextWave.Enemies)
        {
            for (int count = 0; count < entry.Count; count++)
            {
                int spawnTileIndex = spawnTileIndices[spawnTileListIndex];
                spawnTileListIndex++;

                if (!TrySpawnEnemy(
                        entry.EnemyData,
                        spawnTileIndex,
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
        spawnedEnemy = null;

        if (enemyData == null || enemyPrefabTemplate == null
            || CalculateAvailableEnemySlots(
                GetLivingEnemyCount(),
                maximumActiveEnemyCount) <= 0
            || IsPlayerAtTile(spawnTileIndex)
            || IsTileOccupied(spawnTileIndex)
            || IsTileReservedForMovement(spawnTileIndex)
            || !boardManager.TryGetTilePosition(
                spawnTileIndex,
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
                this))
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
            || !TrySelectSpawnTileIndices(
                1,
                out List<int> spawnTileIndices))
        {
            return false;
        }

        int poolIndex = UnityEngine.Random.Range(
            0,
            duelClockEnemySpawnPool.RemainingCount);

        if (!duelClockEnemySpawnPool.TryGet(
                poolIndex,
                out EnemyData enemyData)
            || !TrySpawnEnemy(
                enemyData,
                spawnTileIndices[0],
                out EnemyController spawnedEnemy))
        {
            return false;
        }

        if (!duelClockEnemySpawnPool.TryConsumeAt(poolIndex, enemyData))
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
                out int playerIndex))
        {
            return 0;
        }

        int availableCount = 0;

        for (int tileIndex = 0; tileIndex < boardManager.BoardCount; tileIndex++)
        {
            if (tileIndex != playerIndex && !IsTileOccupied(tileIndex)
                && !IsTileReservedForMovement(tileIndex))
            {
                availableCount++;
            }
        }

        return Mathf.Min(
            availableCount,
            CalculateAvailableEnemySlots(
                GetLivingEnemyCount(),
                maximumActiveEnemyCount));
    }

    private bool TrySelectSpawnTileIndices(
        int requestedCount,
        out List<int> selectedTileIndices)
    {
        selectedTileIndices = new List<int>();
        List<int> preferredTileIndices = new List<int>();
        List<int> adjacentFallbackTileIndices = new List<int>();

        if (requestedCount <= 0
            || requestedCount > CalculateAvailableEnemySlots(
                GetLivingEnemyCount(),
                maximumActiveEnemyCount)
            || boardManager == null || playerMove == null
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerIndex))
        {
            return false;
        }

        for (int tileIndex = 0; tileIndex < boardManager.BoardCount; tileIndex++)
        {
            if (tileIndex == playerIndex || IsTileOccupied(tileIndex)
                || IsTileReservedForMovement(tileIndex))
            {
                continue;
            }

            if (Mathf.Abs(tileIndex - playerIndex) == 1)
            {
                adjacentFallbackTileIndices.Add(tileIndex);
            }
            else
            {
                preferredTileIndices.Add(tileIndex);
            }
        }

        if (preferredTileIndices.Count
            + adjacentFallbackTileIndices.Count < requestedCount)
        {
            return false;
        }

        SelectRandomSpawnTiles(
            preferredTileIndices,
            requestedCount,
            selectedTileIndices);

        if (selectedTileIndices.Count < requestedCount)
        {
            SelectRandomSpawnTiles(
                adjacentFallbackTileIndices,
                requestedCount,
                selectedTileIndices);
        }

        return selectedTileIndices.Count == requestedCount;
    }

    private void SelectRandomSpawnTiles(
        List<int> candidates,
        int requestedTotalCount,
        List<int> selectedTileIndices)
    {
        while (selectedTileIndices.Count < requestedTotalCount
               && candidates.Count > 0)
        {
            int randomListIndex = UnityEngine.Random.Range(
                0,
                candidates.Count);
            selectedTileIndices.Add(candidates[randomListIndex]);
            candidates.RemoveAt(randomListIndex);
        }
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
            || !TrySelectSpawnTileIndices(
                enemyCount,
                out List<int> selectedTileIndices))
        {
            return false;
        }

        reservedSpawnTileIndices.AddRange(selectedTileIndices);

        foreach (int tileIndex in reservedSpawnTileIndices)
        {
            if (!boardManager.SetTileWarningActive(tileIndex, true))
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
            foreach (int tileIndex in reservedSpawnTileIndices)
            {
                boardManager.SetTileWarningActive(tileIndex, false);
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
            rewardManager.SpawnEnemyDrop(enemy.Data, enemy.transform.position);
        }

        enemy.Defeated -= HandleEnemyDefeated;
        ReleaseMovementTiles(enemy);
        activeEnemies.Remove(enemy);
        EnemyDefeated?.Invoke(enemy);

        if (combatPacingMode == CombatPacingMode.DuelClock)
        {
            TryCompleteDuelClockBattle();
        }
        else if (activeEnemies.Count == 0)
        {
            HandleWaveCleared();
        }

        StateChanged?.Invoke();
    }

    private void HandleWaveCleared()
    {
        if (combatPacingMode == CombatPacingMode.DuelClock)
        {
            TryCompleteDuelClockBattle();
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
        if (!isBattleCompletionPending || isBattleCompleted)
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

    private void ResetBattleRuntime()
    {
        ClearSpawnWarnings();

        if (enemyTurnCoroutine != null)
        {
            StopCoroutine(enemyTurnCoroutine);
            enemyTurnCoroutine = null;
        }

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
        duelClockEnemySpawnPool.Clear();
        duelClockAuthoredEnemies = Array.Empty<EnemyData>();
        duelClockEnemySpawnInterval = 5;
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
        duelClockAuthoredEnemies = BuildDuelClockEnemyPool(battleData);
        duelClockEnemySpawnInterval = battleData == null
            ? 5
            : battleData.DuelClockEnemyWaveCount;
        if (!duelClockEnemySpawnPool.ConfigureFresh(
                duelClockAuthoredEnemies))
        {
            Debug.LogError(
                "A Duel Clock battle must contain at least one valid enemy in its spawn pool.",
                this);
            return false;
        }

        isDuelClockEnemyPoolConfigured = true;
        return true;
    }

    private bool RestoreDuelClockEnemyPool(
        BattleData battleData,
        RunSaveData saveData)
    {
        duelClockAuthoredEnemies = BuildDuelClockEnemyPool(battleData);
        duelClockEnemySpawnInterval = battleData == null
            ? 5
            : battleData.DuelClockEnemyWaveCount;
        IReadOnlyList<string> remainingEnemyNames =
            saveData.duelClockSpawnPoolInitialized
                ? saveData.duelClockRemainingEnemyAssetNames
                : BuildLegacyRemainingEnemyNames(
                    saveData.currentWaveIndex);

        if (duelClockEnemySpawnPool.Restore(
                duelClockAuthoredEnemies,
                remainingEnemyNames,
                ResolveSavedEnemy))
        {
            isDuelClockEnemyPoolConfigured = true;
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

        if (combatPacingMode != CombatPacingMode.DuelClock)
        {
            saveData.duelClockSpawnPoolInitialized = false;
            saveData.duelClockPendingEnemySpawns = 0;
            return;
        }

        saveData.duelClockSpawnPoolInitialized = true;
        saveData.duelClockPendingEnemySpawns = 0;
        duelClockEnemySpawnPool.Capture(
            saveData.duelClockRemainingEnemyAssetNames);
    }

    private EnemyData[] BuildDuelClockEnemyPool(BattleData battleData)
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
                TryCompleteDuelClockBattle();
            }
            else if (activeEnemies.Count == 0)
            {
                HandleWaveCleared();
            }

            StateChanged?.Invoke();
        }
    }

    private void AdvanceDuelClockEnemySpawns()
    {
        if (isBattleCompleted || !isDuelClockEnemyPoolConfigured)
        {
            return;
        }

        int interval = Mathf.Max(1, duelClockEnemySpawnInterval);

        if (ShouldSpawnDuelClockEnemy(
                currentEnemyTurnCycle,
                interval,
                duelClockEnemySpawnPool.RemainingCount,
                GetLivingEnemyCount(),
                maximumActiveEnemyCount))
        {
            if (GetAvailableSpawnTileCount() > 0
                && !TrySpawnOneDuelClockEnemy())
            {
                FailBattle(
                    "A Duel Clock enemy reinforcement could not be spawned.");
                return;
            }
        }

        TryCompleteDuelClockBattle();
    }

    internal static bool ShouldSpawnDuelClockEnemy(
        int completedEnemyCycles,
        int spawnInterval,
        int remainingSpawnCount,
        int livingEnemyCount,
        int configuredMaximumEnemyCount)
    {
        int sanitizedInterval = Mathf.Max(1, spawnInterval);
        return completedEnemyCycles > 0
            && completedEnemyCycles % sanitizedInterval == 0
            && remainingSpawnCount > 0
            && CalculateAvailableEnemySlots(
                livingEnemyCount,
                configuredMaximumEnemyCount) > 0;
    }

    internal static int CalculateMaximumActiveEnemyCount(int boardCount)
    {
        int sanitizedBoardCount = Mathf.Max(1, boardCount);
        long scaledCapacity = (long)sanitizedBoardCount
            * EnemyCapacityPercentage;
        long roundedCapacity = (scaledCapacity + 50L) / 100L;
        return (int)Math.Min(
            int.MaxValue,
            Math.Max(1L, roundedCapacity));
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

    private void ConfigureMaximumActiveEnemyCount(BattleData battleData)
    {
        int boardCount = battleData == null
            ? boardManager == null ? 1 : boardManager.BoardCount
            : battleData.BoardCount;
        maximumActiveEnemyCount = CalculateMaximumActiveEnemyCount(boardCount);
    }

    private void TryCompleteDuelClockBattle()
    {
        if (combatPacingMode != CombatPacingMode.DuelClock
            || !isDuelClockEnemyPoolConfigured
            || isBattleCompleted || activeEnemies.Count > 0
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
    }

    private void DeactivateCombatPacing()
    {
        combatPacingMode = CombatPacingMode.Legacy;
        pendingEnemyTurnCycles = 0;

        if (duelClockController != null)
        {
            duelClockController.BeatsCommitted -= HandleDuelClockBeatsCommitted;
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
