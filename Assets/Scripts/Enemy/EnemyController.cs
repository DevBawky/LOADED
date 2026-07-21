using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum EnemyTurnActionType
{
    None,
    Move,
    Rotate,
    Wait,
    Reload,
    Fire,
    CreateQueue,
    RegisterAttack,
    PrepareAttack
}

public class EnemyController : MonoBehaviour, IStatusEffectTarget
{
    private const int InitialFacingDirection = -1;

    [Header("Data")]
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Transform canvasTransform;
    [SerializeField] private ActorMotion actorMotion;
    [SerializeField] private EnemyActionQueueUI actionQueueUI;
    [SerializeField] private StatusEffectController statusEffects;
    [SerializeField] private EnemyDamageNumberDisplay damageNumberDisplay;

    [Header("Runtime State")]
    [SerializeField] private int currentHealth;
    [SerializeField] private List<EnemyActionData> queuedAttackActions =
        new List<EnemyActionData>();
    [SerializeField] private bool isQueueCreated;
    [SerializeField] private bool isAttackPrepared;
    [SerializeField] private bool isRetreating;
    [SerializeField] private int preparedTargetTileIndex = -1;
    [SerializeField] private Vector3 preparedTargetPosition;
    [SerializeField] private EnemyTurnActionType lastTurnAction;
    [SerializeField] private bool isActing;

    private BoardManager boardManager;
    private PlayerMove playerMove;
    private PlayerHealth playerHealth;
    private WaveManager waveManager;
    private bool isInitialized;
    private LineRenderer attackTelegraphLine;
    private readonly List<Vector3> movePath = new List<Vector3>();
    private readonly List<EnemyController> attackTargetBuffer =
        new List<EnemyController>();

    public event Action<EnemyController, EnemyTurnActionType> TurnActionCompleted;
    public event Action<EnemyController, EnemyAttackData> AttackExecuted;
    public event Action<EnemyController, int, int> HealthChanged;
    public event Action<EnemyController> Defeated;

    public EnemyData Data => enemyData;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => enemyData == null ? 0 : enemyData.MaxHealth;
    public EnemyActionData LoadedAttackAction => queuedAttackActions.Count > 0
        ? queuedAttackActions[0]
        : null;
    public IReadOnlyList<EnemyActionData> QueuedAttackActions =>
        queuedAttackActions;
    public bool IsQueueCreated => isQueueCreated;
    public bool IsAttackPrepared => isAttackPrepared;
    public bool IsRetreating => isRetreating;
    public int PreparedTargetTileIndex => preparedTargetTileIndex;
    public EnemyTurnActionType LastTurnAction => lastTurnAction;
    public bool IsActing => isActing;

    private void Awake()
    {
        if (statusEffects == null)
        {
            statusEffects = GetComponent<StatusEffectController>();
        }

        if (damageNumberDisplay == null)
        {
            damageNumberDisplay = GetComponent<EnemyDamageNumberDisplay>();
        }

        ResetRuntimeState();
        ApplySprite();
        ApplyCanvasOrientation();
    }

    private void LateUpdate()
    {
        if (isAttackPrepared && enemyData != null
            && enemyData.BehaviorType != EnemyBehaviorType.Melee)
        {
            RefreshAttackTelegraph();
        }
    }

    public bool Initialize(
        BoardManager assignedBoardManager,
        PlayerMove assignedPlayerMove,
        PlayerHealth assignedPlayerHealth,
        WaveManager assignedWaveManager)
    {
        if (enemyData == null || assignedBoardManager == null
            || assignedPlayerMove == null || assignedPlayerHealth == null
            || assignedWaveManager == null || actorMotion == null
            || actionQueueUI == null)
        {
            Debug.LogError(
                "Enemy Data, Actor Motion, Action Queue UI, Board Manager, Player Move, Player Health, and Wave Manager must be assigned.",
                this);
            return false;
        }

        boardManager = assignedBoardManager;
        playerMove = assignedPlayerMove;
        playerHealth = assignedPlayerHealth;
        waveManager = assignedWaveManager;
        ApplyInitialFacingDirection();
        ResetRuntimeState();
        ApplySprite();
        ApplyCanvasOrientation();
        isInitialized = true;
        PrimeRangedAttackOnSpawn();
        return true;
    }

