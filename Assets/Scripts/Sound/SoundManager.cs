using UnityEngine;

public sealed class SoundManager : MonoBehaviour
{
    private const string DefaultLibraryPath = "Sound/SoundClipLibrary";

    private static SoundManager instance;

    [SerializeField] private SoundClipLibrary clipLibrary;
    [SerializeField] private AudioSource audioSource;
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;

    public static SoundManager Instance
    {
        get
        {
            EnsureInstance();
            return instance;
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
        EnsureAudioSource();
        EnsureClipLibrary();
    }

    public static void PlayFire()
    {
        Instance.PlayOneShot(Instance.clipLibrary?.FireClip);
    }

    public static void PlayReload()
    {
        Instance.PlayOneShot(Instance.clipLibrary?.ReloadClip);
    }

    public static void PlayHit()
    {
        Instance.PlayOneShot(Instance.clipLibrary?.HitClip);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindFirstObjectByType<SoundManager>();

        if (instance != null)
        {
            return;
        }

        GameObject soundManagerObject = new GameObject(nameof(SoundManager));
        instance = soundManagerObject.AddComponent<SoundManager>();
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        EnsureAudioSource();
        audioSource.PlayOneShot(clip, masterVolume);
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }

    private void EnsureClipLibrary()
    {
        if (clipLibrary == null)
        {
            clipLibrary = Resources.Load<SoundClipLibrary>(DefaultLibraryPath);
        }
    }
}
