using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DuelClockController : MonoBehaviour
{
    internal const double NaturalProgressSpeedMultiplier = 1.35d;
    internal const double SpawnProgressRateMultiplier = 0.5d;
    public const int TempoCapacity = 6;
    public const int MovementTempoCost = 2;
    public const int RotationTempoCost = 1;
    public const int WaitTempoCost = 1;
    public const int ReloadTempoCost = 3;
    public const int ShootTempoCost = 3;

    private const double ProgressPerTempo =
        DuelClockState.CycleLength / TempoCapacity;

    [Header("References")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerShoot playerShoot;
    [SerializeField] private WaveManager waveManager;

    private DuelClockState state = new DuelClockState();
    private CombatPacingMode pacingMode = CombatPacingMode.Legacy;
    private int pendingActionTempoCost;
    private int deferredTempoCost;
    private bool shootProgressCommitted;
    private bool hasReservedBeat;
    private bool playerActionPending;
    private bool paidActionProgressSuppressed;
    private bool subscribedToCombatEvents;

    public event Action StateChanged;
    public event Action<long> BeatsCommitted;

    // Retained for source compatibility. Reinforcements are now scheduled by
    // WaveManager after a complete Cylinder Tempo enemy cycle.
    public event Action<long> SpawnCyclesCommitted;

    public bool IsActive => pacingMode == CombatPacingMode.DuelClock;
    public CombatPacingMode PacingMode => pacingMode;
    public double Progress => hasReservedBeat
        ? DuelClockState.CycleLength
        : state.Snapshot.Progress;
    public double TempoProgress => hasReservedBeat
        ? TempoCapacity
        : ConvertProgressToTempo(state.Snapshot.Progress);
    public long CumulativeBeats => state.Snapshot.CumulativeBeats;
    public double SpawnProgress => 0d;
    public long CumulativeSpawnCycles => 0L;
    public bool IsTempoCycleReserved => IsActive && hasReservedBeat;
    internal DuelClockSnapshot Snapshot => state.Snapshot;
    internal bool HasReservedBeat => IsTempoCycleReserved;

    internal void Initialize(
        PlayerMove assignedPlayerMove,
        WaveManager assignedWaveManager)
    {
        UnsubscribeFromCombatEvents();

        if (assignedPlayerMove != null)
        {
            playerMove = assignedPlayerMove;
        }

        if (assignedWaveManager != null)
        {
            waveManager = assignedWaveManager;
        }

        if (playerShoot == null && playerMove != null)
        {
            playerShoot = playerMove.GetComponent<PlayerShoot>();
        }

        SubscribeToCombatEvents();
    }

    internal void ConfigureFresh(
        BattleData battleData,
        CombatPacingMode configuredMode)
    {
        ConfigureSettings(battleData, configuredMode);
        state = new DuelClockState();
        hasReservedBeat = false;
        deferredTempoCost = 0;
        ResetPlayerActionTracking();
        playerMove?.SetDuelClockActive(IsActive);
        StateChanged?.Invoke();
    }

    internal void ConfigureRestored(
        BattleData battleData,
        CombatPacingMode configuredMode,
        RunSaveData saveData)
    {
        ConfigureSettings(battleData, configuredMode);
        state = IsActive && saveData != null
            ? RestoreStateOrDefault(
                saveData.duelClockProgress,
                saveData.duelClockCumulativeBeats)
            : new DuelClockState();
        hasReservedBeat = false;
        deferredTempoCost = 0;
        ResetPlayerActionTracking();
        playerMove?.SetDuelClockActive(IsActive);
        StateChanged?.Invoke();
    }

    internal void CaptureRunState(RunSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        saveData.combatPacingMode = (int)pacingMode;

        if (!IsActive)
        {
            saveData.duelClockProgress = 0d;
            saveData.duelClockCumulativeBeats = 0;
            saveData.duelClockSpawnProgress = 0d;
            saveData.duelClockCumulativeSpawns = 0;
            return;
        }

        DuelClockSnapshot snapshot = state.Snapshot;
        saveData.duelClockProgress = snapshot.Progress;
        saveData.duelClockCumulativeBeats = snapshot.CumulativeBeats;
        saveData.duelClockSpawnProgress = 0d;
        saveData.duelClockCumulativeSpawns = 0;
    }

    internal DuelClockAdvanceResult PreviewPaidAction()
    {
        return PreviewAction(PlayerBehaviourAction.Shoot);
    }

    internal DuelClockAdvanceResult PreviewAction(
        PlayerBehaviourAction action)
    {
        double progress = IsActive && !hasReservedBeat
            ? ConvertTempoToProgress(GetTempoCost(action))
            : 0d;
        return state.Preview(progress);
    }

    internal DuelClockAdvanceResult PreviewFreeAction()
    {
        return state.Preview(0d);
    }

    internal bool TryAdvanceNaturalTime(double elapsedSeconds)
    {
        return false;
    }

    internal void Deactivate()
    {
        pacingMode = CombatPacingMode.Legacy;
        state = new DuelClockState();
        hasReservedBeat = false;
        deferredTempoCost = 0;
        ResetPlayerActionTracking();
        playerMove?.SetDuelClockActive(false);
        StateChanged?.Invoke();
    }

    private void OnEnable()
    {
        SubscribeToCombatEvents();
        playerMove?.SetDuelClockActive(IsActive);
    }

    private void OnDisable()
    {
        UnsubscribeFromCombatEvents();
        playerMove?.SetDuelClockActive(false);
    }

    private void HandlePlayerTurnCompleted()
    {
        int completedActionCost = pendingActionTempoCost;
        bool shouldCommitPaidAction = playerActionPending
            && !shootProgressCommitted
            && !paidActionProgressSuppressed
            && completedActionCost > 0;
        ResetPlayerActionTracking();

        if (!IsActive || !shouldCommitPaidAction)
        {
            return;
        }

        if (hasReservedBeat)
        {
            AddDeferredTempo(completedActionCost);
            return;
        }

        TryCommitTempo(completedActionCost);
    }

    internal void HandlePlayerActionStarted(PlayerBehaviourAction action)
    {
        ResetPlayerActionTracking();
        playerActionPending = true;
        pendingActionTempoCost = GetTempoCost(action);

        if (IsActive && action == PlayerBehaviourAction.Shoot)
        {
            shootProgressCommitted = TryCommitTempo(
                pendingActionTempoCost);
        }
    }

    internal void HandlePlayerDodgeSucceededDuringAction()
    {
        if (!IsActive || !playerActionPending || shootProgressCommitted)
        {
            return;
        }

        paidActionProgressSuppressed = true;
    }

    internal bool ApplyEnemyDefeat()
    {
        return false;
    }

    internal static double CalculateEnemyDefeatReduction(
        double configuredPaidActionProgress)
    {
        return 0d;
    }

    internal static double CalculateNaturalProgressMultiplier(
        int livingEnemyCount)
    {
        return 0d;
    }

    internal void HandleEnemyCycleStarted()
    {
        // Keep all six slots lit while enemies resolve their actions.
    }

    internal void HandleEnemyCycleCompleted()
    {
        if (!hasReservedBeat)
        {
            return;
        }

        hasReservedBeat = false;
        int carriedActionCost = deferredTempoCost;
        deferredTempoCost = 0;

        if (carriedActionCost > 0
            && TryCommitTempo(carriedActionCost))
        {
            return;
        }

        StateChanged?.Invoke();
    }

    internal static int GetTempoCost(PlayerBehaviourAction action)
    {
        return action switch
        {
            PlayerBehaviourAction.MoveLeft => MovementTempoCost,
            PlayerBehaviourAction.MoveRight => MovementTempoCost,
            PlayerBehaviourAction.MoveUp => MovementTempoCost,
            PlayerBehaviourAction.MoveDown => MovementTempoCost,
            PlayerBehaviourAction.Rotate => RotationTempoCost,
            PlayerBehaviourAction.Wait => WaitTempoCost,
            PlayerBehaviourAction.Reload => ReloadTempoCost,
            PlayerBehaviourAction.Shoot => ShootTempoCost,
            _ => 0
        };
    }

    private bool TryCommitTempo(int tempoCost)
    {
        if (!IsActive || tempoCost <= 0)
        {
            return false;
        }

        if (hasReservedBeat)
        {
            AddDeferredTempo(tempoCost);
            return true;
        }

        DuelClockAdvanceResult result;

        try
        {
            result = state.Commit(ConvertTempoToProgress(tempoCost));
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }

        if (result.TriggeredBeatCount > 0)
        {
            hasReservedBeat = true;
        }

        StateChanged?.Invoke();

        if (result.TriggeredBeatCount > 0)
        {
            BeatsCommitted?.Invoke(result.TriggeredBeatCount);
        }

        return true;
    }

    private void AddDeferredTempo(int tempoCost)
    {
        int acceptedCost = Mathf.Max(0, tempoCost);
        deferredTempoCost = acceptedCost > int.MaxValue - deferredTempoCost
            ? int.MaxValue
            : deferredTempoCost + acceptedCost;
    }

    private void ConfigureSettings(
        BattleData battleData,
        CombatPacingMode configuredMode)
    {
        pacingMode = battleData != null
            && configuredMode == CombatPacingMode.DuelClock
                ? CombatPacingMode.DuelClock
                : CombatPacingMode.Legacy;
    }

    private void SubscribeToCombatEvents()
    {
        if (subscribedToCombatEvents || !isActiveAndEnabled)
        {
            return;
        }

        if (playerMove != null)
        {
            playerMove.BehaviourActionStarted += HandlePlayerActionStarted;
            playerMove.DodgeSucceededDuringAction +=
                HandlePlayerDodgeSucceededDuringAction;
            playerMove.TurnCompleted += HandlePlayerTurnCompleted;
        }

        if (playerShoot != null)
        {
            playerShoot.BehaviourActionStarted += HandlePlayerActionStarted;
        }

        subscribedToCombatEvents = true;
    }

    private void UnsubscribeFromCombatEvents()
    {
        if (!subscribedToCombatEvents)
        {
            return;
        }

        if (playerMove != null)
        {
            playerMove.BehaviourActionStarted -= HandlePlayerActionStarted;
            playerMove.DodgeSucceededDuringAction -=
                HandlePlayerDodgeSucceededDuringAction;
            playerMove.TurnCompleted -= HandlePlayerTurnCompleted;
        }

        if (playerShoot != null)
        {
            playerShoot.BehaviourActionStarted -= HandlePlayerActionStarted;
        }

        subscribedToCombatEvents = false;
    }

    private void ResetPlayerActionTracking()
    {
        pendingActionTempoCost = 0;
        shootProgressCommitted = false;
        playerActionPending = false;
        paidActionProgressSuppressed = false;
    }

    private static DuelClockState RestoreStateOrDefault(
        double savedProgress,
        long savedCumulativeBeats)
    {
        try
        {
            return DuelClockState.Restore(
                savedProgress,
                savedCumulativeBeats);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new DuelClockState();
        }
        catch (OverflowException)
        {
            return new DuelClockState();
        }
    }

    private static double ConvertTempoToProgress(int tempoCost)
    {
        return Math.Max(0, tempoCost) * ProgressPerTempo;
    }

    private static double ConvertProgressToTempo(double progress)
    {
        return Math.Max(0d, progress) / ProgressPerTempo;
    }

    internal static bool ShouldAdvanceNaturalClock(
        bool isActive,
        bool componentEnabled,
        bool gamePaused,
        bool hasPlayerMove,
        bool hasWaveManager,
        bool battleCompleted,
        bool guidePanelOpen = false,
        bool playerFiring = false)
    {
        return false;
    }

    internal static bool ShouldAdvanceSpawnGaugeNaturally(
        bool isActive,
        bool componentEnabled,
        bool gamePaused,
        bool hasPlayerMove,
        bool hasWaveManager,
        bool battleCompleted,
        bool guidePanelOpen = false)
    {
        return false;
    }
}
