using System;
using System.Collections.Generic;
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
        SoundClipLibrary library = soundManager.ClipLibrary;

        if (TryGetScenePlaylist(library, scene.name, out var scenePlaylist))
        {
            ObserveStateManager(null);
            soundManager.PlayPlaylist(scenePlaylist);
            return;
        }

        ObserveStateManager(UnityEngine.Object.FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include));

        if (observedStateManager != null)
        {
            RefreshForGameState();
            return;
        }

        soundManager.PlayPlaylist(null);
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
        soundManager.PlayPlaylist(battle == null
            ? null
            : ResolveBattlePlaylist(library, battle.BattleType));
    }

    internal static IReadOnlyList<AudioClip> ResolveBattlePlaylist(
        SoundClipLibrary library,
        BattleType battleType)
    {
        if (library == null)
        {
            return null;
        }

        return battleType switch
        {
            BattleType.Boss => library.BossBgm,
            BattleType.Elite => library.EliteBattleBgm,
            _ => library.NormalBattleBgm
        };
    }

    private static bool TryGetScenePlaylist(
        SoundClipLibrary library,
        string sceneName,
        out IReadOnlyList<AudioClip> playlist)
    {
        playlist = null;
        if (library == null)
        {
            return false;
        }

        if (sceneName.IndexOf(
                "MainMenu",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            playlist = library.MainMenuBgm;
            return true;
        }

        if (UsesNodeMapBgm(sceneName))
        {
            playlist = library.NodeMapBgm;
            return true;
        }

        if (UsesEventAndTreasureBgm(sceneName))
        {
            playlist = library.EventAndTreasureBgm;
            return true;
        }

        return false;
    }

    internal static bool UsesNodeMapBgm(string sceneName)
    {
        return string.Equals(
            sceneName,
            "NodeMap",
            StringComparison.OrdinalIgnoreCase);
    }

    internal static bool UsesEventAndTreasureBgm(string sceneName)
    {
        return string.Equals(
                sceneName,
                "Event",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                sceneName,
                "Treasure",
                StringComparison.OrdinalIgnoreCase);
    }
}
