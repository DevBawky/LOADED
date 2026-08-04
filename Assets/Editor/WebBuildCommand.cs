using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebBuildCommand
{
    [MenuItem("Tools/LOADED/Build WebGL")]
    public static void BuildWebGL()
    {
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
}
