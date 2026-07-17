using System;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Stage Settings")]
    [SerializeField] private List<EnemyController> stageEnemyPool =
        new List<EnemyController>();
    [Min(0)]
    [SerializeField] private int maxActiveEnemies = 3;

    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Transform enemyParent;

    [Header("Runtime State")]
    [SerializeField] private List<EnemyController> activeEnemies =
        new List<EnemyController>();

    private bool isResolvingTurn;

    public event Action StateChanged;

    public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies;
    public int MaxActiveEnemies => maxActiveEnemies;

    private void Awake()
    {
        activeEnemies.Clear();
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
        FillAvailableEnemySlots();
    }

    private void OnDisable()
    {
        if (playerMove != null)
        {
            playerMove.TurnCompleted -= HandlePlayerTurnCompleted;
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
                results.Add(enemy);
            }
        }

        results.Sort((first, second) =>
            GetTileDistanceFromOrigin(originIndex, first)
                .CompareTo(GetTileDistanceFromOrigin(originIndex, second)));
    }

    public void FillAvailableEnemySlots()
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

        isResolvingTurn = true;
        RemoveMissingEnemies();

        EnemyController[] enemiesThisTurn = activeEnemies.ToArray();

        foreach (EnemyController enemy in enemiesThisTurn)
        {
            if (enemy != null && activeEnemies.Contains(enemy))
            {
                enemy.TakeTurn();
            }
        }

        RemoveMissingEnemies();
        FillAvailableEnemySlots();
        isResolvingTurn = false;
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
        return boardManager.TryGetTilePosition(randomTileIndex, out spawnPosition);
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

    private int GetTileDistanceFromOrigin(
        int originIndex,
        EnemyController enemy)
    {
        if (enemy == null || !boardManager.TryGetTileIndex(
                enemy.transform.position,
                out int enemyIndex))
        {
            return int.MaxValue;
        }

        return Mathf.Abs(enemyIndex - originIndex);
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
}
