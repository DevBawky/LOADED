using System.Collections.Generic;
using UnityEngine;

public partial class EnemyController
{
    private sealed class EnemyTelegraphPresenter
    {
        private readonly EnemyController owner;
        private readonly List<LineRenderer> bigBarrelTelegraphLines =
            new List<LineRenderer>();
        private LineRenderer attackTelegraphLine;
        private LineRenderer shieldIndicatorLine;
        private MaterialPropertyBlock lineColorProperties;
        private Vector3 bigBarrelTelegraphAnchorPosition;

        private EnemyData enemyData => owner.enemyData;
        private BoardManager boardManager => owner.boardManager;
        private PlayerMove playerMove => owner.playerMove;
        private Transform transform => owner.transform;
        private SpriteRenderer avatarSortingRenderer =>
            owner.avatarSortingRenderer;
        private bool isAttackPrepared => owner.isAttackPrepared;
        private int preparedTargetTileIndex =>
            owner.preparedTargetTileIndex;
        private Vector3 preparedTargetPosition =>
            owner.preparedTargetPosition;
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
            if (!isAttackPrepared || enemyData == null
                || enemyData.BehaviorType == EnemyBehaviorType.Melee)
            {
                HideAttackTelegraph();
                return;
            }
    
            if (enemyData.BehaviorType == EnemyBehaviorType.BigBarrel)
            {
                if (bigBarrelStep == BigBarrelStep.ExecuteShotgun)
                {
                    CreateBigBarrelShotgunTelegraphs();
                }
    
                return;
            }
    
            Material telegraphMaterial = enemyData.BehaviorType switch
            {
                EnemyBehaviorType.Thrower =>
                    enemyData.ThrowerTelegraphMaterial,
                EnemyBehaviorType.Porter =>
                    enemyData.SupportTelegraphMaterial,
                _ => enemyData.GunnerTelegraphMaterial
            };
    
            if (telegraphMaterial == null)
            {
                HideAttackTelegraph();
                return;
            }
    
            LineRenderer lineRenderer = GetOrCreateAttackTelegraphLine();
            lineRenderer.sharedMaterial = telegraphMaterial;
            lineRenderer.widthMultiplier = enemyData.TelegraphLineWidth;
            lineRenderer.sortingOrder = enemyData.TelegraphSortingOrder;
            Color telegraphColor = enemyData.BehaviorType
                == EnemyBehaviorType.Porter
                    ? preparedSupportType == EnemySupportType.Heal
                        ? enemyData.SupportHealColor
                        : enemyData.SupportShieldColor
                    : Color.white;
            lineRenderer.startColor = telegraphColor;
            lineRenderer.endColor = telegraphColor;
    
            if (enemyData.BehaviorType == EnemyBehaviorType.Porter)
            {
                ApplyLineShaderColor(lineRenderer, telegraphColor);
            }
    
            if (avatarSortingRenderer != null)
            {
                lineRenderer.sortingLayerID =
                    avatarSortingRenderer.sortingLayerID;
            }
    
            bool positionsApplied = enemyData.BehaviorType switch
            {
                EnemyBehaviorType.Thrower =>
                    ApplyThrowerTelegraphPositions(lineRenderer),
                EnemyBehaviorType.Porter =>
                    ApplySupportTelegraphPositions(lineRenderer),
                _ => ApplyGunnerTelegraphPositions(lineRenderer)
            };
            lineRenderer.enabled = positionsApplied;
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
    
        private bool ApplyGunnerTelegraphPositions(LineRenderer lineRenderer)
        {
            if (boardManager == null
                || !boardManager.TryGetTileIndex(
                    transform.position,
                    out int attackerTileIndex))
            {
                return false;
            }
    
            int attackDirection = transform.localScale.x >= 0f ? 1 : -1;
            int endTileIndex = Mathf.Clamp(
                attackerTileIndex + attackDirection * enemyData.FiringRange,
                0,
                boardManager.BoardCount - 1);
    
            if (endTileIndex == attackerTileIndex
                || !boardManager.TryGetTilePosition(
                    endTileIndex,
                    out Vector3 endPosition))
            {
                return false;
            }
    
            Vector3 startPosition = transform.position;
            float verticalOffset = enemyData.TelegraphVerticalOffset;
            startPosition.y += verticalOffset;
            endPosition.y = startPosition.y;
            endPosition.z = startPosition.z;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, endPosition);
            return true;
        }
    
