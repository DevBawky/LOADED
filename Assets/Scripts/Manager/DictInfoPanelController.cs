using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public sealed class DictInfoPanelController : MonoBehaviour
{
    private enum MainTab
    {
        Info,
        Control,
        Dictionary
    }

    private enum ControlTab
    {
        Basic,
        High
    }

    private readonly struct VideoBinding
    {
        public VideoBinding(string elementName, string relativePath)
        {
            ElementName = elementName;
            RelativePath = relativePath;
        }

        public string ElementName { get; }
        public string RelativePath { get; }
    }

    private sealed class ButtonStyle
    {
        public Button Button;
        public ColorBlock Colors;
        public Color GraphicColor;
    }

    private sealed class VideoRuntime
    {
        public RawImage Container;
        public RawImage Display;
        public Color ContainerColor;
        public VideoPlayer Player;
        public RenderTexture Texture;
    }

    private static readonly VideoBinding[] VideoBindings =
    {
        new("Element | Movement", "Videos/Movement.mp4"),
        new("Element | Rotate", "Videos/Rotate.mp4"),
        new("Element | Shoot", "Videos/Shoot.mp4"),
        new("Element | Reload", "Videos/Reload.mp4"),
        new("Element | Kick", "Videos/Kick.mp4"),
        new("Element | Wait", "Videos/Wait.mp4"),
        new("Element | Show_Expectation", "Videos/Show_Expectation.mp4"),
        new("Element | Switch_BulletQueue", "Videos/Switch_Bullet_Queue.mp4")
    };

    [Header("Selected Button")]
    [Tooltip("Info/Control/Dict와 Basic/High에서 현재 선택된 버튼에 함께 적용되는 색상입니다.")]
    [SerializeField] private Color selectedButtonColor = new(0.545f, 0.353f, 0.169f, 1f);

    [Header("Video Render Fallback")]
    [Tooltip("브라우저가 원본 해상도를 보고하지 못할 때만 사용하는 예비 크기입니다.")]
    [Min(16)] [SerializeField] private int fallbackRenderTextureSize = 512;

    [Header("Auto-resolved References")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private GameObject controlPanel;
    [SerializeField] private GameObject dictionaryPanel;
    [SerializeField] private GameObject controlBasicLayout;
    [SerializeField] private GameObject controlHighLayout;
    [SerializeField] private Button infoButton;
    [SerializeField] private Button controlButton;
    [SerializeField] private Button dictionaryButton;
    [SerializeField] private Button basicButton;
    [SerializeField] private Button highButton;

    private readonly List<ButtonStyle> originalButtonStyles = new();
    private readonly List<VideoRuntime> videoRuntimes = new();
    private bool initialized;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
        SetupVideos();
        initialized = true;

        ShowInfo();
        ShowBasicControls();
    }

    private void OnValidate()
    {
        fallbackRenderTextureSize = Mathf.Max(16, fallbackRenderTextureSize);

        if (initialized)
        {
            RefreshButtonColors();
        }
    }

    private void OnDestroy()
    {
        UnbindButtons();

        foreach (VideoRuntime runtime in videoRuntimes)
        {
            if (runtime.Player != null)
            {
                runtime.Player.Stop();
            }

            if (runtime.Container != null)
            {
                runtime.Container.texture = null;
                runtime.Container.color = runtime.ContainerColor;
            }

            if (runtime.Display != null)
            {
                Destroy(runtime.Display.gameObject);
            }

            if (runtime.Texture != null)
            {
                runtime.Texture.Release();
                Destroy(runtime.Texture);
            }
        }

        videoRuntimes.Clear();
    }

    public void ShowInfo()
    {
        SetMainTab(MainTab.Info);
    }

    public void ShowControl()
    {
        SetMainTab(MainTab.Control);
    }

    public void ShowDictionary()
    {
        SetMainTab(MainTab.Dictionary);
    }

    public void ShowBasicControls()
    {
        SetControlTab(ControlTab.Basic);
    }

    public void ShowHighControls()
    {
        SetControlTab(ControlTab.High);
    }

    private void ResolveReferences()
    {
        Transform root = transform;
        infoPanel ??= FindDescendant(root, "Panel | Info")?.gameObject;
        controlPanel ??= FindDescendant(root, "Panel | Control")?.gameObject;
        dictionaryPanel ??= FindDescendant(root, "Panel | Dictionary")?.gameObject;
        controlBasicLayout ??= FindDescendant(root, "Layout | Control Basic")?.gameObject;
        controlHighLayout ??= FindDescendant(root, "Layout | Control High")?.gameObject;

        infoButton ??= FindComponent<Button>(root, "Button | Info");
        controlButton ??= FindComponent<Button>(root, "Button | Control");
        dictionaryButton ??= FindComponent<Button>(root, "Button | Dict");
        basicButton ??= FindComponent<Button>(root, "Button | Basic");
        highButton ??= FindComponent<Button>(root, "Button | High");
    }

    private void BindButtons()
    {
        BindButton(infoButton, ShowInfo);
        BindButton(controlButton, ShowControl);
        BindButton(dictionaryButton, ShowDictionary);
        BindButton(basicButton, ShowBasicControls);
        BindButton(highButton, ShowHighControls);
    }

    private void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            Debug.LogWarning($"A Dict & Info panel button could not be resolved for {action.Method.Name}.", this);
            return;
        }

        originalButtonStyles.Add(new ButtonStyle
        {
            Button = button,
            Colors = button.colors,
            GraphicColor = button.targetGraphic == null
                ? Color.white
                : button.targetGraphic.color
        });
        button.onClick.AddListener(action);
    }

    private void UnbindButtons()
    {
        if (infoButton != null) infoButton.onClick.RemoveListener(ShowInfo);
        if (controlButton != null) controlButton.onClick.RemoveListener(ShowControl);
        if (dictionaryButton != null) dictionaryButton.onClick.RemoveListener(ShowDictionary);
        if (basicButton != null) basicButton.onClick.RemoveListener(ShowBasicControls);
        if (highButton != null) highButton.onClick.RemoveListener(ShowHighControls);
    }

    private void SetMainTab(MainTab tab)
    {
        SetActive(infoPanel, tab == MainTab.Info);
        SetActive(controlPanel, tab == MainTab.Control);
        SetActive(dictionaryPanel, tab == MainTab.Dictionary);

        SetSelected(infoButton, tab == MainTab.Info);
        SetSelected(controlButton, tab == MainTab.Control);
        SetSelected(dictionaryButton, tab == MainTab.Dictionary);
    }

    private void SetControlTab(ControlTab tab)
    {
        SetActive(controlBasicLayout, tab == ControlTab.Basic);
        SetActive(controlHighLayout, tab == ControlTab.High);

        SetSelected(basicButton, tab == ControlTab.Basic);
        SetSelected(highButton, tab == ControlTab.High);
    }

    private void SetSelected(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        ButtonStyle original = originalButtonStyles.Find(style => style.Button == button);
        if (original == null)
        {
            return;
        }

        ColorBlock colors = original.Colors;
        if (selected)
        {
            colors.normalColor = selectedButtonColor;
            colors.selectedColor = selectedButtonColor;
            colors.highlightedColor = Color.Lerp(selectedButtonColor, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(selectedButtonColor, Color.black, 0.2f);
        }

        button.colors = colors;
        Color displayedColor = selected ? colors.normalColor : original.GraphicColor;
        button.targetGraphic?.CrossFadeColor(displayedColor, 0f, true, true);
    }

    private void RefreshButtonColors()
    {
        if (infoPanel != null && infoPanel.activeSelf) SetMainTab(MainTab.Info);
        else if (controlPanel != null && controlPanel.activeSelf) SetMainTab(MainTab.Control);
        else if (dictionaryPanel != null && dictionaryPanel.activeSelf) SetMainTab(MainTab.Dictionary);

        if (controlHighLayout != null && controlHighLayout.activeSelf) SetControlTab(ControlTab.High);
        else SetControlTab(ControlTab.Basic);
    }

    private void SetupVideos()
    {
        foreach (VideoBinding binding in VideoBindings)
        {
            Transform element = FindDescendant(transform, binding.ElementName);
            RawImage rawImage = element == null ? null : element.GetComponentInChildren<RawImage>(true);
            if (rawImage == null)
            {
                Debug.LogWarning($"No video RawImage was found below '{binding.ElementName}'.", this);
                continue;
            }

            CreateVideoRuntime(rawImage, binding.RelativePath);
        }
    }

    private void CreateVideoRuntime(RawImage rawImage, string relativePath)
    {
        GameObject playerObject = new($"VideoPlayer | {rawImage.transform.parent.name}");
        playerObject.transform.SetParent(transform, false);

        GameObject displayObject = new("RawImage | Video Content", typeof(RectTransform));
        RectTransform displayTransform = (RectTransform)displayObject.transform;
        displayTransform.SetParent(rawImage.rectTransform, false);
        displayTransform.anchorMin = Vector2.zero;
        displayTransform.anchorMax = Vector2.one;
        displayTransform.offsetMin = Vector2.zero;
        displayTransform.offsetMax = Vector2.zero;
        displayTransform.localScale = Vector3.one;

        RawImage display = displayObject.AddComponent<RawImage>();
        display.color = Color.white;
        display.uvRect = new Rect(0f, 0f, 1f, 1f);
        display.raycastTarget = false;

        AspectRatioFitter aspectRatioFitter = displayObject.AddComponent<AspectRatioFitter>();
        aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        VideoPlayer player = playerObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.source = VideoSource.Url;
        player.url = StreamingVideoPlayer.GetStreamingAssetsUrl(relativePath);
        player.renderMode = VideoRenderMode.APIOnly;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.isLooping = true;
        player.skipOnDrop = true;
        player.waitForFirstFrame = true;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;

        Color containerColor = rawImage.color;
        rawImage.texture = null;
        rawImage.color = Color.clear;
        rawImage.raycastTarget = false;

        VideoRuntime runtime = new()
        {
            Container = rawImage,
            Display = display,
            ContainerColor = containerColor,
            Player = player,
            Texture = null
        };
        videoRuntimes.Add(runtime);

        player.prepareCompleted += HandleVideoPrepared;
        player.errorReceived += HandleVideoError;
        player.Prepare();
    }

    private void HandleVideoPrepared(VideoPlayer player)
    {
        VideoRuntime runtime = videoRuntimes.Find(candidate => candidate.Player == player);
        if (runtime == null || runtime.Display == null)
        {
            return;
        }

        int width = GetVideoDimension(player.width);
        int height = GetVideoDimension(player.height);
        RenderTexture texture = new(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = $"Control Video | {runtime.Container.transform.parent.name} | {width}x{height}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.Create();

        runtime.Texture = texture;
        runtime.Display.texture = texture;

        AspectRatioFitter aspectRatioFitter = runtime.Display.GetComponent<AspectRatioFitter>();
        if (aspectRatioFitter != null)
        {
            aspectRatioFitter.aspectRatio = (float)width / height;
        }

        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = texture;
        player.time = 0d;
        player.Play();
    }

    private int GetVideoDimension(ulong dimension)
    {
        if (dimension == 0)
        {
            return fallbackRenderTextureSize;
        }

        return (int)Math.Min(dimension, 8192UL);
    }

    private void HandleVideoError(VideoPlayer player, string message)
    {
        Debug.LogError($"Control guide video failed: '{player.url}'. {message}", this);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private static T FindComponent<T>(Transform root, string objectName) where T : Component
    {
        Transform match = FindDescendant(root, objectName);
        return match == null ? null : match.GetComponent<T>();
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            if (string.Equals(descendant.name, objectName, StringComparison.Ordinal))
            {
                return descendant;
            }
        }

        return null;
    }
}
