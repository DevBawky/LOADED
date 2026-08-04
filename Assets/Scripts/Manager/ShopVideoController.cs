using System.Collections;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class ShopVideoController : MonoBehaviour
{
    private enum PlaybackState
    {
        IdleForward,
        IdleReverse,
        Purchase
    }

    [Header("References")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Clips")]
    [SerializeField] private VideoClip idleClip;
    [Tooltip("Optional. A pre-rendered reverse clip gives the smoothest and most portable reverse playback.")]
    [SerializeField] private VideoClip idleReverseClip;
    [SerializeField] private VideoClip purchaseClip;

    private PlaybackState playbackState;
    private Coroutine manualReverseCoroutine;
    private int queuedPurchases;
    private bool isSubscribed;
    private bool isWatchingNativeReverse;
    private bool nativeReverseHasAdvanced;
    private double nativeReverseStartTime;
    private float nativeReverseStartedAt;

    public int QueuedPurchases => queuedPurchases;

    private void Awake()
    {
        ResolveReferences();

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        queuedPurchases = 0;
        PlayState(PlaybackState.IdleForward);
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopManualReverse();

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
    }

    private void Update()
    {
        if (!isWatchingNativeReverse || videoPlayer == null
            || playbackState != PlaybackState.IdleReverse)
        {
            return;
        }

        double completionThreshold = videoPlayer.frameRate > 0f
            ? 1.5d / videoPlayer.frameRate
            : 0.02d;

        if (videoPlayer.time < nativeReverseStartTime - completionThreshold)
        {
            nativeReverseHasAdvanced = true;
        }

        if (nativeReverseHasAdvanced
            && videoPlayer.time <= completionThreshold)
        {
            isWatchingNativeReverse = false;
            HandlePlaybackCompleted(videoPlayer);
            return;
        }

        if (!nativeReverseHasAdvanced
            && Time.unscaledTime - nativeReverseStartedAt >= 1f)
        {
            isWatchingNativeReverse = false;
            videoPlayer.playbackSpeed = 1f;
            manualReverseCoroutine = StartCoroutine(
                PlayReverseBySeeking(videoPlayer, nativeReverseStartTime));
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
        videoPlayer.loopPointReached += HandlePlaybackCompleted;
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
        videoPlayer.loopPointReached -= HandlePlaybackCompleted;
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

        PlayState(PlaybackState.Purchase);
    }

    private void PlayState(PlaybackState nextState)
    {
        if (videoPlayer == null)
        {
            return;
        }

        VideoClip nextClip = GetClip(nextState);

        if (nextClip == null)
        {
            Debug.LogWarning(
                $"Shop video clip for {nextState} is not assigned.",
                this);
            return;
        }

        StopManualReverse();
        isWatchingNativeReverse = false;
        playbackState = nextState;
        videoPlayer.Stop();
        videoPlayer.playbackSpeed = 1f;
        videoPlayer.isLooping = false;
        videoPlayer.clip = nextClip;
        videoPlayer.Prepare();
    }

    private VideoClip GetClip(PlaybackState state)
    {
        if (state == PlaybackState.Purchase)
        {
            return purchaseClip;
        }

        if (state == PlaybackState.IdleReverse && idleReverseClip != null)
        {
            return idleReverseClip;
        }

        return idleClip;
    }

    private void HandlePrepared(VideoPlayer preparedPlayer)
    {
        bool needsNativeReverse = playbackState == PlaybackState.IdleReverse
            && idleReverseClip == null;

        if (!needsNativeReverse)
        {
            preparedPlayer.time = 0d;
            preparedPlayer.Play();
            return;
        }

        double lastFrameTime = GetLastFrameTime(preparedPlayer);

        if (preparedPlayer.canSetPlaybackSpeed)
        {
            preparedPlayer.time = lastFrameTime;
            preparedPlayer.playbackSpeed = -1f;
            preparedPlayer.Play();
            nativeReverseStartTime = lastFrameTime;
            nativeReverseStartedAt = Time.unscaledTime;
            nativeReverseHasAdvanced = false;
            isWatchingNativeReverse = true;
            return;
        }

        manualReverseCoroutine = StartCoroutine(
            PlayReverseBySeeking(preparedPlayer, lastFrameTime));
    }

    private IEnumerator PlayReverseBySeeking(
        VideoPlayer preparedPlayer,
        double startTime)
    {
        preparedPlayer.time = startTime;
        preparedPlayer.Play();
        yield return null;
        preparedPlayer.Pause();

        double currentTime = startTime;

        while (isActiveAndEnabled
            && playbackState == PlaybackState.IdleReverse
            && currentTime > 0d)
        {
            currentTime = System.Math.Max(
                0d,
                currentTime - Time.unscaledDeltaTime);
            preparedPlayer.time = currentTime;
            yield return null;
        }

        manualReverseCoroutine = null;

        if (isActiveAndEnabled
            && playbackState == PlaybackState.IdleReverse)
        {
            HandlePlaybackCompleted(preparedPlayer);
        }
    }

    private static double GetLastFrameTime(VideoPlayer player)
    {
        if (player.frameRate > 0f)
        {
            return System.Math.Max(0d, player.length - 1d / player.frameRate);
        }

        return System.Math.Max(0d, player.length - 0.001d);
    }

    private void HandlePlaybackCompleted(VideoPlayer _)
    {
        isWatchingNativeReverse = false;

        switch (playbackState)
        {
            case PlaybackState.Purchase:
                if (queuedPurchases > 0)
                {
                    queuedPurchases--;
                    PlayState(PlaybackState.Purchase);
                }
                else
                {
                    PlayState(PlaybackState.IdleForward);
                }

                break;

            case PlaybackState.IdleForward:
                PlayState(PlaybackState.IdleReverse);
                break;

            case PlaybackState.IdleReverse:
                PlayState(PlaybackState.IdleForward);
                break;
        }
    }

    private void HandleVideoError(VideoPlayer _, string message)
    {
        Debug.LogError($"Shop video playback failed: {message}", this);
        HandlePlaybackCompleted(videoPlayer);
    }

    private void StopManualReverse()
    {
        if (manualReverseCoroutine == null)
        {
            return;
        }

        StopCoroutine(manualReverseCoroutine);
        manualReverseCoroutine = null;
    }
}
