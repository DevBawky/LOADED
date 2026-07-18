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
    [SerializeField] private Transform pushVisualTransform;

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
    [Header("Push Player Motion")]
    [Range(0f, 1f)]
    [SerializeField] private float playerPushImpactRatio = 0.65f;
    [Min(0f)]
    [SerializeField] private float playerPushImpactDuration = 0.08f;
    [Min(0f)]
    [SerializeField] private float playerPushReturnDuration = 0.12f;
    [Min(0f)]
    [SerializeField] private float playerPushImpactHeight = 0.08f;
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
    private bool isInputLocked;
    private bool isPushVisualDisplaced;
    private Vector3 pushVisualRestLocalPosition;
    private Vector3 pushVisualRestWorldPosition;
    private readonly List<Vector3> pushPathBuffer = new List<Vector3>();

    public event Action TurnCompleted;
    public event Action<int> PushCooldownChanged;

    public int TurnCount { get; private set; }
    public bool IsShooting => isShooting;
    public bool IsActing => isActing;
    public bool IsEnemyTurnResolving => isEnemyTurnResolving;
    public bool IsInputLocked => isInputLocked;
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

    public void SetInputLocked(bool inputLocked)
    {
        isInputLocked = inputLocked;
    }

    private void OnDisable()
    {
        RestorePushVisualPosition();
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
        int direction)
    {
        isActing = true;

        if (!TryCreateEnemyPushPlan(
                pushedEnemy,
                direction,
                int.MaxValue,
                out EnemyPushPlan pushPlan))
        {
            isActing = false;
            yield break;
        }

        yield return ExecuteEnemyPush(pushPlan, true);

        nextPushAvailableTurn = TurnCount
            + Mathf.Max(0, pushCooldownTurns)
            + 1;
        isActing = false;
        CompleteTurn();
    }

    public IEnumerator PushEnemyFromBullet(
        EnemyController pushedEnemy,
        int direction,
        int knockbackDistance)
    {
        if (knockbackDistance <= 0
            || !TryCreateEnemyPushPlan(
                pushedEnemy,
                direction,
                knockbackDistance,
                out EnemyPushPlan pushPlan))
        {
            yield break;
        }

        yield return ExecuteEnemyPush(pushPlan, false);
    }

    public IEnumerator SwapPositionWithEnemy(EnemyController enemy)
    {
        if (enemy == null || enemy.CurrentHealth <= 0
            || boardManager == null || actorMotion == null
            || !boardManager.TryGetTileIndex(
                transform.position,
                out int playerTileIndex)
            || !boardManager.TryGetTileIndex(
                enemy.transform.position,
                out int enemyTileIndex)
            || playerTileIndex == enemyTileIndex
            || !boardManager.TryGetTilePosition(
                playerTileIndex,
                out Vector3 playerTilePosition)
            || !boardManager.TryGetTilePosition(
                enemyTileIndex,
                out Vector3 enemyTilePosition))
        {
            yield break;
        }

        Vector3 playerPositionOffset = transform.position - playerTilePosition;
        Vector3 enemyPositionOffset = enemy.transform.position - enemyTilePosition;
        Vector3 playerTargetPosition = enemyTilePosition + playerPositionOffset;
        Vector3 enemyTargetPosition = playerTilePosition + enemyPositionOffset;
        float swapDuration = Mathf.Max(0f, actorMotion.MoveDuration);

        Coroutine enemySwapRoutine = StartCoroutine(
            enemy.FlyTo(enemyTargetPosition, swapDuration));
        yield return actorMotion.FlyTo(playerTargetPosition, swapDuration);

        if (enemySwapRoutine != null)
        {
            yield return enemySwapRoutine;
        }
    }

    private IEnumerator ExecuteEnemyPush(
        EnemyPushPlan pushPlan,
        bool playPlayerImpact)
    {
        if (pushPlan == null || pushPlan.PushedEnemy == null)
        {
            yield break;
        }

        Coroutine playerReturnRoutine = null;

        if (playPlayerImpact)
        {
            yield return PlayPlayerPushImpact(
                pushPlan.PushedEnemy.transform.position);
            playerReturnRoutine = isPushVisualDisplaced
                ? StartCoroutine(ReturnPlayerPushVisual())
                : null;
        }

        if (pushPlan.CollidedEnemy != null)
        {
            Vector3 impactPosition = Vector3.Lerp(
                pushPlan.RestingPosition,
                pushPlan.CollidedEnemy.transform.position,
                Mathf.Clamp01(pushCollisionImpactRatio));
            impactPosition.y = pushPlan.RestingPosition.y
                + Mathf.Max(0f, pushCollisionImpactHeight);
            impactPosition.z = pushPlan.RestingPosition.z;

            yield return pushPlan.PushedEnemy.FlyIntoCollision(
                impactPosition,
                pushPlan.RestingPosition,
                pushPlan.FlightDuration,
                pushCollisionSettleDuration);
        }
        else if (pushPlan.VacatesStartingTile)
        {
            yield return pushPlan.PushedEnemy.FlyTo(
                pushPlan.RestingPosition,
                pushPlan.FlightDuration);
        }

        if (playerReturnRoutine != null)
        {
            yield return playerReturnRoutine;
        }

        if (pushPlan.CollidedEnemy != null
            && pushPlan.PushedEnemy != null)
        {
            ApplyPushCollisionDamage(
                pushPlan.PushedEnemy,
                pushPlan.CollidedEnemy);
        }
    }

    private bool TryCreateEnemyPushPlan(
        EnemyController pushedEnemy,
        int direction,
        int maxTravelDistance,
        out EnemyPushPlan pushPlan)
    {
        pushPlan = null;

        if (pushedEnemy == null || maxTravelDistance <= 0
            || !TryBuildPushPath(
                pushedEnemy,
                direction,
                maxTravelDistance,
                out EnemyController collidedEnemy))
        {
            return false;
        }

        bool vacatesStartingTile = pushPathBuffer.Count > 0;
        Vector3 restingPosition = vacatesStartingTile
            ? pushPathBuffer[pushPathBuffer.Count - 1]
            : pushedEnemy.transform.position;
        int flightTileCount = pushPathBuffer.Count
            + (collidedEnemy == null ? 0 : 1);
        float flightDuration = Mathf.Max(0f, pushTileDuration)
            * flightTileCount;

        pushPlan = new EnemyPushPlan(
            pushedEnemy,
            collidedEnemy,
            restingPosition,
            vacatesStartingTile,
            flightDuration);
        return true;
    }

    private IEnumerator PlayPlayerPushImpact(Vector3 enemyPosition)
    {
        if (pushVisualTransform == null)
        {
            yield break;
        }

        pushVisualRestLocalPosition = pushVisualTransform.localPosition;
        pushVisualRestWorldPosition = pushVisualTransform.position;
        isPushVisualDisplaced = true;

        Vector3 impactPosition = Vector3.Lerp(
            pushVisualRestWorldPosition,
            enemyPosition,
            Mathf.Clamp01(playerPushImpactRatio));
        impactPosition.y = pushVisualRestWorldPosition.y;
        impactPosition.z = pushVisualRestWorldPosition.z;

        yield return MovePushVisual(
            pushVisualRestWorldPosition,
            impactPosition,
            playerPushImpactDuration,
            playerPushImpactHeight);
    }

    private IEnumerator ReturnPlayerPushVisual()
    {
        yield return MovePushVisual(
            pushVisualTransform.position,
            pushVisualRestWorldPosition,
            playerPushReturnDuration,
            0f);

        RestorePushVisualPosition();
    }

    private IEnumerator MovePushVisual(
        Vector3 startPosition,
        Vector3 targetPosition,
        float duration,
        float arcHeight)
    {
        duration = Mathf.Max(0f, duration);
        arcHeight = Mathf.Max(0f, arcHeight);

        if (duration <= 0f)
        {
            pushVisualTransform.position = targetPosition;
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
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 position = Vector3.Lerp(
                startPosition,
                targetPosition,
                smoothProgress);
            position += Vector3.up
                * (Mathf.Sin(progress * Mathf.PI) * arcHeight);
            pushVisualTransform.position = position;
        }

        pushVisualTransform.position = targetPosition;
    }

    private void RestorePushVisualPosition()
    {
        if (!isPushVisualDisplaced || pushVisualTransform == null)
        {
            return;
        }

        pushVisualTransform.localPosition = pushVisualRestLocalPosition;
        isPushVisualDisplaced = false;
    }

    private bool TryBuildPushPath(
        EnemyController pushedEnemy,
        int direction,
        int maxTravelDistance,
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

        int inspectedTileCount = 0;

        for (int tileIndex = pushedEnemyIndex + moveDirection;
             tileIndex >= 0 && tileIndex < boardManager.BoardCount
             && inspectedTileCount < maxTravelDistance;
             tileIndex += moveDirection)
        {
            inspectedTileCount++;

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
        if (GamePauseController.IsPaused || isInputLocked
            || isShooting || isActing
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
            && !isInputLocked
            && !isShooting
            && !isActing
            && !isEnemyTurnResolving;
    }

    private sealed class EnemyPushPlan
    {
        public EnemyPushPlan(
            EnemyController pushedEnemy,
            EnemyController collidedEnemy,
            Vector3 restingPosition,
            bool vacatesStartingTile,
            float flightDuration)
        {
            PushedEnemy = pushedEnemy;
            CollidedEnemy = collidedEnemy;
            RestingPosition = restingPosition;
            VacatesStartingTile = vacatesStartingTile;
            FlightDuration = flightDuration;
        }

        public EnemyController PushedEnemy { get; }
        public EnemyController CollidedEnemy { get; }
        public Vector3 RestingPosition { get; }
        public bool VacatesStartingTile { get; }
        public float FlightDuration { get; }
    }
}
