using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private ActorMotion actorMotion;

    [Header("Push")]
    [Range(0f, 1f)]
    [SerializeField] private float pushCollisionDamageRatio = 0.1f;
    [Min(0f)]
    [SerializeField] private float pushTileDuration = 0.1f;
    [Range(0f, 1f)]
    [SerializeField] private float pushCollisionImpactRatio = 0.5f;
    [Min(0f)]
    [SerializeField] private float pushCollisionImpactHeight = 0.2f;
    [Min(0f)]
    [SerializeField] private float pushCollisionSettleDuration = 0.12f;
    [Min(0)]
    [SerializeField] private int pushCooldownTurns = 3;

    [Header("Push Runtime State")]
    [Min(0)]
    [SerializeField] private int nextPushAvailableTurn;

    private StatusEffectController statusEffects;

    private WaveManager waveManager;
    private bool isShooting;
    private bool isActing;
    private bool isEnemyTurnResolving;
    private readonly List<Vector3> pushPathBuffer = new List<Vector3>();

    public event Action TurnCompleted;
    public event Action<int> PushCooldownChanged;

    public int TurnCount { get; private set; }
    public bool IsShooting => isShooting;
    public bool IsActing => isActing;
    public bool IsEnemyTurnResolving => isEnemyTurnResolving;
    public bool CanStartAction => CanPerformAction();
    public int RemainingPushCooldownTurns => Mathf.Max(
        0,
        nextPushAvailableTurn - TurnCount);
    public bool CanPush => RemainingPushCooldownTurns == 0;

    private void Awake()
    {
        statusEffects = GetComponent<StatusEffectController>();
    }

    public void SetWaveManager(WaveManager assignedWaveManager)
    {
        waveManager = assignedWaveManager;
    }

    public void SetShooting(bool shooting)
    {
        isShooting = shooting;
    }

    public void SetEnemyTurnResolving(bool resolving)
    {
        isEnemyTurnResolving = resolving;
    }

    private void OnDisable()
    {
        isActing = false;
        isEnemyTurnResolving = false;
    }

    private void Update()
    {
        if (!CanPerformAction())
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.aKey.wasPressedThisFrame)
            {
                MoveLeft();
                return;
            }

            if (keyboard.dKey.wasPressedThisFrame)
            {
                MoveRight();
                return;
            }

            if (keyboard.sKey.wasPressedThisFrame)
            {
                Wait();
                return;
            }
        }

        Mouse mouse = Mouse.current;

        if (mouse != null && mouse.middleButton.wasPressedThisFrame)
        {
            Rotate();
        }
    }

    public void MoveForward()
    {
        if (!CanPerformAction())
        {
            return;
        }

        int direction = transform.localScale.x >= 0f ? 1 : -1;
        Move(direction);
    }

    public void MoveLeft()
    {
        if (!CanPerformAction())
        {
            return;
        }

        Move(-1);
    }

    public void MoveRight()
    {
        if (!CanPerformAction())
        {
            return;
        }

        Move(1);
    }

    public void Rotate()
    {
        if (!CanPerformAction() || actorMotion == null)
        {
            return;
        }

        int targetDirection = transform.localScale.x >= 0f ? -1 : 1;
        StartCoroutine(RotateRoutine(targetDirection));
    }

    public void Wait()
    {
        if (!CanPerformAction())
        {
            return;
        }

        CompleteTurn();
    }

    private void Move(int direction)
    {
        if (boardManager == null || waveManager == null || actorMotion == null)
        {
            Debug.LogError(
                "Board Manager and Actor Motion must be assigned, and Wave Manager must initialize Player Move.",
                this);
            return;
        }

        if (!boardManager.TryGetAdjacentTilePosition(
                transform.position,
                direction,
                out Vector3 targetPosition))
        {
            return;
        }

        if (!boardManager.TryGetTileIndex(targetPosition, out int targetTileIndex))
        {
            return;
        }

        if (waveManager.TryGetEnemyAtTile(
                targetTileIndex,
                out EnemyController adjacentEnemy))
        {
            int facingDirection = transform.localScale.x >= 0f ? 1 : -1;
            int moveDirection = direction > 0 ? 1 : -1;

            if (moveDirection == facingDirection && CanPush)
            {
                StartCoroutine(PushRoutine(
                    adjacentEnemy,
                    targetPosition,
                    direction));
            }

            return;
        }

        if (waveManager.IsTileReservedForSpawn(targetTileIndex))
        {
            return;
        }

        StartCoroutine(MoveRoutine(targetPosition));
    }

    private IEnumerator PushRoutine(
        EnemyController pushedEnemy,
        Vector3 playerTargetPosition,
        int direction)
    {
        isActing = true;

        if (pushedEnemy == null
            || !TryBuildPushPath(
                pushedEnemy,
                direction,
                out EnemyController collidedEnemy))
        {
            isActing = false;
            yield break;
        }

        bool vacatesStartingTile = pushPathBuffer.Count > 0;
        Vector3 restingPosition = vacatesStartingTile
            ? pushPathBuffer[pushPathBuffer.Count - 1]
            : pushedEnemy.transform.position;
        int flightTileCount = pushPathBuffer.Count
            + (collidedEnemy == null ? 0 : 1);
        float flightDuration = Mathf.Max(0f, pushTileDuration)
            * flightTileCount;

        if (collidedEnemy != null)
        {
            Vector3 impactPosition = Vector3.Lerp(
                restingPosition,
                collidedEnemy.transform.position,
                Mathf.Clamp01(pushCollisionImpactRatio));
            impactPosition.y = restingPosition.y
                + Mathf.Max(0f, pushCollisionImpactHeight);
            impactPosition.z = restingPosition.z;

            yield return pushedEnemy.FlyIntoCollision(
                impactPosition,
                restingPosition,
                flightDuration,
                pushCollisionSettleDuration);
        }
        else if (vacatesStartingTile)
        {
            yield return pushedEnemy.FlyTo(
                restingPosition,
                flightDuration);
        }

        if (collidedEnemy != null && pushedEnemy != null)
        {
            ApplyPushCollisionDamage(pushedEnemy, collidedEnemy);
        }

        if (vacatesStartingTile || pushedEnemy == null
            || pushedEnemy.CurrentHealth <= 0)
        {
            yield return actorMotion.MoveTo(playerTargetPosition);
        }

        nextPushAvailableTurn = TurnCount
            + Mathf.Max(0, pushCooldownTurns)
            + 1;
        isActing = false;
        CompleteTurn();
    }

    private bool TryBuildPushPath(
        EnemyController pushedEnemy,
        int direction,
        out EnemyController collidedEnemy)
    {
        collidedEnemy = null;
        pushPathBuffer.Clear();

        if (pushedEnemy == null || direction == 0
            || !boardManager.TryGetTileIndex(
                pushedEnemy.transform.position,
                out int pushedEnemyIndex))
        {
            return false;
        }

        int moveDirection = direction > 0 ? 1 : -1;

        for (int tileIndex = pushedEnemyIndex + moveDirection;
             tileIndex >= 0 && tileIndex < boardManager.BoardCount;
             tileIndex += moveDirection)
        {
            if (waveManager.TryGetEnemyAtTile(
                    tileIndex,
                    out collidedEnemy,
                    pushedEnemy))
            {
                break;
            }

            if (!boardManager.TryGetTilePosition(
                    tileIndex,
                    out Vector3 tilePosition))
            {
                return false;
            }

            tilePosition.y = pushedEnemy.transform.position.y;
            tilePosition.z = pushedEnemy.transform.position.z;
            pushPathBuffer.Add(tilePosition);
        }

        return true;
    }

    private void ApplyPushCollisionDamage(
        EnemyController pushedEnemy,
        EnemyController collidedEnemy)
    {
        float damageRatio = Mathf.Clamp01(pushCollisionDamageRatio);

        if (damageRatio <= 0f)
        {
            return;
        }

        int pushedEnemyDamage = Mathf.Max(
            1,
            Mathf.CeilToInt(pushedEnemy.MaxHealth * damageRatio));
        int collidedEnemyDamage = Mathf.Max(
            1,
            Mathf.CeilToInt(collidedEnemy.MaxHealth * damageRatio));

        pushedEnemy.ApplyCollisionDamage(pushedEnemyDamage);
        collidedEnemy.ApplyCollisionDamage(collidedEnemyDamage);
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition)
    {
        isActing = true;
        yield return actorMotion.MoveTo(targetPosition);
        CompleteTurn();
        isActing = false;
    }

    private IEnumerator RotateRoutine(int targetDirection)
    {
        isActing = true;
        yield return actorMotion.RotateToDirection(targetDirection);
        CompleteTurn();
        isActing = false;
    }

    public void CompleteTurn()
    {
        if (statusEffects != null)
        {
            statusEffects.ProcessTurnEnd();
        }

        TurnCount++;
        PushCooldownChanged?.Invoke(RemainingPushCooldownTurns);
        TurnCompleted?.Invoke();
    }

    public bool TrySkipStunnedTurn()
    {
        if (GamePauseController.IsPaused || isShooting || isActing
            || isEnemyTurnResolving || statusEffects == null
            || !statusEffects.ConsumeStunTurn())
        {
            return false;
        }

        CompleteTurn();
        return true;
    }

    private bool CanPerformAction()
    {
        return !GamePauseController.IsPaused
            && !isShooting
            && !isActing
            && !isEnemyTurnResolving;
    }
}
