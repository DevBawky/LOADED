using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DuelClockController : MonoBehaviour
{
    private PlayerMove playerMove;
    private WaveManager waveManager;
    private DuelClockState state = new DuelClockState();
    private CombatPacingMode pacingMode = CombatPacingMode.Legacy;
    private double naturalProgressPerSecond;
    private double paidActionProgress;
    private int enemyWaveCount = 5;
    private bool subscribedToPlayer;

    public event Action StateChanged;
    public event Action<long> BeatsCommitted;

    public bool IsActive => pacingMode == CombatPacingMode.DuelClock;
    public CombatPacingMode PacingMode => pacingMode;
    public double Progress => state.Snapshot.Progress;
    public long CumulativeBeats => state.Snapshot.CumulativeBeats;
    public int EnemyWaveCount => enemyWaveCount;
    public int EnemyWaveProgress => IsActive
        ? (int)(CumulativeBeats % enemyWaveCount)
        : 0;
    internal DuelClockSnapshot Snapshot => state.Snapshot;

    internal void Initialize(
        PlayerMove assignedPlayerMove,
        WaveManager assignedWaveManager)
    {
        UnsubscribeFromPlayer();
        playerMove = assignedPlayerMove;
        waveManager = assignedWaveManager;
        SubscribeToPlayer();
    }

    internal void ConfigureFresh(
        BattleData battleData,
        CombatPacingMode configuredMode)
    {
        ConfigureSettings(battleData, configuredMode);
        state = new DuelClockState();
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
            return;
        }

        DuelClockSnapshot snapshot = state.Snapshot;
        saveData.duelClockProgress = snapshot.Progress;
        saveData.duelClockCumulativeBeats = snapshot.CumulativeBeats;
    }

    internal DuelClockAdvanceResult PreviewPaidAction()
    {
        return state.Preview(IsActive ? paidActionProgress : 0d);
    }

    internal DuelClockAdvanceResult PreviewFreeAction()
    {
        return state.Preview(0d);
    }

    internal bool TryAdvanceNaturalTime(double elapsedSeconds)
    {
        if (!CanAdvanceNaturally()
            || double.IsNaN(elapsedSeconds)
            || double.IsInfinity(elapsedSeconds)
            || elapsedSeconds <= 0d
            || naturalProgressPerSecond <= 0d)
        {
            return false;
        }

        return TryCommitProgress(naturalProgressPerSecond * elapsedSeconds);
    }

    internal void Deactivate()
    {
        pacingMode = CombatPacingMode.Legacy;
        naturalProgressPerSecond = 0d;
        paidActionProgress = 0d;
        enemyWaveCount = 5;
        state = new DuelClockState();
        playerMove?.SetDuelClockActive(false);
        StateChanged?.Invoke();
    }

    private void OnEnable()
    {
        SubscribeToPlayer();
        playerMove?.SetDuelClockActive(IsActive);
    }

    private void OnDisable()
    {
        UnsubscribeFromPlayer();
        playerMove?.SetDuelClockActive(false);
    }

    private void Update()
    {
        TryAdvanceNaturalTime(Time.unscaledDeltaTime);
    }

    private void HandlePlayerTurnCompleted()
    {
        if (IsActive)
        {
            TryCommitProgress(paidActionProgress);
        }
    }

    private bool TryCommitProgress(double addedProgress)
    {
        DuelClockAdvanceResult result;

        try
        {
            result = state.Commit(addedProgress);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }

        StateChanged?.Invoke();

        if (result.TriggeredBeatCount > 0)
        {
            BeatsCommitted?.Invoke(result.TriggeredBeatCount);
        }

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
            FirstRunGuideController.IsGuidePanelOpen);
    }

    internal static bool ShouldAdvanceNaturalClock(
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
            : 0d;
        paidActionProgress = IsActive
            ? SanitizeProgress(battleData.DuelClockPaidActionProgress)
            : 0d;
        enemyWaveCount = IsActive
            ? battleData.DuelClockEnemyWaveCount
            : 5;
    }

    private void SubscribeToPlayer()
    {
        if (subscribedToPlayer || playerMove == null || !isActiveAndEnabled)
        {
            return;
        }

        playerMove.TurnCompleted += HandlePlayerTurnCompleted;
        subscribedToPlayer = true;
    }

    private void UnsubscribeFromPlayer()
    {
        if (!subscribedToPlayer)
        {
            return;
        }

        if (playerMove != null)
        {
            playerMove.TurnCompleted -= HandlePlayerTurnCompleted;
        }

        subscribedToPlayer = false;
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
