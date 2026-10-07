using System.Collections.Generic;
using UnityEngine;

public partial class EnemyController
{
    internal readonly struct TurnIntent
    {
        public TurnIntent(EnemyTurnActionType action, int direction = 0,
            Vector3[] path = null, int lane = 0,
            EnemyController support = null, EnemySupportType supportType = EnemySupportType.None)
        {
            Action = action;
            Direction = direction;
            Path = path;
            Lane = lane;
            Support = support;
            SupportType = supportType;
        }

        public EnemyTurnActionType Action { get; }
        public int Direction { get; }
        public Vector3[] Path { get; }
        public int Lane { get; }
        public EnemyController Support { get; }
        public EnemySupportType SupportType { get; }
    }

    private bool hasCommittedIntent;
    private TurnIntent committedIntent;
    private Vector3 committedOrigin;
    private int committedLane;
    private readonly HashSet<Vector2Int> committedAttackCells = new HashSet<Vector2Int>();
    private int preparationWaitTurns;
    private bool preparationDeferred;

    internal bool HasCommittedTurnIntent => hasCommittedIntent;
    internal int PreparationWaitTurns => preparationWaitTurns;
    internal EnemyAttackIconType AttackIconType => IsBoss
        ? IsShotgunIntent() ? EnemyAttackIconType.Shotgun : EnemyAttackIconType.Bomb
        : enemyData != null && enemyData.BehaviorType == EnemyBehaviorType.Thrower
            ? EnemyAttackIconType.Throw
            : enemyData != null && enemyData.BehaviorType == EnemyBehaviorType.Gunner
                ? EnemyAttackIconType.Ranged : EnemyAttackIconType.Melee;

    // Reading an intent never retargets it or consumes RNG. A new plan is
    // committed only on the settled board, before the player's next action.
    internal TurnIntent GetNextTurnIntent()
    {
        if (currentHealth <= 0 || statusEffects != null && statusEffects.IsStunned)
            return new TurnIntent(EnemyTurnActionType.Wait);
        if (!hasCommittedIntent) return CalculateTurnIntent();
        // Forced displacement interrupts a plan; it must not turn into a new
        // attack or a path back through occupied cells.
        if (!isActing && (committedLane != currentLaneIndex
            || (transform.position - committedOrigin).sqrMagnitude > 0.001f))
            return new TurnIntent(EnemyTurnActionType.Wait);
        return committedIntent;
    }

    internal void CommitTurnIntent()
    {
        if (hasCommittedIntent || !isInitialized || currentHealth <= 0) return;
        if (waveManager != null && waveManager.TryCommitEnemyTurnIntents(this)) return;
        CommitScheduledTurnIntent(true);
    }

    internal void CommitScheduledTurnIntent(bool allowPreparation)
    {
        if (hasCommittedIntent || !isInitialized || currentHealth <= 0) return;
        TurnIntent desired = CalculateTurnIntent();
        preparationDeferred = desired.Action == EnemyTurnActionType.PrepareAttack && !allowPreparation;
        if (!preparationDeferred) preparationWaitTurns = 0;
        committedIntent = preparationDeferred ? CalculateTurnIntent(false) : desired;
        committedOrigin = transform.position;
        committedLane = currentLaneIndex;
        hasCommittedIntent = true;
        committedAttackCells.Clear();
        if (committedIntent.Action == EnemyTurnActionType.Support)
        {
            preparedSupportTarget = committedIntent.Support;
            preparedSupportType = committedIntent.SupportType;
        }
        // Preparation advertises the action only. Its aim is captured when
        // preparation actually happens, after the player's movement settles.
        if (committedIntent.Action != EnemyTurnActionType.Fire) return;
        CaptureCommittedAttackCells();
    }

