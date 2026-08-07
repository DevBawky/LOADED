using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GamePauseController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    [SerializeField] private GameObject pausedPanel;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider vfxIntensitySlider;

    public static bool IsPaused { get; private set; }

    private void Awake()
    {
        CachePauseMenuReferences();
        BindPauseMenuControls();
        SetPaused(false);
    }

    public void TogglePause()
    {
        if (LoadingTransitionController.IsTransitioning)
        {
            return;
        }

        SetPaused(!IsPaused);
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void ExitToMainMenu()
    {
        IsPaused = false;

        if (pausedPanel != null)
        {
            pausedPanel.SetActive(false);
        }

        Time.timeScale = 1f;

        if (!LoadingTransitionController.LoadScene(MainMenuSceneName))
        {
            SceneManager.LoadScene(MainMenuSceneName);
        }
    }

    private void SetPaused(bool isPaused)
    {
        if (IsPaused == isPaused)
        {
            if (pausedPanel != null)
            {
                pausedPanel.SetActive(isPaused);
            }

            return;
        }

        IsPaused = isPaused;

        if (pausedPanel != null)
        {
            pausedPanel.SetActive(isPaused);
        }
    }

    private void OnDestroy()
    {
        if (!IsPaused)
        {
            return;
        }

        IsPaused = false;
    }

    private void CachePauseMenuReferences()
    {
        if (pausedPanel == null)
        {
            pausedPanel = FindGameObject("Panel | Paused");
        }

        if (pausedPanel == null)
        {
            return;
        }

        foreach (Slider slider in pausedPanel.GetComponentsInChildren<Slider>(true))
        {
            if (HasAncestorNamed(slider.transform, "Layout | BGM"))
            {
                bgmVolumeSlider = slider;
            }
            else if (HasAncestorNamed(slider.transform, "Layout | SFX"))
            {
                sfxVolumeSlider = slider;
            }
            else if (HasAncestorNamed(slider.transform, "Layout | VFX"))
            {
                vfxIntensitySlider = slider;
            }
        }
    }

    private void BindPauseMenuControls()
    {
        foreach (Button button in FindObjectsByType<Button>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (button.name == "Button | Pause")
            {
                AddListenerWhenMissing(button, TogglePause);
            }
            else if (button.name == "Button _ Resume")
            {
                AddListenerWhenMissing(button, Resume);
            }
            else if (button.name == "Button _ Exit")
            {
                AddListenerWhenMissing(button, ExitToMainMenu);
            }
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.SetValueWithoutNotify(SoundManager.BgmVolume);
            bgmVolumeSlider.onValueChanged.AddListener(SoundManager.SetBgmVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(SoundManager.SfxVolume);
            sfxVolumeSlider.onValueChanged.AddListener(SoundManager.SetSfxVolume);
        }

        if (vfxIntensitySlider != null)
        {
            vfxIntensitySlider.SetValueWithoutNotify(
                CombatAccessibilitySettings.PresentationIntensity);
            vfxIntensitySlider.onValueChanged.AddListener(
                CombatAccessibilitySettings.SetPresentationIntensity);
        }
    }

    private void AddListenerWhenMissing(Button button, UnityEngine.Events.UnityAction action)
    {
        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
        {
            if (button.onClick.GetPersistentTarget(index) == this
                && button.onClick.GetPersistentMethodName(index) == action.Method.Name)
            {
                return;
            }
        }

        button.onClick.AddListener(action);
    }

    private static GameObject FindGameObject(string objectName)
    {
        foreach (Transform transform in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (transform.name == objectName)
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    private bool HasAncestorNamed(Transform transform, string ancestorName)
    {
        Transform current = transform.parent;

        while (current != null && current != pausedPanel.transform)
        {
            if (current.name == ancestorName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}

internal static class EscapePanelInput
{
    private static readonly string[] ClosablePanelNames =
    {
        "Panel | Credits",
        "Panel | Statistics",
        "Panel | Dict & Info"
    };

    private static InputAction escapeAction;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (escapeAction != null)
        {
            escapeAction.performed -= HandleEscape;
            escapeAction.Disable();
            escapeAction.Dispose();
        }

        escapeAction = new InputAction(
            "Close Active Panel",
            InputActionType.Button,
            "<Keyboard>/escape");
        escapeAction.performed += HandleEscape;
        escapeAction.Enable();
    }

    private static void HandleEscape(InputAction.CallbackContext _)
    {
        if (LoadingTransitionController.IsTransitioning)
        {
            return;
        }

        BulletManagementUI bulletManagement =
            Object.FindFirstObjectByType<BulletManagementUI>();
        if (bulletManagement != null
            && bulletManagement.TryCloseFromEscape())
        {
            return;
        }

        if (CloseMainMenuPanels())
        {
            return;
        }

        GamePauseController pauseController =
            Object.FindFirstObjectByType<GamePauseController>();
        if (pauseController != null)
        {
            pauseController.TogglePause();
        }
    }

    private static bool CloseMainMenuPanels()
    {
        bool closedAny = false;

        foreach (Transform candidate in Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (!candidate.gameObject.activeInHierarchy
                || !IsClosablePanel(candidate.name))
            {
                continue;
            }

            candidate.gameObject.SetActive(false);
            closedAny = true;
        }

        return closedAny;
    }

    private static bool IsClosablePanel(string objectName)
    {
        foreach (string panelName in ClosablePanelNames)
        {
            if (objectName == panelName)
            {
                return true;
            }
        }

        return false;
    }
}
