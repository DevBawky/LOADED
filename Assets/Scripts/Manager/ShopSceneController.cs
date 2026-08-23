using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the scene lifecycle for a map Shop node. The Battle manager hierarchy
/// is present for shared systems, while its combat-only behaviours stay
/// disabled. Run data is hydrated into the Shop-facing managers here.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class ShopSceneController : MonoBehaviour
{
    private const string NodeMapSceneName = "NodeMap";
    private const string ShopPanelName = "Panel | Shop";
    private const string LegacyShopPanelName = "Panel_Shop";
    private const string ExitButtonName = "Button | Go To Battle";

    [Header("Scene-local Managers")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private RelicManager relicManager;
    [SerializeField] private StateManager stateManager;

    [Header("Navigation")]
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_Text exitButtonText;
    [SerializeField] private string exitLabel = "TO MAP";

    [Header("Run HUD")]
    [SerializeField] private Image playerHealthFillImage;
    [SerializeField] private TMP_Text playerHealthText;

    private RunSaveData runData;
    private GameObject shopPanel;
    private bool initialized;
    private bool leaving;

    private void Awake()
    {
        ResolveReferences();
        shopManager?.ConfigureStandaloneShop();

        shopPanel = FindSceneGameObject(ShopPanelName);
        shopPanel ??= FindSceneGameObject(LegacyShopPanelName);
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }

        if (exitButtonText != null)
        {
            exitButtonText.text = exitLabel;
        }
    }

    private void OnEnable()
    {
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ReturnToNodeMap);
        }

        if (shopManager != null)
        {
            shopManager.OffersChanged += HandleShopChanged;
            shopManager.PurchaseCompleted += HandleShopChanged;
        }
        if (playerInventory != null)
        {
            playerInventory.ItemUsed += HandleInventoryItemUsed;
        }
    }

    private void Start()
    {
        if (!TryInitializeShop())
        {
            Debug.LogError(
                "Shop scene could not restore the current run. Returning to the node map.",
                this);
            LoadNodeMap();
        }
    }

    private void OnDisable()
    {
        if (!leaving)
        {
            SaveShopState();
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ReturnToNodeMap);
        }

        if (shopManager != null)
        {
            shopManager.OffersChanged -= HandleShopChanged;
            shopManager.PurchaseCompleted -= HandleShopChanged;
        }
        if (playerInventory != null)
        {
            playerInventory.ItemUsed -= HandleInventoryItemUsed;
            playerInventory.ConfigureExternalHealing(null);
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveShopState();
        }
    }

    private void OnApplicationQuit()
    {
        SaveShopState();
    }

    public void ReturnToNodeMap()
    {
        if (leaving || !initialized
            || shopManager != null && shopManager.IsRefreshing)
        {
            return;
        }

        leaving = true;

        if (exitButton != null)
        {
            exitButton.interactable = false;
        }

        SaveShopState();
        runData.shopVisitActive = false;
        runData.shop = new RunShopSaveData();
        RunSaveSystem.Save(runData);
        NodeMapSaveSystem.CompleteActiveNode();
        LoadNodeMap();
    }

    private bool TryInitializeShop()
    {
        if (shopManager == null || deckManager == null
            || currencyManager == null || playerInventory == null
            || relicManager == null
            || !RunSaveSystem.TryLoad(out runData)
            || !deckManager.RestoreRunState(
                runData.bullets,
                shopManager.ResolveSavedBullet,
                runData.paidBulletRemovalCount,
                runData.nextCycleAcquisitionOrders)
            || !relicManager.RestoreRunState(runData.relics))
        {
            return false;
        }

        currencyManager.RestoreRunMoney(runData.money);
        playerInventory.RestoreRunState(
            runData.inventoryItemAssetNames,
            shopManager.ResolveSavedItem);
        playerInventory.ConfigureExternalHealing(TryHealRunHealth);
        RefreshHealthPresentation();
        stateManager?.ConfigureExternalSceneState(
            runData.stageIndex,
            runData.battleIndex,
            GameFlowState.Shop);

        bool resumeExistingVisit = runData.shopVisitActive
            && runData.flowState
                == (int)GameFlowState.Shop
            && runData.shop != null
            && runData.shop.bulletOfferAssetNames != null
            && runData.shop.itemOfferAssetNames != null
            && runData.shop.bulletOfferAssetNames.Count > 0
            && runData.shop.itemOfferAssetNames.Count > 0;

        if (resumeExistingVisit)
        {
            if (!shopManager.RestoreShopRunState(
                    runData.shop,
                    runData.shopRefreshCost))
            {
                return false;
            }
        }
        else
        {
            shopManager.OpenShop();
        }

        runData.shopVisitActive = true;

        foreach (BulletManagementUI managementUI in
                 FindObjectsByType<BulletManagementUI>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            managementUI.ConfigureDedicatedShop(
                shopPanel == null ? null : shopPanel.transform,
                deckManager,
                currencyManager);
        }

        Canvas rootCanvas = shopPanel == null
            ? FindFirstObjectByType<Canvas>(FindObjectsInactive.Include)
            : shopPanel.GetComponentInParent<Canvas>();
        Transform canvasRoot = rootCanvas == null
            ? null
            : rootCanvas.rootCanvas.transform;

        foreach (InventoryTooltipUI tooltipUI in
                 FindObjectsByType<InventoryTooltipUI>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            tooltipUI.ConfigureDedicatedShop(
                canvasRoot,
                playerInventory,
                shopManager,
                deckManager,
                currencyManager,
                stateManager);
        }

        StageProgressUI.EnsureSupportedSceneBinding();
        foreach (StageProgressUI progressUI in
                 FindObjectsByType<StageProgressUI>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            progressUI.SetExternalStageTitle(
                StageProgressUI.ShopStageTitle);
        }

        int cumulativeCount = Mathf.Max(
            0,
            runData.cumulativeBattleTurnCount);
        runData.cumulativeBattleTurnCount = cumulativeCount;

        foreach (TurnCountText turnText in
                 FindObjectsByType<TurnCountText>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            turnText.SetExternalCount(cumulativeCount);
        }

        initialized = true;
        SaveShopState();
        return true;
    }

    private void HandleShopChanged()
    {
        if (initialized)
        {
            SaveShopState();
        }
    }

    private void HandleInventoryItemUsed(int slotIndex, ItemData item)
    {
        if (!initialized)
        {
            return;
        }

        RefreshHealthPresentation();
        SaveShopState();
    }

    private bool TryHealRunHealth(int amount)
    {
        if (runData == null || amount <= 0)
        {
            return false;
        }

        int maximumHealth = Mathf.Max(1, runData.maxHealth);
        int previousHealth = Mathf.Clamp(
            runData.currentHealth,
            1,
            maximumHealth);
        if (previousHealth >= maximumHealth)
        {
            return false;
        }

        runData.maxHealth = maximumHealth;
        runData.currentHealth = (int)System.Math.Min(
            maximumHealth,
            (long)previousHealth + amount);
        RefreshHealthPresentation();
        return runData.currentHealth > previousHealth;
    }

    private bool SaveShopState()
    {
        if (!initialized || runData == null || deckManager == null
            || currencyManager == null || playerInventory == null)
        {
            return false;
        }

        currencyManager.FlushPendingMoney();
        runData.flowState = (int)GameFlowState.Shop;
        runData.shopVisitActive = true;
        runData.startSelectedBattleFresh = false;
        runData.money = currencyManager.CurrentMoney;
        runData.paidBulletRemovalCount = deckManager.PaidBulletRemovalCount;
        runData.shopRefreshCost = shopManager == null
            ? runData.shopRefreshCost
            : shopManager.CurrentRefreshCost;
        deckManager.CaptureRunState(
            runData.bullets,
            runData.nextCycleAcquisitionOrders);
        playerInventory.CaptureRunState(runData.inventoryItemAssetNames);
        relicManager?.CaptureRunState(runData.relics);
        shopManager?.CaptureRunState(runData);
        return RunSaveSystem.Save(runData);
    }

    private void ResolveReferences()
    {
        shopManager ??= FindFirstObjectByType<ShopManager>(
            FindObjectsInactive.Include);
        deckManager ??= FindFirstObjectByType<DeckManager>(
            FindObjectsInactive.Include);
        currencyManager ??= FindFirstObjectByType<CurrencyManager>(
            FindObjectsInactive.Include);
        playerInventory ??= FindFirstObjectByType<PlayerInventory>(
            FindObjectsInactive.Include);
        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);
        relicManager ??= gameObject.AddComponent<RelicManager>();
        stateManager ??= FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include);
        playerHealthFillImage ??= FindSceneComponent<Image>(
            "Image | Fill Amount");
        playerHealthText ??= FindSceneComponent<TMP_Text>(
            "Text | Player HP");

        if (exitButton == null)
        {
            foreach (Button button in FindObjectsByType<Button>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (button != null && button.gameObject.scene.IsValid()
                    && button.name == ExitButtonName
                    && HasNamedAncestor(button.transform, ShopPanelName))
                {
                    exitButton = button;
                    break;
                }
            }
        }

        exitButtonText ??= exitButton == null
            ? null
            : exitButton.GetComponentInChildren<TMP_Text>(true);
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        foreach (Transform transform in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (transform != null && transform.gameObject.scene.IsValid()
                && transform.name == objectName)
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    private static T FindSceneComponent<T>(string objectName)
        where T : Component
    {
        foreach (T component in FindObjectsByType<T>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (component != null && component.gameObject.scene.IsValid()
                && component.name == objectName)
            {
                return component;
            }
        }

        return null;
    }

    private void RefreshHealthPresentation()
    {
        if (runData == null)
        {
            return;
        }

        int maximumHealth = Mathf.Max(1, runData.maxHealth);
        runData.maxHealth = maximumHealth;
        runData.currentHealth = Mathf.Clamp(
            runData.currentHealth,
            1,
            maximumHealth);

        if (playerHealthFillImage != null)
        {
            playerHealthFillImage.fillAmount =
                (float)runData.currentHealth / maximumHealth;
        }

        if (playerHealthText != null)
        {
            playerHealthText.text =
                $"{runData.currentHealth}/{maximumHealth}";
        }
    }

    private static bool HasNamedAncestor(
        Transform transform,
        string objectName)
    {
        Transform current = transform;

        while (current != null)
        {
            if (current.name == objectName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void LoadNodeMap()
    {
        if (!LoadingTransitionController.LoadScene(NodeMapSceneName))
        {
            SceneManager.LoadScene(NodeMapSceneName);
        }
    }
}
