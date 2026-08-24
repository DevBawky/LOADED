using System;

internal readonly struct DuelClockSnapshot
{
    public DuelClockSnapshot(double progress, long cumulativeBeats)
    {
        Progress = progress;
        CumulativeBeats = cumulativeBeats;
    }

    public double Progress { get; }
    public long CumulativeBeats { get; }
}

internal readonly struct DuelClockAdvanceResult
{
    public DuelClockAdvanceResult(
        DuelClockSnapshot before,
        DuelClockSnapshot after,
        long triggeredBeatCount,
        double addedProgress)
    {
        Before = before;
        After = after;
        TriggeredBeatCount = triggeredBeatCount;
        AddedProgress = addedProgress;
    }

    public DuelClockSnapshot Before { get; }
    public DuelClockSnapshot After { get; }
    public long TriggeredBeatCount { get; }
    public double AddedProgress { get; }
}

internal sealed class DuelClockState
{
    public const double CycleLength = 100d;
    private const double LongOverflowThreshold = 9223372036854775808d;

    private double progress;
    private long cumulativeBeats;

    public DuelClockSnapshot Snapshot =>
        new DuelClockSnapshot(progress, cumulativeBeats);

    public DuelClockState()
    {
    }

    public static DuelClockState Restore(
        double savedProgress,
        long savedCumulativeBeats)
    {
        ValidateProgress(savedProgress, nameof(savedProgress));

        if (savedCumulativeBeats < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(savedCumulativeBeats),
                savedCumulativeBeats,
                "Cumulative beats cannot be negative.");
        }

        DuelClockAdvanceResult normalized = Calculate(
            new DuelClockSnapshot(0d, savedCumulativeBeats),
            savedProgress);
        return new DuelClockState(normalized.After);
    }

    public DuelClockAdvanceResult Preview(double addedProgress)
    {
        return Calculate(Snapshot, addedProgress);
    }

    public DuelClockAdvanceResult Commit(double addedProgress)
    {
        DuelClockAdvanceResult result = Calculate(Snapshot, addedProgress);
        progress = result.After.Progress;
        cumulativeBeats = result.After.CumulativeBeats;
        return result;
    }

    public double Reduce(double removedProgress)
    {
        ValidateProgress(removedProgress, nameof(removedProgress));
        double previousProgress = progress;
        progress = Math.Max(0d, progress - removedProgress);
        return previousProgress - progress;
    }

    private DuelClockState(DuelClockSnapshot snapshot)
    {
        progress = snapshot.Progress;
        cumulativeBeats = snapshot.CumulativeBeats;
    }

    private static DuelClockAdvanceResult Calculate(
        DuelClockSnapshot before,
        double addedProgress)
    {
        ValidateProgress(addedProgress, nameof(addedProgress));

        double totalProgress = before.Progress + addedProgress;
        double triggeredBeatValue = Math.Floor(totalProgress / CycleLength);

        if (triggeredBeatValue >= LongOverflowThreshold)
        {
            throw new OverflowException(
                "The duel clock beat count exceeds its supported range.");
        }

        long triggeredBeatCount = (long)triggeredBeatValue;

        if (triggeredBeatCount
            > long.MaxValue - before.CumulativeBeats)
        {
            throw new OverflowException(
                "The duel clock beat count exceeds its supported range.");
        }

        long nextCumulativeBeats =
            before.CumulativeBeats + triggeredBeatCount;
        double nextProgress = totalProgress % CycleLength;
        DuelClockSnapshot after = new DuelClockSnapshot(
            nextProgress,
            nextCumulativeBeats);
        return new DuelClockAdvanceResult(
            before,
            after,
            triggeredBeatCount,
            addedProgress);
    }

    private static void ValidateProgress(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Duel clock progress must be finite and nonnegative.");
        }
    }
}
