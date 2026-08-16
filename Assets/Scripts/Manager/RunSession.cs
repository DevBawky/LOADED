using System;
using UnityEngine;

/// <summary>
/// Scene-independent owner of the current run snapshot. Only serializable run
/// data lives here; scene objects and presentation managers remain local to
/// their scene.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class RunSession : MonoBehaviour
{
    private static RunSession instance;

    [Header("Runtime State (read only)")]
    [SerializeField] private RunSaveData currentRun;

    public static RunSession Instance => EnsureInstance();
    public bool HasRun => currentRun != null;
    public event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        gameObject.name = "##--RUN SESSION--##";
        DontDestroyOnLoad(gameObject);
    }

    public bool TryGetSnapshot(out RunSaveData snapshot)
    {
        snapshot = Clone(currentRun);
        return snapshot != null;
    }

    public void SetSnapshot(RunSaveData snapshot)
    {
        currentRun = Clone(snapshot);
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (currentRun == null)
        {
            return;
        }

        currentRun = null;
        Changed?.Invoke();
    }

    private static RunSession EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<RunSession>(
            FindObjectsInactive.Include);

        if (instance == null)
        {
            GameObject root = new GameObject("##--RUN SESSION--##");
            instance = root.AddComponent<RunSession>();
        }

        return instance;
    }

    private static RunSaveData Clone(RunSaveData source)
    {
        if (source == null)
        {
            return null;
        }

        return JsonUtility.FromJson<RunSaveData>(
            JsonUtility.ToJson(source));
    }
}
