using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SoundManager : MonoBehaviour
{
    private const string DefaultLibraryPath = "Sound/SoundClipLibrary";
    private const float ComboPitchStep = 0.2f;
    private const float UiClickPitchMultiplier = 0.9f;
    private const float UiButtonRescanInterval = 0.5f;
    private const float BgmCompletionToleranceSeconds = 0.25f;
    private const float BgmFadeOutDuration = 0.45f;
    private const float BgmFadeInDuration = 0.65f;
    private const string UiButtonSfxId = "UI_Button_Hover_Click";
    private const string BgmVolumePreferenceKey = "Audio.BGM.Volume";
    private const string SfxVolumePreferenceKey = "Audio.SFX.Volume";
    private static SoundManager instance;

    private static readonly string[] SpecialButtonNames =
    {
        "Button | Refresh",
        "Button | Remove",
        "Button | Upgrade",
        "Button | Move",
        "Button | Move (1)",
        "Button | Move L",
        "Button | Move R",
        "Button | Rotate",
        "Button | Wait",
        "Button | Reload",
        "Button | Shoot"
    };

    private static readonly string[] HoverScaleSpriteNames =
    {
        "Button_Delete",
        "Button_Management",
        "Button_Refresh",
        "Button_Settings",
        "Button_Upgrade"
    };

    private static readonly string[] HoverScaleButtonNames =
    {
        "Button | Go To Battle",
        "Button | Pause"
    };

    [SerializeField] private SoundClipLibrary clipLibrary;
    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [Range(0f, 1f)] [SerializeField] private float bgmVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    private readonly List<AudioSource> sfxSources = new List<AudioSource>();
    private readonly Dictionary<string, float> nonOverlappingSfxEndTimes =
        new Dictionary<string, float>();
    private readonly HashSet<Button> boundUiButtons = new HashSet<Button>();
    private IReadOnlyList<AudioClip> currentPlaylist;
    private IReadOnlyList<AudioClip> pendingPlaylist;
    private Coroutine bgmTransitionCoroutine;
    private float bgmFadeMultiplier = 1f;
    private bool gameOverBgmLocked;
    private int lastBgmIndex = -1;
    private AudioClip lastKnownBgmClip;
    private int lastKnownBgmTimeSamples;
    private StateManager observedStateManager;
    private float nextUiButtonScanTime;

    public static SoundManager Instance { get { EnsureInstance(); return instance; } }
    public static float BgmVolume => Instance.bgmVolume;
    public static float SfxVolume => Instance.sfxVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Application.runInBackground = true;
        EnsureInstance();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureClipLibrary();
        LoadVolumePreferences();
        EnsureAudioSources();
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void Start()
    {
        RefreshForScene(SceneManager.GetActiveScene());
        BindSceneUiButtons();
    }

    private void Update()
    {
        if (bgmSource != null && clipLibrary != null)
        {
            // Time.timeScale must not alter the authored BGM playback pitch.
            bgmSource.pitch = 1f;
            bgmSource.volume = clipLibrary.BgmVolume * bgmVolume
                * bgmFadeMultiplier;
            ApplyMixerRouting();
        }

        if (bgmSource != null && bgmSource.isPlaying)
        {
            RememberBgmPlaybackPosition();
        }
        else if (currentPlaylist != null && bgmTransitionCoroutine == null)
        {
            if (!TryResumeInterruptedBgm()) PlayNextBgm();
        }

        if (Time.unscaledTime >= nextUiButtonScanTime)
        {
            BindSceneUiButtons();
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ObserveStateManager(null);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            RememberBgmPlaybackPosition();
            return;
        }

        if (bgmTransitionCoroutine == null)
        {
            TryResumeInterruptedBgm();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            RememberBgmPlaybackPosition();
            return;
        }

        if (bgmTransitionCoroutine == null)
        {
            TryResumeInterruptedBgm();
        }
    }

    public static void PlayFire() => PlaySfx("SFX_Player_Shoot");
    public static void PlayReload() => PlaySfx("SFX_Player_Reload");
    public static void PlayHit() => PlaySfx("SFX_Player_Hit");

    public static void PlaySfx(string id)
    {
        PlaySfxPitched(id, 1f);
    }

    public static void PlaySfxNonOverlapping(string id)
    {
        SoundManager manager = Instance;
        if (string.IsNullOrWhiteSpace(id)
            || manager.nonOverlappingSfxEndTimes.TryGetValue(
                id,
                out float endTime)
            && Time.realtimeSinceStartup < endTime
            || manager.clipLibrary == null
            || !manager.clipLibrary.TryGetSfx(
                id,
                out AudioClip clip,
                out float volume,
                out float pitch,
                out UnityEngine.Audio.AudioMixerGroup mixerGroup))
        {
            return;
        }

        float playbackPitch = Mathf.Clamp(pitch, 0.01f, 3f);
        manager.nonOverlappingSfxEndTimes[id] =
            Time.realtimeSinceStartup + clip.length / playbackPitch;
        manager.PlayOneShot(clip, playbackPitch, volume, mixerGroup);
    }

    public static void PlaySfxPitched(string id, float pitchMultiplier)
    {
        SoundManager manager = Instance;
        if (manager.clipLibrary != null
            && manager.clipLibrary.TryGetSfx(
                id,
                out AudioClip clip,
                out float volume,
                out float pitch,
                out UnityEngine.Audio.AudioMixerGroup mixerGroup))
        {
            manager.PlayOneShot(
                clip,
                pitch * Mathf.Max(0.01f, pitchMultiplier),
                volume,
                mixerGroup);
        }
    }

    public static void BindUiButtonSfx(Button button)
    {
        Instance.TryBindUiButton(button);
    }

    public static void PlayComboDie(int comboKillCount)
    {
        SoundManager manager = Instance;
        float pitch = 1f + (Mathf.Max(1, comboKillCount) - 1) * ComboPitchStep;
        if (manager.clipLibrary != null
            && manager.clipLibrary.TryGetSfx(
                "SFX_Combo_Die",
                out AudioClip clip,
                out float volume,
                out _,
                out UnityEngine.Audio.AudioMixerGroup mixerGroup))
        {
            manager.PlayOneShot(clip, pitch, volume, mixerGroup);
        }
    }

    public static void ResetComboPitch() { }
    public static void StopBgm() => Instance.SetPlaylist(null);

    public static void PlayGameOverBgm()
    {
        SoundManager manager = Instance;
        manager.EnsureClipLibrary();
        manager.gameOverBgmLocked = true;
        manager.SetPlaylist(manager.clipLibrary?.GameOverBgm);
    }

    public static void SetBgmVolume(float volume)
    {
        SoundManager manager = Instance;
        PreviewBgmVolume(volume);
        PlayerPrefs.SetFloat(BgmVolumePreferenceKey, manager.bgmVolume);
        PlayerPrefs.Save();
    }

    public static void SetSfxVolume(float volume)
    {
        SoundManager manager = Instance;
        PreviewSfxVolume(volume);
        PlayerPrefs.SetFloat(SfxVolumePreferenceKey, manager.sfxVolume);
        PlayerPrefs.Save();
    }

    public static void PreviewBgmVolume(float volume)
    {
        SoundManager manager = Instance;
        manager.bgmVolume = Mathf.Clamp01(volume);
        manager.ApplyVolumes();
    }

    public static void PreviewSfxVolume(float volume)
    {
        SoundManager manager = Instance;
        manager.sfxVolume = Mathf.Clamp01(volume);
        manager.ApplyVolumes();
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        instance = FindFirstObjectByType<SoundManager>();
        if (instance == null)
        {
            instance = new GameObject(nameof(SoundManager)).AddComponent<SoundManager>();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshForScene(scene);
        BindSceneUiButtons();
    }

    private void RefreshForScene(Scene scene)
    {
        EnsureClipLibrary();
        gameOverBgmLocked = false;
        StateManager stateManager = FindFirstObjectByType<StateManager>(FindObjectsInactive.Include);
        ObserveStateManager(stateManager);
        if (stateManager != null) { RefreshForGameState(); return; }

        SetPlaylist(scene.name.IndexOf("MainMenu", System.StringComparison.OrdinalIgnoreCase) >= 0
            ? clipLibrary?.MainMenuBgm : null);
    }

    private void ObserveStateManager(StateManager stateManager)
    {
        if (observedStateManager == stateManager) return;
        if (observedStateManager != null) observedStateManager.StateChanged -= RefreshForGameState;
        observedStateManager = stateManager;
        if (observedStateManager != null) observedStateManager.StateChanged += RefreshForGameState;
    }

    private void RefreshForGameState()
    {
        if (gameOverBgmLocked) return;
        if (observedStateManager == null || clipLibrary == null) { SetPlaylist(null); return; }
        if (observedStateManager.CurrentState == GameFlowState.Shop)
        {
            SetPlaylist(clipLibrary.ShopBgm);
            return;
        }

        if (observedStateManager.CurrentState == GameFlowState.BattleClear)
        {
            // Keep the battle playlist running through the clear presentation.
            // The shop state will replace it with the shop playlist.
            return;
        }

        if (observedStateManager.CurrentState != GameFlowState.Battle)
        {
            SetPlaylist(null);
            return;
        }

        BattleData battle = observedStateManager.CurrentBattle;
        SetPlaylist(battle != null && battle.IsBoss
            ? clipLibrary.BossBgm
            : clipLibrary.GetBattleBgm(
                observedStateManager.CurrentStage?.StageId,
                observedStateManager.CurrentBattleIndex,
                battle?.BattleId));
    }

    private void SetPlaylist(IReadOnlyList<AudioClip> playlist)
    {
        if (ReferenceEquals(pendingPlaylist, playlist)) return;
        pendingPlaylist = playlist;

        if (bgmTransitionCoroutine != null)
        {
            StopCoroutine(bgmTransitionCoroutine);
        }

        bgmTransitionCoroutine = StartCoroutine(TransitionPlaylist());
    }

    private IEnumerator TransitionPlaylist()
    {
        bool hasOutgoingBgm = bgmSource != null
            && (bgmSource.isPlaying || bgmSource.clip != null);

        if (hasOutgoingBgm)
        {
            float startMultiplier = bgmFadeMultiplier;
            float elapsed = 0f;
            while (elapsed < BgmFadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / BgmFadeOutDuration);
                bgmFadeMultiplier = Mathf.Lerp(startMultiplier, 0f,
                    Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }
        }

        bgmFadeMultiplier = 0f;
        currentPlaylist = pendingPlaylist;
        lastBgmIndex = -1;
        lastKnownBgmClip = null;
        lastKnownBgmTimeSamples = 0;
        bgmSource.Stop();
        bgmSource.clip = null;

        if (!HasValidClip(currentPlaylist))
        {
            bgmTransitionCoroutine = null;
            yield break;
        }

        PlayNextBgm();
        float fadeInElapsed = 0f;
        while (fadeInElapsed < BgmFadeInDuration)
        {
            fadeInElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(fadeInElapsed / BgmFadeInDuration);
            bgmFadeMultiplier = Mathf.SmoothStep(0f, 1f, progress);
            yield return null;
        }

        bgmFadeMultiplier = 1f;
        bgmTransitionCoroutine = null;
    }

    private void PlayNextBgm()
    {
        if (!TryChooseNextBgmIndex(out int nextIndex)) { currentPlaylist = null; return; }
        lastBgmIndex = nextIndex;
        bgmSource.clip = currentPlaylist[nextIndex];
        lastKnownBgmClip = bgmSource.clip;
        lastKnownBgmTimeSamples = 0;
        RequestAudioData(bgmSource.clip);
        if (bgmSource.clip.loadState == AudioDataLoadState.Loaded)
        {
            bgmSource.Play();
        }
    }

    private void RememberBgmPlaybackPosition()
    {
        if (bgmSource == null || bgmSource.clip == null) return;

        int timeSamples = bgmSource.timeSamples;
        if (timeSamples <= 0 && lastKnownBgmClip == bgmSource.clip) return;

        lastKnownBgmClip = bgmSource.clip;
        lastKnownBgmTimeSamples = timeSamples;
    }

    private bool TryResumeInterruptedBgm()
    {
        if (bgmSource == null || bgmSource.isPlaying || lastKnownBgmClip == null
            || !PlaylistContains(lastKnownBgmClip))
        {
            return false;
        }

        RequestAudioData(lastKnownBgmClip);
        if (lastKnownBgmClip.loadState != AudioDataLoadState.Loaded)
        {
            return true;
        }

        int completionTolerance = Mathf.Max(
            1,
            Mathf.CeilToInt(lastKnownBgmClip.frequency * BgmCompletionToleranceSeconds));
        if (lastKnownBgmTimeSamples >= lastKnownBgmClip.samples - completionTolerance)
        {
            return false;
        }

        bgmSource.clip = lastKnownBgmClip;
        bgmSource.timeSamples = Mathf.Clamp(
            lastKnownBgmTimeSamples,
            0,
            Mathf.Max(0, lastKnownBgmClip.samples - 1));
        bgmSource.Play();
        return true;
    }

    private static void RequestAudioData(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }
    }

    private bool PlaylistContains(AudioClip clip)
    {
        if (currentPlaylist == null || clip == null) return false;
        foreach (AudioClip candidate in currentPlaylist)
        {
            if (candidate == clip) return true;
        }

        return false;
    }

    private bool TryChooseNextBgmIndex(out int selectedIndex)
    {
        selectedIndex = -1;
        if (currentPlaylist == null) return false;
        List<int> candidates = new List<int>();
        for (int index = 0; index < currentPlaylist.Count; index++)
        {
            if (currentPlaylist[index] != null && index != lastBgmIndex) candidates.Add(index);
        }
        if (candidates.Count == 0 && lastBgmIndex >= 0 && lastBgmIndex < currentPlaylist.Count
            && currentPlaylist[lastBgmIndex] != null)
        {
            selectedIndex = lastBgmIndex;
            return true;
        }
        if (candidates.Count == 0) return false;
        selectedIndex = candidates[Random.Range(0, candidates.Count)];
        return true;
    }

    private static bool HasValidClip(IReadOnlyList<AudioClip> clips)
    {
        if (clips == null) return false;
        foreach (AudioClip clip in clips) if (clip != null) return true;
        return false;
    }

    private void PlayOneShot(
        AudioClip clip,
        float pitch = 1f,
        float volumeScale = 1f,
        UnityEngine.Audio.AudioMixerGroup mixerGroup = null)
    {
        if (clip == null) return;
        RequestAudioData(clip);
        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            StartCoroutine(PlayOneShotWhenLoaded(
                clip,
                pitch,
                volumeScale,
                mixerGroup));
            return;
        }

        AudioSource source = GetAvailableSfxSource();
        source.outputAudioMixerGroup = GetWebCompatibleMixerGroup(
            mixerGroup != null ? mixerGroup : clipLibrary?.SfxMixerGroup);
        source.pitch = Mathf.Clamp(pitch, 0.01f, 3f);
        source.volume = sfxVolume;
        source.clip = null;
        source.PlayOneShot(clip, Mathf.Clamp(volumeScale, 0f, 2f));
    }

    private IEnumerator PlayOneShotWhenLoaded(
        AudioClip clip,
        float pitch,
        float volumeScale,
        UnityEngine.Audio.AudioMixerGroup mixerGroup)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (clip != null && clip.loadState == AudioDataLoadState.Loading
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (clip != null && clip.loadState == AudioDataLoadState.Loaded)
        {
            PlayOneShot(clip, pitch, volumeScale, mixerGroup);
        }
    }

    private AudioSource GetAvailableSfxSource()
    {
        foreach (AudioSource source in sfxSources)
        {
            if (source != null && !source.isPlaying) return source;
        }
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        ConfigureSfxSource(newSource);
        newSource.outputAudioMixerGroup = GetWebCompatibleMixerGroup(
            clipLibrary?.SfxMixerGroup);
        sfxSources.Add(newSource);
        return newSource;
    }

    private void EnsureAudioSources()
    {
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = false;
        bgmSource.spatialBlend = 0f;
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        ConfigureSfxSource(sfxSource);
        sfxSources.Clear();
        sfxSources.Add(sfxSource);
        ApplyVolumes();
        ApplyMixerRouting();
    }

    private void LoadVolumePreferences()
    {
        bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(
            BgmVolumePreferenceKey,
            bgmVolume));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(
            SfxVolumePreferenceKey,
            sfxVolume));
    }

    private void ApplyVolumes()
    {
        if (bgmSource != null)
        {
            float authoredVolume = clipLibrary == null ? 1f : clipLibrary.BgmVolume;
            bgmSource.volume = authoredVolume * bgmVolume
                * bgmFadeMultiplier;
        }

        foreach (AudioSource source in sfxSources)
        {
            if (source != null)
            {
                source.volume = sfxVolume;
            }
        }
    }

    private void ApplyMixerRouting()
    {
        if (clipLibrary == null) return;

        if (bgmSource != null)
        {
            bgmSource.outputAudioMixerGroup = GetWebCompatibleMixerGroup(
                clipLibrary.BgmMixerGroup);
        }

        if (sfxSource != null && !sfxSource.isPlaying)
        {
            sfxSource.outputAudioMixerGroup = GetWebCompatibleMixerGroup(
                clipLibrary.SfxMixerGroup);
        }
    }

    private static UnityEngine.Audio.AudioMixerGroup GetWebCompatibleMixerGroup(
        UnityEngine.Audio.AudioMixerGroup mixerGroup)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Unity Web only supports mixer volume. Bypass the authored effect
        // chains so unsupported effects cannot silence the Web Audio output.
        return null;
