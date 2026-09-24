using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossBombManager : MonoBehaviour
{
    private const float BombSpawnOffsetY = -0.3f;

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
    private CombatFeedbackController combatFeedback;
    private bool bombsPaused;
    private bool isProcessingDetonations;
    private int pendingExplosionResolutions;

    public IReadOnlyList<BossBomb> ActiveBombs => activeBombs;
    public BoardManager BoardManager => boardManager;
    internal bool IsResolvingExplosions => pendingExplosionResolutions > 0;

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
        combatFeedback = playerMove == null
            ? FindFirstObjectByType<CombatFeedbackController>()
            : playerMove.GetComponent<CombatFeedbackController>();
        bombsPaused = false;

        if (waveManager != null)
        {
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
        return TrySpawnBomb(
            sourceData,
            tileIndex,
            0,
            fuseTurns,
            out spawnedBomb);
    }

    public bool TrySpawnBomb(
        EnemyData sourceData,
        int tileIndex,
        int laneIndex,
        int fuseTurns,
        out BossBomb spawnedBomb)
    {
        spawnedBomb = null;
        int cellKey = GetCellKey(tileIndex, laneIndex);

        if (bombsPaused || sourceData == null || boardManager == null
            || cellKey < 0 || bombsByTile.ContainsKey(cellKey)
            || sourceData.BigBarrel.BossBombPrefab == null
            || !boardManager.TryGetTilePosition(
                tileIndex,
                laneIndex,
                out Vector3 spawnPosition))
        {
            return false;
        }

        spawnPosition.y += BombSpawnOffsetY;

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
                laneIndex,
                fuseTurns,
                currentCycle))
        {
            bombObject.SetActive(false);
            Destroy(bombObject);
            return false;
        }

        activeBombs.Add(bomb);
        bombsByTile.Add(cellKey, bomb);
        spawnedBomb = bomb;
        return true;
    }

    public void CaptureRunState(List<RunBombSaveData> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();

        foreach (BossBomb bomb in activeBombs)
        {
            if (bomb == null || bomb.IsExploding || bomb.SourceData == null)
            {
                continue;
            }

            results.Add(new RunBombSaveData
            {
                sourceEnemyAssetName = bomb.SourceData.name,
                tileIndex = bomb.TileIndex,
                laneIndex = bomb.LaneIndex,
                remainingFuse = bomb.RemainingFuse,
                createdTurnCycle = bomb.CreatedTurnCycle
            });
        }
    }

    public bool RestoreRunState(
        IReadOnlyList<RunBombSaveData> savedBombs,
        Func<string, EnemyData> resolveEnemyData)
    {
        ClearAll();
        ResumeForBattle();

        if (savedBombs == null)
        {
            return true;
        }

        foreach (RunBombSaveData savedBomb in savedBombs)
        {
            EnemyData sourceData = savedBomb == null
                ? null
                : resolveEnemyData?.Invoke(
                    savedBomb.sourceEnemyAssetName);

            if (savedBomb == null || sourceData == null
                || !TrySpawnBomb(
                    sourceData,
                    savedBomb.tileIndex,
                    savedBomb.laneIndex,
                    savedBomb.remainingFuse,
                    out BossBomb bomb))
            {
                ClearAll();
                return false;
            }

            bomb.RestoreRunTiming(
                savedBomb.remainingFuse,
                savedBomb.createdTurnCycle);
        }

        return true;
    }

    public bool HasBombAtTile(int tileIndex)
    {
        return HasBombAtTile(tileIndex, 0);
    }

    public bool HasBombAtTile(int tileIndex, int laneIndex)
    {
        return bombsByTile.TryGetValue(
                GetCellKey(tileIndex, laneIndex),
                out BossBomb bomb)
            && bomb != null && !bomb.IsExploding;
    }

    public bool TryGetBombAtTile(int tileIndex, out BossBomb bomb)
    {
        return TryGetBombAtTile(tileIndex, 0, out bomb);
    }

    public bool TryGetBombAtTile(
        int tileIndex,
        int laneIndex,
        out BossBomb bomb)
    {
        if (bombsByTile.TryGetValue(GetCellKey(tileIndex, laneIndex), out bomb)
            && bomb != null && !bomb.IsExploding)
        {
            return true;
        }

        bomb = null;
        return false;
    }

    public bool IsTileThreatened(int tileIndex, int maximumFuse)
    {
        return IsTileThreatened(tileIndex, 0, maximumFuse);
    }

    public bool IsTileThreatened(
        int tileIndex,
        int laneIndex,
        int maximumFuse)
    {
        foreach (BossBomb bomb in activeBombs)
        {
            if (bomb == null || bomb.IsExploding
                || bomb.RemainingFuse > maximumFuse
                || bomb.SourceData == null)
            {
                continue;
            }

            if (IsCellInExplosionRange(
                    bomb.TileIndex,
                    bomb.LaneIndex,
                    tileIndex,
                    laneIndex,
                    bomb.SourceData.BigBarrel.BombExplosionRadius))
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
        StopAllCoroutines();
        detonationQueue.Clear();
        queuedDetonations.Clear();
        isProcessingDetonations = false;
        pendingExplosionResolutions = 0;
        foreach (BossBomb bomb in activeBombs)
        {
            bomb?.DisposeVisuals();
        }
    }

    public void ClearAll()
    {
        bombsPaused = true;
        StopAllCoroutines();
        detonationQueue.Clear();
        queuedDetonations.Clear();
        isProcessingDetonations = false;
        pendingExplosionResolutions = 0;

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

        int cellKey = GetCellKey(bomb.TileIndex, bomb.LaneIndex);

        if (bombsByTile.TryGetValue(cellKey, out BossBomb registered)
            && registered == bomb)
        {
            bombsByTile.Remove(cellKey);
        }
    }

    internal void ProcessEnemyTurnCycleEnd(int completedTurnCycle)
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
            int laneIndex = bomb.LaneIndex;
            int radius = sourceData.BigBarrel.BombExplosionRadius;
            EnemyPlayerDodgeWindowState dodgeState =
                CapturePlayerDodgeWindow(centerTile, laneIndex, radius);
            SoundManager.PlaySfx("SFX_BigBarrel_Bomb");
            combatFeedback ??=
                FindFirstObjectByType<CombatFeedbackController>();
            combatFeedback?.RecordExplosionCameraShake();
            SpawnExplosionVfxOnAffectedTiles(
                sourceData,
                centerTile,
                laneIndex,
                radius);
            QueueChainBombs(centerTile, laneIndex, radius, bomb);
            pendingExplosionResolutions++;
            StartCoroutine(ResolveExplosionAfterDodgeWindow(
                bomb,
                sourceData,
                centerTile,
                laneIndex,
                radius,
                dodgeState));
        }

        detonationQueue.Clear();
        queuedDetonations.Clear();
        isProcessingDetonations = false;
    }

    private IEnumerator ResolveExplosionAfterDodgeWindow(
        BossBomb bomb,
        EnemyData sourceData,
        int centerTile,
        int laneIndex,
        int radius,
        EnemyPlayerDodgeWindowState dodgeState)
    {
        float elapsedTime = 0f;
        EnemyPlayerDodgeResolution dodgeResolution = default;
        float dodgeWindowDuration = sourceData == null
            ? EnemyData.DefaultAttackDodgeWindowDuration
            : sourceData.AttackDodgeWindowDuration;

        while (elapsedTime < dodgeWindowDuration)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                elapsedTime += Time.deltaTime;
                TryConfirmPlayerDodge(
                    dodgeState,
                    IsPlayerThreatened(centerTile, laneIndex, radius),
                    sourceData,
                    ref dodgeResolution);
            }
        }

        if (bombsPaused || bomb == null || sourceData == null
            || !activeBombs.Contains(bomb))
        {
            pendingExplosionResolutions = Mathf.Max(
                0,
                pendingExplosionResolutions - 1);
            yield break;
        }

        pendingExplosionResolutions = Mathf.Max(
            0,
            pendingExplosionResolutions - 1);
        ResolvePlayerDodgeAtImpact(
            dodgeState,
            IsPlayerThreatened(centerTile, laneIndex, radius),
            sourceData,
            ref dodgeResolution);
        ApplyExplosionDamage(
            sourceData,
            centerTile,
            laneIndex,
            radius,
            dodgeResolution.PlayerDodged);

        if (!bombsPaused && bomb != null && activeBombs.Contains(bomb))
        {
            boardManager?.CompleteTileWarnings(bomb);
            RemoveBomb(bomb);
        }
    }

    private EnemyPlayerDodgeWindowState CapturePlayerDodgeWindow(
        int centerTile,
        int laneIndex,
        int radius)
    {
        if (!IsPlayerThreatened(centerTile, laneIndex, radius)
            || boardManager == null || playerMove == null
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                playerMove.CurrentLaneIndex,
                out int playerTileIndex))
        {
            return default;
        }

        return new EnemyPlayerDodgeWindowState(
            true,
            playerTileIndex,
            playerMove.CurrentLaneIndex,
            playerMove.transform.position);
    }

    private bool IsPlayerThreatened(
        int centerTile,
        int laneIndex,
        int radius)
    {
        return boardManager != null && playerMove != null
            && boardManager.TryGetTileIndex(
                playerMove.transform.position,
                playerMove.CurrentLaneIndex,
                out int playerTileIndex)
            && IsCellInExplosionRange(
                centerTile,
                laneIndex,
                playerTileIndex,
                playerMove.CurrentLaneIndex,
                radius);
    }

    private bool TryConfirmPlayerDodge(
        EnemyPlayerDodgeWindowState dodgeState,
        bool playerIsThreatened,
        EnemyData sourceData,
        ref EnemyPlayerDodgeResolution resolution)
    {
        if (boardManager == null || playerMove == null)
        {
            return false;
        }

        if (!boardManager.TryGetTileIndex(
                playerMove.transform.position,
                playerMove.CurrentLaneIndex,
                out int currentPlayerTileIndex))
        {
            return false;
        }

        if (!resolution.TryConfirmBeforeImpact(
                dodgeState,
                playerIsThreatened,
                currentPlayerTileIndex,
                playerMove.CurrentLaneIndex,
                playerMove.transform.position,
                out int movementDirection))
        {
            return false;
        }

        HandlePlayerDodgeConfirmed(
            dodgeState,
            movementDirection,
            sourceData);
        return true;
    }

    private void ResolvePlayerDodgeAtImpact(
        EnemyPlayerDodgeWindowState dodgeState,
        bool playerIsThreatened,
        EnemyData sourceData,
        ref EnemyPlayerDodgeResolution resolution)
    {
        int currentPlayerTileIndex = -1;
        int currentPlayerLaneIndex = -1;
        Vector3 currentPlayerPosition = playerMove == null
            ? dodgeState.PlayerPosition
            : playerMove.transform.position;

        if (boardManager != null && playerMove != null)
        {
            currentPlayerLaneIndex = playerMove.CurrentLaneIndex;
            boardManager.TryGetTileIndex(
                currentPlayerPosition,
                currentPlayerLaneIndex,
                out currentPlayerTileIndex);
        }

        if (resolution.ResolveAtImpact(
                dodgeState,
                playerIsThreatened,
                currentPlayerTileIndex,
                currentPlayerLaneIndex,
                currentPlayerPosition,
                out int movementDirection))
        {
            HandlePlayerDodgeConfirmed(
                dodgeState,
                movementDirection,
                sourceData);
        }
    }

    private void HandlePlayerDodgeConfirmed(
        EnemyPlayerDodgeWindowState dodgeState,
        int movementDirection,
        EnemyData sourceData)
    {
        if (playerMove == null)
        {
            return;
        }

        ApplyExposedToSourceEnemy(sourceData);
        playerMove.TryNotifyDodgeSucceededDuringAction();
        combatFeedback ??= playerMove.GetComponent<
            CombatFeedbackController>();
        combatFeedback?.RecordPlayerDodge(
            dodgeState.PlayerPosition,
            movementDirection);
    }

    private void ApplyExposedToSourceEnemy(EnemyData sourceData)
    {
        if (sourceData == null || waveManager == null)
        {
            return;
        }

        foreach (EnemyController enemy in waveManager.ActiveEnemies)
        {
            if (enemy != null && enemy.CurrentHealth > 0
                && enemy.Data == sourceData)
            {
                enemy.ApplyExposedFromDodge();
                return;
            }
        }
    }

    private void SpawnExplosionVfxOnAffectedTiles(
        EnemyData sourceData,
        int centerTile,
        int laneIndex,
        int radius)
    {
        if (sourceData == null || boardManager == null
            || sourceData.ExplosionVfxPrefab == null)
        {
            return;
        }

        int firstTile = Mathf.Max(0, centerTile - radius);
        int lastTile = Mathf.Min(
            boardManager.BoardCount - 1,
            centerTile + radius);

        for (int tileIndex = firstTile;
             tileIndex <= lastTile;
             tileIndex++)
        {
            if (!boardManager.TryGetTilePosition(
                    tileIndex,
                    laneIndex,
                    out Vector3 effectPosition))
            {
                continue;
            }

            effectPosition.y += 0.3f;
            TransientVfx.Spawn(
                sourceData.ExplosionVfxPrefab,
                effectPosition,
                Quaternion.identity,
                sourceData.ExplosionVfxScale);
        }
    }

    private void QueueChainBombs(
        int centerTile,
        int laneIndex,
        int radius,
        BossBomb explodingBomb)
    {
        BossBomb[] snapshot = activeBombs.ToArray();

        foreach (BossBomb otherBomb in snapshot)
        {
            if (otherBomb == null || otherBomb == explodingBomb
                || otherBomb.IsExploding
                || !IsCellInExplosionRange(
                    centerTile,
                    laneIndex,
                    otherBomb.TileIndex,
                    otherBomb.LaneIndex,
                    radius)
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
        int laneIndex,
        int radius,
        bool playerDodged)
    {
        BigBarrelSettings settings = sourceData.BigBarrel;

        if (!playerDodged && playerMove != null && playerHealth != null
            && boardManager.TryGetTileIndex(
                playerMove.transform.position,
                playerMove.CurrentLaneIndex,
                out int playerTile)
            && IsCellInExplosionRange(
                centerTile,
                laneIndex,
                playerTile,
                playerMove.CurrentLaneIndex,
                radius))
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
                        enemy.CurrentLaneIndex,
                        out int enemyTile)
                    || !IsCellInExplosionRange(
                        centerTile,
                        laneIndex,
                        enemyTile,
                        enemy.CurrentLaneIndex,
                        radius))
                {
                    continue;
                }

                enemy.ApplyExplosionDamage(
                    settings.BombDamage,
                    settings.BossSelfExplosionDamage);
            }
        }

    }

    internal static bool IsCellInExplosionRange(
        int centerTileIndex,
        int explosionLaneIndex,
        int targetTileIndex,
        int targetLaneIndex,
        int radius)
    {
        return centerTileIndex >= 0
            && targetTileIndex >= 0
            && explosionLaneIndex >= 0
            && targetLaneIndex == explosionLaneIndex
            && Mathf.Abs(targetTileIndex - centerTileIndex)
                <= Mathf.Max(0, radius);
    }

    private void RemoveBomb(BossBomb bomb)
    {
        NotifyBombDestroyed(bomb);
        bomb.DisposeVisuals();
        bomb.gameObject.SetActive(false);
        Destroy(bomb.gameObject);
    }

    private int GetCellKey(int tileIndex, int laneIndex)
    {
        if (boardManager == null || tileIndex < 0
            || tileIndex >= boardManager.BoardCount || laneIndex < 0
            || laneIndex >= boardManager.LaneCount)
        {
            return -1;
        }

        return laneIndex * boardManager.BoardCount + tileIndex;
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
            waveManager.BattleCompleted -= ClearAll;
            waveManager.BattleFailed -= ClearAll;
        }

        if (playerHealth != null)
        {
            playerHealth.Defeated -= ClearAll;
        }
    }
}
