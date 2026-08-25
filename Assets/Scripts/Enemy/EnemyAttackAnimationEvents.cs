using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class EnemyAttackAnimationEvents : MonoBehaviour
{
    public const string DodgeFunctionName = nameof(BeginAttackDodgeWindow);
    public const string BeginFunctionName = nameof(BeginAttackActiveWindow);
    public const string EndFunctionName = nameof(EndAttackActiveWindow);

    private EnemyController owner;

    internal void Initialize(EnemyController assignedOwner)
    {
        owner = assignedOwner;
    }

    public void BeginAttackDodgeWindow()
    {
        if (owner != null)
        {
            owner.SetAttackDodgeWindowOpen(true);
        }
    }

    public void BeginAttackActiveWindow()
    {
        if (owner != null)
        {
            owner.SetAttackActiveWindowOpen(true);
        }
    }

    public void EndAttackActiveWindow()
    {
        if (owner != null)
        {
            owner.SetAttackDodgeWindowOpen(false);
            owner.SetAttackActiveWindowOpen(false);
        }
    }

    private void OnDisable()
    {
        if (owner != null)
        {
            owner.SetAttackDodgeWindowOpen(false);
            owner.SetAttackActiveWindowOpen(false);
        }
    }
}

internal readonly struct EnemyAttackActiveWindowTiming
{
    private EnemyAttackActiveWindowTiming(
        float dodgeStartNormalizedTime,
        float startNormalizedTime,
        float endNormalizedTime)
    {
        DodgeStartNormalizedTime = dodgeStartNormalizedTime;
        StartNormalizedTime = startNormalizedTime;
        EndNormalizedTime = endNormalizedTime;
    }

    public float DodgeStartNormalizedTime { get; }
    public float StartNormalizedTime { get; }
    public float EndNormalizedTime { get; }

    public bool IsInDodgeWindow(float normalizedTime)
    {
        return normalizedTime >= DodgeStartNormalizedTime
            && normalizedTime < StartNormalizedTime;
    }

    public bool Contains(float normalizedTime)
    {
        return normalizedTime >= StartNormalizedTime
            && normalizedTime <= EndNormalizedTime;
    }

    public bool Overlaps(
        float previousNormalizedTime,
        float currentNormalizedTime)
    {
        float start = Mathf.Min(
            previousNormalizedTime,
            currentNormalizedTime);
        float end = Mathf.Max(
            previousNormalizedTime,
            currentNormalizedTime);
        return end >= StartNormalizedTime && start <= EndNormalizedTime;
    }

    public bool CrossesDodgeStart(
        float previousNormalizedTime,
        float currentNormalizedTime)
    {
        return Crosses(
            DodgeStartNormalizedTime,
            previousNormalizedTime,
            currentNormalizedTime);
    }

    public bool CrossesActiveStart(
        float previousNormalizedTime,
        float currentNormalizedTime)
    {
        return Crosses(
            StartNormalizedTime,
            previousNormalizedTime,
            currentNormalizedTime);
    }

    public static bool TryCreate(
        AnimationClip clip,
        out EnemyAttackActiveWindowTiming timing)
    {
        timing = default;

        if (clip == null || clip.length <= 0f)
        {
            return false;
        }

        AnimationEvent[] events = clip.events;
        float startTime = float.PositiveInfinity;
        float endTime = float.PositiveInfinity;

        foreach (AnimationEvent animationEvent in events)
        {
            if (animationEvent == null
                || animationEvent.functionName
                    != EnemyAttackAnimationEvents.BeginFunctionName)
            {
                continue;
            }

            foreach (AnimationEvent endEvent in events)
            {
                if (endEvent == null
                    || endEvent.functionName
                        != EnemyAttackAnimationEvents.EndFunctionName
                    || endEvent.time <= animationEvent.time)
                {
                    continue;
                }

                if (animationEvent.time < startTime
                    || Mathf.Approximately(animationEvent.time, startTime)
                    && endEvent.time < endTime)
                {
                    startTime = animationEvent.time;
                    endTime = endEvent.time;
                }

                break;
            }
        }

        if (float.IsPositiveInfinity(startTime)
            || float.IsPositiveInfinity(endTime))
        {
            return false;
        }

        float dodgeStartTime = 0f;

        foreach (AnimationEvent animationEvent in events)
        {
            if (animationEvent != null
                && animationEvent.functionName
                    == EnemyAttackAnimationEvents.DodgeFunctionName
                && animationEvent.time <= startTime)
            {
                dodgeStartTime = Mathf.Max(
                    dodgeStartTime,
                    animationEvent.time);
            }
        }

        timing = new EnemyAttackActiveWindowTiming(
            Mathf.Clamp01(dodgeStartTime / clip.length),
            Mathf.Clamp01(startTime / clip.length),
            Mathf.Clamp01(endTime / clip.length));
        return timing.EndNormalizedTime > timing.StartNormalizedTime;
    }

    private static bool Crosses(
        float targetNormalizedTime,
        float previousNormalizedTime,
        float currentNormalizedTime)
    {
        float start = Mathf.Min(
            previousNormalizedTime,
            currentNormalizedTime);
        float end = Mathf.Max(
            previousNormalizedTime,
            currentNormalizedTime);
        return start <= targetNormalizedTime && end >= targetNormalizedTime;
    }
}

