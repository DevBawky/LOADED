using System.Collections.Generic;
using UnityEngine;

public partial class EnemyController
{
    private sealed class EnemyTelegraphPresenter
    {
        private readonly EnemyController owner;
        private readonly HashSet<Vector2Int> warningCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> nextWarningCells = new HashSet<Vector2Int>();
        private LineRenderer attackTelegraphLine;
        private LineRenderer shieldIndicatorLine;
        private MaterialPropertyBlock lineColorProperties;
        private bool isExecutingDirectAttack;
        private EnemyAttackData executingAttackData;

        private EnemyData enemyData => owner.enemyData;
        private BoardManager boardManager => owner.boardManager;
        private Transform transform => owner.transform;
        private SpriteRenderer avatarSortingRenderer =>
            owner.avatarSortingRenderer;
        private bool isAttackPrepared => owner.isAttackPrepared;
        private int preparedTargetTileIndex =>
            owner.preparedTargetTileIndex;
        private int preparedTargetLaneIndex =>
            owner.preparedTargetLaneIndex;
        private int currentLaneIndex => owner.currentLaneIndex;
        private int preparedBigBarrelLaneIndex =>
            owner.preparedBigBarrelLaneIndex;
        private List<int> preparedShotgunTileIndices =>
            owner.preparedShotgunTileIndices;
        private int currentShield => owner.currentShield;
        private BigBarrelStep bigBarrelStep => owner.bigBarrelStep;
        private EnemySupportType preparedSupportType =>
            owner.preparedSupportType;
        private EnemyController preparedSupportTarget =>
            owner.preparedSupportTarget;

        public EnemyTelegraphPresenter(EnemyController owner)
        {
            this.owner = owner;
            lineColorProperties = new MaterialPropertyBlock();
        }

        public void HideShieldIndicator()
        {
            if (shieldIndicatorLine != null)
            {
                shieldIndicatorLine.enabled = false;
            }
        }

        public void RefreshAttackTelegraph()
        {
            if (!isAttackPrepared || enemyData == null)
            {
                HideAttackTelegraph();
                return;
            }
    
            if (enemyData.BehaviorType != EnemyBehaviorType.Porter)
            {
                RefreshDamageTileWarnings();
                return;
            }

            Material telegraphMaterial = enemyData.SupportTelegraphMaterial;

            if (telegraphMaterial == null)
            {
                HideAttackTelegraph();
                return;
            }
    
            LineRenderer lineRenderer = GetOrCreateAttackTelegraphLine();
            lineRenderer.sharedMaterial = telegraphMaterial;
            lineRenderer.widthMultiplier = enemyData.TelegraphLineWidth;
            lineRenderer.sortingOrder = enemyData.TelegraphSortingOrder;
            Color telegraphColor = preparedSupportType == EnemySupportType.Heal
                ? enemyData.SupportHealColor
                : enemyData.SupportShieldColor;
            lineRenderer.startColor = telegraphColor;
            lineRenderer.endColor = telegraphColor;
            ApplyLineShaderColor(lineRenderer, telegraphColor);

            if (avatarSortingRenderer != null)
            {
                lineRenderer.sortingLayerID =
                    avatarSortingRenderer.sortingLayerID;
            }
    
            lineRenderer.enabled = ApplySupportTelegraphPositions(lineRenderer);
        }

        public void BeginAttack(EnemyAttackData executingAttack = null)
        {
            if (enemyData == null || enemyData.BehaviorType == EnemyBehaviorType.Porter)
            {
                return;
            }
            isExecutingDirectAttack = enemyData.BehaviorType == EnemyBehaviorType.Melee
                || enemyData.BehaviorType == EnemyBehaviorType.Gunner;
            executingAttackData = executingAttack;
            boardManager?.SetWarningUrgent(owner, false);
            RefreshDamageTileWarnings(executingAttack);
        }

        public bool RefreshExecutingAttack()
        {
            if (!isExecutingDirectAttack)
            {
                return false;
            }
            RefreshDamageTileWarnings(executingAttackData);
            return true;
        }

        public void MarkAttackImminent()
        {
            boardManager?.SetWarningUrgent(owner, true);
        }

