using System;
using UnityEngine;
using UnityEngine.UI;

public enum EnemyTurnActionType
{
    None,
    Move,
    Rotate,
    Wait,
    Reload,
    Fire
}

public class EnemyController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Image healthFillImage;

    [Header("Runtime State")]
    [SerializeField] private int currentHealth;
    [SerializeField] private EnemyActionData loadedAttackAction;
    [SerializeField] private bool isAttackPrepared;
    [SerializeField] private EnemyTurnActionType lastTurnAction;

    private BoardManager boardManager;
    private PlayerMove playerMove;
    private PlayerHealth playerHealth;
    private WaveManager waveManager;
    private bool isInitialized;

    public event Action<EnemyController, EnemyTurnActionType> TurnActionCompleted;
    public event Action<EnemyController, EnemyAttackData> AttackExecuted;
    public event Action<EnemyController, int, int> HealthChanged;
    public event Action<EnemyController> Defeated;

    public EnemyData Data => enemyData;
    public int CurrentHealth => currentHealth;
    public EnemyActionData LoadedAttackAction => loadedAttackAction;
    public bool IsAttackPrepared => isAttackPrepared;
    public EnemyTurnActionType LastTurnAction => lastTurnAction;

    private void Awake()
    {
        ResetRuntimeState();
        ApplySprite();
    }

    public bool Initialize(
        BoardManager assignedBoardManager,
        PlayerMove assignedPlayerMove,
        PlayerHealth assignedPlayerHealth,
        WaveManager assignedWaveManager)
    {
        if (enemyData == null || assignedBoardManager == null
            || assignedPlayerMove == null || assignedPlayerHealth == null
            || assignedWaveManager == null)
        {
            Debug.LogError(
                "Enemy Data, Board Manager, Player Move, Player Health, and Wave Manager must be assigned.",
                this);
            return false;
        }

        boardManager = assignedBoardManager;
        playerMove = assignedPlayerMove;
        playerHealth = assignedPlayerHealth;
        waveManager = assignedWaveManager;
        ResetRuntimeState();
        ApplySprite();
        isInitialized = true;
        return true;
    }

    public void TakeTurn()
    {
        if (!isInitialized || enemyData == null || boardManager == null
            || playerMove == null || waveManager == null)
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (!TryGetTurnContext(out int directionToPlayer, out int distanceToPlayer))
        {
            CompleteAction(EnemyTurnActionType.Wait);
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
            Defeated?.Invoke(this);
            Destroy(gameObject);
        }

        return true;
    }

    private void TakeMeleeTurn(int directionToPlayer, int distanceToPlayer)
    {
        EnemyActionData attackAction = loadedAttackAction != null
            ? loadedAttackAction
            : FindAction(EnemyActionType.MeleeAttack);

        if (!TryGetAttackData(attackAction, out EnemyAttackData attackData))
        {
            MoveTowardPlayer(directionToPlayer);
            return;
        }

        if (isAttackPrepared)
        {
            ResolvePreparedAttack(
                directionToPlayer,
                distanceToPlayer,
                attackData.Range);
            return;
        }

        if (loadedAttackAction != null)
        {
            if (distanceToPlayer <= attackData.Range)
            {
                PrepareAttack();
            }
            else
            {
                MoveTowardPlayer(directionToPlayer);
            }

            return;
        }

        int reloadDistance = Mathf.Max(enemyData.PreferredDistance, attackData.Range);

        if (distanceToPlayer <= reloadDistance)
        {
            ReloadAttack(attackAction);
        }
        else
        {
            MoveTowardPlayer(directionToPlayer);
        }
    }

    private void TakeRangeTurn(int directionToPlayer, int distanceToPlayer)
    {
        EnemyActionData attackAction = loadedAttackAction != null
            ? loadedAttackAction
            : FindAction(EnemyActionType.RangedAttack);

        if (!TryGetAttackData(attackAction, out EnemyAttackData attackData))
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (isAttackPrepared)
        {
            ResolvePreparedAttack(
                directionToPlayer,
                distanceToPlayer,
                attackData.Range);
            return;
        }

        if (loadedAttackAction == null)
        {
            ReloadAttack(attackAction);
            return;
        }

        if (distanceToPlayer <= attackData.Range)
        {
            PrepareAttack();
        }
        else
        {
            MoveTowardPlayer(directionToPlayer);
        }
    }

    private void ResolvePreparedAttack(
        int directionToPlayer,
        int distanceToPlayer,
        int attackRange)
    {
        if (distanceToPlayer <= attackRange)
        {
            FireAttack();
            return;
        }

        isAttackPrepared = false;
        MoveTowardPlayer(directionToPlayer);
    }

    private void ReloadAttack(EnemyActionData attackAction)
    {
        loadedAttackAction = attackAction;
        isAttackPrepared = false;
        CompleteAction(EnemyTurnActionType.Reload);
    }

    private void PrepareAttack()
    {
        isAttackPrepared = true;
        CompleteAction(EnemyTurnActionType.Wait);
    }

    private void FireAttack()
    {
        if (!TryGetAttackData(loadedAttackAction, out EnemyAttackData attackData))
        {
            loadedAttackAction = null;
            isAttackPrepared = false;
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (attackData.AttackEffectPrefab != null)
        {
            Instantiate(
                attackData.AttackEffectPrefab,
                playerMove.transform.position,
                Quaternion.identity);
        }

        playerHealth.ApplyDamage(attackData.Damage);
        AttackExecuted?.Invoke(this, attackData);
        loadedAttackAction = null;
        isAttackPrepared = false;
        CompleteAction(EnemyTurnActionType.Fire);
    }

    private void RotateToward(int directionToPlayer)
    {
        Vector3 localScale = transform.localScale;
        float scaleMagnitude = Mathf.Abs(localScale.x);

        if (scaleMagnitude <= Mathf.Epsilon)
        {
            scaleMagnitude = 1f;
        }

        localScale.x = scaleMagnitude * directionToPlayer;
        transform.localScale = localScale;
        CompleteAction(EnemyTurnActionType.Rotate);
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

        Vector3 currentPosition = transform.position;
        bool moved = false;

        for (int step = 0; step < approachAction.MovementDistance; step++)
        {
            if (!boardManager.TryGetAdjacentTilePosition(
                    currentPosition,
                    directionToPlayer,
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
            moved = true;
        }

        if (moved)
        {
            transform.position = currentPosition;
            CompleteAction(EnemyTurnActionType.Move);
        }
        else
        {
            CompleteAction(EnemyTurnActionType.Wait);
        }
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
        loadedAttackAction = null;
        isAttackPrepared = false;
        lastTurnAction = EnemyTurnActionType.None;
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

    private void CompleteAction(EnemyTurnActionType actionType)
    {
        lastTurnAction = actionType;
        TurnActionCompleted?.Invoke(this, actionType);
    }
}
