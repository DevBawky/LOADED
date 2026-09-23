using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DuelClockController : MonoBehaviour
{
    private const double EnemyDefeatReductionDivisor = 4d;
    internal const double NaturalProgressSpeedMultiplier = 1.35d;
    internal const double SpawnProgressRateMultiplier = 0.5d;
    private const int NaturalProgressBaselineEnemyCount = 3;
    private const double NaturalProgressRateStepPerEnemy = 0.3d;

    private PlayerMove playerMove;
    private PlayerShoot playerShoot;
    private WaveManager waveManager;
    private DuelClockState state = new DuelClockState();
    private DuelClockState spawnState = new DuelClockState();
    private CombatPacingMode pacingMode = CombatPacingMode.Legacy;
    private double naturalProgressPerSecond;
    private double paidActionProgress;
    private bool shootProgressCommitted;
    private bool hasReservedBeat;
    private bool playerActionPending;
    private bool paidActionProgressSuppressed;
    private bool subscribedToCombatEvents;

    public event Action StateChanged;
    public event Action<long> BeatsCommitted;
    public event Action<long> SpawnCyclesCommitted;

    public bool IsActive => pacingMode == CombatPacingMode.DuelClock;
    public CombatPacingMode PacingMode => pacingMode;
    public double Progress => hasReservedBeat
        ? DuelClockState.CycleLength
        : state.Snapshot.Progress;
    public long CumulativeBeats => state.Snapshot.CumulativeBeats;
    public double SpawnProgress => waveManager != null
        && waveManager.IsDuelClockEnemySpawnPoolExhausted
            ? 0d
            : spawnState.Snapshot.Progress;
    public long CumulativeSpawnCycles =>
        spawnState.Snapshot.CumulativeBeats;
    internal DuelClockSnapshot Snapshot => state.Snapshot;
    internal bool HasReservedBeat => IsActive && hasReservedBeat;

    internal void Initialize(
        PlayerMove assignedPlayerMove,
        WaveManager assignedWaveManager)
    {
        UnsubscribeFromCombatEvents();
        playerMove = assignedPlayerMove;
        playerShoot = playerMove == null
            ? null
            : playerMove.GetComponent<PlayerShoot>();
        waveManager = assignedWaveManager;
        SubscribeToCombatEvents();
    }

    internal void ConfigureFresh(
        BattleData battleData,
        CombatPacingMode configuredMode)
    {
        ConfigureSettings(battleData, configuredMode);
        state = new DuelClockState();
        spawnState = new DuelClockState();
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
                saveData.duelClockProgress,
                saveData.duelClockCumulativeBeats)
            : new DuelClockState();
        spawnState = IsActive && saveData != null
            ? RestoreStateOrDefault(
                saveData.duelClockSpawnProgress,
                saveData.duelClockCumulativeSpawns)
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
        DuelClockSnapshot spawnSnapshot = spawnState.Snapshot;
        saveData.duelClockSpawnProgress = SpawnProgress;
        saveData.duelClockCumulativeSpawns =
            spawnSnapshot.CumulativeBeats;
    }

    internal DuelClockAdvanceResult PreviewPaidAction()
    {
        return state.Preview(
            IsActive && !hasReservedBeat ? paidActionProgress : 0d);
    }

    internal DuelClockAdvanceResult PreviewFreeAction()
    {
        return state.Preview(0d);
    }

    internal bool TryAdvanceNaturalTime(double elapsedSeconds)
    {
        if (!CanAdvanceSpawnGaugeNaturally()
            || double.IsNaN(elapsedSeconds)
            || double.IsInfinity(elapsedSeconds)
            || elapsedSeconds <= 0d
            || naturalProgressPerSecond <= 0d)
        {
            return false;
        }

        double naturalProgressMultiplier =
            CalculateNaturalProgressMultiplier(waveManager.LivingEnemyCount);
        return TryCommitNaturalProgress(
            naturalProgressPerSecond
            * naturalProgressMultiplier
            * elapsedSeconds);
    }

    internal void Deactivate()
    {
        pacingMode = CombatPacingMode.Legacy;
        naturalProgressPerSecond = 0d;
        paidActionProgress = 0d;
        state = new DuelClockState();
        spawnState = new DuelClockState();
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
        TryAdvanceNaturalTime(Time.unscaledDeltaTime);
    }

    private void HandlePlayerTurnCompleted()
    {
        bool shouldCommitPaidAction = !shootProgressCommitted
            && !paidActionProgressSuppressed;
        ResetPlayerActionTracking();

        if (IsActive && shouldCommitPaidAction)
        {
            TryCommitProgress(paidActionProgress);
        }
    }

    internal void HandlePlayerActionStarted(PlayerBehaviourAction action)
    {
        ResetPlayerActionTracking();
        playerActionPending = true;

        if (IsActive && action == PlayerBehaviourAction.Shoot)
        {
            shootProgressCommitted = TryCommitProgress(
                paidActionProgress);
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

    private void HandleEnemyDefeated(EnemyController enemy)
    {
        ApplyEnemyDefeat();
    }

    internal bool ApplyEnemyDefeat()
    {
        if (!IsActive)
        {
            return false;
        }

        return TryReduceProgress(
            CalculateEnemyDefeatReduction(paidActionProgress));
    }

    internal static double CalculateEnemyDefeatReduction(
        double configuredPaidActionProgress)
    {
        if (double.IsNaN(configuredPaidActionProgress)
            || double.IsInfinity(configuredPaidActionProgress)
            || configuredPaidActionProgress <= 0d)
        {
            return 0d;
        }

        return configuredPaidActionProgress
            / EnemyDefeatReductionDivisor;
    }

    internal static double CalculateNaturalProgressMultiplier(
        int livingEnemyCount)
    {
        int sanitizedEnemyCount = Math.Max(0, livingEnemyCount);
        double multiplier = 1d
            + (NaturalProgressBaselineEnemyCount - sanitizedEnemyCount)
            * NaturalProgressRateStepPerEnemy;
        return Math.Max(0d, multiplier);
    }

    internal void HandleEnemyCycleStarted()
    {
        if (!hasReservedBeat)
        {
            return;
        }

        hasReservedBeat = false;
        StateChanged?.Invoke();
    }

    private bool TryCommitProgress(double addedProgress)
    {
        if (hasReservedBeat)
        {
            return false;
        }

        DuelClockAdvanceResult result;
        DuelClockAdvanceResult spawnResult;

        try
        {
            double acceptedProgress = Math.Min(
                addedProgress,
                DuelClockState.CycleLength - state.Snapshot.Progress);
            DuelClockAdvanceResult preview = state.Preview(
                acceptedProgress);
            double spawnProgress = CanAdvanceSpawnGauge()
                ? preview.AddedProgress * SpawnProgressRateMultiplier
                : 0d;
            spawnState.Preview(spawnProgress);
            result = state.Commit(preview.AddedProgress);
            spawnResult = spawnState.Commit(spawnProgress);
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

        if (spawnResult.TriggeredBeatCount > 0)
        {
            SpawnCyclesCommitted?.Invoke(
                spawnResult.TriggeredBeatCount);
        }

        if (result.TriggeredBeatCount > 0)
        {
            BeatsCommitted?.Invoke(result.TriggeredBeatCount);
        }

        return true;
    }

    private bool TryCommitNaturalProgress(double addedProgress)
    {
        if (addedProgress <= 0d)
        {
            return false;
        }

        bool advanceClock = CanAdvanceNaturally() && !hasReservedBeat;
        bool advanceSpawnGauge = CanAdvanceSpawnGauge();

        if (!advanceClock && !advanceSpawnGauge)
        {
            return false;
        }

        DuelClockAdvanceResult clockResult = default;
        DuelClockAdvanceResult spawnResult = default;

        try
        {
            DuelClockAdvanceResult clockPreview = default;
            DuelClockAdvanceResult spawnPreview = default;

            if (advanceClock)
            {
                double acceptedProgress = Math.Min(
                    addedProgress,
                    DuelClockState.CycleLength - state.Snapshot.Progress);
                clockPreview = state.Preview(
                    acceptedProgress);
            }

            if (advanceSpawnGauge)
            {
                double spawnProgress = addedProgress
                    * SpawnProgressRateMultiplier;
                spawnPreview = spawnState.Preview(spawnProgress);
            }

            if (advanceClock)
            {
                clockResult = state.Commit(clockPreview.AddedProgress);
            }

            if (advanceSpawnGauge)
            {
                spawnResult = spawnState.Commit(
                    spawnPreview.AddedProgress);
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }

        if (advanceClock && clockResult.TriggeredBeatCount > 0)
        {
            hasReservedBeat = true;
        }

        StateChanged?.Invoke();

        if (advanceSpawnGauge && spawnResult.TriggeredBeatCount > 0)
        {
            SpawnCyclesCommitted?.Invoke(
                spawnResult.TriggeredBeatCount);
        }

        if (advanceClock && clockResult.TriggeredBeatCount > 0)
        {
            BeatsCommitted?.Invoke(clockResult.TriggeredBeatCount);
        }

        return true;
    }

    private bool CanAdvanceSpawnGauge()
    {
        return waveManager == null
            || !waveManager.IsSpawnGaugePaused;
    }

    private bool CanAdvanceSpawnGaugeNaturally()
    {
        return ShouldAdvanceSpawnGaugeNaturally(
            IsActive,
            isActiveAndEnabled,
            GamePauseController.IsPaused,
            playerMove != null,
            waveManager != null,
            waveManager != null && waveManager.IsBattleCompleted,
            FirstRunGuideController.IsGuidePanelOpen);
    }

    private bool TryReduceProgress(double removedProgress)
    {
        double actualReduction;

        try
        {
            actualReduction = state.Reduce(removedProgress);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        if (actualReduction <= 0d)
        {
            return false;
        }

        StateChanged?.Invoke();
        return true;
    }

    private bool CanAdvanceNaturally()
    {
        return ShouldAdvanceNaturalClock(
            IsActive,
            isActiveAndEnabled,
            GamePauseController.IsPaused,
            playerMove != null,
            waveManager != null,
            waveManager != null && waveManager.IsBattleCompleted,
            FirstRunGuideController.IsGuidePanelOpen,
            playerShoot != null && playerShoot.IsFiring);
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
        return isActive && componentEnabled && !gamePaused
            && !guidePanelOpen && !playerFiring
            && hasPlayerMove && hasWaveManager
            && !battleCompleted;
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
        return isActive && componentEnabled && !gamePaused
            && !guidePanelOpen && hasPlayerMove && hasWaveManager
            && !battleCompleted;
    }

    private void ConfigureSettings(
        BattleData battleData,
        CombatPacingMode configuredMode)
    {
        pacingMode = battleData != null
            && configuredMode == CombatPacingMode.DuelClock
                ? CombatPacingMode.DuelClock
                : CombatPacingMode.Legacy;
        naturalProgressPerSecond = IsActive
            ? SanitizeProgress(battleData.DuelClockNaturalProgressPerSecond)
                * NaturalProgressSpeedMultiplier
            : 0d;
        paidActionProgress = IsActive
            ? SanitizeProgress(battleData.DuelClockPaidActionProgress)
            : 0d;
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

        if (waveManager != null)
        {
            waveManager.EnemyDefeated += HandleEnemyDefeated;
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

        if (waveManager != null)
        {
            waveManager.EnemyDefeated -= HandleEnemyDefeated;
        }

        subscribedToCombatEvents = false;
    }

    private void ResetPlayerActionTracking()
    {
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

    private static double SanitizeProgress(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value)
            ? 0d
            : Math.Max(0d, value);
    }
}
