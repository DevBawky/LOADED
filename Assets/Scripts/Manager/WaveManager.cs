using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemyWaveEntry
{
    [SerializeField] private EnemyController enemyPrefab;
    [Min(1)]
    [SerializeField] private int count = 1;

    public EnemyController EnemyPrefab => enemyPrefab;
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

public class WaveManager : MonoBehaviour
{
    [Header("Stage Settings")]
    [SerializeField] private EnemyWave[] waves = Array.Empty<EnemyWave>();
    [SerializeField] private Vector3 spawnPositionOffset =
        new Vector3(0f, 0.3f, 0f);
    [Min(0)]
    [SerializeField] private int spawnTerm = 2;

    [Header("Turn Timing")]
    [Min(0f)]
    [SerializeField] private float enemyTurnDelay = 0.35f;
    [Min(0f)]
    [SerializeField] private float enemyActionInterval = 0.15f;

    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Transform enemyParent;
    [SerializeField] private GameObject mainGamePanel;
    [SerializeField] private GameObject stageClearPanel;

    [Header("Runtime State")]
    [SerializeField] private List<EnemyController> activeEnemies =
        new List<EnemyController>();
    [SerializeField] private List<int> reservedSpawnTileIndices =
        new List<int>();
    [SerializeField] private int currentWaveIndex = -1;
    [SerializeField] private int remainingSpawnTurns;
    [SerializeField] private bool isWaitingForNextWave;
    [SerializeField] private bool isStageCleared;

    private bool isResolvingTurn;
    private Coroutine enemyTurnCoroutine;
    private readonly List<EnemyTargetData> enemyTargetBuffer =
        new List<EnemyTargetData>();

    public event Action StateChanged;

    public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies;
    public IReadOnlyList<EnemyWave> Waves => waves ?? Array.Empty<EnemyWave>();
    public int CurrentWaveIndex => currentWaveIndex;
    public Vector3 SpawnPositionOffset => spawnPositionOffset;
    public int SpawnTerm => spawnTerm;
    public int RemainingSpawnTurns => remainingSpawnTurns;
    public float EnemyTurnDelay => enemyTurnDelay;
    public float EnemyActionInterval => enemyActionInterval;
    public bool IsWaitingForNextWave => isWaitingForNextWave;
    public bool IsStageCleared => isStageCleared;
    public bool IsResolvingTurn => isResolvingTurn;

    private void Awake()
    {
        activeEnemies.Clear();
        reservedSpawnTileIndices.Clear();
        currentWaveIndex = -1;
        remainingSpawnTurns = 0;
        isWaitingForNextWave = false;
        isStageCleared = false;
        SetStageClearUI(false);
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
        if (!ValidateReferences())
        {
            return;
        }

        playerMove.SetWaveManager(this);

        if (waves == null || waves.Length == 0)
        {
            CompleteStage();
            return;
        }

        TrySpawnNextWave();
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

        if (playerMove != null)
        {
            playerMove.TurnCompleted -= HandlePlayerTurnCompleted;
            playerMove.SetEnemyTurnResolving(false);
        }

        UnsubscribeFromActiveEnemies();
    }

    public bool IsTileOccupied(int tileIndex, EnemyController ignoredEnemy = null)
    {
        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy == null || enemy == ignoredEnemy)
            {
                continue;
            }

            if (boardManager.TryGetTileIndex(enemy.transform.position, out int enemyIndex)
                && enemyIndex == tileIndex)
            {
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
        if (isResolvingTurn || isStageCleared || !ValidateReferences()
            || playerHealth.IsDefeated)
        {
            return;
        }

        enemyTurnCoroutine = StartCoroutine(ResolveEnemyTurns());
    }

    private IEnumerator ResolveEnemyTurns()
    {
        isResolvingTurn = true;
        playerMove.SetEnemyTurnResolving(true);
        StateChanged?.Invoke();

        yield return WaitForTurnTime(enemyTurnDelay);
        RemoveMissingEnemies();

        EnemyController[] enemiesThisTurn = activeEnemies.ToArray();

        for (int enemyIndex = 0;
             enemyIndex < enemiesThisTurn.Length;
             enemyIndex++)
        {
            EnemyController enemy = enemiesThisTurn[enemyIndex];

            if (enemy != null && activeEnemies.Contains(enemy))
            {
                enemy.TakeTurn();

                while (enemy != null && enemy.IsActing)
                {
                    yield return null;
                }

                if (playerHealth.IsDefeated)
                {
                    break;
                }

                if (enemyIndex < enemiesThisTurn.Length - 1)
                {
                    yield return WaitForTurnTime(enemyActionInterval);
                }
            }
        }

        RemoveMissingEnemies();
        AdvanceWaveCountdown();
        playerMove.SetEnemyTurnResolving(false);
        isResolvingTurn = false;
        enemyTurnCoroutine = null;
        StateChanged?.Invoke();

        if (!isStageCleared && !playerHealth.IsDefeated)
        {
            playerMove.TrySkipStunnedTurn();
        }
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

    private bool TrySpawnNextWave()
    {
        int nextWaveIndex = currentWaveIndex + 1;

        if (waves == null || nextWaveIndex < 0 || nextWaveIndex >= waves.Length)
        {
            CompleteStage();
            return false;
        }

        EnemyWave nextWave = waves[nextWaveIndex];

        if (!TryGetWaveEnemyCount(nextWave, out int enemyCount)
            || enemyCount > GetAvailableSpawnTileCount())
        {
            Debug.LogError(
                $"Wave {nextWaveIndex + 1} must contain valid enemy prefabs and fit on the available board tiles.",
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
                        entry.EnemyPrefab,
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
        EnemyController enemyPrefab,
        int spawnTileIndex,
        out EnemyController spawnedEnemy)
    {
        spawnedEnemy = null;

        if (enemyPrefab == null
            || !boardManager.TryGetTilePosition(
                spawnTileIndex,
                out Vector3 spawnPosition))
        {
            return false;
        }

        spawnPosition += spawnPositionOffset;

        EnemyController enemy = Instantiate(
            enemyPrefab,
            spawnPosition,
            Quaternion.identity,
            enemyParent);

        if (!enemy.Initialize(boardManager, playerMove, playerHealth, this))
        {
            Destroy(enemy.gameObject);
            return false;
        }

        activeEnemies.Add(enemy);
        enemy.Defeated += HandleEnemyDefeated;
        spawnedEnemy = enemy;
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
            if (entry == null || entry.EnemyPrefab == null
                || entry.EnemyPrefab.Data == null || entry.Count <= 0)
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
            if (tileIndex != playerIndex && !IsTileOccupied(tileIndex))
            {
                availableCount++;
            }
        }

        return availableCount;
    }

    private bool TrySelectSpawnTileIndices(
        int requestedCount,
        out List<int> selectedTileIndices)
    {
        selectedTileIndices = new List<int>();
        List<int> availableTileIndices = new List<int>();

        if (requestedCount <= 0 || boardManager == null || playerMove == null
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerIndex))
        {
            return false;
        }

        for (int tileIndex = 0; tileIndex < boardManager.BoardCount; tileIndex++)
        {
            if (tileIndex != playerIndex && !IsTileOccupied(tileIndex))
            {
                availableTileIndices.Add(tileIndex);
            }
        }

        if (availableTileIndices.Count < requestedCount)
        {
            return false;
        }

        for (int selectionIndex = 0;
             selectionIndex < requestedCount;
             selectionIndex++)
        {
            int randomListIndex = UnityEngine.Random.Range(
                0,
                availableTileIndices.Count);
            selectedTileIndices.Add(availableTileIndices[randomListIndex]);
            availableTileIndices.RemoveAt(randomListIndex);
        }

        return true;
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

        enemy.Defeated -= HandleEnemyDefeated;
        activeEnemies.Remove(enemy);

        if (activeEnemies.Count == 0)
        {
            HandleWaveCleared();
        }

        StateChanged?.Invoke();
    }

    private void HandleWaveCleared()
    {
        if (currentWaveIndex < 0 || isStageCleared)
        {
            return;
        }

        if (waves == null || currentWaveIndex >= waves.Length - 1)
        {
            CompleteStage();
            return;
        }

        isWaitingForNextWave = true;
        remainingSpawnTurns = Mathf.Max(0, spawnTerm);

        if (!PrepareNextWaveWarnings())
        {
            Debug.LogError(
                $"Wave {currentWaveIndex + 2} spawn warnings could not be prepared.",
                this);
        }

        if (remainingSpawnTurns == 0)
        {
            TrySpawnNextWave();
        }
    }

    private void AdvanceWaveCountdown()
    {
        if (!isWaitingForNextWave || isStageCleared || activeEnemies.Count > 0)
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
            TrySpawnNextWave();
        }
    }

    private void CompleteStage()
    {
        if (isStageCleared)
        {
            return;
        }

        isStageCleared = true;
        isWaitingForNextWave = false;
        remainingSpawnTurns = 0;
        ClearSpawnWarnings();
        SetStageClearUI(true);
        StateChanged?.Invoke();
    }

    private void SetStageClearUI(bool isCleared)
    {
        if (stageClearPanel != null)
        {
            stageClearPanel.SetActive(isCleared);
        }

        if (mainGamePanel != null)
        {
            mainGamePanel.SetActive(!isCleared);
        }
    }

    private void RemoveMissingEnemies()
    {
        if (activeEnemies.RemoveAll(enemy => enemy == null) > 0)
        {
            if (activeEnemies.Count == 0)
            {
                HandleWaveCleared();
            }

            StateChanged?.Invoke();
        }
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
        if (boardManager != null && playerMove != null && playerHealth != null
            && mainGamePanel != null && stageClearPanel != null)
        {
            return true;
        }

        Debug.LogError(
            "Board Manager, Player Move, Player Health, Main Game Panel, and Stage Clear Panel must be assigned in the Inspector.",
            this);
        return false;
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
