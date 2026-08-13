#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NodeMapMilestoneVerifier
{
    public static void BuildRuntimeSmokePlayer()
    {
        string projectRoot = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(Application.dataPath, ".."));
        string outputPath = System.IO.Path.Combine(
            projectRoot,
            "NodeMapSmokeBuild",
            "LOADED.exe");
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Node map runtime smoke build failed: {report.summary.result}");
        }

        Debug.Log($"NODE_MAP_RUNTIME_SMOKE_BUILD_PASSED: {outputPath}");
    }

    public static void Verify()
    {
        VerifyScene<NodeMapController>("Assets/Scenes/NodeMap.unity");
        VerifyScene<StandaloneShopController>("Assets/Scenes/Shop.unity");
        VerifyScene<TreasureNodeController>("Assets/Scenes/Event.unity");
        VerifyBuildSettings();
        VerifyCatalog();
        VerifyProgressRules();
        VerifySaveRoundTrip();
        Debug.Log("NODE_MAP_MILESTONE_VERIFICATION_PASSED");
    }

    private static void VerifyScene<TController>(string path)
        where TController : Component
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            throw new InvalidOperationException($"Missing or invalid scene: {path}");
        }

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        if (UnityEngine.Object.FindFirstObjectByType<TController>() == null)
        {
            throw new InvalidOperationException(
                $"Scene '{path}' does not contain {typeof(TController).Name}.");
        }
    }

    private static void VerifyBuildSettings()
    {
        HashSet<string> enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToHashSet(StringComparer.Ordinal);
        string[] required =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/NodeMap.unity",
            "Assets/Scenes/Stage 1.unity",
            "Assets/Scenes/Shop.unity",
            "Assets/Scenes/Event.unity",
            "Assets/Scenes/Ending.unity"
        };

        foreach (string path in required)
        {
            if (!enabledScenes.Contains(path))
            {
                throw new InvalidOperationException(
                    $"Required build scene is not enabled: {path}");
            }
        }
    }

    private static void VerifyCatalog()
    {
        ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(
            "Assets/Resources/Run/ShopCatalog.asset");

        if (catalog == null || catalog.Bullets.Count < 3
            || catalog.Items.Count < 3)
        {
            throw new InvalidOperationException(
                "The standalone shop catalog requires at least three bullets and three items.");
        }

        if (catalog.Bullets.Any(bullet => bullet == null)
            || catalog.Items.Any(item => item == null))
        {
            throw new InvalidOperationException(
                "The standalone shop catalog contains a missing asset reference.");
        }
    }

    private static void VerifyProgressRules()
    {
        MethodInfo createMap = typeof(RunManager).GetMethod(
            "CreateMilestoneMap",
            BindingFlags.Static | BindingFlags.NonPublic);
        ActMapData map = createMap?.Invoke(null, null) as ActMapData;

        Require(map != null, "The fixed milestone map could not be created.");

        if (!map.Validate(out string error))
        {
            throw new InvalidOperationException(error);
        }

        RunMapProgress progress = new RunMapProgress(map, new RunMapState());
        Require(progress.CanEnter("battle_1"), "The connected battle must be enterable.");
        Require(!progress.CanEnter("shop"), "An unconnected shop must stay locked.");
        Require(progress.TryEnter("battle_1"), "Entering the first battle failed.");
        Require(!progress.TryEnter("battle_1"), "An active node was entered twice.");
        Require(progress.TryCompleteActiveNode(), "Completing the active battle failed.");
        Require(progress.IsCompleted("battle_1"), "Completed battle was not recorded.");
        Require(progress.CanEnter("shop") && progress.CanEnter("treasure"),
            "Completing the battle did not unlock both branches.");
        Require(!progress.CanEnter("boss"), "The boss unlocked before its prerequisite.");

        CompletePath(progress, "shop", "battle_2", "boss");
        Require(progress.IsCompleted("boss"), "The shop route did not complete the boss.");

        RunMapProgress treasureRoute = new RunMapProgress(map, new RunMapState());
        CompletePath(treasureRoute, "battle_1", "treasure", "battle_2", "boss");
        Require(treasureRoute.IsCompleted("treasure"),
            "The treasure route was not completed.");
        Require(!treasureRoute.CanEnter("treasure"),
            "A completed treasure node became enterable again.");
        UnityEngine.Object.DestroyImmediate(map);
    }

    private static void CompletePath(RunMapProgress progress, params string[] nodeIds)
    {
        foreach (string nodeId in nodeIds)
        {
            Require(progress.TryEnter(nodeId), $"Could not enter node '{nodeId}'.");
            Require(progress.TryCompleteActiveNode(),
                $"Could not complete node '{nodeId}'.");
        }
    }

    private static void VerifySaveRoundTrip()
    {
        RunSaveData source = new RunSaveData
        {
            stageIndex = 0,
            battleIndex = 0,
            currentHealth = 7,
            maxHealth = 10,
            money = 325,
            bullets = new List<RunBulletSaveData>
            {
                new RunBulletSaveData
                {
                    assetName = "VerificationBullet",
                    bulletId = "verify-bullet",
                    acquisitionOrder = 2,
                    location = 0,
                    locationIndex = 1
                }
            },
            inventoryItemAssetNames = new List<string>
            {
                "VerificationItem",
                string.Empty,
                string.Empty
            },
            map = new RunMapState
            {
                actId = "act_1",
                currentNodeId = "battle_1",
                visitedNodeIds = new List<string> { "start", "battle_1" },
                completedNodeIds = new List<string> { "battle_1" },
                pendingGold = 100
            }
        };
        string json = JsonUtility.ToJson(source);
        RunSaveData restored = JsonUtility.FromJson<RunSaveData>(json);

        Require(restored != null && restored.map != null,
            "Run map state was lost during JSON serialization.");
        Require(restored.map.currentNodeId == "battle_1",
            "Current node did not survive JSON serialization.");
        Require(restored.map.completedNodeIds.Contains("battle_1"),
            "Completed nodes did not survive JSON serialization.");
        Require(restored.map.pendingGold == 100,
            "Pending node reward did not survive JSON serialization.");
        Require(restored.currentHealth == 7 && restored.maxHealth == 10,
            "Player health did not survive JSON serialization.");
        Require(restored.money == 325,
            "Player gold did not survive JSON serialization.");
        Require(restored.bullets.Count == 1
            && restored.bullets[0].bulletId == "verify-bullet",
            "Owned bullets did not survive JSON serialization.");
        Require(restored.inventoryItemAssetNames.Count == 3
            && restored.inventoryItemAssetNames[0] == "VerificationItem",
            "Inventory items did not survive JSON serialization.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