        public void CompleteAttack()
        {
            isExecutingDirectAttack = false;
            executingAttackData = null;
            boardManager?.CompleteTileWarnings(owner);
            warningCells.Clear();
            nextWarningCells.Clear();
        }

        private void RefreshDamageTileWarnings(EnemyAttackData executingAttack = null)
        {
            if (attackTelegraphLine != null)
            {
                attackTelegraphLine.enabled = false;
            }

            nextWarningCells.Clear();
            if (boardManager != null)
            {
                switch (enemyData.BehaviorType)
                {
                    case EnemyBehaviorType.Thrower:
                        nextWarningCells.Add(new Vector2Int(
                            preparedTargetTileIndex, preparedTargetLaneIndex));
                        break;
                    case EnemyBehaviorType.BigBarrel:
                        if (bigBarrelStep == BigBarrelStep.ExecuteShotgun)
                        {
                            foreach (int tile in preparedShotgunTileIndices)
                            {
                                nextWarningCells.Add(new Vector2Int(
                                    tile, preparedBigBarrelLaneIndex));
                            }
                        }
                        break;
                    case EnemyBehaviorType.Melee:
                    case EnemyBehaviorType.Gunner:
                        if (boardManager.TryGetTileIndex(transform.position,
                                currentLaneIndex, out int attackerTile))
                        {
                            int direction = transform.localScale.x >= 0f ? 1 : -1;
                            int range = enemyData.FiringRange;
                            if (enemyData.BehaviorType == EnemyBehaviorType.Melee
                                && executingAttack != null)
                            {
                                range = executingAttack.Range;
                            }
                            else if (enemyData.BehaviorType == EnemyBehaviorType.Melee)
                            {
                                range = 0;
                                foreach (EnemyActionData action in owner.queuedAttackActions)
                                {
                                    if (owner.TryGetAttackData(action, out EnemyAttackData attack))
                                    {
                                        range = Mathf.Max(range, attack.Range);
                                    }
                                }
                            }
                            range = Mathf.Min(range, boardManager.BoardCount - 1);
                            // Use the same first-hit query as damage resolution, including friendly fire.
                            if (owner.TryGetDirectAttackTarget(range, out _, out _, out Vector3 hitPosition)
                                && boardManager.TryGetTileDistance(transform.position, hitPosition,
                                    out int hitDistance))
                            {
                                range = Mathf.Min(range, hitDistance);
                            }
                            for (int distance = 1; distance <= range; distance++)
                            {
                                int tile = attackerTile + direction * distance;
                                if (tile < 0 || tile >= boardManager.BoardCount)
                                {
                                    break;
                                }
                                nextWarningCells.Add(new Vector2Int(tile, currentLaneIndex));
                            }
                        }
                        break;
                }

                foreach (Vector2Int cell in warningCells)
                {
                    if (!nextWarningCells.Contains(cell))
                    {
                        boardManager.SetTileWarningActive(cell.x, cell.y, owner, false);
                    }
                }
                foreach (Vector2Int cell in nextWarningCells)
                {
                    boardManager.SetTileWarningActive(cell.x, cell.y, owner, true);
                }
            }
            warningCells.Clear();
            warningCells.UnionWith(nextWarningCells);
        }

        private LineRenderer GetOrCreateAttackTelegraphLine()
        {
            if (attackTelegraphLine != null)
            {
                return attackTelegraphLine;
            }
    
            GameObject telegraphObject = new GameObject(
                "Line | Attack Telegraph");
            telegraphObject.transform.SetParent(transform, false);
            attackTelegraphLine = telegraphObject.AddComponent<LineRenderer>();
            attackTelegraphLine.useWorldSpace = true;
            attackTelegraphLine.loop = false;
            attackTelegraphLine.alignment = LineAlignment.View;
            attackTelegraphLine.textureMode = LineTextureMode.Stretch;
            attackTelegraphLine.startColor = Color.white;
            attackTelegraphLine.endColor = Color.white;
            attackTelegraphLine.numCapVertices = 2;
            attackTelegraphLine.enabled = false;
            return attackTelegraphLine;
        }
    
