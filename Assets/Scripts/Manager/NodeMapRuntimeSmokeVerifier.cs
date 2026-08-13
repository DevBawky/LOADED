#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class NodeMapRuntimeSmokeVerifier : MonoBehaviour
{
    private const string CommandLineFlag = "-verifyNodeMapBootstrap";
    private const string MapSaveKey = "loaded.run.map.v1";
    private bool hadRunSave;
    private string previousRunSave;
    private bool hadMapSave;
    private string previousMapSave;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!System.Environment.GetCommandLineArgs().Contains(CommandLineFlag))
        {
            return;
        }

        GameObject owner = new GameObject("Node Map Runtime Smoke Verifier");
        DontDestroyOnLoad(owner);
        owner.AddComponent<NodeMapRuntimeSmokeVerifier>();
    }

    private IEnumerator Start()
    {
        BackupAndClearSaves();
        yield return null;
        SceneManager.LoadScene(RunManager.NodeMapSceneName);
        yield return null;
        yield return null;

        NodeMapController controller = FindFirstObjectByType<NodeMapController>();
        GameObject canvas = GameObject.Find("Canvas | Node Map");
        int nodeButtonCount = FindObjectsByType<Button>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None).Count(button =>
                button.name.StartsWith("Node | "));

        if (controller == null || canvas == null || nodeButtonCount < 6)
        {
            Fail(
                $"node map controller={controller != null}, "
                + $"canvas={canvas != null}, nodeButtons={nodeButtonCount}");
            yield break;
        }

        Debug.Log(
            $"NODE_MAP_RUNTIME_SMOKE_PASSED: nodeButtons={nodeButtonCount}");
        if (!PrepareShopRun())
        {
            Fail("shop smoke save could not be created");
            yield break;
        }

        SceneManager.LoadScene(RunManager.ShopSceneName);
        yield return null;
        yield return null;

        GameObject shopCanvas = GameObject.Find("Canvas | Shop");
        StandaloneShopController shopController =
            FindFirstObjectByType<StandaloneShopController>();
        Button[] shopButtons = shopCanvas == null
            ? System.Array.Empty<Button>()
            : shopCanvas.GetComponentsInChildren<Button>(true);
        int bulletOffers = shopButtons.Count(
            button => button.name == "Button | Bullet Item");
        int itemOffers = shopButtons.Count(
            button => button.name == "Button | Shop Item");
        bool hasStageOneShopPanel = shopCanvas != null
            && shopCanvas.GetComponentsInChildren<Transform>(true)
                .Any(candidate => candidate.name == "Panel | Shop");

        if (Camera.main == null || shopController == null
            || !hasStageOneShopPanel || bulletOffers != 3 || itemOffers != 2)
        {
            Fail(
                $"shop camera={Camera.main != null}, "
                + $"controller={shopController != null}, "
                + $"stageOnePanel={hasStageOneShopPanel}, "
                + $"bulletOffers={bulletOffers}, itemOffers={itemOffers}");
            yield break;
        }

        Debug.Log(
            "SHOP_RUNTIME_SMOKE_PASSED: Stage 1 Canvas, Main Camera, "
            + "3 bullet offers, 2 item offers");
        RestoreSaves();
        Application.Quit(0);
    }

    private bool PrepareShopRun()
    {
        ShopCatalog catalog = Resources.Load<ShopCatalog>("Run/ShopCatalog");
        BulletData starter = catalog.Bullets.First(bullet => bullet != null);
        RunMapState mapState = new RunMapState
        {
            actId = "act_1",
            currentNodeId = "battle_1",
            activeNodeId = "shop",
            visitedNodeIds = new List<string> { "start", "battle_1", "shop" },
            completedNodeIds = new List<string> { "battle_1" }
        };
        RunManager.Instance.Restore(mapState);
        RunSaveData saveData = new RunSaveData
        {
            flowState = (int)GameFlowState.Shop,
            stageIndex = 0,
            battleIndex = 0,
            currentHealth = 10,
            maxHealth = 10,
            money = 1000,
            bullets = new List<RunBulletSaveData>
            {
                new RunBulletSaveData
                {
                    assetName = starter.name,
                    bulletId = starter.BulletId,
                    acquisitionOrder = 0,
                    location = 0,
                    locationIndex = 0
                }
            },
            inventoryItemAssetNames = new List<string>
            {
                string.Empty,
                string.Empty,
                string.Empty
            },
            map = mapState
        };
        RunManager.Instance.ApplyToSave(saveData);

        return RunSaveSystem.Save(saveData);
    }

    private void BackupAndClearSaves()
    {
        hadRunSave = File.Exists(RunSaveSystem.SavePath);
        previousRunSave = hadRunSave
            ? File.ReadAllText(RunSaveSystem.SavePath)
            : null;
        hadMapSave = PlayerPrefs.HasKey(MapSaveKey);
        previousMapSave = PlayerPrefs.GetString(MapSaveKey, string.Empty);
        RunSaveSystem.DeleteSave();
        PlayerPrefs.DeleteKey(MapSaveKey);
        PlayerPrefs.Save();
    }

    private void RestoreSaves()
    {
        RunSaveSystem.DeleteSave();

        if (hadRunSave)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RunSaveSystem.SavePath));
            File.WriteAllText(RunSaveSystem.SavePath, previousRunSave);
        }

        if (hadMapSave)
        {
            PlayerPrefs.SetString(MapSaveKey, previousMapSave);
        }
        else
        {
            PlayerPrefs.DeleteKey(MapSaveKey);
        }

        PlayerPrefs.Save();
    }

    private void Fail(string details)
    {
        Debug.LogError($"NODE_MAP_RUNTIME_SMOKE_FAILED: {details}");
        RestoreSaves();
        Application.Quit(1);
    }
}
#endif
