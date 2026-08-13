using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public sealed class PersistentEventSystem : MonoBehaviour
{
    private static PersistentEventSystem instance;
    private EventSystem eventSystem;

    public static PersistentEventSystem Instance => EnsureInstance();
    public EventSystem EventSystem => eventSystem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        instance = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static void PrepareForScene(string sceneName)
    {
        if (sceneName == RunManager.CombatSceneName && instance != null)
        {
            instance.gameObject.SetActive(false);
        }
    }

    public static PersistentEventSystem EnsureInstance()
    {
        if (instance != null)
        {
            instance.Activate();
            return instance;
        }

        instance = FindFirstObjectByType<PersistentEventSystem>(
            FindObjectsInactive.Include);

        if (instance == null)
        {
            GameObject owner = new GameObject(
                "Global EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule),
                typeof(PersistentEventSystem));
            instance = owner.GetComponent<PersistentEventSystem>();
            owner.GetComponent<InputSystemUIInputModule>()
                .AssignDefaultActions();
        }

        instance.Activate();
        return instance;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EventSystem[] eventSystems = FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        PersistentEventSystem persistent = instance;

        if (persistent == null)
        {
            EventSystem sceneEventSystem = null;

            foreach (EventSystem candidate in eventSystems)
            {
                if (candidate != null && candidate.gameObject.scene == scene)
                {
                    sceneEventSystem = candidate;
                    break;
                }
            }

            if (sceneEventSystem != null)
            {
                persistent = sceneEventSystem.gameObject
                    .AddComponent<PersistentEventSystem>();
            }
            else
            {
                persistent = EnsureInstance();
                eventSystems = FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            }
        }

        foreach (EventSystem candidate in eventSystems)
        {
            if (candidate == null || candidate == persistent.eventSystem)
            {
                continue;
            }

            candidate.gameObject.SetActive(false);
            Destroy(candidate.gameObject);
        }

        persistent.Activate();
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
        eventSystem = GetComponent<EventSystem>();
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

    private void Activate()
    {
        eventSystem ??= GetComponent<EventSystem>();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (eventSystem != null && !eventSystem.enabled)
        {
            eventSystem.enabled = true;
        }

        EventSystem.current = eventSystem;
    }
}
