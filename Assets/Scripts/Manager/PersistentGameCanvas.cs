using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1100)]
public sealed class PersistentGameCanvas : MonoBehaviour
{
    private static PersistentGameCanvas instance;

    public static PersistentGameCanvas Instance => instance;
    public GameObject Root => gameObject;

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
            GameObject stageCanvas = FindGameCanvas(scene);

            if (stageCanvas != null)
            {
                Adopt(stageCanvas, true);
                instance.RebindCamera();
            }

            return;
        }

        if (scene.name == RunManager.ShopSceneName)
        {
            instance?.ShowShopMode();
            return;
        }

        instance?.Hide();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static PersistentGameCanvas Adopt(
        GameObject canvasRoot,
        bool replaceExisting)
    {
        if (canvasRoot == null)
        {
            return instance;
        }

        PersistentGameCanvas marker =
            canvasRoot.GetComponent<PersistentGameCanvas>();

        if (instance != null && instance.gameObject != canvasRoot)
        {
            if (!replaceExisting)
            {
                return instance;
            }

            instance.gameObject.SetActive(false);
            Destroy(instance.gameObject);
            instance = null;
        }

        marker ??= canvasRoot.AddComponent<PersistentGameCanvas>();
        instance = marker;
        canvasRoot.transform.SetParent(null);
        DontDestroyOnLoad(canvasRoot);
        return marker;
    }

    public static void PrepareForScene(string sceneName)
    {
        if (instance == null || sceneName == RunManager.CombatSceneName)
        {
            return;
        }

        instance.Hide();
    }

    public void ShowShopMode()
    {
        Transform shopPanel = FindDescendant(transform, "Panel | Shop");
        Transform floatingPanel = FindDescendant(transform, "Panel | Floating");

        foreach (Transform child in transform)
        {
            bool active = IsOrContains(child, shopPanel)
                || IsOrContains(child, floatingPanel);
            child.gameObject.SetActive(active);
        }

        shopPanel?.gameObject.SetActive(true);
        floatingPanel?.gameObject.SetActive(true);
        RebindCamera();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void RebindCamera()
    {
        Canvas canvas = GetComponent<Canvas>();

        if (canvas == null)
        {
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 10f;
    }

    private static GameObject FindGameCanvas(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);

            foreach (Canvas canvas in canvases)
            {
                if (FindDescendant(canvas.transform, "Panel | Shop") != null
                    && FindDescendant(canvas.transform, "Panel | Floating") != null)
                {
                    return canvas.gameObject;
                }
            }
        }

        return null;
    }

    public static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform candidate in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(
                    candidate.name,
                    objectName,
                    StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsOrContains(Transform branch, Transform target)
    {
        return target != null
            && (target == branch || target.IsChildOf(branch));
    }
}
