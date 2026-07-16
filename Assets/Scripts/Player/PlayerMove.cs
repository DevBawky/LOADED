using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;

    public event Action TurnCompleted;

    public int TurnCount { get; private set; }

    private void Update()
    {
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

            if (keyboard.wKey.wasPressedThisFrame)
            {
                Rotate();
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
        int direction = transform.localScale.x >= 0f ? 1 : -1;
        Move(direction);
    }

    public void MoveLeft()
    {
        Move(-1);
    }

    public void MoveRight()
    {
        Move(1);
    }

    public void Rotate()
    {
        Vector3 localScale = transform.localScale;
        localScale.x *= -1f;
        transform.localScale = localScale;

        CompleteTurn();
    }

    public void Wait()
    {
        CompleteTurn();
    }

    private void Move(int direction)
    {
        if (boardManager == null)
        {
            Debug.LogError("Board Manager를 Inspector에서 할당해야 합니다.", this);
            return;
        }

        if (!boardManager.TryGetAdjacentTilePosition(
                transform.position,
                direction,
                out Vector3 targetPosition))
        {
            return;
        }

        transform.position = targetPosition;
        CompleteTurn();
    }

    private void CompleteTurn()
    {
        TurnCount++;
        TurnCompleted?.Invoke();
    }
}
