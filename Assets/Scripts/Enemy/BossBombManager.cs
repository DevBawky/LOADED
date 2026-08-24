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

        spawnPosition.y = BombSpawnOffsetY;

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
        StopAllCoroutines();
        detonationQueue.Clear();
        queuedDetonations.Clear();
        isProcessingDetonations = false;
        pendingExplosionResolutions = 0;
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

        if (bombsByTile.TryGetValue(bomb.TileIndex, out BossBomb registered)
            && registered == bomb)
        {
            bombsByTile.Remove(bomb.TileIndex);
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
            int radius = sourceData.BigBarrel.BombExplosionRadius;
            EnemyPlayerDodgeWindowState dodgeState =
                CapturePlayerDodgeWindow(centerTile, radius);
            SoundManager.PlaySfx("SFX_BigBarrel_Bomb");
            combatFeedback ??=
                FindFirstObjectByType<CombatFeedbackController>();
            combatFeedback?.RecordExplosionCameraShake();
            SpawnExplosionVfxOnAffectedTiles(
                sourceData,
                centerTile,
                radius);
            QueueChainBombs(centerTile, radius, bomb);
            pendingExplosionResolutions++;
            StartCoroutine(ResolveExplosionAfterDodgeWindow(
                bomb,
                sourceData,
                centerTile,
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
                    IsPlayerThreatened(centerTile, radius),
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
            IsPlayerThreatened(centerTile, radius),
            sourceData,
            ref dodgeResolution);
        ApplyExplosionDamage(
            sourceData,
            centerTile,
            radius,
            dodgeResolution.PlayerDodged);

        if (!bombsPaused && bomb != null && activeBombs.Contains(bomb))
        {
            RemoveBomb(bomb);
        }
    }

    private EnemyPlayerDodgeWindowState CapturePlayerDodgeWindow(
        int centerTile,
        int radius)
    {
        if (!IsPlayerThreatened(centerTile, radius)
            || boardManager == null || playerMove == null
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerTileIndex))
        {
            return default;
        }

        return new EnemyPlayerDodgeWindowState(
            true,
            playerTileIndex,
            playerMove.transform.position);
    }

    private bool IsPlayerThreatened(int centerTile, int radius)
    {
        return boardManager != null && playerMove != null
            && boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerTileIndex)
            && Mathf.Abs(playerTileIndex - centerTile) <= radius;
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
                out int currentPlayerTileIndex))
        {
            return false;
        }

        if (!resolution.TryConfirmBeforeImpact(
                dodgeState,
                playerIsThreatened,
                currentPlayerTileIndex,
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
        Vector3 currentPlayerPosition = playerMove == null
            ? dodgeState.PlayerPosition
            : playerMove.transform.position;

        if (boardManager != null && playerMove != null)
        {
            boardManager.TryGetTileIndex(
                currentPlayerPosition,
                out currentPlayerTileIndex);
        }

        if (resolution.ResolveAtImpact(
                dodgeState,
                playerIsThreatened,
                currentPlayerTileIndex,
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
        int radius,
        bool playerDodged)
    {
        BigBarrelSettings settings = sourceData.BigBarrel;

        if (!playerDodged && playerMove != null && playerHealth != null
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
            waveManager.BattleCompleted -= ClearAll;
            waveManager.BattleFailed -= ClearAll;
        }

        if (playerHealth != null)
        {
            playerHealth.Defeated -= ClearAll;
        }
    }
}