        private bool ApplyThrowerTelegraphPositions(LineRenderer lineRenderer)
        {
            if (preparedTargetTileIndex < 0)
            {
                return false;
            }
    
            int segmentCount = enemyData.ThrowerTelegraphSegments;
            Vector3 startPosition = transform.position;
            Vector3 targetPosition = preparedTargetPosition;
            float verticalOffset = enemyData.TelegraphVerticalOffset;
            startPosition.y += verticalOffset;
            targetPosition.y += verticalOffset;
            targetPosition.z = startPosition.z;
            lineRenderer.positionCount = segmentCount;
    
            for (int segmentIndex = 0;
                 segmentIndex < segmentCount;
                 segmentIndex++)
            {
                float progress = segmentCount <= 1
                    ? 1f
                    : (float)segmentIndex / (segmentCount - 1);
                Vector3 position = Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    progress);
                position += Vector3.up * (Mathf.Sin(progress * Mathf.PI)
                    * enemyData.ThrownProjectileArcHeight);
                lineRenderer.SetPosition(segmentIndex, position);
            }
    
            return true;
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
            if (attackTelegraphLine != null)
            {
                attackTelegraphLine.enabled = false;
            }
    
            foreach (LineRenderer line in bigBarrelTelegraphLines)
            {
                if (line != null)
                {
                    line.gameObject.SetActive(false);
                    Destroy(line.gameObject);
                }
            }
    
            bigBarrelTelegraphLines.Clear();
        }
    
        public void CreateBigBarrelShotgunTelegraphs()
        {
            ClearBigBarrelTelegraphsOnly();
            Color color = new Color(1f, 0.08f, 0.04f, 0.78f);
    
            foreach (int tileIndex in preparedShotgunTileIndices)
            {
                LineRenderer line = BoardTelegraphUtility.CreateTileRange(
                    transform,
                    "Line | Shotgun Target",
                    boardManager,
                    tileIndex,
                    tileIndex,
                    enemyData.BigBarrel.ShotgunTelegraphMaterial,
                    color,
                    enemyData.TelegraphVerticalOffset * 0.5f,
                    enemyData.TelegraphSortingOrder);
    
                if (line != null)
                {
                    bigBarrelTelegraphLines.Add(line);
                }
            }
    
            bigBarrelTelegraphAnchorPosition = transform.position;
        }
    
        public void MoveBigBarrelTelegraphsWithBoss()
        {
            if (bigBarrelStep != BigBarrelStep.ExecuteShotgun
                || bigBarrelTelegraphLines.Count == 0)
            {
                bigBarrelTelegraphAnchorPosition = transform.position;
                return;
            }
    
            Vector3 movement = transform.position
                - bigBarrelTelegraphAnchorPosition;
    
            if (movement.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }
    
            foreach (LineRenderer line in bigBarrelTelegraphLines)
            {
                if (line == null)
                {
                    continue;
                }
    
                for (int positionIndex = 0;
                     positionIndex < line.positionCount;
                     positionIndex++)
                {
                    line.SetPosition(
                        positionIndex,
                        line.GetPosition(positionIndex) + movement);
                }
            }
    
            bigBarrelTelegraphAnchorPosition = transform.position;
        }
    
        public void ClearBigBarrelTelegraphsOnly()
        {
            foreach (LineRenderer line in bigBarrelTelegraphLines)
            {
                if (line != null)
                {
                    line.gameObject.SetActive(false);
                    Destroy(line.gameObject);
                }
            }
    
            bigBarrelTelegraphLines.Clear();
            bigBarrelTelegraphAnchorPosition = transform.position;
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
