using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;

    private WaveManager waveManager;

    public event Action TurnCompleted;

    public int TurnCount { get; private set; }

    public void SetWaveManager(WaveManager assignedWaveManager)
    {
        waveManager = assignedWaveManager;
    }

    private void Update()
    {
        if (GamePauseController.IsPaused)
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
        if (GamePauseController.IsPaused)
        {
            return;
        }

        int direction = transform.localScale.x >= 0f ? 1 : -1;
        Move(direction);
    }

    public void MoveLeft()
    {
        if (GamePauseController.IsPaused)
        {
            return;
        }

        Move(-1);
    }

    public void MoveRight()
    {
        if (GamePauseController.IsPaused)
        {
            return;
        }

        Move(1);
    }

    public void Rotate()
    {
        if (GamePauseController.IsPaused)
        {
            return;
        }

        Vector3 localScale = transform.localScale;
        localScale.x *= -1f;
        transform.localScale = localScale;

        CompleteTurn();
    }

    public void Wait()
    {
        if (GamePauseController.IsPaused)
        {
            return;
        }

        CompleteTurn();
    }

    private void Move(int direction)
    {
        if (boardManager == null || waveManager == null)
        {
            Debug.LogError(
                "Board Manager must be assigned and Wave Manager must initialize Player Move.",
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
            || waveManager.IsTileOccupied(targetTileIndex))
        {
            return;
        }

        transform.position = targetPosition;
        CompleteTurn();
    }

    public void CompleteTurn()
    {
        TurnCount++;
        TurnCompleted?.Invoke();
    }
}