    public void TakeTurn()
    {
        if (isActing)
        {
            return;
        }

        isActing = true;

        if (statusEffects != null && statusEffects.ConsumeStunTurn())
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (!isInitialized || enemyData == null || boardManager == null
            || playerMove == null || waveManager == null || actorMotion == null)
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (isAttackPrepared && queuedAttackActions.Count == 0)
        {
            ClearAttackQueue();
        }

        if (isAttackPrepared)
        {
            isAttackPrepared = false;
            HideAttackTelegraph();
            StartCoroutine(FireAttackQueue());
            return;
        }

        if (!TryGetTurnContext(out int directionToPlayer, out int distanceToPlayer))
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.Thrower)
        {
            TakeThrowerTurn(distanceToPlayer);
            return;
        }

        if (directionToPlayer != 0 && !IsFacing(directionToPlayer))
        {
            RotateToward(directionToPlayer);
            return;
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.Melee)
        {
            TakeMeleeTurn(directionToPlayer, distanceToPlayer);
            return;
        }

        TakeGunnerTurn(directionToPlayer, distanceToPlayer);
    }

    private void OnDisable()
    {
        HideAttackTelegraph();
        isActing = false;
    }

    public bool ApplyDamage(int damage)
    {
        return ApplyAttackDamage(damage) > 0;
    }

    public int ApplyAttackDamage(int damage)
    {
        return ApplyAttackDamage(damage, false);
    }

    public int ApplyAttackDamage(int damage, bool isCritical)
    {
        if (damage <= 0 || currentHealth <= 0)
        {
            return 0;
        }

        int modifiedDamage = statusEffects == null
            ? damage
            : statusEffects.ModifyIncomingAttackDamage(damage);
        int appliedDamage = ApplyDamageInternal(modifiedDamage);
        int markBonusDamage = Mathf.Max(0, modifiedDamage - damage);

        // Damage popups communicate the attack's full power, not the amount
        // clamped by the enemy's remaining health.
        damageNumberDisplay?.ShowAttackDamage(damage, isCritical);
        damageNumberDisplay?.ShowMarkBonusDamage(markBonusDamage);
        return appliedDamage;
    }

    public bool ApplyStatusDamage(int damage)
    {
        int appliedDamage = ApplyDamageInternal(damage);

        // Status damage popups show the effect's full damage, even when the
        // target has less health remaining than the requested damage.
        if (appliedDamage > 0)
        {
            damageNumberDisplay?.ShowPoisonDamage(damage);
        }

        return appliedDamage > 0;
    }

    public bool ApplyCollisionDamage(int damage)
    {
        int appliedDamage = ApplyDamageInternal(damage);
        damageNumberDisplay?.ShowAttackDamage(appliedDamage, false);
        return appliedDamage > 0;
    }

    public IEnumerator FlyTo(
        Vector3 targetPosition,
        float duration)
    {
        if (actorMotion == null)
        {
            yield break;
        }

        yield return actorMotion.FlyTo(targetPosition, duration);
        ApplyCanvasOrientation();
    }

    public IEnumerator FlyIntoCollision(
        Vector3 impactPosition,
        Vector3 restingPosition,
        float flightDuration,
        float settleDuration)
    {
        if (actorMotion == null)
        {
            yield break;
        }

        yield return actorMotion.FlyIntoCollision(
            impactPosition,
            restingPosition,
            flightDuration,
            settleDuration);
        ApplyCanvasOrientation();
    }

    public bool AddStatusEffect(StatusEffectType type, int stacks)
    {
        bool applied = currentHealth > 0 && statusEffects != null
            && statusEffects.Add(type, stacks);

        if (applied)
        {
            damageNumberDisplay?.ShowStatus(type);
        }

        return applied;
    }

    public void ShowLifeStealStatus()
    {
        damageNumberDisplay?.ShowLifeStealStatus();
    }

    private int ApplyDamageInternal(int damage)
    {
        if (damage <= 0 || currentHealth <= 0)
        {
            return 0;
        }

        int previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        int appliedDamage = previousHealth - currentHealth;
        RefreshHealthUI();
        HealthChanged?.Invoke(this, currentHealth, enemyData.MaxHealth);

        if (currentHealth == 0)
        {
            if (actionQueueUI != null)
            {
                actionQueueUI.ResetDisplay();
            }

            Defeated?.Invoke(this);
            Destroy(gameObject);
        }

        return appliedDamage;
    }

    private void TakeMeleeTurn(int directionToPlayer, int distanceToPlayer)
    {
        if (isRetreating)
        {
            if (enemyData.PreferredDistance <= 0
                || distanceToPlayer >= enemyData.PreferredDistance)
            {
                isRetreating = false;
            }
            else if (TryMoveAwayFromPlayer(
                         directionToPlayer,
                         distanceToPlayer))
            {
                return;
            }
            else
            {
                isRetreating = false;
            }
        }

        HandleAttackQueue(
            EnemyActionType.MeleeAttack,
            directionToPlayer,
            distanceToPlayer);
    }

    private void TakeGunnerTurn(int directionToPlayer, int distanceToPlayer)
    {
        int definedAttackCount = GetAvailableAttackCount(
            EnemyActionType.RangedAttack);

        if (definedAttackCount == 0)
        {
            ClearAttackQueue();
            MoveTowardPlayer(directionToPlayer);
            return;
        }

        EnsureAttackQueueVisible();

        if (queuedAttackActions.Count == 0)
        {
            RegisterAttack(EnemyActionType.RangedAttack, 0);
            return;
        }

        if (CanPrepareGunnerAttack(directionToPlayer, distanceToPlayer))
        {
            PrepareCurrentAttackQueue();
            return;
        }

        int availableAttackCount = Mathf.Min(
            definedAttackCount,
            enemyData.MaxQueuedAttacks);

        if (queuedAttackActions.Count < availableAttackCount)
        {
            RegisterAttack(
                EnemyActionType.RangedAttack,
                queuedAttackActions.Count);
            return;
        }

        MoveTowardPlayer(directionToPlayer);
    }

    private void TakeThrowerTurn(int distanceToPlayer)
    {
        if (GetAvailableAttackCount(EnemyActionType.RangedAttack) == 0)
        {
            ClearAttackQueue();
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        EnsureAttackQueueVisible();

        if (queuedAttackActions.Count == 0)
        {
            RegisterAttack(EnemyActionType.RangedAttack, 0);
            return;
        }

        if (distanceToPlayer > enemyData.FiringRange
            || !CaptureThrowerTargetTile())
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        PrepareCurrentAttackQueue();
    }

    private void HandleAttackQueue(
        EnemyActionType attackActionType,
        int directionToPlayer,
        int distanceToPlayer)
    {
        int definedAttackCount = GetAvailableAttackCount(attackActionType);
        int queueLimit = enemyData.MaxQueuedAttacks;

        if (definedAttackCount == 0)
        {
            ClearAttackQueue();
            MoveTowardPlayer(directionToPlayer);
            return;
        }

        if (!isQueueCreated)
        {
            isQueueCreated = true;
            isAttackPrepared = false;
            actionQueueUI.ShowQueue();
            CompleteAction(EnemyTurnActionType.CreateQueue);
            return;
        }

        if (attackActionType == EnemyActionType.MeleeAttack)
        {
            HandleMeleeAttackQueue(
                definedAttackCount,
                queueLimit,
                directionToPlayer,
                distanceToPlayer);
            return;
        }

        int availableAttackCount = Mathf.Min(
            definedAttackCount,
            queueLimit);

        if (queuedAttackActions.Count < availableAttackCount)
        {
            RegisterAttack(
                attackActionType,
                queuedAttackActions.Count);
            return;
        }

        int preparationRange = GetPreparationRange();

        if (distanceToPlayer > preparationRange)
        {
            MoveTowardPlayer(directionToPlayer);
            return;
        }

        isAttackPrepared = true;
        actionQueueUI.SetPrepared(true);
        CompleteAction(EnemyTurnActionType.PrepareAttack);
    }

    private void HandleMeleeAttackQueue(
        int definedAttackCount,
        int queueLimit,
        int directionToPlayer,
        int distanceToPlayer)
    {
        if (queuedAttackActions.Count == 0)
        {
            RegisterAttack(EnemyActionType.MeleeAttack, 0);
            return;
        }

        if (distanceToPlayer <= 1)
        {
            isAttackPrepared = true;
            actionQueueUI.SetPrepared(true);
            CompleteAction(EnemyTurnActionType.PrepareAttack);
            return;
        }

        if (distanceToPlayer <= enemyData.PreferredDistance
            && queuedAttackActions.Count < queueLimit)
        {
            if (UnityEngine.Random.value
                < enemyData.MeleeAdditionalAttackChance)
            {
                int attackIndex = queuedAttackActions.Count
                    % definedAttackCount;
                RegisterAttack(EnemyActionType.MeleeAttack, attackIndex);
            }
            else
            {
                MoveTowardPlayer(directionToPlayer);
            }

            return;
        }

        MoveTowardPlayer(directionToPlayer);
    }

    private void RegisterAttack(
        EnemyActionType attackActionType,
        int attackIndex)
    {
        EnsureAttackQueueVisible();

        if (!TryAppendAttack(attackActionType, attackIndex))
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        CompleteAction(EnemyTurnActionType.RegisterAttack);
    }

    private bool TryAppendAttack(
        EnemyActionType attackActionType,
        int attackIndex)
    {
        if (queuedAttackActions.Count >= enemyData.MaxQueuedAttacks)
        {
            return false;
        }

        EnemyActionData attackAction = GetAvailableAttackAction(
            attackActionType,
            attackIndex);

        if (attackAction == null
            || !actionQueueUI.AddAttackIcon(attackAction))
        {
            return false;
        }

        queuedAttackActions.Add(attackAction);
        return true;
    }

    private void ApplyInitialFacingDirection()
    {
        Vector3 localScale = transform.localScale;
        float scaleMagnitude = Mathf.Abs(localScale.x);

        if (scaleMagnitude <= Mathf.Epsilon)
        {
            scaleMagnitude = 1f;
        }

        localScale.x = scaleMagnitude * InitialFacingDirection;
        transform.localScale = localScale;
    }

    private void PrimeRangedAttackOnSpawn()
    {
        if (enemyData == null
            || enemyData.BehaviorType == EnemyBehaviorType.Melee)
        {
            return;
        }

        EnsureAttackQueueVisible();
        int availableAttackCount = GetAvailableAttackCount(
            EnemyActionType.RangedAttack);
        int startingAttackIndex = enemyData.RandomizeStartingActionIndex
            && availableAttackCount > 1
                ? UnityEngine.Random.Range(0, availableAttackCount)
                : 0;

        if (!TryAppendAttack(
                EnemyActionType.RangedAttack,
                startingAttackIndex))
        {
            ClearAttackQueue();
        }
    }

    private void EnsureAttackQueueVisible()
    {
        if (isQueueCreated)
        {
            return;
        }

        isQueueCreated = true;
        isAttackPrepared = false;
        actionQueueUI.ShowQueue();
    }

    private void PrepareCurrentAttackQueue()
    {
        isAttackPrepared = true;
        actionQueueUI.SetPrepared(true);
        RefreshAttackTelegraph();
        CompleteAction(EnemyTurnActionType.PrepareAttack);
    }

    private void RefreshAttackTelegraph()
    {
        if (!isAttackPrepared || enemyData == null
            || enemyData.BehaviorType == EnemyBehaviorType.Melee)
        {
            HideAttackTelegraph();
            return;
        }

        Material telegraphMaterial = enemyData.BehaviorType
            == EnemyBehaviorType.Thrower
                ? enemyData.ThrowerTelegraphMaterial
                : enemyData.GunnerTelegraphMaterial;

        if (telegraphMaterial == null)
        {
            HideAttackTelegraph();
            return;
        }

        LineRenderer lineRenderer = GetOrCreateAttackTelegraphLine();
        lineRenderer.sharedMaterial = telegraphMaterial;
        lineRenderer.widthMultiplier = enemyData.TelegraphLineWidth;
        lineRenderer.sortingOrder = enemyData.TelegraphSortingOrder;

        if (spriteRenderer != null)
        {
            lineRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        }

        bool positionsApplied = enemyData.BehaviorType
            == EnemyBehaviorType.Thrower
                ? ApplyThrowerTelegraphPositions(lineRenderer)
                : ApplyGunnerTelegraphPositions(lineRenderer);
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

    private void HideAttackTelegraph()
    {
        if (attackTelegraphLine != null)
        {
            attackTelegraphLine.enabled = false;
        }
    }

    private bool CanPrepareGunnerAttack(
        int directionToPlayer,
        int distanceToPlayer)
    {
        if (directionToPlayer == 0 || distanceToPlayer <= 0
            || distanceToPlayer > enemyData.FiringRange)
        {
            return false;
        }

        attackTargetBuffer.Clear();
        waveManager.GetEnemiesInDirection(
            transform.position,
            directionToPlayer,
            distanceToPlayer,
            attackTargetBuffer);
        return attackTargetBuffer.Count == 0;
    }

    private bool CaptureThrowerTargetTile()
    {
        if (!boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out preparedTargetTileIndex))
        {
            preparedTargetTileIndex = -1;
            preparedTargetPosition = Vector3.zero;
            return false;
        }

        preparedTargetPosition = playerMove.transform.position;
        return true;
    }

    private IEnumerator FireAttackQueue()
    {
        while (queuedAttackActions.Count > 0)
        {
            EnemyActionData attackAction = queuedAttackActions[0];
            queuedAttackActions.RemoveAt(0);
            yield return ExecuteQueuedAttack(attackAction);
            actionQueueUI.RemoveFirstIcon();

            if (queuedAttackActions.Count > 0)
            {
                yield return WaitForQueuedActionInterval();
            }
        }

        isRetreating = enemyData.BehaviorType == EnemyBehaviorType.Melee
            && enemyData.PreferredDistance > 0;
        isQueueCreated = false;
        isAttackPrepared = false;
        preparedTargetTileIndex = -1;
        preparedTargetPosition = Vector3.zero;
        actionQueueUI.ResetDisplay();
        CompleteAction(EnemyTurnActionType.Fire);
    }

    private IEnumerator ExecuteQueuedAttack(EnemyActionData attackAction)
    {
        if (!TryGetAttackData(attackAction, out EnemyAttackData attackData))
        {
            yield break;
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.Thrower)
        {
            yield return ExecuteThrowerAttack(attackData);
            yield break;
        }

        if (!TryGetAttackTarget(
                attackData,
                out EnemyController enemyTarget,
                out bool targetsPlayer,
                out Vector3 targetPosition))
        {
            AttackExecuted?.Invoke(this, attackData);
            yield break;
        }

        if (attackData.AttackEffectPrefab != null)
        {
            Instantiate(
                attackData.AttackEffectPrefab,
                targetPosition,
                Quaternion.identity);
        }

        ApplyAttackToTarget(
            attackData,
            enemyTarget,
            targetsPlayer);
        AttackExecuted?.Invoke(this, attackData);
    }

    private IEnumerator ExecuteThrowerAttack(EnemyAttackData attackData)
    {
        if (preparedTargetTileIndex < 0)
        {
            AttackExecuted?.Invoke(this, attackData);
            yield break;
        }

        yield return PlayThrownProjectile(preparedTargetPosition);

        if (attackData.AttackEffectPrefab != null)
        {
            Instantiate(
                attackData.AttackEffectPrefab,
                preparedTargetPosition,
                Quaternion.identity);
        }

        bool targetsPlayer = boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerTileIndex)
            && playerTileIndex == preparedTargetTileIndex;
        EnemyController enemyTarget = null;

        if (!targetsPlayer)
        {
            waveManager.TryGetEnemyAtTile(
                preparedTargetTileIndex,
                out enemyTarget,
                this);
        }

        ApplyAttackToTarget(
            attackData,
            enemyTarget,
            targetsPlayer);
        AttackExecuted?.Invoke(this, attackData);
    }

    private IEnumerator PlayThrownProjectile(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        float duration = enemyData.ThrownProjectileDuration;
        float arcHeight = enemyData.ThrownProjectileArcHeight;
        GameObject projectile = enemyData.ThrownProjectilePrefab == null
            ? null
            : Instantiate(
                enemyData.ThrownProjectilePrefab,
                startPosition,
                Quaternion.identity);

        if (duration <= 0f)
        {
            if (projectile != null)
            {
                projectile.transform.position = targetPosition;
                Destroy(projectile);
            }

            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            Vector3 position = Vector3.Lerp(
                startPosition,
                targetPosition,
                progress);
            position += Vector3.up
                * (Mathf.Sin(progress * Mathf.PI) * arcHeight);

            if (projectile != null)
            {
                projectile.transform.position = position;
            }
        }

        if (projectile != null)
        {
            projectile.transform.position = targetPosition;
            Destroy(projectile);
        }
    }

    private void ApplyAttackToTarget(
        EnemyAttackData attackData,
        EnemyController enemyTarget,
        bool targetsPlayer)
    {
        int attackDamage = statusEffects == null
            ? attackData.Damage
            : statusEffects.ModifyOutgoingAttackDamage(
                attackData.Damage);

        if (targetsPlayer)
        {
            playerHealth.ApplyDamage(attackDamage);

            if (!playerHealth.IsDefeated)
            {
                ApplyAttackStatusEffects(playerHealth, attackData);
            }
        }
        else if (enemyTarget != null)
        {
            enemyTarget.ApplyAttackDamage(attackDamage);

            if (enemyTarget != null && enemyTarget.CurrentHealth > 0)
            {
                ApplyAttackStatusEffects(enemyTarget, attackData);
            }
        }
    }

    private bool TryGetAttackTarget(
        EnemyAttackData attackData,
        out EnemyController enemyTarget,
        out bool targetsPlayer,
        out Vector3 targetPosition)
    {
        enemyTarget = null;
        targetsPlayer = false;
        targetPosition = Vector3.zero;

        if (attackData == null || boardManager == null
            || playerMove == null || waveManager == null
            || !boardManager.TryGetTileIndex(
                transform.position,
                out int attackerIndex)
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerIndex))
        {
            return false;
        }

        int attackDirection = transform.localScale.x >= 0f ? 1 : -1;
        int attackRange = enemyData.BehaviorType == EnemyBehaviorType.Melee
            ? attackData.Range
            : enemyData.FiringRange;
        int playerOffset = playerIndex - attackerIndex;
        int distanceToPlayer = Mathf.Abs(playerOffset);
        bool playerInAttackLine = playerOffset * attackDirection > 0
            && distanceToPlayer <= attackRange;

        attackTargetBuffer.Clear();
        waveManager.GetEnemiesInDirection(
            transform.position,
            attackDirection,
            attackRange,
            attackTargetBuffer);

        EnemyController closestEnemy = attackTargetBuffer.Count == 0
            ? null
            : attackTargetBuffer[0];
        int closestEnemyDistance = int.MaxValue;

        if (closestEnemy != null
            && boardManager.TryGetTileDistance(
                transform.position,
                closestEnemy.transform.position,
                out int measuredEnemyDistance))
        {
            closestEnemyDistance = measuredEnemyDistance;
        }

        if (closestEnemy != null
            && (!playerInAttackLine
                || closestEnemyDistance < distanceToPlayer))
        {
            enemyTarget = closestEnemy;
            targetPosition = closestEnemy.transform.position;
            return true;
        }

        if (!playerInAttackLine)
        {
            return false;
        }

        targetsPlayer = true;
        targetPosition = playerMove.transform.position;
        return true;
    }

    private static void ApplyAttackStatusEffects(
        PlayerHealth target,
        EnemyAttackData attackData)
    {
        target.AddStatusEffect(
            StatusEffectType.Mark,
            attackData.MarkDurationTurns);
        target.AddStatusEffect(
            StatusEffectType.Poison,
            attackData.PoisonStackCount);
        target.AddStatusEffect(
            StatusEffectType.Stun,
            attackData.StunDurationTurns);
        target.AddStatusEffect(
            StatusEffectType.Weakness,
            attackData.WeaknessDurationTurns);
    }

    private static void ApplyAttackStatusEffects(
        EnemyController target,
        EnemyAttackData attackData)
    {
        target.AddStatusEffect(
            StatusEffectType.Mark,
            attackData.MarkDurationTurns);
        target.AddStatusEffect(
            StatusEffectType.Poison,
            attackData.PoisonStackCount);
        target.AddStatusEffect(
            StatusEffectType.Stun,
            attackData.StunDurationTurns);
        target.AddStatusEffect(
            StatusEffectType.Weakness,
            attackData.WeaknessDurationTurns);
    }

    private IEnumerator WaitForQueuedActionInterval()
    {
        float elapsedTime = 0f;

        while (elapsedTime < enemyData.QueuedActionInterval)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                elapsedTime += Time.deltaTime;
            }
        }
    }

    private int GetAvailableAttackCount(EnemyActionType attackActionType)
    {
        int count = 0;

        foreach (EnemyActionData actionData in enemyData.Actions)
        {
            if (actionData != null
                && actionData.ActionType == attackActionType
                && TryGetAttackData(actionData, out _))
            {
                count++;
            }
        }

        return count;
    }

    private EnemyActionData GetAvailableAttackAction(
        EnemyActionType attackActionType,
        int attackIndex)
    {
        int currentIndex = 0;

        foreach (EnemyActionData actionData in enemyData.Actions)
        {
            if (actionData == null
                || actionData.ActionType != attackActionType
                || !TryGetAttackData(actionData, out _))
            {
                continue;
            }

            if (currentIndex == attackIndex)
            {
                return actionData;
            }

            currentIndex++;
        }

        return null;
    }

    private int GetPreparationRange()
    {
        int preparationRange = int.MaxValue;

        foreach (EnemyActionData actionData in queuedAttackActions)
        {
            if (TryGetAttackData(actionData, out EnemyAttackData attackData))
            {
                preparationRange = Mathf.Min(
                    preparationRange,
                    attackData.Range);
            }
        }

        return preparationRange == int.MaxValue ? 0 : preparationRange;
    }

    private void ClearAttackQueue()
    {
        queuedAttackActions.Clear();
        isQueueCreated = false;
        isAttackPrepared = false;
        preparedTargetTileIndex = -1;
        preparedTargetPosition = Vector3.zero;
        HideAttackTelegraph();

        if (actionQueueUI != null)
        {
            actionQueueUI.ResetDisplay();
        }
    }

    private void RotateToward(int directionToPlayer)
    {
        StartCoroutine(RotateRoutine(directionToPlayer));
    }

    private void MoveTowardPlayer(int directionToPlayer)
    {
        EnemyActionData approachAction = FindAction(EnemyActionType.Approach);

        if (directionToPlayer == 0 || approachAction == null
            || approachAction.MovementDistance <= 0)
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (TryBuildMovePath(
                directionToPlayer,
                approachAction.MovementDistance,
                out Vector3[] path))
        {
            StartCoroutine(MoveRoutine(path, false));
        }
        else
        {
            CompleteAction(EnemyTurnActionType.Wait);
        }
    }

    private bool TryMoveAwayFromPlayer(
        int directionToPlayer,
        int distanceToPlayer)
    {
        if (directionToPlayer == 0)
        {
            return false;
        }

        EnemyActionData retreatAction = FindAction(EnemyActionType.Retreat);
        int movementDistance = retreatAction != null
            && retreatAction.MovementDistance > 0
                ? retreatAction.MovementDistance
                : 1;
        int distanceToPreferred = enemyData.PreferredDistance - distanceToPlayer;
        movementDistance = Mathf.Min(movementDistance, distanceToPreferred);

        if (!TryBuildMovePath(
                -directionToPlayer,
                movementDistance,
                out Vector3[] path))
        {
            return false;
        }

        StartCoroutine(MoveRoutine(path, true));
        return true;
    }

    private bool TryBuildMovePath(
        int direction,
        int movementDistance,
        out Vector3[] path)
    {
        movePath.Clear();
        Vector3 currentPosition = transform.position;

        for (int step = 0; step < movementDistance; step++)
        {
            if (!boardManager.TryGetAdjacentTilePosition(
                    currentPosition,
                    direction,
                    out Vector3 targetPosition)
                || !boardManager.TryGetTileIndex(targetPosition, out int targetIndex)
                || !boardManager.TryGetTileIndex(
                    playerMove.transform.position,
                    out int playerIndex)
                || targetIndex == playerIndex
                || waveManager.IsTileOccupied(targetIndex, this)
                || waveManager.IsTileReservedForSpawn(targetIndex))
            {
                break;
            }

            currentPosition = targetPosition;
            movePath.Add(targetPosition);
        }

        path = movePath.ToArray();
        return path.Length > 0;
    }

    private IEnumerator MoveRoutine(Vector3[] path, bool updateRetreatState)
    {
        yield return actorMotion.MoveAlongPath(path);

        if (updateRetreatState
            && boardManager.TryGetTileDistance(
                transform.position,
                playerMove.transform.position,
                out int updatedDistanceToPlayer)
            && updatedDistanceToPlayer >= enemyData.PreferredDistance)
        {
            isRetreating = false;
        }

        CompleteAction(EnemyTurnActionType.Move);
    }

    private IEnumerator RotateRoutine(int directionToPlayer)
    {
        yield return actorMotion.RotateToDirection(directionToPlayer);
        ApplyCanvasOrientation();
        CompleteAction(EnemyTurnActionType.Rotate);
    }

    private bool TryGetTurnContext(
        out int directionToPlayer,
        out int distanceToPlayer)
    {
        directionToPlayer = 0;
        distanceToPlayer = 0;

        if (!boardManager.TryGetTileIndex(transform.position, out int enemyIndex)
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerIndex))
        {
            return false;
        }

        directionToPlayer = playerIndex > enemyIndex
            ? 1
            : playerIndex < enemyIndex ? -1 : 0;
        distanceToPlayer = Mathf.Abs(playerIndex - enemyIndex);
        return true;
    }

    private bool IsFacing(int direction)
    {
        int facingDirection = transform.localScale.x >= 0f ? 1 : -1;
        return facingDirection == direction;
    }

    private EnemyActionData FindAction(EnemyActionType actionType)
    {
        foreach (EnemyActionData actionData in enemyData.Actions)
        {
            if (actionData != null && actionData.ActionType == actionType)
            {
                return actionData;
            }
        }

        return null;
    }

    private bool TryGetAttackData(
        EnemyActionData attackAction,
        out EnemyAttackData attackData)
    {
        attackData = attackAction == null ? null : attackAction.AttackData;
        return attackData != null && attackData.Range >= 0;
    }

    private void ResetRuntimeState()
    {
        HideAttackTelegraph();
        currentHealth = enemyData == null ? 0 : enemyData.MaxHealth;
        queuedAttackActions.Clear();
        isQueueCreated = false;
        isAttackPrepared = false;
        isRetreating = false;
        preparedTargetTileIndex = -1;
        preparedTargetPosition = Vector3.zero;
        lastTurnAction = EnemyTurnActionType.None;
        isActing = false;

        if (statusEffects != null)
        {
            statusEffects.Clear();
        }

        if (actionQueueUI != null)
        {
            actionQueueUI.ResetDisplay();
        }

        RefreshHealthUI();
    }

    private void ApplySprite()
    {
        if (spriteRenderer != null && enemyData != null && enemyData.Sprite != null)
        {
            spriteRenderer.sprite = enemyData.Sprite;
        }
    }

    private void RefreshHealthUI()
    {
        if (healthFillImage == null)
        {
            return;
        }

        int maxHealth = enemyData == null ? 0 : enemyData.MaxHealth;
        healthFillImage.fillAmount = maxHealth <= 0
            ? 0f
            : (float)currentHealth / maxHealth;
    }

    private void ApplyCanvasOrientation()
    {
        if (actorMotion != null)
        {
            actorMotion.RefreshOrientationLock();
            return;
        }

        if (canvasTransform == null)
        {
            return;
        }

        Vector3 canvasScale = canvasTransform.localScale;
        float scaleMagnitude = Mathf.Abs(canvasScale.x);

        if (scaleMagnitude <= Mathf.Epsilon)
        {
            scaleMagnitude = 1f;
        }

        float enemyDirection = transform.localScale.x >= 0f ? 1f : -1f;
        canvasScale.x = scaleMagnitude * enemyDirection;
        canvasTransform.localScale = canvasScale;
    }

    private void CompleteAction(EnemyTurnActionType actionType)
    {
        if (statusEffects != null && currentHealth > 0)
        {
            statusEffects.ProcessTurnEnd();
        }

        lastTurnAction = actionType;
        isActing = false;
        TurnActionCompleted?.Invoke(this, actionType);
    }
}
