using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneIntegrityTests
{
    [Test]
    public void ShopStageBindingUsesTownLabel()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            EditorSceneManager.OpenScene(
                "Assets/Scenes/Shop.unity",
                OpenSceneMode.Single);
            StageProgressUI.EnsureSupportedSceneBinding();

            foreach (StageProgressUI progressUI in
                     Object.FindObjectsByType<StageProgressUI>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                progressUI.SetExternalStageTitle(
                    StageProgressUI.ShopStageTitle);
            }

            TMP_Text activeTitle = null;
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (text.name == "Text | Stage Title"
                    && text.gameObject.activeInHierarchy)
                {
                    activeTitle = text;
                    break;
                }
            }

            Assert.That(activeTitle, Is.Not.Null);
            Assert.That(activeTitle.text, Is.EqualTo("상점. 마을"));
        }
        finally
        {
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

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
