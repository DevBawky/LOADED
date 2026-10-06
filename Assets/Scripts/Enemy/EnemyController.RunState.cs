using System;
using System.Collections.Generic;
using UnityEngine;

public partial class EnemyController
{
    private sealed class EnemyRunStateSerializer
    {
        private readonly EnemyController owner;

        private EnemyData enemyData => owner.enemyData;
        private BoardManager boardManager => owner.boardManager;
        private Transform transform => owner.transform;
        private StatusEffectController statusEffects => owner.statusEffects;
        private EnemyActionQueueUI actionQueueUI => owner.actionQueueUI;
        private int MaxHealth => owner.MaxHealth;
        private List<EnemyActionData> queuedAttackActions =>
            owner.queuedAttackActions;
        private List<int> preparedBombTargetTileIndices =>
            owner.preparedBombTargetTileIndices;
        private List<int> preparedShotgunTileIndices =>
            owner.preparedShotgunTileIndices;

        private int currentHealth
        {
            get => owner.currentHealth;
            set => owner.currentHealth = value;
        }

        private int currentShield
        {
            get => owner.currentShield;
            set => owner.currentShield = value;
        }

        private int remainingSupportCharges
        {
            get => owner.remainingSupportCharges;
            set => owner.remainingSupportCharges = value;
        }

        private int recoveryTurnsRemaining
        {
            get => owner.recoveryTurnsRemaining;
            set => owner.recoveryTurnsRemaining = value;
        }

        private bool isQueueCreated
        {
            get => owner.isQueueCreated;
            set => owner.isQueueCreated = value;
        }

        private bool isAttackPrepared
        {
            get => owner.isAttackPrepared;
            set => owner.isAttackPrepared = value;
        }

        private bool isRetreating
        {
            get => owner.isRetreating;
            set => owner.isRetreating = value;
        }

        private int preparedTargetTileIndex
        {
            get => owner.preparedTargetTileIndex;
            set => owner.preparedTargetTileIndex = value;
        }

        private int currentLaneIndex
        {
            get => owner.currentLaneIndex;
            set => owner.currentLaneIndex = value;
        }

        private int preparedTargetLaneIndex
        {
            get => owner.preparedTargetLaneIndex;
            set => owner.preparedTargetLaneIndex = value;
        }

        private int preparedBigBarrelLaneIndex
        {
            get => owner.preparedBigBarrelLaneIndex;
            set => owner.preparedBigBarrelLaneIndex = value;
        }

        private Vector3 preparedTargetPosition
        {
            get => owner.preparedTargetPosition;
            set => owner.preparedTargetPosition = value;
        }

        private EnemyController preparedSupportTarget
        {
            get => owner.preparedSupportTarget;
            set => owner.preparedSupportTarget = value;
        }

        private EnemySupportType preparedSupportType
        {
            get => owner.preparedSupportType;
            set => owner.preparedSupportType = value;
        }

        private EnemyTurnActionType lastTurnAction
        {
            get => owner.lastTurnAction;
            set => owner.lastTurnAction = value;
        }

        private bool isActing
        {
            get => owner.isActing;
            set => owner.isActing = value;
        }

        private BigBarrelStep bigBarrelStep
        {
            get => owner.bigBarrelStep;
            set => owner.bigBarrelStep = value;
        }

        private bool isBigBarrelPhaseTwo
        {
            get => owner.isBigBarrelPhaseTwo;
            set => owner.isBigBarrelPhaseTwo = value;
        }

        private bool bigBarrelActionUsesPhaseTwo
        {
            get => owner.bigBarrelActionUsesPhaseTwo;
            set => owner.bigBarrelActionUsesPhaseTwo = value;
        }

        private int preparedBigBarrelFuse
        {
            get => owner.preparedBigBarrelFuse;
            set => owner.preparedBigBarrelFuse = value;
        }

        private int bigBarrelReloadTurnsRemaining
        {
            get => owner.bigBarrelReloadTurnsRemaining;
            set => owner.bigBarrelReloadTurnsRemaining = value;
        }

        public EnemyRunStateSerializer(EnemyController owner)
        {
            this.owner = owner;
        }

