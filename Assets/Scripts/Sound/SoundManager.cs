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
    private const string UiButtonSfxId = "UI_Button_Hover_Click";
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

    [SerializeField] private SoundClipLibrary clipLibrary;
    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    private readonly List<AudioSource> sfxSources = new List<AudioSource>();
    private readonly HashSet<Button> boundUiButtons = new HashSet<Button>();
    private IReadOnlyList<AudioClip> currentPlaylist;
    private int lastBgmIndex = -1;
    private AudioClip lastKnownBgmClip;
    private int lastKnownBgmTimeSamples;
    private StateManager observedStateManager;
    private float nextUiButtonScanTime;
    private bool webAudioUnlocked = true;

    public static SoundManager Instance { get { EnsureInstance(); return instance; } }

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
        EnsureAudioSources();
#if UNITY_WEBGL && !UNITY_EDITOR
        // Browsers suspend Web Audio until the page receives a user gesture.
        webAudioUnlocked = false;
#endif
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void Start()
    {
        RefreshForScene(SceneManager.GetActiveScene());
        BindSceneUiButtons();
    }

    private void Update()
    {
        TryUnlockWebAudio();

        if (bgmSource != null && clipLibrary != null)
        {
            // Time.timeScale must not alter the authored BGM playback pitch.
            bgmSource.pitch = 1f;
            bgmSource.volume = clipLibrary.BgmVolume;
            ApplyMixerRouting();
        }

        if (bgmSource != null && bgmSource.isPlaying)
        {
            RememberBgmPlaybackPosition();
        }
        else if (currentPlaylist != null && CanPlayAudio)
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

        TryResumeInterruptedBgm();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            RememberBgmPlaybackPosition();
            return;
        }

        TryResumeInterruptedBgm();
    }

    public static void PlayFire() => PlaySfx("SFX_Player_Shoot");
    public static void PlayReload() => PlaySfx("SFX_Player_Reload");
    public static void PlayHit() => PlaySfx("SFX_Player_Hit");

    public static void PlaySfx(string id)
    {
        PlaySfxPitched(id, 1f);
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
        if (ReferenceEquals(currentPlaylist, playlist)) return;
        currentPlaylist = playlist;
        lastBgmIndex = -1;
        lastKnownBgmClip = null;
        lastKnownBgmTimeSamples = 0;
        bgmSource.Stop();
        bgmSource.clip = null;
        if (HasValidClip(currentPlaylist)) PlayNextBgm();
    }

    private void PlayNextBgm()
    {
        if (!TryChooseNextBgmIndex(out int nextIndex)) { currentPlaylist = null; return; }
        lastBgmIndex = nextIndex;
        bgmSource.clip = currentPlaylist[nextIndex];
        lastKnownBgmClip = bgmSource.clip;
        lastKnownBgmTimeSamples = 0;
        RequestAudioData(bgmSource.clip);
        if (CanPlayAudio && bgmSource.clip.loadState == AudioDataLoadState.Loaded)
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
        if (!CanPlayAudio || bgmSource == null || bgmSource.isPlaying
            || lastKnownBgmClip == null
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

    private bool CanPlayAudio => webAudioUnlocked;

    private void TryUnlockWebAudio()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (webAudioUnlocked || !ReceivedAudioUnlockInput()) return;

        webAudioUnlocked = true;
        AudioListener.pause = false;
        TryResumeInterruptedBgm();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static bool ReceivedAudioUnlockInput()
    {
        return Input.anyKeyDown
            || Input.GetMouseButtonDown(0)
            || Input.GetMouseButtonDown(1)
            || Input.GetMouseButtonDown(2)
            || Input.touchCount > 0;
    }
#endif

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
        TryUnlockWebAudio();
        if (!CanPlayAudio) return;
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
        source.outputAudioMixerGroup = mixerGroup != null
            ? mixerGroup
            : clipLibrary?.SfxMixerGroup;
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
        newSource.outputAudioMixerGroup = clipLibrary?.SfxMixerGroup;
        sfxSources.Add(newSource);
        return newSource;
    }

    private void EnsureAudioSources()
    {
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = false;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume = clipLibrary == null ? 1f : clipLibrary.BgmVolume;
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        ConfigureSfxSource(sfxSource);
        sfxSources.Clear();
        sfxSources.Add(sfxSource);
        ApplyMixerRouting();
    }

    private void ApplyMixerRouting()
    {
        if (clipLibrary == null) return;

        if (bgmSource != null)
        {
            bgmSource.outputAudioMixerGroup = clipLibrary.BgmMixerGroup;
        }

        if (sfxSource != null && !sfxSource.isPlaying)
        {
            sfxSource.outputAudioMixerGroup = clipLibrary.SfxMixerGroup;
        }
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
            TryBindUiButton(candidate);
        }
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

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers ??= new List<EventTrigger.Entry>();

        EventTrigger.Entry hoverEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter,
            callback = new EventTrigger.TriggerEvent()
        };
        hoverEntry.callback.AddListener(_ =>
        {
            if (button != null && button.IsActive() && button.IsInteractable())
            {
                PlaySfx(UiButtonSfxId);
            }
        });
        trigger.triggers.Add(hoverEntry);
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
