using System.Collections.Generic;
using UnityEngine;

public class BossBombManager : MonoBehaviour
{
    private readonly List<BossBomb> activeBombs = new List<BossBomb>();
    private readonly Dictionary<int, BossBomb> bombsByTile =
        new Dictionary<int, BossBomb>();
    private readonly Queue<BossBomb> detonationQueue = new Queue<BossBomb>();
    private readonly HashSet<BossBomb> queuedDetonations =
        new HashSet<BossBomb>();

    private WaveManager waveManager;
    private BoardManager boardManager;
    private PlayerMove playerMove;
    private PlayerHealth playerHealth;
    private bool bombsPaused;
    private bool isProcessingDetonations;

    public IReadOnlyList<BossBomb> ActiveBombs => activeBombs;
    public BoardManager BoardManager => boardManager;

    public void Initialize(
        WaveManager assignedWaveManager,
        BoardManager assignedBoardManager,
        PlayerMove assignedPlayerMove,
        PlayerHealth assignedPlayerHealth)
    {
        Unsubscribe();
        waveManager = assignedWaveManager;
        boardManager = assignedBoardManager;
        playerMove = assignedPlayerMove;
        playerHealth = assignedPlayerHealth;
        bombsPaused = false;

        if (waveManager != null)
        {
            waveManager.EnemyTurnCycleCompleted += HandleEnemyTurnCycleCompleted;
            waveManager.BattleCompleted += ClearAll;
            waveManager.BattleFailed += ClearAll;
        }

        if (playerHealth != null)
        {
            playerHealth.Defeated += ClearAll;
        }
    }

    public bool TrySpawnBomb(
        EnemyData sourceData,
        int tileIndex,
        int fuseTurns,
        out BossBomb spawnedBomb)
    {
        spawnedBomb = null;

        if (bombsPaused || sourceData == null || boardManager == null
            || tileIndex < 0 || tileIndex >= boardManager.BoardCount
            || bombsByTile.ContainsKey(tileIndex)
            || sourceData.BigBarrel.BossBombPrefab == null
            || !boardManager.TryGetTilePosition(
                tileIndex,
                out Vector3 spawnPosition))
        {
            return false;
        }

        spawnPosition.y += 0.3f;

        GameObject bombObject = Instantiate(
            sourceData.BigBarrel.BossBombPrefab,
            spawnPosition,
            Quaternion.identity,
            transform);
        BossBomb bomb = bombObject.GetComponent<BossBomb>();

        if (bomb == null)
        {
            bombObject.SetActive(false);
            Destroy(bombObject);
            return false;
        }

        int currentCycle = waveManager == null
            ? 0
            : waveManager.CurrentEnemyTurnCycle;

        if (!bomb.Initialize(
                this,
                sourceData,
                tileIndex,
                fuseTurns,
                currentCycle))
        {
            bombObject.SetActive(false);
            Destroy(bombObject);
            return false;
        }

        activeBombs.Add(bomb);
        bombsByTile.Add(tileIndex, bomb);
        spawnedBomb = bomb;
        return true;
    }

    public bool HasBombAtTile(int tileIndex)
    {
        return bombsByTile.TryGetValue(tileIndex, out BossBomb bomb)
            && bomb != null && !bomb.IsExploding;
    }

    public bool TryGetBombAtTile(int tileIndex, out BossBomb bomb)
    {
        if (bombsByTile.TryGetValue(tileIndex, out bomb)
            && bomb != null && !bomb.IsExploding)
        {
            return true;
        }

        bomb = null;
        return false;
    }

    public bool IsTileThreatened(int tileIndex, int maximumFuse)
    {
        foreach (BossBomb bomb in activeBombs)
        {
            if (bomb == null || bomb.IsExploding
                || bomb.RemainingFuse > maximumFuse
                || bomb.SourceData == null)
            {
                continue;
            }

            if (Mathf.Abs(tileIndex - bomb.TileIndex)
                <= bomb.SourceData.BigBarrel.BombExplosionRadius)
            {
                return true;
            }
        }

        return false;
    }

    public void RequestDetonation(BossBomb bomb)
    {
        if (bombsPaused || bomb == null || bomb.IsExploding
            || !queuedDetonations.Add(bomb))
        {
            return;
        }

        detonationQueue.Enqueue(bomb);

        if (!isProcessingDetonations)
        {
            ProcessDetonationQueue();
        }
    }

