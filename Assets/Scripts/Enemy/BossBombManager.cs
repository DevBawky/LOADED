using System;
using System.Collections;
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
        return TrySpawnBombInternal(
            sourceData,
            null,
            tileIndex,
            laneIndex,
            fuseTurns,
            out spawnedBomb);
    }

    public bool TrySpawnBomb(
        SpecialBattleBombProfile profile,
        int tileIndex,
        int laneIndex,
        out BossBomb spawnedBomb)
    {
        return TrySpawnBombInternal(
            null,
            profile,
            tileIndex,
            laneIndex,
            profile == null ? 0 : profile.FuseTurns,
            out spawnedBomb);
    }

    private bool TrySpawnBombInternal(
        EnemyData sourceData,
        SpecialBattleBombProfile profile,
        int tileIndex,
        int laneIndex,
        int fuseTurns,
        out BossBomb spawnedBomb)
    {
        spawnedBomb = null;
        int cellKey = GetCellKey(tileIndex, laneIndex);
        GameObject bombPrefab = profile == null
            ? sourceData == null ? null : sourceData.BigBarrel.BossBombPrefab
            : profile.BombPrefab;

        if (bombsPaused
            || sourceData == null && profile == null
            || boardManager == null
            || cellKey < 0 || bombsByTile.ContainsKey(cellKey)
            || bombPrefab == null
            || !boardManager.TryGetTilePosition(
                tileIndex,
                laneIndex,
                out Vector3 spawnPosition))
        {
            return false;
        }

        GameObject bombObject = Instantiate(
            bombPrefab,
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

        bool initialized = profile == null
            ? bomb.Initialize(
                this,
                sourceData,
                tileIndex,
                laneIndex,
                fuseTurns,
                currentCycle)
            : bomb.Initialize(
                this,
                profile,
                tileIndex,
                laneIndex,
                fuseTurns,
                currentCycle);
        if (!initialized)
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
            if (bomb == null || bomb.IsExploding
                || bomb.SourceData == null && bomb.SpecialProfile == null)
            {
                continue;
            }

            results.Add(new RunBombSaveData
            {
                sourceEnemyAssetName = bomb.SourceData == null
                    ? string.Empty
                    : bomb.SourceData.name,
                specialBombProfileId = bomb.SpecialProfile == null
                    ? string.Empty
                    : bomb.SpecialProfile.ProfileId,
                tileIndex = bomb.TileIndex,
                laneIndex = bomb.LaneIndex,
                remainingFuse = bomb.RemainingFuse,
                createdTurnCycle = bomb.CreatedTurnCycle
            });
        }
    }

    public bool RestoreRunState(
        IReadOnlyList<RunBombSaveData> savedBombs,
        Func<string, EnemyData> resolveEnemyData,
        Func<string, SpecialBattleBombProfile> resolveSpecialProfile = null)
    {
        ClearAll();
        ResumeForBattle();

        if (savedBombs == null)
        {
            return true;
        }

        foreach (RunBombSaveData savedBomb in savedBombs)
        {
            bool usesSpecialProfile = savedBomb != null
                && !string.IsNullOrWhiteSpace(
                    savedBomb.specialBombProfileId);
            EnemyData sourceData = savedBomb == null
                ? null
                : resolveEnemyData?.Invoke(
                    savedBomb.sourceEnemyAssetName);
            SpecialBattleBombProfile profile = usesSpecialProfile
                ? resolveSpecialProfile?.Invoke(
                    savedBomb.specialBombProfileId)
                : null;
            BossBomb bomb = null;

            bool spawned = savedBomb != null
                && (usesSpecialProfile
                    ? profile != null && TrySpawnBombInternal(
                        null,
                        profile,
                        savedBomb.tileIndex,
                        savedBomb.laneIndex,
                        savedBomb.remainingFuse,
                        out bomb)
                    : sourceData != null && TrySpawnBombInternal(
                        sourceData,
                        null,
                        savedBomb.tileIndex,
                        savedBomb.laneIndex,
                        savedBomb.remainingFuse,
                        out bomb));

            if (!spawned)
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
                || bomb.SourceData == null && bomb.SpecialProfile == null)
            {
                continue;
            }

            if (IsCellInExplosionRange(
                    bomb.TileIndex,
                    bomb.LaneIndex,
                    tileIndex,
                    laneIndex,
                    bomb.ExplosionRadius))
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

    private void ReleaseBombCell(BossBomb bomb)
    {
        if (bomb == null)
        {
            return;
        }

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

            ReleaseBombCell(bomb);
            EnemyData sourceData = bomb.SourceData;
            int centerTile = bomb.TileIndex;
            int laneIndex = bomb.LaneIndex;
            int radius = bomb.ExplosionRadius;
            QueueChainBombs(centerTile, laneIndex, radius, bomb);
            pendingExplosionResolutions++;
            StartCoroutine(ResolveExplosionAfterWarning(
                bomb,
                sourceData,
                centerTile,
                laneIndex,
                radius));
        }

        detonationQueue.Clear();
        queuedDetonations.Clear();
        isProcessingDetonations = false;
    }

    private IEnumerator ResolveExplosionAfterWarning(
        BossBomb bomb,
        EnemyData sourceData,
        int centerTile,
        int laneIndex,
        int radius)
    {
        float dodgeWindowDuration = bomb == null
            ? EnemyData.DefaultAttackDodgeWindowDuration
            : bomb.DodgeWindowDuration;
        float chargeElapsedTime = 0f;

        while (chargeElapsedTime < dodgeWindowDuration)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                chargeElapsedTime += Time.deltaTime;
                boardManager?.SetWarningProgress(bomb,
                    dodgeWindowDuration <= 0f
                        ? 1f
                        : chargeElapsedTime / dodgeWindowDuration);
            }
        }

        if (bombsPaused || bomb == null
            || !activeBombs.Contains(bomb))
        {
            pendingExplosionResolutions = Mathf.Max(
                0,
                pendingExplosionResolutions - 1);
            yield break;
        }

        boardManager?.SetWarningProgress(bomb, 1f);
        SoundManager.PlayEnemyAttackWarning();
        EnemyPlayerDodgeWindowState dodgeState =
            CapturePlayerDodgeWindow(centerTile, laneIndex, radius);
        EnemyPlayerDodgeResolution dodgeResolution = default;
        float dodgeElapsedTime = 0f;

        while (dodgeElapsedTime < dodgeWindowDuration)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                dodgeElapsedTime += Time.deltaTime;
                TryConfirmPlayerDodge(
                    dodgeState,
                    IsPlayerThreatened(centerTile, laneIndex, radius),
                    sourceData,
                    ref dodgeResolution);
            }
        }

        if (bombsPaused || bomb == null
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
        SoundManager.PlaySfx("SFX_BigBarrel_Bomb");
        combatFeedback ??= FindFirstObjectByType<CombatFeedbackController>();
        combatFeedback?.RecordExplosionCameraShake();
        SpawnExplosionVfxOnAffectedTiles(bomb, centerTile, laneIndex, radius);
        ApplyExplosionDamage(
            bomb,
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
        BossBomb bomb,
        int centerTile,
        int laneIndex,
        int radius)
    {
        if (bomb == null || boardManager == null
            || bomb.ExplosionVfxPrefab == null)
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
                bomb.ExplosionVfxPrefab,
                effectPosition,
                Quaternion.identity,
                bomb.ExplosionVfxScale);
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
        BossBomb bomb,
        int centerTile,
        int laneIndex,
        int radius,
        bool playerDodged)
    {
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
            playerHealth.ApplyDamage(bomb.PlayerDamage);
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
                    bomb.EnemyDamage,
                    bomb.BossDamage);
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