    private void CaptureCommittedAttackCells(bool preparing = false)
    {
        committedAttackCells.Clear();
        if (preparing)
        {
            committedOrigin = transform.position;
            committedLane = currentLaneIndex;
            preparedBombTargetTileIndices.Clear();
            preparedShotgunTileIndices.Clear();
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.Thrower)
        {
            if (!isAttackPrepared || preparedTargetTileIndex < 0) CaptureThrowerTargetTile();
            if (preparedTargetTileIndex >= 0)
                committedAttackCells.Add(new Vector2Int(preparedTargetTileIndex, preparedTargetLaneIndex));
        }
        else if (IsBoss)
        {
            bool shotgun = IsShotgunIntent();
            if (queuedAttackActions.Count == 0 && GetAvailableAction(
                shotgun ? EnemyActionType.ShotgunAttack : EnemyActionType.ExplosiveThrow, 0, false) == null)
            {
                committedIntent = new TurnIntent(EnemyTurnActionType.Wait);
                return;
            }
            if (shotgun)
            {
                if (!isAttackPrepared || preparedShotgunTileIndices.Count == 0)
                    CaptureBigBarrelShotgunTargets();
                foreach (int tile in preparedShotgunTileIndices)
                    committedAttackCells.Add(new Vector2Int(tile, preparedBigBarrelLaneIndex));
            }
            else
            {
                if (bigBarrelStep != BigBarrelStep.ExecuteBomb || preparedBombTargetTileIndices.Count == 0)
                {
                    bigBarrelActionUsesPhaseTwo = isBigBarrelPhaseTwo
                        || MaxHealth > 0 && (float)currentHealth / MaxHealth <= enemyData.BigBarrel.PhaseTwoHealthRatio;
                    CaptureBigBarrelBombTargets();
                    preparedBigBarrelFuse = bigBarrelActionUsesPhaseTwo
                        ? enemyData.BigBarrel.PhaseTwoBombFuseTurns : enemyData.BigBarrel.BombFuseTurns;
                    bigBarrelStep = BigBarrelStep.ExecuteBomb;
                }
                // These are landing cells. Installed bombs own their later
                // explosion warning and fuse, independently of this intent.
                foreach (int tile in preparedBombTargetTileIndices)
                    committedAttackCells.Add(new Vector2Int(tile, preparedBigBarrelLaneIndex));
            }
        }
        else
        {
            CaptureDirectAttackCells(committedAttackCells);
        }
    }

    private void ClearTurnIntent()
    {
        hasCommittedIntent = false;
        committedAttackCells.Clear();
    }

    private bool IsShotgunIntent()
    {
        return bigBarrelStep == BigBarrelStep.CreateShotgunQueue
            || bigBarrelStep == BigBarrelStep.RegisterShotgun
            || bigBarrelStep == BigBarrelStep.AdjustDistance
            || bigBarrelStep == BigBarrelStep.PrepareShotgun
            || bigBarrelStep == BigBarrelStep.ExecuteShotgun;
    }

    private void CaptureDirectAttackCells(HashSet<Vector2Int> cells, EnemyAttackData executingAttack = null)
    {
        if (boardManager == null || !boardManager.TryGetTileIndex(transform.position, currentLaneIndex, out int origin)) return;
        int range = enemyData.FiringRange;
        if (enemyData.BehaviorType == EnemyBehaviorType.Melee)
        {
            range = 0;
            if (executingAttack != null)
            {
                range = executingAttack.Range;
            }
            else if (queuedAttackActions.Count > 0)
            {
                foreach (EnemyActionData action in queuedAttackActions)
                    if (TryGetAttackData(action, out EnemyAttackData attack)) range = Mathf.Max(range, attack.Range);
            }
            else if (TryGetAttackData(GetAvailableAction(EnemyActionType.MeleeAttack, 0, true), out EnemyAttackData attack))
                range = attack.Range;
        }
        range = Mathf.Min(range, boardManager.BoardCount - 1);
        // A gunner reserves its full firing lane. Occupants determine the
        // first impact at execution, not the permanent reach of the shot.
        if (enemyData.BehaviorType != EnemyBehaviorType.Gunner
            && TryGetDirectAttackTarget(range, out _, out _, out Vector3 hit)
            && boardManager.TryGetTileDistance(transform.position, hit, out int distance))
            range = Mathf.Min(range, distance);
        int facing = transform.localScale.x >= 0f ? 1 : -1;
        for (int step = 1; step <= range; step++)
        {
            int tile = origin + step * facing;
            if (tile < 0 || tile >= boardManager.BoardCount) break;
            cells.Add(new Vector2Int(tile, currentLaneIndex));
        }
    }