        public RunEnemySaveData Capture(
            IReadOnlyList<EnemyController> allEnemies)
        {
            boardManager.TryGetTileIndex(transform.position, currentLaneIndex, out int tileIndex);
            RunEnemySaveData state = new RunEnemySaveData
            {
                enemyAssetName = enemyData == null ? string.Empty : enemyData.name,
                tileIndex = tileIndex,
                laneIndex = currentLaneIndex,
                facingRight = transform.localScale.x >= 0f,
                currentHealth = currentHealth,
                currentShield = currentShield,
                remainingSupportCharges = remainingSupportCharges,
                recoveryTurnsRemaining = recoveryTurnsRemaining,
                preparationWaitTurns = owner.preparationWaitTurns,
                preparationDeferred = owner.preparationDeferred,
                isQueueCreated = isQueueCreated,
                isAttackPrepared = isAttackPrepared,
                isRetreating = isRetreating,
                preparedTargetTileIndex = preparedTargetTileIndex,
                preparedTargetLaneIndex = preparedTargetLaneIndex,
                preparedSupportType = (int)preparedSupportType,
                lastTurnAction = (int)lastTurnAction,
                nextIntent = CaptureIntent(),
                bigBarrelStep = (int)bigBarrelStep,
                isBigBarrelPhaseTwo = isBigBarrelPhaseTwo,
                bigBarrelActionUsesPhaseTwo = bigBarrelActionUsesPhaseTwo,
                preparedBigBarrelFuse = preparedBigBarrelFuse,
                preparedBigBarrelLaneIndex = preparedBigBarrelLaneIndex,
                bigBarrelReloadTurnsRemaining = bigBarrelReloadTurnsRemaining,
                statusEffects = statusEffects == null
                    ? new RunStatusEffectSaveData()
                    : statusEffects.CaptureRunState()
            };
    
            foreach (EnemyActionData action in queuedAttackActions)
            {
                state.queuedActionAssetNames.Add(
                    action == null ? string.Empty : action.name);
            }
    
            state.preparedBombTargetTileIndices.AddRange(
                preparedBombTargetTileIndices);
            state.preparedShotgunTileIndices.AddRange(
                preparedShotgunTileIndices);
    
            if (preparedSupportTarget != null && allEnemies != null)
            {
                for (int index = 0; index < allEnemies.Count; index++)
                {
                    if (allEnemies[index] == preparedSupportTarget)
                    {
                        state.preparedSupportTargetIndex = index;
                        break;
                    }
                }
            }
    
            return state;
        }
    
