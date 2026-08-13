using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1200)]
public sealed class PersistentRunContext : MonoBehaviour
{
    private static PersistentRunContext instance;

    private readonly List<GameObject> stageRoots = new List<GameObject>();
    private StateManager stateManager;
    private GameObject managerRoot;
    private GameObject canvasRoot;
    private bool runEnded;
    private Coroutine resumeCoroutine;

    public static PersistentRunContext Instance => instance;
    public StateManager StateManager => stateManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBootstrap()
    {
        instance = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == RunManager.CombatSceneName)
        {
            if (instance == null || instance.runEnded)
            {
                instance?.DisposeContext();
                CaptureFirstBattle(scene);
            }
            else
            {
                instance.ReplaceReloadedBattle(scene);
            }

            return;
        }

        if (instance == null)
        {
            return;
        }

        if (scene.name == "MainMenu" || scene.name == "Ending")
        {
            instance.DisposeContext();
            return;
        }

        instance.SuspendCombat();

        if (scene.name == RunManager.ShopSceneName)
        {
            instance.EnterMapShop();
        }
    }

    private static void CaptureFirstBattle(Scene scene)
    {
        StateManager state = FindInScene<StateManager>(scene);

        if (state == null)
        {
            return;
        }

        GameObject owner = state.transform.root.gameObject;
        PersistentRunContext context =
            owner.GetComponent<PersistentRunContext>();
        context ??= owner.AddComponent<PersistentRunContext>();
        context.Capture(scene, state);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Capture(Scene scene, StateManager state)
    {
        stateManager = state;
        managerRoot = state.transform.root.gameObject;
        stageRoots.Clear();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null)
            {
                continue;
            }

            if (root.GetComponentInChildren<EventSystem>(true) != null)
            {
                continue;
            }

            stageRoots.Add(root);
            DontDestroyOnLoad(root);

            if (root.GetComponent<Canvas>() != null
                && PersistentGameCanvas.FindDescendant(
                    root.transform,
                    "Panel | Shop") != null)
            {
                canvasRoot = root;
            }
        }

        if (canvasRoot != null)
        {
            PersistentGameCanvas.Adopt(canvasRoot, true);
        }
    }

    public static void PrepareForScene(string sceneName)
    {
        if (instance == null)
        {
            return;
        }

        if (sceneName == RunManager.CombatSceneName)
        {
            instance.SetStageRootsActive(false, false);
            return;
        }

        instance.SuspendCombat();
    }

    public static void MarkRunEnded()
    {
        if (instance != null)
        {
            instance.runEnded = true;
        }
    }

    public bool EnterMapShop()
    {
        if (runEnded || stateManager == null)
        {
            return false;
        }

        SetStageRootsActive(false, true);
        PersistentGameCanvas.Instance?.ShowShopMode();
        return stateManager.EnterMapNodeShop();
    }

    private void SuspendCombat()
    {
        SetStageRootsActive(false, false);
    }

    private void ReplaceReloadedBattle(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null || stageRoots.Contains(root))
            {
                continue;
            }

            root.SetActive(false);
            Destroy(root);
        }

        SetStageRootsActive(true, true);
        PersistentGameCanvas.Instance?.RebindCamera();

        if (resumeCoroutine != null)
        {
            StopCoroutine(resumeCoroutine);
        }

        resumeCoroutine = StartCoroutine(ResumeBattleNextFrame());
    }

    private IEnumerator ResumeBattleNextFrame()
    {
        yield return null;
        resumeCoroutine = null;

        if (stateManager == null || !stateManager.ResumeMapNodeBattle())
        {
            Debug.LogError(
                "The persistent run context could not resume the selected battle.",
                this);
        }
    }

    private void SetStageRootsActive(bool combatActive, bool canvasActive)
    {
        foreach (GameObject root in stageRoots)
        {
            if (root == null)
            {
                continue;
            }

            bool active = root == managerRoot
                || root == canvasRoot && canvasActive
                || combatActive;
            root.SetActive(active);
        }
    }

    private void DisposeContext()
    {
        if (resumeCoroutine != null)
        {
            StopCoroutine(resumeCoroutine);
            resumeCoroutine = null;
        }

        List<GameObject> roots = new List<GameObject>(stageRoots);
        stageRoots.Clear();
        instance = null;

        foreach (GameObject root in roots)
        {
            if (root == null)
            {
                continue;
            }

            root.SetActive(false);
            Destroy(root);
        }
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);

            if (component != null)
            {
                return component;
            }
        }

        return null;
    }
}