internal readonly struct EnemyPlayerDodgeWindowState
{
    public EnemyPlayerDodgeWindowState(
        bool playerWasThreatened,
        int playerTileIndex,
        Vector3 playerPosition)
    {
        PlayerWasThreatened = playerWasThreatened;
        PlayerTileIndex = playerTileIndex;
        PlayerPosition = playerPosition;
    }

    public bool PlayerWasThreatened { get; }
    public int PlayerTileIndex { get; }
    public Vector3 PlayerPosition { get; }

    public bool TryResolveDodge(
        bool playerIsThreatened,
        int currentPlayerTileIndex,
        Vector3 currentPlayerPosition,
        out int movementDirection)
    {
        movementDirection = 0;

        if (!PlayerWasThreatened || playerIsThreatened
            || PlayerTileIndex < 0
            || currentPlayerTileIndex < 0
            || currentPlayerTileIndex == PlayerTileIndex)
        {
            return false;
        }

        movementDirection = Math.Sign(
            currentPlayerTileIndex - PlayerTileIndex);

        if (movementDirection == 0)
        {
            movementDirection = currentPlayerPosition.x >= PlayerPosition.x
                ? 1
                : -1;
        }

        return true;
    }
}

internal struct EnemyPlayerDodgeResolution
{
    public bool IsResolved { get; private set; }
    public bool PlayerDodged { get; private set; }

    public bool TryConfirmBeforeImpact(
        EnemyPlayerDodgeWindowState windowState,
        bool playerIsThreatened,
        int currentPlayerTileIndex,
        Vector3 currentPlayerPosition,
        out int movementDirection)
    {
        movementDirection = 0;

        if (IsResolved || !windowState.TryResolveDodge(
                playerIsThreatened,
                currentPlayerTileIndex,
                currentPlayerPosition,
                out movementDirection))
        {
            return false;
        }

        IsResolved = true;
        PlayerDodged = true;
        return true;
    }

    public bool ResolveAtImpact(
        EnemyPlayerDodgeWindowState windowState,
        bool playerIsThreatened,
        int currentPlayerTileIndex,
        Vector3 currentPlayerPosition,
        out int movementDirection)
    {
        movementDirection = 0;

        if (IsResolved)
        {
            return false;
        }

        IsResolved = true;
        PlayerDodged = windowState.TryResolveDodge(
            playerIsThreatened,
            currentPlayerTileIndex,
            currentPlayerPosition,
            out movementDirection);
        return PlayerDodged;
    }
}
