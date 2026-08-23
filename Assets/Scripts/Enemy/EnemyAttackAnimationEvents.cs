using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAttackAnimationEvents : MonoBehaviour
{
    public const string BeginFunctionName = nameof(BeginAttackActiveWindow);
    public const string EndFunctionName = nameof(EndAttackActiveWindow);

    private EnemyController owner;

    internal void Initialize(EnemyController assignedOwner)
    {
        owner = assignedOwner;
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
            owner.SetAttackActiveWindowOpen(false);
        }
    }

    private void OnDisable()
    {
        if (owner != null)
        {
            owner.SetAttackActiveWindowOpen(false);
        }
    }
}

internal readonly struct EnemyAttackActiveWindowTiming
{
    private EnemyAttackActiveWindowTiming(
        float startNormalizedTime,
        float endNormalizedTime)
    {
        StartNormalizedTime = startNormalizedTime;
        EndNormalizedTime = endNormalizedTime;
    }

    public float StartNormalizedTime { get; }
    public float EndNormalizedTime { get; }

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

        timing = new EnemyAttackActiveWindowTiming(
            Mathf.Clamp01(startTime / clip.length),
            Mathf.Clamp01(endTime / clip.length));
        return timing.EndNormalizedTime > timing.StartNormalizedTime;
    }
}
