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

public class EnemyController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Transform canvasTransform;
    [SerializeField] private ActorMotion actorMotion;
    [SerializeField] private EnemyActionQueueUI actionQueueUI;

    [Header("Attack Queue")]
    [Range(1, 3)]
    [SerializeField] private int maxQueuedAttacks = 3;
    [Min(0f)]
    [SerializeField] private float queuedActionInterval = 0.2f;

    [Header("Runtime State")]
    [SerializeField] private int currentHealth;
    [SerializeField] private List<EnemyActionData> queuedAttackActions =
        new List<EnemyActionData>();
    [SerializeField] private bool isQueueCreated;
    [SerializeField] private bool isAttackPrepared;
    [SerializeField] private bool isRetreating;
    [SerializeField] private EnemyTurnActionType lastTurnAction;
    [SerializeField] private bool isActing;

    private BoardManager boardManager;
    private PlayerMove playerMove;
    private PlayerHealth playerHealth;
    private WaveManager waveManager;
    private bool isInitialized;
    private readonly List<Vector3> movePath = new List<Vector3>();

    public event Action<EnemyController, EnemyTurnActionType> TurnActionCompleted;
    public event Action<EnemyController, EnemyAttackData> AttackExecuted;
    public event Action<EnemyController, int, int> HealthChanged;
    public event Action<EnemyController> Defeated;

    public EnemyData Data => enemyData;
    public int CurrentHealth => currentHealth;
    public EnemyActionData LoadedAttackAction => queuedAttackActions.Count > 0
        ? queuedAttackActions[0]
        : null;
    public IReadOnlyList<EnemyActionData> QueuedAttackActions =>
        queuedAttackActions;
    public bool IsQueueCreated => isQueueCreated;
    public bool IsAttackPrepared => isAttackPrepared;
    public bool IsRetreating => isRetreating;
    public EnemyTurnActionType LastTurnAction => lastTurnAction;
    public bool IsActing => isActing;

    private void Awake()
    {
        ResetRuntimeState();
        ApplySprite();
        ApplyCanvasOrientation();
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
        ResetRuntimeState();
        ApplySprite();
        ApplyCanvasOrientation();
        isInitialized = true;
        return true;
    }

    public void TakeTurn()
    {
        if (isActing)
        {
            return;
        }

        isActing = true;

        if (!isInitialized || enemyData == null || boardManager == null
            || playerMove == null || waveManager == null || actorMotion == null)
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (!TryGetTurnContext(out int directionToPlayer, out int distanceToPlayer))
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (isAttackPrepared)
        {
            StartCoroutine(FireAttackQueue());
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

        TakeRangeTurn(directionToPlayer, distanceToPlayer);
    }

    private void OnDisable()
    {
        isActing = false;
    }

    public bool ApplyDamage(int damage)
    {
        if (damage <= 0 || currentHealth <= 0)
        {
            return false;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
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

        return true;
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
            else
            {
                MoveAwayFromPlayer(directionToPlayer, distanceToPlayer);
                return;
            }
        }

        HandleAttackQueue(
            EnemyActionType.MeleeAttack,
            directionToPlayer,
            distanceToPlayer);
    }

    private void TakeRangeTurn(int directionToPlayer, int distanceToPlayer)
    {
        HandleAttackQueue(
            EnemyActionType.RangedAttack,
            directionToPlayer,
            distanceToPlayer);
    }

    private void HandleAttackQueue(
        EnemyActionType attackActionType,
        int directionToPlayer,
        int distanceToPlayer)
    {
        int definedAttackCount = GetAvailableAttackCount(attackActionType);
        int queueLimit = Mathf.Clamp(maxQueuedAttacks, 1, 3);

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
            int attackIndex = queuedAttackActions.Count
                % definedAttackCount;
            RegisterAttack(EnemyActionType.MeleeAttack, attackIndex);
            return;
        }

        MoveTowardPlayer(directionToPlayer);
    }

    private void RegisterAttack(
        EnemyActionType attackActionType,
        int attackIndex)
    {
        EnemyActionData attackAction = GetAvailableAttackAction(
            attackActionType,
            attackIndex);

        if (attackAction == null
            || !actionQueueUI.AddAttackIcon(attackAction))
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        queuedAttackActions.Add(attackAction);
        CompleteAction(EnemyTurnActionType.RegisterAttack);
    }

    private IEnumerator FireAttackQueue()
    {
        while (queuedAttackActions.Count > 0)
        {
            EnemyActionData attackAction = queuedAttackActions[0];
            queuedAttackActions.RemoveAt(0);
            ExecuteQueuedAttack(attackAction);
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
        actionQueueUI.ResetDisplay();
        CompleteAction(EnemyTurnActionType.Fire);
    }

    private void ExecuteQueuedAttack(EnemyActionData attackAction)
    {
        if (!TryGetAttackData(attackAction, out EnemyAttackData attackData))
        {
            return;
        }

        bool canHitPlayer = TryGetTurnContext(
                out int directionToPlayer,
                out int distanceToPlayer)
            && directionToPlayer != 0
            && IsFacing(directionToPlayer)
            && distanceToPlayer <= attackData.Range;

        if (canHitPlayer)
        {
            if (attackData.AttackEffectPrefab != null)
            {
                Instantiate(
                    attackData.AttackEffectPrefab,
                    playerMove.transform.position,
                    Quaternion.identity);
            }

            playerHealth.ApplyDamage(attackData.Damage);
        }

        AttackExecuted?.Invoke(this, attackData);
    }

    private IEnumerator WaitForQueuedActionInterval()
    {
        float elapsedTime = 0f;

        while (elapsedTime < queuedActionInterval)
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

    private void MoveAwayFromPlayer(
        int directionToPlayer,
        int distanceToPlayer)
    {
        if (directionToPlayer == 0)
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
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
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        StartCoroutine(MoveRoutine(path, true));
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
                || waveManager.IsTileOccupied(targetIndex, this))
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
        currentHealth = enemyData == null ? 0 : enemyData.MaxHealth;
        queuedAttackActions.Clear();
        isQueueCreated = false;
        isAttackPrepared = false;
        isRetreating = false;
        lastTurnAction = EnemyTurnActionType.None;
        isActing = false;

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
        lastTurnAction = actionType;
        isActing = false;
        TurnActionCompleted?.Invoke(this, actionType);
    }
}
