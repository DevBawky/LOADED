using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-900)]
public sealed class GameOverController : MonoBehaviour
{
    private const string StageOneSceneName = "Stage 1";
    private const string MainMenuSceneName = "MainMenu";
    private const string GameOverPanelName = "Panel | GameOver";
    private const string GameOverReasonTextName = "Text | Reason";
    private const string HealthDepletedReason = "체력을 모두 소모했습니다...";
    private const string BulletsDepletedReason = "싸울 탄환이 없습니다..";
    private const string ReasonPrefix = "게임오버\n<size=50><color=red>";

    private static GameOverController instance;
    private PlayerHealth observedPlayer;
    private DeckManager observedDeck;
    private bool handlingGameOver;

    public static bool IsGameOver { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null)
        {
            new GameObject(nameof(GameOverController))
                .AddComponent<GameOverController>();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        ResetForScene();
        BindGameOverSources();
    }

    private void OnDestroy()
    {
        if (instance != this) return;

        UnbindGameOverSources();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;
        IsGameOver = false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetForScene();
        BindGameOverSources();
    }

    private void ResetForScene()
    {
        UnbindGameOverSources();
        handlingGameOver = false;
        IsGameOver = false;
        Time.timeScale = 1f;
    }

    private void BindGameOverSources()
    {
        observedPlayer = FindFirstObjectByType<PlayerHealth>(
            FindObjectsInactive.Include);
        if (observedPlayer != null)
        {
            observedPlayer.Defeated += HandlePlayerDefeated;
        }

        observedDeck = FindFirstObjectByType<DeckManager>(
            FindObjectsInactive.Include);
        if (observedDeck != null)
        {
            observedDeck.BulletsDepleted += HandleBulletsDepleted;
        }
    }

    private void UnbindGameOverSources()
    {
        if (observedPlayer != null)
        {
            observedPlayer.Defeated -= HandlePlayerDefeated;
        }

        observedPlayer = null;

        if (observedDeck != null)
        {
            observedDeck.BulletsDepleted -= HandleBulletsDepleted;
        }

        observedDeck = null;
    }

    private void HandlePlayerDefeated()
    {
        HandleGameOver(HealthDepletedReason);
    }

    private void HandleBulletsDepleted()
    {
        HandleGameOver(BulletsDepletedReason);
    }

    private void HandleGameOver(string reason)
    {
        if (handlingGameOver) return;

        handlingGameOver = true;
        IsGameOver = true;

        SoundManager.PlaySfx("SFX_Enemy_Die");
        SoundManager.PlayGameOverBgm();
        ShowGameOverPanel(reason);

        if (observedPlayer != null)
        {
            Destroy(observedPlayer.gameObject);
            observedPlayer = null;
        }

        Time.timeScale = 0f;
    }

    private void ShowGameOverPanel(string reason)
    {
        GameObject gameOverPanel = FindSceneObject(GameOverPanelName);
        if (gameOverPanel == null)
        {
            Debug.LogError($"Could not find '{GameOverPanelName}' in the active scene.");
            return;
        }

        Canvas canvas = gameOverPanel.GetComponentInParent<Canvas>(true);
        Transform searchRoot = canvas == null
            ? gameOverPanel.transform.root
            : canvas.transform;

        foreach (Transform candidate in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            if (candidate != gameOverPanel.transform
                && candidate.name.StartsWith("Panel | ",
                    System.StringComparison.Ordinal))
            {
                candidate.gameObject.SetActive(false);
            }
        }

        SetGameOverReason(gameOverPanel, reason);
        BindGameOverButtons(gameOverPanel);
        gameOverPanel.transform.SetAsLastSibling();
        gameOverPanel.SetActive(true);
    }

    private static void SetGameOverReason(
        GameObject gameOverPanel,
        string reason)
    {
        foreach (TMP_Text text in gameOverPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == GameOverReasonTextName)
            {
                text.text = ReasonPrefix + reason;
                return;
            }
        }

        Debug.LogError(
            $"Could not find '{GameOverReasonTextName}' under '{GameOverPanelName}'.");
    }

    private void BindGameOverButtons(GameObject gameOverPanel)
    {
        foreach (Button button in gameOverPanel.GetComponentsInChildren<Button>(true))
        {
            switch (button.name)
            {
                case "Button | Restart":
                    button.onClick.AddListener(RestartStageOne);
                    break;
                case "Button | MainMenu":
                    button.onClick.AddListener(ReturnToMainMenu);
                    break;
                case "Button | ExitGame":
                    button.onClick.AddListener(ExitGame);
                    break;
            }
        }
    }

    private void RestartStageOne()
    {
        LoadScene(StageOneSceneName);
    }

    private void ReturnToMainMenu()
    {
        LoadScene(MainMenuSceneName);
    }

    private void LoadScene(string sceneName)
    {
        SetGameOverButtonsInteractable(false);
        if (!LoadingTransitionController.LoadScene(sceneName))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
    }

    private static void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void SetGameOverButtonsInteractable(bool interactable)
    {
        GameObject panel = FindSceneObject(GameOverPanelName);
        if (panel == null) return;

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            button.interactable = interactable;
        }
    }

    private static GameObject FindSceneObject(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        foreach (Transform candidate in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == activeScene
                && candidate.name == objectName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }
}