    private TurnIntent CalculateTurnIntent(bool allowPreparation = true)
    {
        TurnIntent wait = new TurnIntent(EnemyTurnActionType.Wait);
        if (!isInitialized || enemyData == null || boardManager == null
            || playerMove == null || waveManager == null || currentHealth <= 0
            || statusEffects != null && statusEffects.IsStunned
            || !TryGetTurnContext(out int direction, out int distance))
        {
            return wait;
        }

        if (isAttackPrepared && recoveryTurnsRemaining == 0)
        {
            return new TurnIntent(EnemyTurnActionType.Fire);
        }

        bool pursuesLane = enemyData.BehaviorType == EnemyBehaviorType.Melee
            || enemyData.BehaviorType == EnemyBehaviorType.Gunner
            || enemyData.BehaviorType == EnemyBehaviorType.BigBarrel;
        if (pursuesLane && currentLaneIndex != playerMove.CurrentLaneIndex)
        {
            EnemyLaneMismatchIntent pursuit = EnemyLanePursuitPolicy.GetMismatchIntent(
                ShouldPursuePlayerLane(), direction, distance, IsFacing(direction));
            if (pursuit == EnemyLaneMismatchIntent.Rotate)
                return new TurnIntent(EnemyTurnActionType.Rotate, direction);
            if (pursuit == EnemyLaneMismatchIntent.ChangeLane)
            {
                if (TryGetLaneMovePath(out Vector3[] lanePath, out int lane))
                    return new TurnIntent(EnemyTurnActionType.Move, path: lanePath, lane: lane);
                int staging = EnemyLanePursuitPolicy.GetPreferredStagingDirection(
                    direction, currentLaneIndex, playerMove.CurrentLaneIndex);
                TurnIntent move = BuildMoveIntent(staging, 1);
                return move.Action == EnemyTurnActionType.Move ? move : BuildMoveIntent(-staging, 1);
            }
            int steps = enemyData.BehaviorType == EnemyBehaviorType.BigBarrel
                ? 1 : FindAction(EnemyActionType.Approach)?.MovementDistance ?? 0;
            return BuildMoveIntent(direction, Mathf.Min(steps, Mathf.Max(0, distance - 1)));
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.Porter)
        {
            if (remainingSupportCharges > 0
                && TrySelectSupportTarget(out EnemyController support, out EnemySupportType type))
                return new TurnIntent(EnemyTurnActionType.Support, support: support, supportType: type);
            return BuildRetreatIntent(direction, distance);
        }
        if (direction != 0 && !IsFacing(direction) && !(IsBoss && IsShotgunIntent()))
            return new TurnIntent(EnemyTurnActionType.Rotate, direction);

        if (IsBoss)
        {
            if (recoveryTurnsRemaining > 0 || !allowPreparation
                || !CanPrepareAttackAtCurrentPlayerPosition())
                return IsShotgunIntent() ? wait
                    : BuildMoveIntent(distance < enemyData.PreferredDistance ? -direction : direction,
                        distance == enemyData.PreferredDistance ? 0 : 1);
            return new TurnIntent(EnemyTurnActionType.PrepareAttack);
        }
        if (enemyData.BehaviorType == EnemyBehaviorType.Melee && isRetreating
            && distance < enemyData.PreferredDistance)
        {
            TurnIntent retreat = BuildRetreatIntent(direction, distance);
            if (retreat.Action == EnemyTurnActionType.Move) return retreat;
        }
        EnemyActionType attackType = enemyData.BehaviorType == EnemyBehaviorType.Melee
            ? EnemyActionType.MeleeAttack : EnemyActionType.RangedAttack;
        if (allowPreparation && recoveryTurnsRemaining == 0 && GetAvailableAttackCount(attackType) > 0)
        {
            if (CanPrepareAttackAtCurrentPlayerPosition())
                return new TurnIntent(EnemyTurnActionType.PrepareAttack);
        }
        if (enemyData.BehaviorType == EnemyBehaviorType.Thrower) return wait;
        if (enemyData.BehaviorType == EnemyBehaviorType.Gunner && distance <= enemyData.FiringRange)
            return wait;
        return BuildMoveIntent(direction, FindAction(EnemyActionType.Approach)?.MovementDistance ?? 0);
    }

