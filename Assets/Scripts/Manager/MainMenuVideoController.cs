using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
[RequireComponent(typeof(VideoPlayer))]
public sealed class MainMenuVideoController : MonoBehaviour
{
    private static readonly string[] ModalPanelNames =
    {
        "Panel | Settings",
        "Panel | Statistics",
        "Panel | Dict & Info",
        "Panel | Credits"
    };

    private enum PlaybackState
    {
        Idle,
        Ready
    }

    [Header("Streaming Assets Videos")]
    [SerializeField] private string idleVideoPath = "Videos/Main_Idle.mp4";
    [SerializeField] private string readyVideoPath = "Videos/Main_Ready.mp4";

    [Header("Game Start")]
    [SerializeField] private Button playGameButton;
    [SerializeField] private CanvasGroup buttonsCanvasGroup;
    [SerializeField] private CanvasGroup settingsButtonCanvasGroup;
    [Min(0.01f)]
    [SerializeField] private float buttonsFadeOutDuration = 0.5f;
    [SerializeField] private string gameSceneName = "Stage 1";

    private VideoPlayer videoPlayer;
    private PlaybackState playbackState;
    private bool gameStartRequested;
    private Coroutine buttonsFadeCoroutine;
    private readonly List<GameObject> modalPanels = new List<GameObject>();
    private bool hadActiveModalPanel;

    private void Awake()
    {
        StatisticsPanelController.EnsureExists();
        ResolvePlayGameButton();
        ResolveButtonsCanvasGroup();
        ResolveSettingsButtonCanvasGroup();
        ResolveModalPanels();
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
    }

    private void OnEnable()
    {
        videoPlayer.prepareCompleted += HandlePrepared;
        videoPlayer.loopPointReached += HandleLoopPointReached;
        videoPlayer.errorReceived += HandleVideoError;

        ResolvePlayGameButton();
        ResolveButtonsCanvasGroup();
        ResolveSettingsButtonCanvasGroup();
        ResolveModalPanels();
        hadActiveModalPanel = HasActiveModalPanel();
        if (playGameButton != null)
        {
            playGameButton.onClick.AddListener(StartGame);
            playGameButton.interactable = true;
        }

        if (buttonsCanvasGroup != null)
        {
            buttonsCanvasGroup.alpha = 1f;
            buttonsCanvasGroup.interactable = true;
            buttonsCanvasGroup.blocksRaycasts = true;
        }

        if (settingsButtonCanvasGroup != null)
        {
            settingsButtonCanvasGroup.alpha = 1f;
            settingsButtonCanvasGroup.interactable = true;
            settingsButtonCanvasGroup.blocksRaycasts = true;
        }

        gameStartRequested = false;
        PlayVideo(PlaybackState.Idle, idleVideoPath, true);
    }

    private void OnDisable()
    {
        videoPlayer.prepareCompleted -= HandlePrepared;
        videoPlayer.loopPointReached -= HandleLoopPointReached;
        videoPlayer.errorReceived -= HandleVideoError;
        if (playGameButton != null)
        {
            playGameButton.onClick.RemoveListener(StartGame);
        }
        videoPlayer.Stop();
    }

    private void LateUpdate()
    {
        bool hasActiveModalPanel = HasActiveModalPanel();

        if (hasActiveModalPanel && !hadActiveModalPanel)
        {
            EventSystem.current?.SetSelectedGameObject(null);
        }

        hadActiveModalPanel = hasActiveModalPanel;
    }

    private void ResolveModalPanels()
    {
        modalPanels.Clear();

        foreach (Transform candidate in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            foreach (string panelName in ModalPanelNames)
            {
                if (candidate.name == panelName)
                {
                    modalPanels.Add(candidate.gameObject);
                    break;
                }
            }
        }
    }

