using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class ShopVideoController : MonoBehaviour
{
    private enum PlaybackState
    {
        Idle,
        Purchase
    }

    [Header("References")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Streaming Assets Videos")]
    [SerializeField] private string idleVideoPath = "Videos/Shop_Idle.mp4";
    [SerializeField] private string purchaseVideoPath = "Videos/Shop_Purchase.mp4";

    [Header("Playback Speed")]
    [Min(0.01f)]
    [FormerlySerializedAs("idleForwardPlaybackSpeed")]
    [SerializeField] private float idlePlaybackSpeed = 1f;
    [Min(0.01f)]
    [SerializeField] private float purchasePlaybackSpeed = 1f;

    private PlaybackState playbackState;
    private int queuedPurchases;
    private bool isSubscribed;

    public int QueuedPurchases => queuedPurchases;

    private void Awake()
    {
        ResolveReferences();

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        queuedPurchases = 0;
        PlayIdle();
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
    }

    private void ResolveReferences()
    {
        videoPlayer ??= GetComponent<VideoPlayer>();

        if (shopManager == null)
        {
            ShopManager[] managers = FindObjectsByType<ShopManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            shopManager = managers.Length == 0 ? null : managers[0];
        }
    }

    private void Subscribe()
    {
        if (isSubscribed || videoPlayer == null)
        {
            return;
        }

        videoPlayer.prepareCompleted += HandlePrepared;
        videoPlayer.loopPointReached += HandleLoopPointReached;
        videoPlayer.errorReceived += HandleVideoError;

        if (shopManager != null)
        {
            shopManager.PurchaseCompleted += HandlePurchaseCompleted;
        }

        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || videoPlayer == null)
        {
            return;
        }

        videoPlayer.prepareCompleted -= HandlePrepared;
        videoPlayer.loopPointReached -= HandleLoopPointReached;
        videoPlayer.errorReceived -= HandleVideoError;

        if (shopManager != null)
        {
            shopManager.PurchaseCompleted -= HandlePurchaseCompleted;
        }

        isSubscribed = false;
    }

    private void HandlePurchaseCompleted()
    {
        if (playbackState == PlaybackState.Purchase)
        {
            queuedPurchases++;
            return;
        }

        PlayPurchase();
    }

    private void PlayIdle()
    {
        PlayClip(
            PlaybackState.Idle,
            idleVideoPath,
            Mathf.Max(0.01f, idlePlaybackSpeed),
            true);
    }

    private void PlayPurchase()
    {
        PlayClip(
            PlaybackState.Purchase,
            purchaseVideoPath,
            Mathf.Max(0.01f, purchasePlaybackSpeed),
            false);
    }

    private void PlayClip(
        PlaybackState nextState,
        string videoPath,
        float playbackSpeed,
        bool loop)
    {
        if (videoPlayer == null || string.IsNullOrWhiteSpace(videoPath))
        {
            Debug.LogWarning(
                $"Shop video path for {nextState} is not assigned.",
                this);
            return;
        }

        playbackState = nextState;
        videoPlayer.Stop();
        videoPlayer.clip = null;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = StreamingVideoPlayer.GetStreamingAssetsUrl(videoPath);
        videoPlayer.playbackSpeed = playbackSpeed;
        videoPlayer.isLooping = loop;
        videoPlayer.Prepare();
    }

    private void HandlePrepared(VideoPlayer preparedPlayer)
    {
        preparedPlayer.time = 0d;
        preparedPlayer.Play();
    }

    private void HandleLoopPointReached(VideoPlayer player)
    {
        if (playbackState == PlaybackState.Idle)
        {
            return;
        }

        if (queuedPurchases > 0)
        {
            queuedPurchases--;
            player.time = 0d;
            player.playbackSpeed = Mathf.Max(
                0.01f,
                purchasePlaybackSpeed);
            player.Play();
            return;
        }

        PlayIdle();
    }

    private void HandleVideoError(VideoPlayer _, string message)
    {
        Debug.LogError($"Shop video playback failed: {message}", this);

        if (playbackState == PlaybackState.Purchase)
        {
            queuedPurchases = 0;
            PlayIdle();
        }
    }
}
