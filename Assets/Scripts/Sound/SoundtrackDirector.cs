using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Selects the soundtrack for the active scene and game-flow state. Audio
/// playback remains owned by <see cref="SoundManager"/>.
/// </summary>
internal sealed class SoundtrackDirector : IDisposable
{
    private readonly SoundManager soundManager;
    private StateManager observedStateManager;

    public SoundtrackDirector(SoundManager soundManager)
    {
        this.soundManager = soundManager;
    }

    public void RefreshForScene(Scene scene)
    {
        soundManager.UnlockGameOverBgm();
        ObserveStateManager(UnityEngine.Object.FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include));

        if (observedStateManager != null)
        {
            RefreshForGameState();
            return;
        }

        SoundClipLibrary library = soundManager.ClipLibrary;
        soundManager.PlayPlaylist(scene.name.IndexOf(
            "MainMenu",
            StringComparison.OrdinalIgnoreCase) >= 0
                ? library?.MainMenuBgm
                : null);
    }

    public void Dispose()
    {
        ObserveStateManager(null);
    }

    private void ObserveStateManager(StateManager stateManager)
    {
        if (observedStateManager == stateManager)
        {
            return;
        }

        if (observedStateManager != null)
        {
            observedStateManager.StateChanged -= RefreshForGameState;
        }

        observedStateManager = stateManager;

        if (observedStateManager != null)
        {
            observedStateManager.StateChanged += RefreshForGameState;
        }
    }

    private void RefreshForGameState()
    {
        if (soundManager.IsGameOverBgmLocked)
        {
            return;
        }

        SoundClipLibrary library = soundManager.ClipLibrary;

        if (observedStateManager == null || library == null)
        {
            soundManager.PlayPlaylist(null);
            return;
        }

        if (observedStateManager.CurrentState == GameFlowState.Shop)
        {
            soundManager.PlayPlaylist(library.ShopBgm);
            return;
        }

        if (observedStateManager.CurrentState == GameFlowState.BattleClear)
        {
            // Keep battle music through the clear presentation. The next
            // stable game-flow state selects its own playlist.
            return;
        }

        if (observedStateManager.CurrentState != GameFlowState.Battle)
        {
            soundManager.PlayPlaylist(null);
            return;
        }

        BattleData battle = observedStateManager.CurrentBattle;
        soundManager.PlayPlaylist(battle != null && battle.IsBoss
            ? library.BossBgm
            : library.GetBattleBgm(
                observedStateManager.CurrentStage?.StageId,
                observedStateManager.CurrentBattleIndex,
                battle?.BattleId));
    }
}