    public void PauseForBossDefeat()
    {
        bombsPaused = true;
        detonationQueue.Clear();
        queuedDetonations.Clear();
    }

    public void ClearAll()
    {
        bombsPaused = true;
        detonationQueue.Clear();
        queuedDetonations.Clear();

        BossBomb[] snapshot = activeBombs.ToArray();
        activeBombs.Clear();
        bombsByTile.Clear();

        foreach (BossBomb bomb in snapshot)
        {
            if (bomb == null)
            {
                continue;
            }

            bomb.DisposeVisuals();
            bomb.gameObject.SetActive(false);
            Destroy(bomb.gameObject);
        }
    }

    public void ResumeForBattle()
    {
        bombsPaused = false;
    }

    public void NotifyBombDestroyed(BossBomb bomb)
    {
        if (bomb == null)
        {
            return;
        }

        activeBombs.Remove(bomb);

        if (bombsByTile.TryGetValue(bomb.TileIndex, out BossBomb registered)
            && registered == bomb)
        {
            bombsByTile.Remove(bomb.TileIndex);
        }
    }

    private void HandleEnemyTurnCycleCompleted(int completedTurnCycle)
    {
        if (bombsPaused)
        {
            return;
        }

        BossBomb[] snapshot = activeBombs.ToArray();

        foreach (BossBomb bomb in snapshot)
        {
            bomb?.ProcessEnemyTurnCycleEnd(completedTurnCycle);
        }
    }

    private void ProcessDetonationQueue()
    {
        isProcessingDetonations = true;

        while (!bombsPaused && detonationQueue.Count > 0)
        {
            BossBomb bomb = detonationQueue.Dequeue();
            queuedDetonations.Remove(bomb);

            if (bomb == null || !bomb.TryBeginExplosion())
            {
                continue;
            }

            EnemyData sourceData = bomb.SourceData;
            int centerTile = bomb.TileIndex;
            int radius = sourceData.BigBarrel.BombExplosionRadius;
            QueueChainBombs(centerTile, radius, bomb);
            ApplyExplosionDamage(sourceData, centerTile, radius);
            RemoveBomb(bomb);
        }

        detonationQueue.Clear();
        queuedDetonations.Clear();
        isProcessingDetonations = false;
    }

    private void QueueChainBombs(
        int centerTile,
        int radius,
        BossBomb explodingBomb)
    {
        BossBomb[] snapshot = activeBombs.ToArray();

        foreach (BossBomb otherBomb in snapshot)
        {
            if (otherBomb == null || otherBomb == explodingBomb
                || otherBomb.IsExploding
                || Mathf.Abs(otherBomb.TileIndex - centerTile) > radius
                || !queuedDetonations.Add(otherBomb))
            {
                continue;
            }

            detonationQueue.Enqueue(otherBomb);
        }
    }

    private void ApplyExplosionDamage(
        EnemyData sourceData,
        int centerTile,
        int radius)
    {
        BigBarrelSettings settings = sourceData.BigBarrel;

        if (playerMove != null && playerHealth != null
            && boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerTile)
            && Mathf.Abs(playerTile - centerTile) <= radius)
        {
            playerHealth.ApplyDamage(settings.BombDamage);
        }

        if (waveManager != null)
        {
            EnemyController[] enemies = new List<EnemyController>(
                waveManager.ActiveEnemies).ToArray();

            foreach (EnemyController enemy in enemies)
            {
                if (enemy == null || enemy.CurrentHealth <= 0
                    || !boardManager.TryGetTileIndex(
                        enemy.transform.position,
                        out int enemyTile)
                    || Mathf.Abs(enemyTile - centerTile) > radius)
                {
                    continue;
                }

                enemy.ApplyExplosionDamage(
                    settings.BombDamage,
                    settings.BossSelfExplosionDamage);
            }
        }

    }

    private void RemoveBomb(BossBomb bomb)
    {
        NotifyBombDestroyed(bomb);
        bomb.DisposeVisuals();
        bomb.gameObject.SetActive(false);
        Destroy(bomb.gameObject);
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearAll();
    }

    private void Unsubscribe()
    {
        if (waveManager != null)
        {
            waveManager.EnemyTurnCycleCompleted -= HandleEnemyTurnCycleCompleted;
            waveManager.BattleCompleted -= ClearAll;
            waveManager.BattleFailed -= ClearAll;
        }

        if (playerHealth != null)
        {
            playerHealth.Defeated -= ClearAll;
        }
    }
}
