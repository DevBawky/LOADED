using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SoundManager : MonoBehaviour
{
    private const string DefaultLibraryPath = "Sound/SoundClipLibrary";
    private const float ComboPitchGrowthRate = 0.22f;
    private const float ComboPitchRange = 1.5f;
    private const float BgmCompletionToleranceSeconds = 0.25f;
    private const float BgmFadeOutDuration = 0.45f;
    private const float BgmFadeInDuration = 0.65f;
    private const string BgmVolumePreferenceKey = "Audio.BGM.Volume";
    private const string SfxVolumePreferenceKey = "Audio.SFX.Volume";
    private static SoundManager instance;

    [SerializeField] private SoundClipLibrary clipLibrary;
    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [Range(0f, 1f)] [SerializeField] private float bgmVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    private readonly List<AudioSource> sfxSources = new List<AudioSource>();
    private readonly Dictionary<string, float> nonOverlappingSfxEndTimes =
        new Dictionary<string, float>();
    private IReadOnlyList<AudioClip> currentPlaylist;
    private IReadOnlyList<AudioClip> pendingPlaylist;
    private Coroutine bgmTransitionCoroutine;
    private float bgmFadeMultiplier = 1f;
    private bool gameOverBgmLocked;
    private int lastBgmIndex = -1;
    private AudioClip lastKnownBgmClip;
    private int lastKnownBgmTimeSamples;
    private SoundtrackDirector soundtrackDirector;
    private UiButtonFeedbackInstaller uiButtonFeedbackInstaller;

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
        soundtrackDirector = new SoundtrackDirector(this);
        uiButtonFeedbackInstaller = new UiButtonFeedbackInstaller();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        soundtrackDirector?.RefreshForScene(SceneManager.GetActiveScene());
    }
    private void Start()
    {
        soundtrackDirector.RefreshForScene(SceneManager.GetActiveScene());
        uiButtonFeedbackInstaller.ScanNow();
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

        uiButtonFeedbackInstaller.Tick();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        soundtrackDirector?.Dispose();
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
        Instance.uiButtonFeedbackInstaller.BindAudio(button);
    }

    public static void PlayComboDie(int firingSequenceKillCount)
    {
        SoundManager manager = Instance;
        float pitch = CalculateFiringSequenceKillPitch(
            firingSequenceKillCount);
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

    internal static float CalculateFiringSequenceKillPitch(
        int firingSequenceKillCount)
    {
        int additionalKills = Mathf.Max(0, firingSequenceKillCount - 1);
        return 1f + ComboPitchRange
            * (1f - Mathf.Exp(-additionalKills * ComboPitchGrowthRate));
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
        soundtrackDirector.RefreshForScene(scene);
        uiButtonFeedbackInstaller.ScanNow();
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

    internal SoundClipLibrary ClipLibrary
    {
        get
        {
            EnsureClipLibrary();
            return clipLibrary;
        }
    }

    internal bool IsGameOverBgmLocked => gameOverBgmLocked;

    internal void UnlockGameOverBgm()
    {
        gameOverBgmLocked = false;
    }

    internal void PlayPlaylist(IReadOnlyList<AudioClip> playlist)
    {
        SetPlaylist(playlist);
    }
}
