using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneIntegrityTests
{
    [Test]
    public void EnabledBuildScenes_OpenWithoutMissingScripts()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        List<string> failures = new List<string>();

        try
        {
            foreach (EditorBuildSettingsScene buildScene
                     in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled)
                {
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        buildScene.path) == null)
                {
                    failures.Add($"Missing enabled scene: {buildScene.path}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(
                    buildScene.path,
                    OpenSceneMode.Single);
                int missingScriptCount = 0;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform child in
                             root.GetComponentsInChildren<Transform>(true))
                    {
                        missingScriptCount +=
                            GameObjectUtility
                                .GetMonoBehavioursWithMissingScriptCount(
                                    child.gameObject);
                    }
                }

                if (missingScriptCount > 0)
                {
                    failures.Add(
                        $"{buildScene.path}: {missingScriptCount} missing script(s)");
                }
            }
        }
        finally
        {
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }
}
