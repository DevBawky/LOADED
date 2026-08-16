using System;

public enum RelicCombatEventType
{
    BattleStarted = 0,
    BattleEnded = 1,
    PlayerMoved = 2,
    ShotStarted = 3,
    ShotCompleted = 4,
    CylinderCompleted = 5,
    LethalDamageIncoming = 6,
    RelicTriggered = 7
}

public readonly struct PlayerMovementContext
{
    public PlayerMovementContext(
        int startTileIndex,
        int endTileIndex,
        int distance,
        PlayerMovementSource source)
    {
        StartTileIndex = startTileIndex;
        EndTileIndex = endTileIndex;
        Distance = Math.Max(0, distance);
        Source = source;
    }

    public int StartTileIndex { get; }
    public int EndTileIndex { get; }
    public int Distance { get; }
    public PlayerMovementSource Source { get; }
}

public readonly struct RelicCombatEventContext
{
    public RelicCombatEventContext(
        RelicCombatEventType eventType,
        PlayerMovementContext movement = default,
        long amount = 0L,
        bool isPreview = false)
    {
        EventType = eventType;
        Movement = movement;
        Amount = Math.Max(0L, amount);
        IsPreview = isPreview;
    }

    public RelicCombatEventType EventType { get; }
    public PlayerMovementContext Movement { get; }
    public long Amount { get; }
    public bool IsPreview { get; }
}

public enum RelicAcquireResult
{
    Acquired = 0,
    Stacked = 1,
    InventoryFull = 2,
    Duplicate = 3,
    InvalidData = 4
}

public enum RelicRemovalReason
{
    Replaced = 0,
    Consumed = 1,
    Removed = 2
}
