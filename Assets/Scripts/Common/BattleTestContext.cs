using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Transient controls scoped to the dedicated test scene.</summary>
internal static class BattleTestContext
{
    internal const string ScenePath = "Assets/Scenes/BattleTest.unity";
    internal static bool ConsoleOpen { get; set; }
    internal static bool Invulnerable { get; set; }
    internal static bool IsActive => SceneManager.GetActiveScene().path == ScenePath;
    internal static bool IsPaused => IsActive && ConsoleOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void Reset()
    {
        ConsoleOpen = false;
        Invulnerable = false;
    }
}
