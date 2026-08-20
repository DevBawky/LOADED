using System;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public sealed class StreamingVideoPlayer : MonoBehaviour
{
    [SerializeField] private string streamingAssetsRelativePath;

    private VideoPlayer videoPlayer;
    private bool playWhenPrepared;

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        playWhenPrepared = videoPlayer.playOnAwake;

        videoPlayer.playOnAwake = false;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.prepareCompleted += HandlePrepared;
        videoPlayer.errorReceived += HandleError;

        videoPlayer.Stop();
        videoPlayer.clip = null;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = GetStreamingAssetsUrl(streamingAssetsRelativePath);
        videoPlayer.Prepare();
    }

    private void OnDestroy()
    {
        if (videoPlayer == null)
        {
            return;
        }

        videoPlayer.prepareCompleted -= HandlePrepared;
        videoPlayer.errorReceived -= HandleError;
    }

    private void HandlePrepared(VideoPlayer preparedPlayer)
    {
        if (playWhenPrepared)
        {
            preparedPlayer.Play();
        }
    }

    private void HandleError(VideoPlayer _, string message)
    {
        Debug.LogError(
            $"Video playback failed for '{streamingAssetsRelativePath}': {message}",
            this);
    }

    public static string GetStreamingAssetsUrl(string relativePath)
    {
        string normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        string[] segments = normalizedPath.Split('/');

        for (int index = 0; index < segments.Length; index++)
        {
            segments[index] = Uri.EscapeDataString(segments[index]);
        }

        return $"{Application.streamingAssetsPath.TrimEnd('/')}/{string.Join("/", segments)}";
    }
}
