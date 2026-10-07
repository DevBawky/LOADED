using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DuelClockController : MonoBehaviour
{
    internal const double NaturalProgressSpeedMultiplier = 1.35d;
    internal const double SpawnProgressRateMultiplier = 0.5d;
    public const int TempoCapacity = 6;
    // Legacy names/scale remain for serialized HUD and save compatibility.
    // Every completed paid action now commits exactly one enemy cycle.
    public const int MovementTempoCost = TempoCapacity;
    public const int RotationTempoCost = TempoCapacity;
    public const int WaitTempoCost = TempoCapacity;
    public const int ReloadTempoCost = TempoCapacity;
    public const int ShootTempoCost = TempoCapacity;

    private const double ProgressPerTempo =
        DuelClockState.CycleLength / TempoCapacity;

    [Header("References")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerShoot playerShoot;
    [SerializeField] private WaveManager waveManager;

    private DuelClockState state = new DuelClockState();
    private CombatPacingMode pacingMode = CombatPacingMode.Legacy;
    private int pendingActionTempoCost;
    private bool hasReservedBeat;
    private bool playerActionPending;
    private bool paidActionProgressSuppressed;
    private bool subscribedToCombatEvents;

    public event Action StateChanged;
    public event Action<long> BeatsCommitted;

    // Retained for source compatibility. Reinforcements are now scheduled by
    // WaveManager after a complete enemy action cycle.
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
    internal bool HasPendingPaidAction => IsActive
        && playerActionPending
        && !paidActionProgressSuppressed
        && pendingActionTempoCost > 0;
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
                0d,
                saveData.duelClockCumulativeBeats)
            : new DuelClockState();
        hasReservedBeat = false;
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

    internal bool TryCommitTestCycle()
    {
        return waveManager != null && waveManager.IsTestBattle
            && !hasReservedBeat
            && TryCommitTempo(Mathf.Max(1,
                TempoCapacity - (int)Math.Round(TempoProgress)));
    }

    internal void Deactivate()
    {
        pacingMode = CombatPacingMode.Legacy;
        state = new DuelClockState();
        hasReservedBeat = false;
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

    private void Update()
    {
        // With no natural clock, stun must explicitly pass a player action.
        if (!IsActive || hasReservedBeat || GamePauseController.IsPaused
            || playerMove == null || !playerMove.IsStunned || playerMove.IsInputLocked
            || playerMove.IsActing || playerMove.IsShooting || waveManager == null
            || waveManager.IsBattleCompleted || waveManager.IsResolvingTurn
            || LoadingTransitionController.IsTransitioning)
            return;
        HandlePlayerActionStarted(PlayerBehaviourAction.Wait);
        playerMove.CompleteTurn();
    }

    private void HandlePlayerTurnCompleted()
    {
        int completedActionCost = pendingActionTempoCost;
        bool shouldCommitPaidAction = playerActionPending
            && !paidActionProgressSuppressed
            && completedActionCost > 0;
        ResetPlayerActionTracking();

        if (!IsActive || !shouldCommitPaidAction)
        {
            return;
        }

        if (hasReservedBeat)
        {
            return;
        }

        TryCommitTempo(completedActionCost);
    }

    internal void HandlePlayerActionStarted(PlayerBehaviourAction action)
    {
        ResetPlayerActionTracking();
        playerActionPending = !hasReservedBeat;
        pendingActionTempoCost = GetTempoCost(action);

        if (HasPendingPaidAction)
        {
            StateChanged?.Invoke();
        }
    }

    internal void HandlePlayerDodgeSucceededDuringAction()
    {
        if (!IsActive || !playerActionPending)
        {
            return;
        }

        paidActionProgressSuppressed = true;
        StateChanged?.Invoke();
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
        // The reservation remains active until every enemy action settles.
    }

    internal void HandleEnemyCycleCompleted()
    {
        if (!hasReservedBeat)
        {
            return;
        }

        hasReservedBeat = false;
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
            return false;
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
