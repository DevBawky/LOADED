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
            boardManager.TryGetTileIndex(transform.position, out int tileIndex);
            RunEnemySaveData state = new RunEnemySaveData
            {
                enemyAssetName = enemyData == null ? string.Empty : enemyData.name,
                tileIndex = tileIndex,
                facingRight = transform.localScale.x >= 0f,
                currentHealth = currentHealth,
                currentShield = currentShield,
                remainingSupportCharges = remainingSupportCharges,
                recoveryTurnsRemaining = recoveryTurnsRemaining,
                isQueueCreated = isQueueCreated,
                isAttackPrepared = isAttackPrepared,
                isRetreating = isRetreating,
                preparedTargetTileIndex = preparedTargetTileIndex,
                preparedSupportType = (int)preparedSupportType,
                lastTurnAction = (int)lastTurnAction,
                bigBarrelStep = (int)bigBarrelStep,
                isBigBarrelPhaseTwo = isBigBarrelPhaseTwo,
                bigBarrelActionUsesPhaseTwo = bigBarrelActionUsesPhaseTwo,
                preparedBigBarrelFuse = preparedBigBarrelFuse,
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
            preparedTargetTileIndex = state.preparedTargetTileIndex;
            preparedTargetPosition = boardManager != null
                && boardManager.TryGetTilePosition(
                    preparedTargetTileIndex,
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
            bigBarrelReloadTurnsRemaining = Mathf.Max(
                0,
                state.bigBarrelReloadTurnsRemaining);
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
            RefreshAttackTelegraph();
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
