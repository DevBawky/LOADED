using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private Slider saturationSlider;
    [SerializeField] private Toggle oldMovieToggle;

    private readonly List<Button> pauseButtons = new List<Button>();
    private StateManager stateManager;
    private GameObject pauseOverlayRoot;
    private bool exitRequested;

    public static bool IsPaused { get; private set; }

    private void Awake()
    {
        stateManager = FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include);
        CachePauseMenuReferences();
        ConfigurePauseOverlay();
        ConfigurePauseAnimators();
        BindPauseMenuControls();
        SetPaused(false);
        RefreshPauseAvailability();
    }

    private void OnEnable()
    {
        if (stateManager != null)
        {
            stateManager.StateChanged += HandleFlowStateChanged;
        }
    }

    private void OnDisable()
    {
        if (stateManager != null)
        {
            stateManager.StateChanged -= HandleFlowStateChanged;
        }
    }

    public void TogglePause()
    {
        if (exitRequested || LoadingTransitionController.IsTransitioning
            || GameOverController.IsGameOver)
        {
            return;
        }

        if (IsPaused)
        {
            SetPaused(false);
            return;
        }

        if (!CanOpenPauseMenu())
        {
            return;
        }

        SetPaused(true);
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void ExitToMainMenu()
    {
        if (!exitRequested)
        {
            StartCoroutine(ExitToMainMenuRoutine());
        }
    }

    private IEnumerator ExitToMainMenuRoutine()
    {
        exitRequested = true;
        stateManager?.LockInputForExitSave();
        SetPaused(false);

        while (stateManager != null
            && !stateManager.IsCombatSettledForExit)
        {
            yield return null;
        }

        stateManager?.SaveCurrentRun();

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
        if (isPaused)
        {
            foreach (CombatPresentation presentation in
                     FindObjectsByType<CombatPresentation>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                presentation.CancelHitStopForPause();
            }

            foreach (CombatFeedbackController feedback in
                     FindObjectsByType<CombatFeedbackController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                feedback.CancelPresentationForPause();
            }

            CombatCameraShake.CancelForPause();
            Time.timeScale = 1f;
        }

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
            if (isPaused)
            {
                pausedPanel.transform.SetAsLastSibling();
            }

            pausedPanel.SetActive(isPaused);
        }
    }

    private void OnDestroy()
    {
        if (pauseOverlayRoot != null)
        {
            Destroy(pauseOverlayRoot);
            pauseOverlayRoot = null;
        }

        if (!IsPaused)
        {
            return;
        }

        IsPaused = false;
    }

    private void ConfigurePauseOverlay()
    {
        if (pausedPanel == null || pauseOverlayRoot != null)
        {
            return;
        }

        Canvas sourceCanvas = pausedPanel.GetComponentInParent<Canvas>();
        pauseOverlayRoot = new GameObject(
            "Canvas | Pause Overlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas overlayCanvas = pauseOverlayRoot.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = short.MaxValue;

        if (sourceCanvas != null)
        {
            overlayCanvas.sortingLayerID = sourceCanvas.sortingLayerID;
            overlayCanvas.targetDisplay = sourceCanvas.targetDisplay;
        }

        CanvasScaler overlayScaler =
            pauseOverlayRoot.GetComponent<CanvasScaler>();
        CanvasScaler sourceScaler = sourceCanvas == null
            ? null
            : sourceCanvas.GetComponent<CanvasScaler>();

        if (sourceScaler != null)
        {
            overlayScaler.uiScaleMode = sourceScaler.uiScaleMode;
            overlayScaler.referencePixelsPerUnit =
                sourceScaler.referencePixelsPerUnit;
            overlayScaler.scaleFactor = sourceScaler.scaleFactor;
            overlayScaler.referenceResolution =
                sourceScaler.referenceResolution;
            overlayScaler.screenMatchMode = sourceScaler.screenMatchMode;
            overlayScaler.matchWidthOrHeight =
                sourceScaler.matchWidthOrHeight;
            overlayScaler.physicalUnit = sourceScaler.physicalUnit;
            overlayScaler.fallbackScreenDPI = sourceScaler.fallbackScreenDPI;
            overlayScaler.defaultSpriteDPI = sourceScaler.defaultSpriteDPI;
            overlayScaler.dynamicPixelsPerUnit =
                sourceScaler.dynamicPixelsPerUnit;
        }

        RectTransform panelRect = pausedPanel.transform as RectTransform;
        panelRect.SetParent(pauseOverlayRoot.transform, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.localScale = Vector3.one;
    }

    private void ConfigurePauseAnimators()
    {
        if (pausedPanel == null)
        {
            return;
        }

        foreach (Animator animator in
                 pausedPanel.GetComponentsInChildren<Animator>(true))
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
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
            else if (HasAncestorNamed(slider.transform, "Layout | Color"))
            {
                saturationSlider = slider;
            }
        }

        foreach (Toggle toggle in pausedPanel.GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.name == "Toggle | OnOffMovie"
                && HasAncestorNamed(toggle.transform, "Layout | OnOffMovie"))
            {
                oldMovieToggle = toggle;
                break;
            }
        }
    }

    private void BindPauseMenuControls()
    {
        pauseButtons.Clear();

        foreach (Button button in FindObjectsByType<Button>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (button.name == "Button | Pause")
            {
                pauseButtons.Add(button);
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

        if (saturationSlider != null)
        {
            saturationSlider.SetValueWithoutNotify(
                GraphicsSaturationSettings.Saturation);
            saturationSlider.onValueChanged.AddListener(
                GraphicsSaturationSettings.SetSaturation);
        }

        if (oldMovieToggle != null)
        {
            oldMovieToggle.SetIsOnWithoutNotify(
                OldMoviePresentationSettings.Enabled);
            oldMovieToggle.onValueChanged.AddListener(
                OldMoviePresentationSettings.SetEnabled);
        }
    }

    private void HandleFlowStateChanged()
    {
        if (!CanOpenPauseMenu() && IsPaused)
        {
            SetPaused(false);
        }

        RefreshPauseAvailability();
    }

    private bool CanOpenPauseMenu()
    {
        if (GameOverController.IsGameOver)
        {
            return false;
        }

        if (stateManager == null)
        {
            return true;
        }

        return stateManager.CurrentState == GameFlowState.Battle
            || stateManager.CurrentState == GameFlowState.BattleClear
            || stateManager.CurrentState == GameFlowState.Shop;
    }

    private void RefreshPauseAvailability()
    {
        bool available = CanOpenPauseMenu();

        foreach (Button pauseButton in pauseButtons)
        {
            if (pauseButton != null)
            {
                pauseButton.gameObject.SetActive(available);
                pauseButton.interactable = available;
            }
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
        "Panel | Dict & Info",
        "Panel | Load Game"
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
        if (LoadingTransitionController.IsTransitioning
            || GameOverController.IsGameOver)
        {
            return;
        }

        if (FirstRunGuideController.TrySkipActiveGuide())
        {
            return;
        }

        GamePauseController pauseController =
            Object.FindFirstObjectByType<GamePauseController>();
        if (pauseController != null && GamePauseController.IsPaused)
        {
            CombatControlPanelController controlPanel =
                Object.FindFirstObjectByType<CombatControlPanelController>();
            if (controlPanel != null && controlPanel.TryClose())
            {
                return;
            }

            pauseController.Resume();
            return;
        }

        BulletManagementUI bulletManagement =
            Object.FindFirstObjectByType<BulletManagementUI>();
        if (bulletManagement != null
            && bulletManagement.TryCloseFromEscape())
        {
            return;
        }

        MainMenuSettingsController settingsController =
            Object.FindFirstObjectByType<MainMenuSettingsController>();
        if (settingsController != null
            && settingsController.TryCloseWithoutSaving())
        {
            return;
        }

        if (CloseMainMenuPanels())
        {
            return;
        }

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
