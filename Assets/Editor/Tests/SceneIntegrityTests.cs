using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneIntegrityTests
{
    [Test]
    public void StageOneBattleList_ContainsOnlyValidAuthoredBattles()
    {
        StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(
            "Assets/Scripts/Manager/Stage SO/Stage 1.asset");

        Assert.That(stage, Is.Not.Null);
        Assert.That(stage.Battles, Is.Not.Empty);

        int lastBattleIndex = stage.Battles.Count - 1;
        for (int index = 0; index < stage.Battles.Count; index++)
        {
            BattleData battle = stage.Battles[index];

            Assert.That(
                battle,
                Is.Not.Null,
                $"Stage 1 battle index {index} has a missing asset reference.");
            Assert.That(
                battle.TilePrefab,
                Is.Not.Null,
                $"Stage 1 battle '{battle.name}' is missing a tile prefab.");
            Assert.That(
                battle.Waves,
                Is.Not.Empty,
                $"Stage 1 battle '{battle.name}' has no authored waves.");
            Assert.That(
                battle.IsBoss,
                Is.EqualTo(index == lastBattleIndex),
                $"Stage 1 battle '{battle.name}' has the wrong boss flag.");
        }
    }

    [Test]
    public void NodeMapSettings_ReferencesBattlesInConfiguredStage()
    {
        NodeMapSettings settings = AssetDatabase.LoadAssetAtPath<
            NodeMapSettings>("Assets/Resources/NodeMapSettings.asset");

        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.Stage, Is.Not.Null);

        HashSet<BattleData> stageBattles = new HashSet<BattleData>(
            settings.Stage.Battles);
        AssertBattlePool(settings.EarlyNormalBattles, stageBattles, "early");
        AssertBattlePool(settings.MiddleNormalBattles, stageBattles, "middle");
        AssertBattlePool(settings.LateNormalBattles, stageBattles, "late");
        AssertBattlePool(settings.EliteBattles, stageBattles, "elite");
        Assert.That(
            settings.BossBattle,
            Is.Not.Null,
            "NodeMapSettings boss battle is missing.");
        Assert.That(
            stageBattles.Contains(settings.BossBattle),
            Is.True,
            "NodeMapSettings boss battle is not in its configured stage.");
    }

    [TestCase(1, NodeMapBattleProgressSection.Early)]
    [TestCase(6, NodeMapBattleProgressSection.Middle)]
    [TestCase(10, NodeMapBattleProgressSection.Late)]
    public void EventReplacementNormalBattleUsesNodeProgressPool(
        int nodeColumn,
        NodeMapBattleProgressSection expectedSection)
    {
        NodeMapSettings settings = AssetDatabase.LoadAssetAtPath<
            NodeMapSettings>("Assets/Resources/NodeMapSettings.asset");
        NodeMapNodeData eventNode = new NodeMapNodeData
        {
            id = 1,
            column = nodeColumn,
            type = NodeMapNodeType.Event
        };
        NodeMapRunData map = new NodeMapRunData();
        map.nodes.Add(new NodeMapNodeData
        {
            id = 0,
            column = 0,
            type = NodeMapNodeType.Start
        });
        map.nodes.Add(eventNode);
        map.nodes.Add(new NodeMapNodeData
        {
            id = 2,
            column = 14,
            type = NodeMapNodeType.Boss
        });

        IReadOnlyList<BattleData> candidates =
            NodeMapControllerDefinition.GetBattleCandidatesForNode(
                settings,
                map,
                eventNode,
                BattleType.Normal);

        Assert.That(
            candidates,
            Is.EqualTo(settings.GetNormalBattles(expectedSection)));
    }

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

    [Test]
    public void EventPanel_PreservesControllerBindingsAndLoadedPresentation()
    {
        GameObject panel = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Event/Panel_Event.prefab");

        Assert.That(panel, Is.Not.Null);
        AssertNamedComponent<Image>(panel, "Image | Event Artwork");
        AssertNamedComponent<TMP_Text>(panel, "Text | Event Title");
        AssertNamedComponent<TMP_Text>(panel, "Text | Event Dialogue");

        for (int index = 1; index <= 3; index++)
        {
            Button choice = AssertNamedComponent<Button>(
                panel,
                $"Button | Event Choice {index}");
            AssertLoadedButton(choice);
        }

        AssertNamedComponent<Image>(panel, "Image | Top Ember Line");
        AssertNamedComponent<Image>(panel, "Image | Artwork Footer");
        AssertNamedComponent<Image>(panel, "Image | Dialogue Divider");
    }

    [Test]
    public void TreasurePanel_PreservesControllerBindingsAndLoadedPresentation()
    {
        GameObject panel = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Treasure/Panel_Treasure.prefab");

        Assert.That(panel, Is.Not.Null);
        Button chest = AssertNamedComponent<Button>(
            panel,
            "Button | Treasure Chest");
        Assert.That(
            chest.GetComponent<MainButtonShaderFeedback>(),
            Is.Null,
            "The chest must keep its authored sprite-compatible UI material.");
        AssertNamedComponent<TMP_Text>(panel, "Text | Chest Label");
        AssertNamedComponent<Transform>(panel, "Panel | Relic Choices");
        AssertNamedComponent<TMP_Text>(
            panel,
            "Text | Treasure Instruction");

        for (int index = 1; index <= 3; index++)
        {
            Button choice = AssertNamedComponent<Button>(
                panel,
                $"Button | Relic Choice {index}");
            AssertLoadedButton(choice);
            AssertNamedComponent<Image>(
                panel,
                $"Image | Relic Icon {index}");
            AssertNamedComponent<TMP_Text>(
                panel,
                $"Text | Relic Name {index}");
            AssertNamedComponent<TMP_Text>(
                panel,
                $"Text | Relic Description {index}");
        }

        Button continueButton = AssertNamedComponent<Button>(
            panel,
            "Button | Treasure Continue");
        AssertLoadedButton(continueButton);
        AssertNamedComponent<Image>(panel, "Image | Top Ember Line");
        AssertNamedComponent<Transform>(panel, "Panel | Chest Housing");
    }

    [Test]
    public void TreasureCanvas_CustomRelicChoicesUseLoadedCards()
    {
        GameObject canvas = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Treasure/TreasureCanvas.prefab");

        Assert.That(canvas, Is.Not.Null);
        Transform choices = AssertNamedComponent<Transform>(
            canvas,
            "Panel | Relic Choice");
        Assert.That(choices.GetComponent<Outline>(), Is.Not.Null);

        for (int index = 1; index <= 3; index++)
        {
            Button choice = AssertNamedComponent<Button>(
                canvas,
                $"Button | Relic {index}");
            AssertLoadedButton(choice);
            Image icon = AssertNamedComponent<Image>(
                canvas,
                $"Image | Relic Icon {index}");
            Assert.That(
                icon.material == null
                    || icon.material.shader.name != "Loaded/UI/Main Button",
                Is.True,
                "Relic artwork must keep a sprite-compatible UI material.");
            AssertNamedComponent<TMP_Text>(
                canvas,
                $"Text | Relic Name {index}");
            AssertNamedComponent<TMP_Text>(
                canvas,
                $"Text | Relic Description {index}");
        }
    }

    private static void AssertBattlePool(
        IReadOnlyList<BattleData> battles,
        HashSet<BattleData> stageBattles,
        string poolName)
    {
        Assert.That(
            battles,
            Is.Not.Empty,
            $"NodeMapSettings {poolName} battle pool is empty.");

        for (int index = 0; index < battles.Count; index++)
        {
            BattleData battle = battles[index];

            Assert.That(
                battle,
                Is.Not.Null,
                $"NodeMapSettings {poolName} battle index {index} is missing.");
            Assert.That(
                stageBattles.Contains(battle),
                Is.True,
                $"NodeMapSettings {poolName} battle '{battle.name}' is not in its configured stage.");
        }
    }

    private static T AssertNamedComponent<T>(
        GameObject root,
        string objectName)
        where T : Component
    {
        Transform match = root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(child => child.name == objectName);
        Assert.That(match, Is.Not.Null, $"Missing '{objectName}'.");

        T component = match.GetComponent<T>();
        Assert.That(
            component,
            Is.Not.Null,
            $"'{objectName}' is missing {typeof(T).Name}.");
        return component;
    }

    private static void AssertLoadedButton(Button button)
    {
        Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));
        Assert.That(
            button.GetComponent<MainButtonShaderFeedback>(),
            Is.Not.Null,
            $"'{button.name}' is missing shader feedback.");
        Assert.That(button.image, Is.Not.Null);
        Assert.That(button.image.material, Is.Not.Null);
        Assert.That(button.image.material.shader, Is.Not.Null);
        Assert.That(
            button.image.material.shader.name,
            Is.EqualTo("Loaded/UI/Main Button"));
    }
}