    private bool CanPrepareAttackAtCurrentPlayerPosition()
    {
        if (enemyData == null || boardManager == null || playerMove == null || waveManager == null
            || recoveryTurnsRemaining > 0 || !TryGetTurnContext(out int direction, out int distance)) return false;

        if (IsBoss)
        {
            if (IsShotgunIntent())
                return currentLaneIndex == playerMove.CurrentLaneIndex && distance > 0;
            // Bomb placement is a board-wide area-control attack, not a direct
            // shot. Require a legal placement whose blast can reach the player.
            if (!boardManager.TryGetTileIndex(transform.position, currentLaneIndex, out int bossTile)
                || !boardManager.TryGetTileIndex(playerMove.transform.position,
                    playerMove.CurrentLaneIndex, out int playerTile)) return false;
            int radius = enemyData.BigBarrel.BombExplosionRadius;
            for (int tile = Mathf.Max(0, playerTile - radius);
                 tile <= Mathf.Min(boardManager.BoardCount - 1, playerTile + radius); tile++)
                if (IsAvailableBigBarrelBombTarget(tile, playerMove.CurrentLaneIndex, bossTile)) return true;
            return false;
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.Thrower)
            return (direction == 0 || IsFacing(direction))
                && Mathf.Max(distance, Mathf.Abs(currentLaneIndex - playerMove.CurrentLaneIndex)) <= enemyData.FiringRange;

        int range = enemyData.FiringRange;
        if (enemyData.BehaviorType == EnemyBehaviorType.Melee)
        {
            range = 0;
            if (queuedAttackActions.Count > 0)
            {
                foreach (EnemyActionData action in queuedAttackActions)
                    if (TryGetAttackData(action, out EnemyAttackData attack)) range = Mathf.Max(range, attack.Range);
            }
            else if (TryGetAttackData(GetAvailableAction(EnemyActionType.MeleeAttack, 0, true), out EnemyAttackData attack))
                range = attack.Range;
        }
        return (enemyData.BehaviorType == EnemyBehaviorType.Melee || enemyData.BehaviorType == EnemyBehaviorType.Gunner)
            && CanPrepareFrontlineAttack()
            && TryGetDirectAttackTarget(range, out _, out bool targetsPlayer, out _) && targetsPlayer;
    }

    private TurnIntent BuildMoveIntent(int direction, int steps)
    {
        return direction != 0 && steps > 0 && TryBuildMovePath(direction, steps, out Vector3[] path)
            ? new TurnIntent(EnemyTurnActionType.Move, direction, path, currentLaneIndex)
            : new TurnIntent(EnemyTurnActionType.Wait);
    }

    private TurnIntent BuildRetreatIntent(int direction, int distance)
    {
        int steps = FindAction(EnemyActionType.Retreat)?.MovementDistance ?? 1;
        return BuildMoveIntent(-direction, Mathf.Min(steps, enemyData.PreferredDistance - distance));
    }

    private bool TryGetLaneMovePath(out Vector3[] path, out int lane)
    {
        path = null;
        lane = currentLaneIndex;
        if (!boardManager.TryGetTileIndex(transform.position, currentLaneIndex, out int tile)
            || !boardManager.TryGetTilePosition(tile, currentLaneIndex, out Vector3 origin)
            || !boardManager.TryGetAdjacentLanePosition(tile, currentLaneIndex,
                playerMove.CurrentLaneIndex > currentLaneIndex ? 1 : -1, out lane, out Vector3 target)
            || !boardManager.TryGetTileIndex(target, lane, out int destination)
            || lane == playerMove.CurrentLaneIndex
                && boardManager.TryGetTileIndex(playerMove.transform.position, lane, out int playerTile)
                && destination == playerTile
            || waveManager.IsTileOccupied(destination, lane, this)
            || waveManager.IsTileReservedForMovement(destination, lane, this)
            || waveManager.IsTileReservedForSpawn(destination, lane)) return false;
        path = new[] { target + transform.position - origin };
        return true;
    }

