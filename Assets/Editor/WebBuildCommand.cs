using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebBuildCommand
{
    private const string WebGlPlatformName = "WebGL";
    private const string TemplateName = "PROJECT:LoadedOptimized";
    private const float WebBgmQuality = 0.55f;

    [MenuItem("Tools/LOADED/Apply WebGL Optimizations")]
    public static void ApplyWebGLOptimizations()
    {
        PlayerSettings.WebGL.compressionFormat =
            WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.initialMemorySize = 128;
        PlayerSettings.WebGL.template = TemplateName;
        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.WebGL,
            ManagedStrippingLevel.Medium);

        ApplyWebGlBgmOverrides();
        AssetDatabase.SaveAssets();

        Debug.Log(
            "WebGL optimizations applied: Brotli, browser caching, hashed " +
            "filenames, 128 MB initial memory, managed stripping, and " +
            "WebGL-specific BGM compression.");
    }

    [MenuItem("Tools/LOADED/Build WebGL")]
    public static void BuildWebGL()
    {
        ApplyWebGLOptimizations();

        string projectRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, ".."));
        string outputPath = Path.Combine(projectRoot, "WebBuild");
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException(
                "No enabled scenes were found in Build Settings.");
        }

        Directory.CreateDirectory(outputPath);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"WebGL build failed: {report.summary.result}");
        }

        Debug.Log(
            $"WebGL build completed: {outputPath} " +
            $"({report.summary.totalSize} bytes)");
    }

    private static void ApplyWebGlBgmOverrides()
    {
        string[] bgmGuids = AssetDatabase.FindAssets(
            "t:AudioClip",
            new[] { "Assets/Sounds/BGM" });

        foreach (string guid in bgmGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
            {
                continue;
            }

            AudioImporterSampleSettings desired =
                importer.defaultSampleSettings;
            desired.loadType = AudioClipLoadType.CompressedInMemory;
            desired.compressionFormat = AudioCompressionFormat.Vorbis;
            desired.quality = WebBgmQuality;
            desired.sampleRateSetting =
                AudioSampleRateSetting.OptimizeSampleRate;
            desired.preloadAudioData = false;

            bool hasOverride = importer.ContainsSampleSettingsOverride(
                WebGlPlatformName);
            AudioImporterSampleSettings current = hasOverride
                ? importer.GetOverrideSampleSettings(WebGlPlatformName)
                : default;

            if (hasOverride && Matches(current, desired))
            {
                continue;
            }

            importer.SetOverrideSampleSettings(WebGlPlatformName, desired);
            importer.SaveAndReimport();
        }
    }

    private static bool Matches(
        AudioImporterSampleSettings current,
        AudioImporterSampleSettings desired)
    {
        return current.loadType == desired.loadType
            && current.compressionFormat == desired.compressionFormat
            && Mathf.Approximately(current.quality, desired.quality)
            && current.sampleRateSetting == desired.sampleRateSetting
            && current.preloadAudioData == desired.preloadAudioData;
    }
}
