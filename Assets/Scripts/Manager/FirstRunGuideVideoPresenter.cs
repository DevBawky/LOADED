using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

internal sealed class FirstRunGuideVideoPresenter : IDisposable
{
    private const string LoadingMessage = "영상 불러오는 중...";
    private const string FailureMessage =
        "영상을 불러오지 못했습니다.\n미션은 그대로 진행할 수 있습니다.";

    private readonly GameObject videoFrame;
    private readonly RawImage videoDisplay;
    private readonly TMP_Text videoLoadingText;
    private readonly AspectRatioFitter videoAspect;
    private readonly VideoPlayer videoPlayer;
    private readonly UnityEngine.Object logContext;

    private bool shouldPlay;
    private bool disposed;

    internal FirstRunGuideVideoPresenter(
        GameObject videoFrame,
        RawImage videoDisplay,
        TMP_Text videoLoadingText,
        AspectRatioFitter videoAspect,
        VideoPlayer videoPlayer,
        UnityEngine.Object logContext)
    {
        this.videoFrame = videoFrame;
        this.videoDisplay = videoDisplay;
        this.videoLoadingText = videoLoadingText;
        this.videoAspect = videoAspect;
        this.videoPlayer = videoPlayer;
        this.logContext = logContext;

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted += HandleVideoPrepared;
            videoPlayer.frameReady += HandleVideoFrameReady;
            videoPlayer.errorReceived += HandleVideoError;
        }
    }

    internal void Show(string relativePath)
    {
        bool hasVideo = !string.IsNullOrWhiteSpace(relativePath);
        if (videoFrame != null)
        {
            videoFrame.SetActive(hasVideo);
        }

        Stop();
        if (!hasVideo || videoPlayer == null)
        {
            return;
        }

        shouldPlay = true;
        if (videoDisplay != null)
        {
            videoDisplay.texture = null;
        }

        if (videoLoadingText != null)
        {
            videoLoadingText.gameObject.SetActive(true);
            videoLoadingText.text = LoadingMessage;
        }

        videoPlayer.url = StreamingVideoPlayer.GetStreamingAssetsUrl(relativePath);
        videoPlayer.Prepare();
    }

    internal void Pause()
    {
        shouldPlay = false;
        if (videoPlayer != null)
        {
            videoPlayer.Pause();
        }
    }

    internal void Stop()
    {
        shouldPlay = false;
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        if (videoDisplay != null)
        {
            videoDisplay.texture = null;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stop();
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= HandleVideoPrepared;
            videoPlayer.frameReady -= HandleVideoFrameReady;
            videoPlayer.errorReceived -= HandleVideoError;
        }
    }

    internal static float CalculateAspectRatio(ulong width, ulong height)
    {
        int clampedWidth = width == 0
            ? 16
            : (int)Math.Min(width, 8192UL);
        int clampedHeight = height == 0
            ? 9
            : (int)Math.Min(height, 8192UL);
        return (float)clampedWidth / Mathf.Max(1, clampedHeight);
    }

    private void HandleVideoPrepared(VideoPlayer preparedPlayer)
    {
        if (preparedPlayer == null || videoDisplay == null)
        {
            return;
        }

        AssignVideoTexture(preparedPlayer);
        if (videoAspect != null)
        {
            videoAspect.aspectRatio = CalculateAspectRatio(
                preparedPlayer.width,
                preparedPlayer.height);
        }

        if (videoLoadingText != null)
        {
            videoLoadingText.gameObject.SetActive(false);
        }

        if (shouldPlay)
        {
            preparedPlayer.time = 0d;
            preparedPlayer.Play();
        }
    }

    private void HandleVideoFrameReady(VideoPlayer preparedPlayer, long _)
    {
        AssignVideoTexture(preparedPlayer);
    }

    private void AssignVideoTexture(VideoPlayer preparedPlayer)
    {
        if (preparedPlayer != null && preparedPlayer.texture != null
            && videoDisplay != null)
        {
            videoDisplay.texture = preparedPlayer.texture;
        }
    }

    private void HandleVideoError(VideoPlayer failedPlayer, string message)
    {
        if (videoLoadingText != null)
        {
            videoLoadingText.gameObject.SetActive(true);
            videoLoadingText.text = FailureMessage;
        }

        string url = failedPlayer != null ? failedPlayer.url : string.Empty;
        Debug.LogWarning(
            $"First-run guide video failed: '{url}'. {message}",
            logContext);
    }
}
