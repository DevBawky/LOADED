using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StandaloneShopController : MonoBehaviour
{
    private const int BulletOfferCount = 3;
    private const int ItemOfferCount = 2;
    [SerializeField] private GameObject stageOneCanvasPrefab;
    private RunSaveData saveData;
    private ShopCatalog catalog;
    private TMP_Text moneyText;
    private TMP_Text bulletCountText;
    private readonly List<OfferView> bulletOfferViews = new List<OfferView>();
    private readonly List<OfferView> itemOfferViews = new List<OfferView>();

    private sealed class OfferView
    {
        public Button button;
        public Image icon;
        public TMP_Text costText;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == RunManager.ShopSceneName
            && FindFirstObjectByType<StandaloneShopController>() == null)
        {
            new GameObject("Standalone Shop Controller")
                .AddComponent<StandaloneShopController>();
        }
    }

    private void Awake()
    {
        if (RunManager.Instance.ActiveNode == null
            || RunManager.Instance.ActiveNode.NodeType != MapNodeType.Shop)
        {
            Debug.LogError("The standalone shop requires an active shop node.", this);
            RunManager.Instance.ReturnToMap();
            return;
        }

        if (PersistentRunContext.Instance != null)
        {
            if (!PersistentRunContext.Instance.EnterMapShop())
            {
                Debug.LogError(
                    "The persistent Stage 1 managers could not open the shop.",
                    this);
                RunManager.Instance.ReturnToMap();
            }

            return;
        }

        if (!RunSaveSystem.TryLoad(out saveData))
        {
            Debug.LogError(
                "The standalone shop requires a valid run save.",
                this);
            RunManager.Instance.ReturnToMap();
            return;
        }

        catalog = Resources.Load<ShopCatalog>("Run/ShopCatalog");

        if (catalog == null || catalog.Bullets.Count == 0)
        {
            Debug.LogError("Shop catalog is missing or empty.", this);
            RunManager.Instance.ReturnToMap();
            return;
        }

        saveData.shop ??= new RunShopSaveData();
        EnsureOffers();
        if (!SaveShopState(GameFlowState.Shop))
        {
            Debug.LogError("The shop state could not be saved.", this);
            RunManager.Instance.ReturnToMap();
            return;
        }
        BuildScreen();
    }

    private void EnsureOffers()
    {
        TrimOffers(saveData.shop.bulletOfferAssetNames, BulletOfferCount);
        TrimOffers(saveData.shop.itemOfferAssetNames, ItemOfferCount);

        if (saveData.shop.bulletOfferAssetNames.Count < BulletOfferCount)
        {
            FillDistinctBulletOffers();
        }

        if (saveData.shop.itemOfferAssetNames.Count < ItemOfferCount)
        {
            FillDistinctItemOffers();
        }

        ResizeFlags(
            saveData.shop.purchasedBulletOffers,
            saveData.shop.bulletOfferAssetNames.Count);
        ResizeFlags(
            saveData.shop.purchasedItemOffers,
            saveData.shop.itemOfferAssetNames.Count);
    }

    private void FillDistinctBulletOffers()
    {
        List<int> indices = CreateShuffledIndices(catalog.Bullets.Count);

        foreach (int index in indices)
        {
            BulletData bullet = catalog.Bullets[index];

            if (bullet != null
                && !saveData.shop.bulletOfferAssetNames.Contains(bullet.name))
            {
                saveData.shop.bulletOfferAssetNames.Add(bullet.name);
            }

            if (saveData.shop.bulletOfferAssetNames.Count >= BulletOfferCount)
            {
                break;
            }
        }
    }

    private void FillDistinctItemOffers()
    {
        List<int> indices = CreateShuffledIndices(catalog.Items.Count);

        foreach (int index in indices)
        {
            ItemData item = catalog.Items[index];

            if (item != null
                && !saveData.shop.itemOfferAssetNames.Contains(item.name))
            {
                saveData.shop.itemOfferAssetNames.Add(item.name);
            }

            if (saveData.shop.itemOfferAssetNames.Count >= ItemOfferCount)
            {
                break;
            }
        }
    }

    private void BuildScreen()
    {
        EnsureEventSystem();

        if (!TryCreateStageOneShopCanvas(out Transform shopPanel))
        {
            Debug.LogError(
                "The Stage 1 Shop Canvas prefab could not be initialized.",
                this);
            RunManager.Instance.ReturnToMap();
            return;
        }

        ConfigureShopControls(shopPanel);
        Refresh();
    }

    private bool TryCreateStageOneShopCanvas(out Transform shopPanel)
    {
        shopPanel = null;
        PersistentGameCanvas persistentCanvas = PersistentGameCanvas.Instance;
        GameObject canvasObject;

        if (persistentCanvas != null)
        {
            canvasObject = persistentCanvas.Root;
        }
        else
        {
            if (stageOneCanvasPrefab == null)
            {
                return false;
            }

            GameObject inactiveOwner = new GameObject("Game Canvas Setup");
            inactiveOwner.SetActive(false);
            canvasObject = Instantiate(
                stageOneCanvasPrefab,
                inactiveOwner.transform);
            canvasObject.name = "Canvas | Game";
            canvasObject.transform.SetParent(null, false);
            Destroy(inactiveOwner);
        }

        canvasObject.SetActive(false);
        canvasObject.transform.localScale = Vector3.one;

        foreach (MonoBehaviour behaviour in canvasObject
                     .GetComponents<MonoBehaviour>())
        {
            if (behaviour is CanvasScaler
                || behaviour is GraphicRaycaster
                || behaviour is PersistentGameCanvas)
            {
                continue;
            }

            behaviour.enabled = false;
        }

        shopPanel = FindDescendant(canvasObject.transform, "Panel | Shop");
        Transform floatingPanel = FindDescendant(
            canvasObject.transform,
            "Panel | Floating");
        Transform moneyPanel = FindDescendant(
            canvasObject.transform,
            "Panel | Money");

        if (shopPanel == null || floatingPanel == null || moneyPanel == null)
        {
            if (persistentCanvas == null)
            {
                Destroy(canvasObject);
            }

            return false;
        }

        persistentCanvas = PersistentGameCanvas.Adopt(canvasObject, false);
        persistentCanvas.ShowShopMode();
        moneyText = FindDescendant(moneyPanel, "Text | Current Money")
            ?.GetComponent<TMP_Text>();
        return true;
    }

    private void ConfigureShopControls(Transform shopPanel)
    {
        Button[] buttons = shopPanel.GetComponentsInChildren<Button>(true);
        Button[] bulletButtons = buttons
            .Where(button => button.name == "Button | Bullet Item")
            .Take(BulletOfferCount)
            .ToArray();
        Button[] itemButtons = buttons
            .Where(button => button.name == "Button | Shop Item")
            .Take(ItemOfferCount)
            .ToArray();

        if (bulletButtons.Length != BulletOfferCount
            || itemButtons.Length != ItemOfferCount)
        {
            throw new InvalidOperationException(
                "The Stage 1 Shop Canvas offer slots do not match the catalog layout.");
        }

        bulletCountText = FindDescendant(shopPanel, "Text | My Bullet Count")
            ?.GetComponent<TMP_Text>();

        for (int index = 0; index < bulletButtons.Length; index++)
        {
            int captured = index;
            OfferView view = CreateOfferView(bulletButtons[index]);
            view.button.onClick.RemoveAllListeners();
            view.button.onClick.AddListener(() => TryBuyBullet(captured));
            bulletOfferViews.Add(view);
        }

        for (int index = 0; index < itemButtons.Length; index++)
        {
            int captured = index;
            OfferView view = CreateOfferView(itemButtons[index]);
            view.button.onClick.RemoveAllListeners();
            view.button.onClick.AddListener(() => TryBuyItem(captured));
            itemOfferViews.Add(view);
        }

        SetNamedButtonActive(buttons, "Button | Refresh", false);
        SetNamedButtonActive(buttons, "Button | Manage Bullet", false);
        Transform managePanel = FindDescendant(shopPanel, "Panel | Manage Bullets");
        managePanel?.gameObject.SetActive(false);

        Button exitButton = buttons.FirstOrDefault(
            button => button.name == "Button | Go To Battle");

        if (exitButton == null)
        {
            throw new InvalidOperationException(
                "The Stage 1 Shop Canvas return button is missing.");
        }

        exitButton.onClick.RemoveAllListeners();
        exitButton.onClick.AddListener(ExitShop);
        TMP_Text exitText = exitButton.GetComponentInChildren<TMP_Text>(true);

        if (exitText != null)
        {
            exitText.text = "RETURN TO MAP";
        }
    }

    private static OfferView CreateOfferView(Button button)
    {
        return new OfferView
        {
            button = button,
            icon = FindDescendant(button.transform, "Image | Sprite")
                ?.GetComponent<Image>(),
            costText = FindDescendant(button.transform, "Text | Cost")
                ?.GetComponent<TMP_Text>()
        };
    }

    private void TryBuyBullet(int offerIndex)
    {
        if (offerIndex < 0
            || offerIndex >= saveData.shop.bulletOfferAssetNames.Count
            || saveData.shop.purchasedBulletOffers[offerIndex]
            || saveData.bullets.Count >= DeckManager.MaximumOwnedBulletCount)
        {
            return;
        }

        BulletData bullet = catalog.FindBullet(
            saveData.shop.bulletOfferAssetNames[offerIndex]);

        if (bullet == null || saveData.money < bullet.Price)
        {
            return;
        }

        int acquisitionOrder = 0;

        foreach (RunBulletSaveData owned in saveData.bullets)
        {
            acquisitionOrder = Mathf.Max(acquisitionOrder, owned.acquisitionOrder + 1);
        }

        saveData.money -= bullet.Price;
        GameStatistics.RecordGoldSpent(bullet.Price);
        saveData.bullets.Add(new RunBulletSaveData
        {
            assetName = bullet.name,
            bulletId = bullet.BulletId,
            acquisitionOrder = acquisitionOrder,
            location = 0,
            locationIndex = saveData.bullets.Count
        });
        saveData.shop.purchasedBulletOffers[offerIndex] = true;
        SaveShopState(GameFlowState.Shop);
        Refresh();
    }

    private void TryBuyItem(int offerIndex)
    {
        if (offerIndex < 0
            || offerIndex >= saveData.shop.itemOfferAssetNames.Count
            || saveData.shop.purchasedItemOffers[offerIndex])
        {
            return;
        }

        ItemData item = catalog.FindItem(saveData.shop.itemOfferAssetNames[offerIndex]);
        int emptySlot = FindEmptyInventorySlot();

        if (item == null || emptySlot < 0 || saveData.money < item.Price)
        {
            return;
        }

        saveData.money -= item.Price;
        GameStatistics.RecordGoldSpent(item.Price);
        saveData.inventoryItemAssetNames[emptySlot] = item.name;
        saveData.shop.purchasedItemOffers[offerIndex] = true;
        SaveShopState(GameFlowState.Shop);
        Refresh();
    }

    private int FindEmptyInventorySlot()
    {
        saveData.inventoryItemAssetNames ??= new List<string>();

        while (saveData.inventoryItemAssetNames.Count < PlayerInventory.MaximumSlotCount)
        {
            saveData.inventoryItemAssetNames.Add(string.Empty);
        }

        for (int index = 0; index < PlayerInventory.MaximumSlotCount; index++)
        {
            if (string.IsNullOrWhiteSpace(saveData.inventoryItemAssetNames[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private void ExitShop()
    {
        if (!RunManager.Instance.CompleteActiveNode())
        {
            return;
        }

        if (!SaveShopState(GameFlowState.Map))
        {
            Debug.LogError("The completed shop node could not be saved.", this);
            return;
        }

        RunManager.Instance.ReturnToMap();
    }

    private bool SaveShopState(GameFlowState state)
    {
        saveData.flowState = (int)state;
        RunManager.Instance.ApplyToSave(saveData);
        bool saved = RunSaveSystem.Save(saveData);

        if (saved)
        {
            GameStatistics.SaveCheckpoint();
        }

        return saved;
    }

    private void Refresh()
    {
        if (moneyText != null)
        {
            moneyText.text = $"$ {Mathf.Max(0, saveData.money)}";
        }

        if (bulletCountText != null)
        {
            bulletCountText.text =
                $"{saveData.bullets.Count}/{DeckManager.MaximumOwnedBulletCount}";
        }

        for (int index = 0; index < bulletOfferViews.Count; index++)
        {
            BulletData bullet = catalog.FindBullet(saveData.shop.bulletOfferAssetNames[index]);
            bool purchased = saveData.shop.purchasedBulletOffers[index];
            bool canPurchase = !purchased && bullet != null
                && saveData.money >= bullet.Price
                && saveData.bullets.Count < DeckManager.MaximumOwnedBulletCount;
            SetOfferView(
                bulletOfferViews[index],
                bullet == null ? null : bullet.CylinderIcon,
                bullet == null ? 0 : bullet.Price,
                purchased,
                canPurchase);
        }

        for (int index = 0; index < itemOfferViews.Count; index++)
        {
            ItemData item = catalog.FindItem(saveData.shop.itemOfferAssetNames[index]);
            bool purchased = saveData.shop.purchasedItemOffers[index];
            bool canPurchase = !purchased && item != null
                && saveData.money >= item.Price && FindEmptyInventorySlot() >= 0;
            SetOfferView(
                itemOfferViews[index],
                item == null ? null : item.Icon,
                item == null ? 0 : item.Price,
                purchased,
                canPurchase);
        }
    }

    private static void SetOfferView(
        OfferView view,
        Sprite icon,
        int price,
        bool purchased,
        bool canPurchase)
    {
        view.button.interactable = canPurchase;

        if (view.icon != null)
        {
            view.icon.sprite = icon;
            view.icon.enabled = icon != null && !purchased;
        }

        if (view.costText != null)
        {
            view.costText.text = purchased
                ? "SOLD"
                : icon == null
                    ? "N/A"
                    : $"$ {price}";
        }
    }

    private static void SetNamedButtonActive(
        IEnumerable<Button> buttons,
        string buttonName,
        bool active)
    {
        Button button = buttons.FirstOrDefault(
            candidate => candidate.name == buttonName);
        button?.gameObject.SetActive(active);
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static List<int> CreateShuffledIndices(int count)
    {
        List<int> values = new List<int>();

        for (int index = 0; index < count; index++)
        {
            values.Add(index);
        }

        for (int index = values.Count - 1; index > 0; index--)
        {
            int swap = UnityEngine.Random.Range(0, index + 1);
            (values[index], values[swap]) = (values[swap], values[index]);
        }

        return values;
    }

    private static void ResizeFlags(List<bool> flags, int count)
    {
        while (flags.Count < count)
        {
            flags.Add(false);
        }

        if (flags.Count > count)
        {
            flags.RemoveRange(count, flags.Count - count);
        }
    }

    private static void TrimOffers(List<string> offers, int count)
    {
        if (offers.Count > count)
        {
            offers.RemoveRange(count, offers.Count - count);
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            GameObject owner = new GameObject("EventSystem", typeof(EventSystem));
            owner.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