        private bool ApplySupportTelegraphPositions(LineRenderer lineRenderer)
        {
            if (preparedSupportTarget == null
                || preparedSupportTarget.CurrentHealth <= 0)
            {
                return false;
            }
    
            Vector3 startPosition = transform.position;
            Vector3 endPosition = preparedSupportTarget.transform.position;
            float verticalOffset = enemyData.TelegraphVerticalOffset;
            startPosition.y += verticalOffset;
            endPosition.y += verticalOffset;
            endPosition.z = startPosition.z;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, endPosition);
            return true;
        }
    
        public void HideAttackTelegraph()
        {
            isExecutingDirectAttack = false;
            executingAttackData = null;
            if (attackTelegraphLine != null)
            {
                attackTelegraphLine.enabled = false;
            }
    
            boardManager?.ReleaseTileWarnings(owner);
            warningCells.Clear();
            nextWarningCells.Clear();
        }

        public void RefreshShieldIndicator(
            Material indicatorMaterial = null,
            Color? indicatorColor = null)
        {
            if (currentShield <= 0)
            {
                if (shieldIndicatorLine != null)
                {
                    shieldIndicatorLine.enabled = false;
                }
    
                return;
            }
    
            if (shieldIndicatorLine == null)
            {
                GameObject indicatorObject = new GameObject(
                    "Line | Shield Indicator");
                indicatorObject.transform.SetParent(transform, false);
                shieldIndicatorLine =
                    indicatorObject.AddComponent<LineRenderer>();
                shieldIndicatorLine.useWorldSpace = false;
                shieldIndicatorLine.loop = true;
                shieldIndicatorLine.alignment = LineAlignment.View;
                shieldIndicatorLine.textureMode = LineTextureMode.Stretch;
                shieldIndicatorLine.numCapVertices = 2;
                shieldIndicatorLine.positionCount = 24;
                shieldIndicatorLine.widthMultiplier = 0.06f;
                shieldIndicatorLine.sortingOrder = enemyData == null
                    ? 19
                    : enemyData.TelegraphSortingOrder - 1;
    
                if (avatarSortingRenderer != null)
                {
                    shieldIndicatorLine.sortingLayerID =
                        avatarSortingRenderer.sortingLayerID;
                }
    
                for (int pointIndex = 0;
                     pointIndex < shieldIndicatorLine.positionCount;
                     pointIndex++)
                {
                    float radians = pointIndex
                        / (float)shieldIndicatorLine.positionCount
                        * Mathf.PI
                        * 2f;
                    shieldIndicatorLine.SetPosition(
                        pointIndex,
                        new Vector3(
                            Mathf.Cos(radians) * 0.58f,
                            Mathf.Sin(radians) * 0.82f + 0.1f,
                            -0.05f));
                }
            }
    
            if (indicatorMaterial != null)
            {
                shieldIndicatorLine.sharedMaterial = indicatorMaterial;
            }
    
            Color color = indicatorColor
                ?? new Color(0.2f, 0.8f, 1f, 1f);
            shieldIndicatorLine.startColor = color;
            shieldIndicatorLine.endColor = color;
            ApplyLineShaderColor(shieldIndicatorLine, color);
            shieldIndicatorLine.enabled =
                shieldIndicatorLine.sharedMaterial != null;
        }
    
        private void ApplyLineShaderColor(
            LineRenderer lineRenderer,
            Color color)
        {
            if (lineRenderer == null)
            {
                return;
            }
    
            lineColorProperties ??= new MaterialPropertyBlock();
            lineColorProperties.Clear();
            Color baseColor = color;
            baseColor.a *= 0.55f;
            Color beamColor = color * 1.35f;
            beamColor.a = color.a;
            Color gridColor = color;
            gridColor.a *= 0.9f;
            lineColorProperties.SetColor(BaseColorId, baseColor);
            lineColorProperties.SetColor(BeamColorId, beamColor);
            lineColorProperties.SetColor(GridColorId, gridColor);
            lineRenderer.SetPropertyBlock(lineColorProperties);
        }
    
    }
}