    private void ExecuteIntent(TurnIntent intent)
    {
        if (intent.Action == EnemyTurnActionType.PrepareAttack)
        {
            EnemyActionType type = IsBoss
                ? IsShotgunIntent() ? EnemyActionType.ShotgunAttack : EnemyActionType.ExplosiveThrow
                : enemyData.BehaviorType == EnemyBehaviorType.Melee
                    ? EnemyActionType.MeleeAttack : EnemyActionType.RangedAttack;
            if (queuedAttackActions.Count == 0 && !TryAppendAction(type, 0, out _, out _))
            {
                CompleteAction(EnemyTurnActionType.Wait);
                return;
            }
            isQueueCreated = true;
            PrepareCurrentAttackQueue();
            return;
        }
        if (intent.Action == EnemyTurnActionType.Move)
        {
            // Occupancy can change after advertising the path. Never find a
            // replacement route at execution, and never overlap another actor.
            var availablePath = new List<Vector3>();
            foreach (Vector3 position in intent.Path)
            {
                if (!boardManager.TryGetTileIndex(position, intent.Lane, out int tile)
                    || waveManager.IsTileOccupied(tile, intent.Lane, this)
                    || waveManager.IsTileReservedForMovement(tile, intent.Lane, this)
                    || waveManager.IsTileReservedForSpawn(tile, intent.Lane)
                    || playerMove.CurrentLaneIndex == intent.Lane
                        && boardManager.TryGetTileIndex(playerMove.transform.position, intent.Lane, out int playerTile)
                        && playerTile == tile) break;
                availablePath.Add(position);
            }
            StartCoroutine(MoveRoutine(availablePath.ToArray(), isRetreating, intent.Lane));
            return;
        }
        if (intent.Action == EnemyTurnActionType.Rotate)
        {
            RotateToward(intent.Direction);
            return;
        }
        if (intent.Action == EnemyTurnActionType.Fire || intent.Action == EnemyTurnActionType.Support)
        {
            if (enemyData.BehaviorType == EnemyBehaviorType.Thrower && preparedTargetTileIndex < 0)
            {
                CompleteAction(EnemyTurnActionType.Wait);
                return;
            }
            if (intent.Action == EnemyTurnActionType.Support)
            {
                preparedSupportTarget = intent.Support;
                preparedSupportType = intent.SupportType;
            }
            if (queuedAttackActions.Count == 0)
            {
                EnemyActionType type = intent.Action == EnemyTurnActionType.Support
                    ? EnemyActionType.Support : enemyData.BehaviorType == EnemyBehaviorType.Melee
                    ? EnemyActionType.MeleeAttack : EnemyActionType.RangedAttack;
                isQueueCreated = true;
                if (!TryAppendAction(type, 0, out _, out _))
                {
                    CompleteAction(EnemyTurnActionType.Wait);
                    return;
                }
            }
            isAttackPrepared = false;
            StartCoroutine(FireAttackQueue());
            return;
        }
        CompleteAction(EnemyTurnActionType.Wait);
    }

    private void ExecuteBossIntent(TurnIntent intent)
    {
        if (intent.Action != EnemyTurnActionType.Fire)
        {
            ExecuteIntent(intent);
            return;
        }
        bool shotgun = IsShotgunIntent();
        TryEnterBigBarrelPhaseTwo();
        if (bigBarrelStep != BigBarrelStep.ExecuteBomb || preparedBombTargetTileIndices.Count == 0)
            bigBarrelActionUsesPhaseTwo = isBigBarrelPhaseTwo;
        if (queuedAttackActions.Count == 0)
        {
            isQueueCreated = true;
            if (!TryAppendAction(shotgun ? EnemyActionType.ShotgunAttack
                : EnemyActionType.ExplosiveThrow, 0, out _, out _))
            {
                CompleteAction(EnemyTurnActionType.Wait);
                return;
            }
        }
        if (shotgun)
        {
            bigBarrelStep = BigBarrelStep.ExecuteShotgun;
            isAttackPrepared = false;
            StartCoroutine(FireBigBarrelShotgun());
        }
        else
        {
            // A restored committed bomb attack retains its authored targets.
            if (!hasCommittedIntent && (bigBarrelStep != BigBarrelStep.ExecuteBomb || preparedBombTargetTileIndices.Count == 0))
            {
                CaptureBigBarrelBombTargets();
                preparedBigBarrelFuse = bigBarrelActionUsesPhaseTwo
                    ? enemyData.BigBarrel.PhaseTwoBombFuseTurns : enemyData.BigBarrel.BombFuseTurns;
            }
            bigBarrelStep = BigBarrelStep.ExecuteBomb;
            isAttackPrepared = false;
            StartCoroutine(FireBigBarrelBombs());
        }
    }
}