    private bool HasActiveModalPanel()
    {
        foreach (GameObject panel in modalPanels)
        {
            if (panel != null && panel.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    public void StartGame()
    {
        if (gameStartRequested)
        {
            return;
        }

        gameStartRequested = true;
        GameStatistics.BeginRun();
        if (playGameButton != null)
        {
            playGameButton.interactable = false;
        }
        FadeOutButtons();
        PlayVideo(PlaybackState.Ready, readyVideoPath, false);
    }

    private void ResolveButtonsCanvasGroup()
    {
        if (buttonsCanvasGroup != null)
        {
            return;
        }

        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (candidate.name != "Layout | Buttons")
            {
                continue;
            }

            buttonsCanvasGroup = candidate.GetComponent<CanvasGroup>();
            if (buttonsCanvasGroup == null)
            {
                buttonsCanvasGroup = candidate.gameObject.AddComponent<CanvasGroup>();
            }

            return;
        }
    }

    private void FadeOutButtons()
    {
        ResolveButtonsCanvasGroup();
        ResolveSettingsButtonCanvasGroup();

        if (buttonsCanvasGroup == null && settingsButtonCanvasGroup == null)
        {
            return;
        }

        if (buttonsFadeCoroutine != null)
        {
            StopCoroutine(buttonsFadeCoroutine);
        }

        buttonsFadeCoroutine = StartCoroutine(FadeOutButtonsRoutine());
    }

    private IEnumerator FadeOutButtonsRoutine()
    {
        SetCanvasGroupInteraction(buttonsCanvasGroup, false);
        SetCanvasGroupInteraction(settingsButtonCanvasGroup, false);
        float buttonsStartAlpha = buttonsCanvasGroup == null
            ? 0f
            : buttonsCanvasGroup.alpha;
        float settingsStartAlpha = settingsButtonCanvasGroup == null
            ? 0f
            : settingsButtonCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < buttonsFadeOutDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / buttonsFadeOutDuration);
            SetCanvasGroupAlpha(
                buttonsCanvasGroup,
                Mathf.SmoothStep(buttonsStartAlpha, 0f, progress));
            SetCanvasGroupAlpha(
                settingsButtonCanvasGroup,
                Mathf.SmoothStep(settingsStartAlpha, 0f, progress));
        }

        SetCanvasGroupAlpha(buttonsCanvasGroup, 0f);
        SetCanvasGroupAlpha(settingsButtonCanvasGroup, 0f);
        buttonsFadeCoroutine = null;
    }

    private void ResolveSettingsButtonCanvasGroup()
    {
        if (settingsButtonCanvasGroup != null)
        {
            return;
        }

        foreach (Button button in FindObjectsByType<Button>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (button.name != "Button | Settings")
            {
                continue;
            }

            settingsButtonCanvasGroup = button.GetComponent<CanvasGroup>();
            if (settingsButtonCanvasGroup == null)
            {
                settingsButtonCanvasGroup =
                    button.gameObject.AddComponent<CanvasGroup>();
            }

            return;
        }
    }

    private static void SetCanvasGroupInteraction(
        CanvasGroup canvasGroup,
        bool enabled)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private static void SetCanvasGroupAlpha(CanvasGroup canvasGroup, float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }

    private void ResolvePlayGameButton()
    {
        if (playGameButton != null)
        {
            return;
        }

        Button[] buttons = FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button.name == "Button | Play Game")
            {
                playGameButton = button;
                return;
            }
        }
    }

    private void PlayVideo(
        PlaybackState nextState,
        string relativePath,
        bool loop)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            Debug.LogError($"Main menu {nextState} video path is not assigned.", this);
            return;
        }

        playbackState = nextState;
        videoPlayer.Stop();
        videoPlayer.clip = null;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = StreamingVideoPlayer.GetStreamingAssetsUrl(relativePath);
        videoPlayer.isLooping = loop;
        videoPlayer.Prepare();
    }

    private void HandlePrepared(VideoPlayer preparedPlayer)
    {
        preparedPlayer.time = 0d;
        preparedPlayer.Play();
    }

    private void HandleLoopPointReached(VideoPlayer _)
    {
        if (playbackState == PlaybackState.Ready)
        {
            LoadGameScene();
        }
    }

    private void HandleVideoError(VideoPlayer _, string message)
    {
        Debug.LogError($"Main menu {playbackState} video playback failed: {message}", this);

        if (playbackState == PlaybackState.Ready)
        {
            LoadGameScene();
        }
    }

    private void LoadGameScene()
    {
        enabled = false;

        if (!LoadingTransitionController.LoadScene(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
