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
        if (!PrepareBattleRun())
        {
            Fail("persistent manager smoke save could not be created");
            yield break;
        }

        RunSaveSystem.RequestStart(RunStartMode.Continue);
        SceneManager.LoadScene(RunManager.CombatSceneName);

        for (int frame = 0; frame < 120
             && (PersistentRunContext.Instance == null
                 || PersistentRunContext.Instance.StateManager == null
                 || PersistentRunContext.Instance.StateManager.CurrentState
                    != GameFlowState.Battle); frame++)
        {
            yield return null;
        }

        PersistentRunContext runContext = PersistentRunContext.Instance;
        StateManager stateManager = runContext?.StateManager;

        if (stateManager == null
            || stateManager.CurrentState != GameFlowState.Battle)
        {
            Fail("Stage 1 managers were not captured after the first battle");
            yield break;
        }

        FindFirstObjectByType<WaveManager>()?.StopBattle();

        if (!RunManager.Instance.CompleteActiveNode()
            || !RunManager.Instance.TryEnterNode("shop"))
        {
            Fail("the completed first battle could not enter the shop node");
            yield break;
        }

        float shopLoadDeadline = Time.realtimeSinceStartup + 15f;

        while (SceneManager.GetActiveScene().name != RunManager.ShopSceneName
               && Time.realtimeSinceStartup < shopLoadDeadline)
        {
            yield return null;
        }

        yield return null;

        PersistentGameCanvas persistentCanvas = PersistentGameCanvas.Instance;
        GameObject shopCanvas = persistentCanvas == null
            ? null
            : persistentCanvas.Root;
        StandaloneShopController shopController =
            FindFirstObjectByType<StandaloneShopController>();
        ShopManager shopManager = FindFirstObjectByType<ShopManager>();
        BulletManagementUI bulletManagement =
            FindFirstObjectByType<BulletManagementUI>();
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
        bool hasActiveFloatingPanel = shopCanvas != null
            && shopCanvas.GetComponentsInChildren<Transform>(true)
                .Any(candidate => candidate.name == "Panel | Floating"
                    && candidate.gameObject.activeInHierarchy);
        bool hasActiveMainGamePanel = shopCanvas != null
            && shopCanvas.GetComponentsInChildren<Transform>(true)
                .Any(candidate => candidate.name == "Panel | MainGame"
                    && candidate.gameObject.activeInHierarchy);
        Transform shopItemsLayout = shopCanvas == null
            ? null
            : shopCanvas.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate =>
                    candidate.name == "Layout | Shop Items"
                    && candidate.gameObject.activeInHierarchy);
        Button refreshButton = shopButtons.FirstOrDefault(
            button => button.name == "Button | Refresh");
        Button manageButton = shopButtons.FirstOrDefault(
            button => button.name == "Button | Manage Bullet");

        if (Camera.main == null || shopController == null
            || runContext == null || shopManager == null
            || bulletManagement == null
            || !hasStageOneShopPanel || !hasActiveFloatingPanel
            || hasActiveMainGamePanel
            || shopItemsLayout == null
            || !shopItemsLayout.gameObject.activeInHierarchy
            || refreshButton == null
            || !refreshButton.gameObject.activeInHierarchy
            || manageButton == null
            || !manageButton.gameObject.activeInHierarchy
            || bulletOffers != 3 || itemOffers != 2)
        {
            Fail(
                $"shop camera={Camera.main != null}, "
                + $"scene={SceneManager.GetActiveScene().name}, "
                + $"controller={shopController != null}, "
                + $"stageOnePanel={hasStageOneShopPanel}, "
                + $"floating={hasActiveFloatingPanel}, "
                + $"mainGame={hasActiveMainGamePanel}, "
                + $"shopItems={shopItemsLayout != null && shopItemsLayout.gameObject.activeInHierarchy}, "
                + $"refresh={refreshButton != null && refreshButton.gameObject.activeInHierarchy}, "
                + $"manage={manageButton != null && manageButton.gameObject.activeInHierarchy}, "
                + $"bulletOffers={bulletOffers}, itemOffers={itemOffers}");
            yield break;
        }

        int refreshCost = shopManager.CurrentRefreshCost;
        int offersChangedCount = 0;
        System.Action handleOffersChanged = () => offersChangedCount++;
        shopManager.OffersChanged += handleOffersChanged;

        if (!shopManager.TryRefreshOffers())
        {
            shopManager.OffersChanged -= handleOffersChanged;
            Fail("the persistent ShopManager could not refresh offers");
            yield break;
        }

        float refreshDeadline = Time.realtimeSinceStartup + 10f;

        while (shopManager.IsRefreshing
               && Time.realtimeSinceStartup < refreshDeadline)
        {
            yield return null;
        }

        shopManager.OffersChanged -= handleOffersChanged;

        if (shopManager.IsRefreshing
            || offersChangedCount == 0)
        {
            Fail(
                $"shop refresh completed={!shopManager.IsRefreshing}, "
                + $"offersChanged={offersChangedCount}, "
                + $"cost={refreshCost}->{shopManager.CurrentRefreshCost}");
            yield break;
        }

        bulletManagement.Open();
        yield return null;
        bool managementOpened = bulletManagement.IsOpen
            && !shopItemsLayout.gameObject.activeInHierarchy;
        bulletManagement.Close();
        yield return null;
        bool managementClosed = !bulletManagement.IsOpen
            && shopItemsLayout.gameObject.activeInHierarchy;

        if (!managementOpened || !managementClosed)
        {
            Fail(
                $"bullet management opened={managementOpened}, "
                + $"closed={managementClosed}");
            yield break;
        }

        Debug.Log(
            "SHOP_RUNTIME_SMOKE_PASSED: persistent Stage 1 Canvas, "
            + "Shop + Floating panels, existing managers, refresh, "
            + "bullet management, 3 bullet offers, 2 item offers");

        float transitionDeadline = Time.realtimeSinceStartup + 30f;

        while (LoadingTransitionController.IsTransitioning
               && Time.realtimeSinceStartup < transitionDeadline)
        {
            yield return null;
        }

        if (LoadingTransitionController.IsTransitioning)
        {
            Fail("the shop entrance transition did not settle");
            yield break;
        }

        stateManager.GoToBattle();

        float mapLoadDeadline = Time.realtimeSinceStartup + 15f;

        while (SceneManager.GetActiveScene().name != RunManager.NodeMapSceneName
               && Time.realtimeSinceStartup < mapLoadDeadline)
        {
            yield return null;
        }

        yield return null;

        bool canvasSurvived = PersistentGameCanvas.Instance != null
            && PersistentGameCanvas.Instance.Root == shopCanvas;
        bool canvasHiddenOnMap = canvasSurvived && !shopCanvas.activeSelf;

        if (!canvasSurvived || !canvasHiddenOnMap)
        {
            Fail(
                $"persistent canvas survived={canvasSurvived}, "
                + $"hiddenOnMap={canvasHiddenOnMap}");
            yield break;
        }

        Debug.Log(
            "PERSISTENT_CANVAS_RUNTIME_SMOKE_PASSED: the same Canvas "
            + "survived Shop -> NodeMap and was hidden on the map");

        transitionDeadline = Time.realtimeSinceStartup + 30f;

        while (LoadingTransitionController.IsTransitioning
               && Time.realtimeSinceStartup < transitionDeadline)
        {
            yield return null;
        }

        if (LoadingTransitionController.IsTransitioning
            || !RunManager.Instance.TryEnterNode("battle_2"))
        {
            Fail("the second battle could not be entered from the map");
            yield break;
        }

        float battleLoadDeadline = Time.realtimeSinceStartup + 20f;

        while ((SceneManager.GetActiveScene().name
                    != RunManager.CombatSceneName
                || stateManager.CurrentState != GameFlowState.Battle)
               && Time.realtimeSinceStartup < battleLoadDeadline)
        {
            yield return null;
        }

        bool sameRunContext = PersistentRunContext.Instance == runContext;
        bool sameStateManager = PersistentRunContext.Instance?.StateManager
            == stateManager;
        bool sameShopManager = FindFirstObjectByType<ShopManager>()
            == shopManager;
        bool resumedBattle = SceneManager.GetActiveScene().name
                == RunManager.CombatSceneName
            && stateManager.CurrentState == GameFlowState.Battle;

        if (!sameRunContext || !sameStateManager || !sameShopManager
            || !resumedBattle)
        {
            Fail(
                $"sameContext={sameRunContext}, "
                + $"sameStateManager={sameStateManager}, "
                + $"sameShopManager={sameShopManager}, "
                + $"resumedBattle={resumedBattle}");
            yield break;
        }

        Debug.Log(
            "PERSISTENT_MANAGERS_RUNTIME_SMOKE_PASSED: the first battle "
            + "StateManager and ShopManager resumed the second battle");
        FindFirstObjectByType<WaveManager>()?.StopBattle();
        RestoreSaves();
        Application.Quit(0);
    }

    private bool PrepareBattleRun()
    {
        ShopCatalog catalog = Resources.Load<ShopCatalog>("Run/ShopCatalog");
        BulletData starter = catalog.Bullets.First(bullet => bullet != null);
        RunMapState mapState = new RunMapState
        {
            actId = "act_1",
            currentNodeId = "battle_1",
            activeNodeId = "battle_1",
            visitedNodeIds = new List<string> { "start", "battle_1" },
            completedNodeIds = new List<string>()
        };
        RunManager.Instance.Restore(mapState);
        RunSaveData saveData = new RunSaveData
        {
            flowState = (int)GameFlowState.Map,
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