        public void Restore(
            RunEnemySaveData state,
            EnemyController restoredSupportTarget)
        {
            if (state == null || enemyData == null)
            {
                return;
            }
    
            currentHealth = Mathf.Clamp(state.currentHealth, 1, MaxHealth);
            currentShield = Mathf.Max(0, state.currentShield);
            remainingSupportCharges = Mathf.Max(
                0,
                state.remainingSupportCharges);
            recoveryTurnsRemaining = Mathf.Max(
                0,
                state.recoveryTurnsRemaining);
            queuedAttackActions.Clear();
    
            if (state.queuedActionAssetNames != null)
            {
                foreach (string actionAssetName in state.queuedActionAssetNames)
                {
                    EnemyActionData action = ResolveSavedAction(actionAssetName);
    
                    if (action != null)
                    {
                        queuedAttackActions.Add(action);
                    }
                }
            }
    
            isQueueCreated = state.isQueueCreated;
            isAttackPrepared = state.isAttackPrepared;
            isRetreating = state.isRetreating;
            currentLaneIndex = boardManager == null
                ? Mathf.Max(0, state.laneIndex)
                : Mathf.Clamp(
                    state.laneIndex,
                    0,
                    boardManager.LaneCount - 1);
            preparedTargetTileIndex = state.preparedTargetTileIndex;
            preparedTargetLaneIndex = boardManager == null
                ? Mathf.Max(0, state.preparedTargetLaneIndex)
                : Mathf.Clamp(
                    state.preparedTargetLaneIndex,
                    0,
                    boardManager.LaneCount - 1);
            preparedTargetPosition = boardManager != null
                && boardManager.TryGetTilePosition(
                    preparedTargetTileIndex,
                    preparedTargetLaneIndex,
                    out Vector3 targetPosition)
                        ? targetPosition
                        : Vector3.zero;
            preparedSupportTarget = restoredSupportTarget;
            preparedSupportType = Enum.IsDefined(
                typeof(EnemySupportType),
                state.preparedSupportType)
                    ? (EnemySupportType)state.preparedSupportType
                    : EnemySupportType.None;
            lastTurnAction = Enum.IsDefined(
                typeof(EnemyTurnActionType),
                state.lastTurnAction)
                    ? (EnemyTurnActionType)state.lastTurnAction
                    : EnemyTurnActionType.None;
            bigBarrelStep = Enum.IsDefined(
                typeof(BigBarrelStep),
                state.bigBarrelStep)
                    ? (BigBarrelStep)state.bigBarrelStep
                    : BigBarrelStep.RotateToPlayer;
            isBigBarrelPhaseTwo = state.isBigBarrelPhaseTwo;
            bigBarrelActionUsesPhaseTwo = state.bigBarrelActionUsesPhaseTwo;
            preparedBigBarrelFuse = Mathf.Max(0, state.preparedBigBarrelFuse);
            preparedBigBarrelLaneIndex = boardManager == null
                ? Mathf.Max(0, state.preparedBigBarrelLaneIndex)
                : Mathf.Clamp(
                    state.preparedBigBarrelLaneIndex,
                    0,
                    boardManager.LaneCount - 1);
            bigBarrelReloadTurnsRemaining = Mathf.Max(
                0,
                state.bigBarrelReloadTurnsRemaining);
            // Fold legacy boss recovery into the shared counter without
            // adding a second cooldown or changing the saved schema.
            if (owner.IsBoss && bigBarrelStep == BigBarrelStep.Reload)
            {
                recoveryTurnsRemaining = Mathf.Max(recoveryTurnsRemaining, bigBarrelReloadTurnsRemaining);
                bigBarrelStep = BigBarrelStep.CreateBombQueue;
            }
            bigBarrelReloadTurnsRemaining = 0;
            if (recoveryTurnsRemaining > 0) isAttackPrepared = false;
            preparedBombTargetTileIndices.Clear();
            preparedShotgunTileIndices.Clear();
    
            if (state.preparedBombTargetTileIndices != null)
            {
                preparedBombTargetTileIndices.AddRange(
                    state.preparedBombTargetTileIndices);
            }
    
            if (state.preparedShotgunTileIndices != null)
            {
                preparedShotgunTileIndices.AddRange(
                    state.preparedShotgunTileIndices);
            }
    
            isActing = false;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Max(0.0001f, Mathf.Abs(scale.x))
                * (state.facingRight ? 1f : -1f);
            transform.localScale = scale;
            statusEffects?.RestoreRunState(state.statusEffects);
            actionQueueUI.ResetDisplay();
    
            foreach (EnemyActionData action in queuedAttackActions)
            {
                actionQueueUI.AddAttackIcon(action);
            }
    
            if (isQueueCreated && queuedAttackActions.Count == 0)
            {
                actionQueueUI.ShowQueue();
            }
    
            actionQueueUI.SetPrepared(isAttackPrepared);
            RefreshGunnerReloadedAnimation();
            RefreshShieldIndicator();
            RefreshHealthUI();
            ApplyCanvasOrientation();
            owner.ApplyLaneSortingOrder();
            RestoreIntent(state.nextIntent);
            owner.preparationWaitTurns = Mathf.Max(0, state.preparationWaitTurns);
            owner.preparationDeferred = state.preparationDeferred && owner.hasCommittedIntent
                && (owner.committedIntent.Action == EnemyTurnActionType.Wait
                    || owner.committedIntent.Action == EnemyTurnActionType.Move
                    || owner.committedIntent.Action == EnemyTurnActionType.Rotate);
            if (owner.hasCommittedIntent && owner.committedIntent.Action != EnemyTurnActionType.Fire)
                isAttackPrepared = false;
            actionQueueUI.SetPrepared(isAttackPrepared);
            RefreshAttackTelegraph();
            // A saved intent owns its warning, including a cancelled/Wait plan.
            // Legacy preparation must not reactivate a tile behind that plan.
            if (!owner.hasCommittedIntent) owner.SetPreparedTargetWarning(isAttackPrepared);
        }
    
        private RunEnemyIntentSaveData CaptureIntent()
        {
            if (!owner.hasCommittedIntent || owner.isActing) return null;
            TurnIntent intent = owner.GetNextTurnIntent();
            var saved = new RunEnemyIntentSaveData
            {
                committed = true,
                action = (int)intent.Action,
                direction = intent.Direction,
                lane = intent.Lane
            };
            if (intent.Path != null)
                foreach (Vector3 point in intent.Path)
                    if (boardManager.TryGetTileIndex(point, intent.Lane, out int tile)) saved.pathTiles.Add(tile);
            if (intent.Action == EnemyTurnActionType.Fire || intent.Action == EnemyTurnActionType.PrepareAttack)
                foreach (Vector2Int cell in owner.committedAttackCells)
                {
                    saved.attackTiles.Add(cell.x);
                    saved.attackLanes.Add(cell.y);
                }
            return saved;
        }

