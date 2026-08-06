using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SoundManager : MonoBehaviour
{
    private const string DefaultLibraryPath = "Sound/SoundClipLibrary";
    private const float ComboPitchStep = 0.2f;
    private static SoundManager instance;

    [SerializeField] private SoundClipLibrary clipLibrary;
    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    private readonly List<AudioSource> sfxSources = new List<AudioSource>();
    private IReadOnlyList<AudioClip> currentPlaylist;
    private int lastBgmIndex = -1;
    private StateManager observedStateManager;

    public static SoundManager Instance { get { EnsureInstance(); return instance; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureInstance();

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureClipLibrary();
        EnsureAudioSources();
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void Start() => RefreshForScene(SceneManager.GetActiveScene());

    private void Update()
    {
        if (bgmSource != null && clipLibrary != null)
        {
            // Time.timeScale must not alter the authored BGM playback pitch.
            bgmSource.pitch = 1f;
            bgmSource.volume = clipLibrary.BgmVolume;
            ApplyMixerRouting();
        }

        if (currentPlaylist != null && !bgmSource.isPlaying) PlayNextBgm();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ObserveStateManager(null);
    }

    public static void PlayFire() => PlaySfx("SFX_Player_Shoot");
    public static void PlayReload() => PlaySfx("SFX_Player_Reload");
    public static void PlayHit() => PlaySfx("SFX_Player_Hit");

    public static void PlaySfx(string id)
    {
        SoundManager manager = Instance;
        if (manager.clipLibrary != null
            && manager.clipLibrary.TryGetSfx(
                id,
                out AudioClip clip,
                out float volume,
                out float pitch))
        {
            manager.PlayOneShot(clip, pitch, volume);
        }
    }

    public static void PlayComboDie(int comboKillCount)
    {
        SoundManager manager = Instance;
        float pitch = 1f + (Mathf.Max(1, comboKillCount) - 1) * ComboPitchStep;
        if (manager.clipLibrary != null
            && manager.clipLibrary.TryGetSfx(
                "SFX_Combo_Die",
                out AudioClip clip,
                out float volume))
        {
            manager.PlayOneShot(clip, pitch, volume);
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

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshForScene(scene);

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
        bgmSource.Stop();
        bgmSource.clip = null;
        if (HasValidClip(currentPlaylist)) PlayNextBgm();
    }

    private void PlayNextBgm()
    {
        if (!TryChooseNextBgmIndex(out int nextIndex)) { currentPlaylist = null; return; }
        lastBgmIndex = nextIndex;
        bgmSource.clip = currentPlaylist[nextIndex];
        bgmSource.Play();
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
        float volumeScale = 1f)
    {
        if (clip == null) return;
        AudioSource source = GetAvailableSfxSource();
        source.pitch = Mathf.Clamp(pitch, 0.01f, 3f);
        source.volume = sfxVolume;
        source.clip = null;
        source.PlayOneShot(clip, Mathf.Clamp(volumeScale, 0f, 2f));
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

        foreach (AudioSource source in sfxSources)
        {
            if (source != null)
            {
                source.outputAudioMixerGroup = clipLibrary.SfxMixerGroup;
            }
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
}