#else
        return mixerGroup;
#endif
    }

    private static void ConfigureSfxSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
    }

    private void EnsureClipLibrary()
    {
        if (clipLibrary == null) clipLibrary = Resources.Load<SoundClipLibrary>(DefaultLibraryPath);
    }

    private void BindSceneUiButtons()
    {
        nextUiButtonScanTime = Time.unscaledTime + UiButtonRescanInterval;
        boundUiButtons.RemoveWhere(button => button == null);

        foreach (Button candidate in FindObjectsByType<Button>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            TryBindSpriteHoverScale(candidate);
            TryBindUiButton(candidate);
        }
    }

    private static void TryBindSpriteHoverScale(Button button)
    {
        if (button == null || !ShouldUseHoverScale(button)) return;

        UiButtonSpriteHoverScale hoverScale =
            button.GetComponent<UiButtonSpriteHoverScale>();
        if (hoverScale == null)
        {
            hoverScale = button.gameObject.AddComponent<UiButtonSpriteHoverScale>();
        }

        hoverScale.Initialize(button);
    }

    private static bool ShouldUseHoverScale(Button button)
    {
        foreach (string buttonName in HoverScaleButtonNames)
        {
            if (button.name == buttonName)
            {
                return true;
            }
        }

        Image image = button.targetGraphic as Image;
        if (image == null)
        {
            image = button.GetComponent<Image>();
        }

        Sprite sprite = image == null ? null : image.sprite;
        if (sprite == null) return false;

        foreach (string spriteName in HoverScaleSpriteNames)
        {
            if (sprite.name == spriteName
                || sprite.name.StartsWith(spriteName + "_",
                    System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void TryBindUiButton(Button button)
    {
        if (button == null || IsSpecialButton(button)
            || !boundUiButtons.Add(button))
        {
            return;
        }

        button.onClick.AddListener(() =>
        {
            PlaySfxPitched(UiButtonSfxId, UiClickPitchMultiplier);
        });

        UiButtonHoverSfx hoverSfx = button.GetComponent<UiButtonHoverSfx>();
        if (hoverSfx == null)
        {
            hoverSfx = button.gameObject.AddComponent<UiButtonHoverSfx>();
        }

        hoverSfx.Initialize(button);
    }

    private static bool IsSpecialButton(Button button)
    {
        foreach (string specialName in SpecialButtonNames)
        {
            if (button.name == specialName) return true;
        }

        return false;
    }
}

[DisallowMultipleComponent]
internal sealed class UiButtonHoverSfx : MonoBehaviour, IPointerEnterHandler
{
    private Button button;

    public void Initialize(Button targetButton)
    {
        button = targetButton;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && button.IsActive() && button.IsInteractable())
        {
            SoundManager.PlaySfx("UI_Button_Hover_Click");
        }
    }
}

[DisallowMultipleComponent]
internal sealed class UiButtonSpriteHoverScale : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private const float HoverScale = 1.1f;
    private const float ScaleSpeed = 18f;

    private Button button;
    private Vector3 baseScale;
    private bool initialized;
    private bool pointerInside;

    public void Initialize(Button targetButton)
    {
        if (initialized && button == targetButton) return;

        button = targetButton;
        baseScale = transform.localScale;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;

        bool canEnlarge = pointerInside
            && button != null
            && button.IsActive()
            && button.IsInteractable();
        Vector3 targetScale = canEnlarge
            ? baseScale * HoverScale
            : baseScale;
        float blend = 1f - Mathf.Exp(-ScaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            blend);

        if ((transform.localScale - targetScale).sqrMagnitude < 0.000001f)
        {
            transform.localScale = targetScale;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
    }

    private void OnDisable()
    {
        pointerInside = false;
        if (initialized)
        {
            transform.localScale = baseScale;
        }
    }
}