        private void RestoreIntent(RunEnemyIntentSaveData saved)
        {
            owner.ClearTurnIntent();
            if (saved == null || !saved.committed || boardManager == null) return;
            owner.hasCommittedIntent = true;
            owner.committedOrigin = transform.position;
            owner.committedLane = currentLaneIndex;
            owner.committedIntent = new TurnIntent(EnemyTurnActionType.Wait);
            EnemyTurnActionType action = (EnemyTurnActionType)saved.action;
            if (recoveryTurnsRemaining > 0 && (action == EnemyTurnActionType.Fire
                || action == EnemyTurnActionType.PrepareAttack)) return;
            if (action != EnemyTurnActionType.Wait && action != EnemyTurnActionType.Move
                && action != EnemyTurnActionType.Rotate && action != EnemyTurnActionType.Fire
                && action != EnemyTurnActionType.Support && action != EnemyTurnActionType.PrepareAttack) return;
            if (saved.lane < 0 || saved.lane >= boardManager.LaneCount) return;
            var path = new List<Vector3>();
            boardManager.TryGetTileIndex(transform.position, currentLaneIndex, out int origin);
            boardManager.TryGetTilePosition(origin, currentLaneIndex, out Vector3 floor);
            Vector3 offset = transform.position - floor;
            if (saved.pathTiles != null)
                foreach (int tile in saved.pathTiles)
                {
                    if (!boardManager.TryGetTilePosition(tile, saved.lane, out Vector3 point)) return;
                    path.Add(point + offset);
                }
            if (action == EnemyTurnActionType.Move && path.Count == 0) return;
            if (action == EnemyTurnActionType.Move)
            {
                if (saved.lane != currentLaneIndex)
                {
                    if (path.Count != 1 || Mathf.Abs(saved.lane - currentLaneIndex) != 1
                        || !boardManager.TryGetAdjacentLanePosition(origin, currentLaneIndex,
                            saved.lane - currentLaneIndex, out _, out Vector3 adjacent)
                        || (path[0] - offset - adjacent).sqrMagnitude > 0.001f) return;
                }
                else
                {
                    int previous = origin;
                    int direction = saved.pathTiles[0] > origin ? 1 : -1;
                    foreach (int tile in saved.pathTiles)
                    {
                        if (tile - previous != direction) return;
                        previous = tile;
                    }
                }
            }
            if (action == EnemyTurnActionType.Rotate && Mathf.Abs(saved.direction) != 1) return;
            if (saved.attackTiles == null || saved.attackLanes == null
                || saved.attackTiles.Count != saved.attackLanes.Count) return;
            var cells = new List<Vector2Int>();
            for (int index = 0; index < saved.attackTiles.Count; index++)
            {
                int tile = saved.attackTiles[index], lane = saved.attackLanes[index];
                if (!boardManager.TryGetTilePosition(tile, lane, out _)) return;
                cells.Add(new Vector2Int(tile, lane));
            }
            owner.committedAttackCells.UnionWith(cells);
            // Pre-preparation saves advertised Fire without a ready state.
            // Preserve their footprint, but grant the new preparation turn.
            if (action == EnemyTurnActionType.Fire && !isAttackPrepared)
                action = EnemyTurnActionType.PrepareAttack;
            if (action != EnemyTurnActionType.Fire) isAttackPrepared = false;
            owner.committedIntent = new TurnIntent(action, saved.direction, path.ToArray(),
                saved.lane, preparedSupportTarget, preparedSupportType);
        }

        private EnemyActionData ResolveSavedAction(string assetName)
        {
            if (enemyData == null || string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }
    
            foreach (EnemyActionData action in enemyData.Actions)
            {
                if (action != null && string.Equals(
                        action.name,
                        assetName,
                        StringComparison.Ordinal))
                {
                    return action;
                }
            }
    
            return null;
        }
    
        private void RefreshGunnerReloadedAnimation()
        {
            owner.RefreshGunnerReloadedAnimation();
        }

        private void RefreshShieldIndicator()
        {
            owner.RefreshShieldIndicator();
        }

        private void RefreshHealthUI()
        {
            owner.RefreshHealthUI();
        }

        private void ApplyCanvasOrientation()
        {
            owner.ApplyCanvasOrientation();
        }

        private void RefreshAttackTelegraph()
        {
            owner.RefreshAttackTelegraph();
        }
    }
}
