using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private ActorMotion actorMotion;

    private StatusEffectController statusEffects;

    private WaveManager waveManager;
    private bool isShooting;
    private bool isActing;
    private bool isEnemyTurnResolving;

    public event Action TurnCompleted;

    public int TurnCount { get; private set; }
    public bool IsShooting => isShooting;
    public bool IsActing => isActing;
    public bool IsEnemyTurnResolving => isEnemyTurnResolving;
    public bool CanStartAction => CanPerformAction();

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

        if (!boardManager.TryGetTileIndex(targetPosition, out int targetTileIndex)
            || waveManager.IsTileOccupied(targetTileIndex)
            || waveManager.IsTileReservedForSpawn(targetTileIndex))
        {
            return;
        }

        StartCoroutine(MoveRoutine(targetPosition));
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
