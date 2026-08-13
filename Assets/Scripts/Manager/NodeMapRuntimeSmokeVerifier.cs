#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class NodeMapRuntimeSmokeVerifier : MonoBehaviour
{
    private const string CommandLineFlag = "-verifyNodeMapBootstrap";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!System.Environment.GetCommandLineArgs().Contains(CommandLineFlag))
        {
            return;
        }

        GameObject owner = new GameObject("Node Map Runtime Smoke Verifier");
        DontDestroyOnLoad(owner);
        owner.AddComponent<NodeMapRuntimeSmokeVerifier>();
    }

    private IEnumerator Start()
    {
        yield return null;
        SceneManager.LoadScene(RunManager.NodeMapSceneName);
        yield return null;
        yield return null;

        NodeMapController controller = FindFirstObjectByType<NodeMapController>();
        GameObject canvas = GameObject.Find("Canvas | Node Map");
        int nodeButtonCount = FindObjectsByType<Button>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None).Count(button =>
                button.name.StartsWith("Node | "));

        if (controller == null || canvas == null || nodeButtonCount < 6)
        {
            Debug.LogError(
                "NODE_MAP_RUNTIME_SMOKE_FAILED: "
                + $"controller={controller != null}, canvas={canvas != null}, "
                + $"nodeButtons={nodeButtonCount}");
            Application.Quit(1);
            yield break;
        }

        Debug.Log(
            $"NODE_MAP_RUNTIME_SMOKE_PASSED: nodeButtons={nodeButtonCount}");
        Application.Quit(0);
    }
}
#endif
