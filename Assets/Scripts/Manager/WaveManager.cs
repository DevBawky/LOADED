using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Stage Settings")]
    [SerializeField] private List<EnemyController> stageEnemyPool =
        new List<EnemyController>();
    [Min(0)]
    [SerializeField] private int maxActiveEnemies = 3;
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

    [Header("Runtime State")]
    [SerializeField] private List<EnemyController> activeEnemies =
        new List<EnemyController>();
    [SerializeField] private int remainingSpawnTurns;

    private bool isResolvingTurn;
    private Coroutine enemyTurnCoroutine;
    private readonly List<EnemyTargetData> enemyTargetBuffer =
        new List<EnemyTargetData>();

    public event Action StateChanged;

    public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies;
    public int MaxActiveEnemies => maxActiveEnemies;
    public Vector3 SpawnPositionOffset => spawnPositionOffset;
    public int SpawnTerm => spawnTerm;
    public int RemainingSpawnTurns => remainingSpawnTurns;
    public float EnemyTurnDelay => enemyTurnDelay;
    public float EnemyActionInterval => enemyActionInterval;
    public bool IsResolvingTurn => isResolvingTurn;

    private void Awake()
    {
        activeEnemies.Clear();
        ResetSpawnCountdown();
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

        if (remainingSpawnTurns == 0)
        {
            FillAvailableEnemySlots();
            ResetSpawnCountdown();
        }
    }

    private void OnDisable()
    {
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

    private void FillAvailableEnemySlots()
    {
        if (!ValidateReferences() || maxActiveEnemies <= 0)
        {
            return;
        }

        RemoveMissingEnemies();

        while (activeEnemies.Count < maxActiveEnemies)
        {
            if (!TrySpawnRandomEnemy())
            {
                break;
            }
        }
    }

    private void HandlePlayerTurnCompleted()
    {
        if (isResolvingTurn || !ValidateReferences())
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
        AdvanceSpawnCountdown();
        playerMove.SetEnemyTurnResolving(false);
        isResolvingTurn = false;
        enemyTurnCoroutine = null;
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

    private bool TrySpawnRandomEnemy()
    {
        EnemyController enemyPrefab = GetRandomEnemyPrefab();

        if (enemyPrefab == null || !TryGetRandomAvailableTile(out Vector3 spawnPosition))
        {
            return false;
        }

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
        StateChanged?.Invoke();
        return true;
    }

    private EnemyController GetRandomEnemyPrefab()
    {
        List<EnemyController> validPrefabs = new List<EnemyController>();

        foreach (EnemyController enemyPrefab in stageEnemyPool)
        {
            if (enemyPrefab != null && enemyPrefab.Data != null)
            {
                validPrefabs.Add(enemyPrefab);
            }
        }

        if (validPrefabs.Count == 0)
        {
            return null;
        }

        return validPrefabs[UnityEngine.Random.Range(0, validPrefabs.Count)];
    }

    private bool TryGetRandomAvailableTile(out Vector3 spawnPosition)
    {
        spawnPosition = Vector3.zero;
        List<int> availableTileIndices = new List<int>();

        if (!boardManager.TryGetTileIndex(
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

        if (availableTileIndices.Count == 0)
        {
            return false;
        }

        int randomListIndex = UnityEngine.Random.Range(0, availableTileIndices.Count);
        int randomTileIndex = availableTileIndices[randomListIndex];

        if (!boardManager.TryGetTilePosition(randomTileIndex, out spawnPosition))
        {
            return false;
        }

        spawnPosition += spawnPositionOffset;
        return true;
    }

    private void HandleEnemyDefeated(EnemyController enemy)
    {
        if (enemy == null)
        {
            return;
        }

        enemy.Defeated -= HandleEnemyDefeated;
        activeEnemies.Remove(enemy);
        StateChanged?.Invoke();
    }

    private void RemoveMissingEnemies()
    {
        if (activeEnemies.RemoveAll(enemy => enemy == null) > 0)
        {
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

    private void AdvanceSpawnCountdown()
    {
        if (activeEnemies.Count >= maxActiveEnemies || maxActiveEnemies <= 0)
        {
            ResetSpawnCountdown();
            return;
        }

        if (remainingSpawnTurns > 0)
        {
            remainingSpawnTurns--;
            StateChanged?.Invoke();
        }

        if (remainingSpawnTurns > 0)
        {
            return;
        }

        FillAvailableEnemySlots();
        ResetSpawnCountdown();
        StateChanged?.Invoke();
    }

    private void ResetSpawnCountdown()
    {
        remainingSpawnTurns = Mathf.Max(0, spawnTerm);
    }

    private bool ValidateReferences()
    {
        if (boardManager != null && playerMove != null && playerHealth != null)
        {
            return true;
        }

        Debug.LogError(
            "Board Manager, Player Move, and Player Health must be assigned in the Inspector.",
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
