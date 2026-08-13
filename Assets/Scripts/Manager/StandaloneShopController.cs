using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StandaloneShopController : MonoBehaviour
{
    private const int BulletOfferCount = 3;
    private const int ItemOfferCount = 3;
    private RunSaveData saveData;
    private ShopCatalog catalog;
    private Font font;
    private Text moneyText;
    private readonly List<Button> offerButtons = new List<Button>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name == RunManager.ShopSceneName
            && FindFirstObjectByType<StandaloneShopController>() == null)
        {
            new GameObject("Standalone Shop Controller")
                .AddComponent<StandaloneShopController>();
        }
    }

    private void Awake()
    {
        if (RunManager.Instance.ActiveNode == null
            || RunManager.Instance.ActiveNode.NodeType != MapNodeType.Shop
            || !RunSaveSystem.TryLoad(out saveData))
        {
            Debug.LogError("The standalone shop requires an active shop node and a valid run save.", this);
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
        if (saveData.shop.bulletOfferAssetNames.Count == 0)
        {
            FillDistinctBulletOffers();
        }

        if (saveData.shop.itemOfferAssetNames.Count == 0)
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

        for (int index = 0; index < Mathf.Min(BulletOfferCount, indices.Count); index++)
        {
            BulletData bullet = catalog.Bullets[indices[index]];

            if (bullet != null)
            {
                saveData.shop.bulletOfferAssetNames.Add(bullet.name);
            }
        }
    }

    private void FillDistinctItemOffers()
    {
        List<int> indices = CreateShuffledIndices(catalog.Items.Count);

        for (int index = 0; index < Mathf.Min(ItemOfferCount, indices.Count); index++)
        {
            ItemData item = catalog.Items[indices[index]];

            if (item != null)
            {
                saveData.shop.itemOfferAssetNames.Add(item.name);
            }
        }
    }

    private void BuildScreen()
    {
        EnsureEventSystem();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Canvas canvas = new GameObject("Canvas | Shop", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image background = CreateImage(canvas.transform, "Background",
            new Color(0.055f, 0.045f, 0.04f, 1f));
        Stretch(background.rectTransform);
        CreateText(canvas.transform, "TRADER", 56,
            new Vector2(0f, 450f), new Vector2(800f, 80f));
        moneyText = CreateText(canvas.transform, string.Empty, 34,
            new Vector2(0f, 365f), new Vector2(500f, 60f));

        for (int index = 0; index < saveData.shop.bulletOfferAssetNames.Count; index++)
        {
            int captured = index;
            BulletData bullet = catalog.FindBullet(
                saveData.shop.bulletOfferAssetNames[index]);
            Button button = CreateOfferButton(canvas.transform,
                new Vector2(-480f + index * 480f, 130f),
                bullet == null ? "UNKNOWN BULLET" :
                    $"{bullet.DisplayName}\n$ {bullet.Price}",
                () => TryBuyBullet(captured));
            offerButtons.Add(button);
        }

        for (int index = 0; index < saveData.shop.itemOfferAssetNames.Count; index++)
        {
            int captured = index;
            ItemData item = catalog.FindItem(saveData.shop.itemOfferAssetNames[index]);
            Button button = CreateOfferButton(canvas.transform,
                new Vector2(-480f + index * 480f, -120f),
                item == null ? "UNKNOWN ITEM" :
                    $"{item.DisplayName}\n$ {item.Price}",
                () => TryBuyItem(captured));
            offerButtons.Add(button);
        }

        CreateOfferButton(canvas.transform, new Vector2(0f, -365f),
            "RETURN TO MAP", ExitShop, new Vector2(420f, 90f));
        Refresh();
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

        int buttonIndex = 0;

        for (int index = 0; index < saveData.shop.bulletOfferAssetNames.Count; index++)
        {
            BulletData bullet = catalog.FindBullet(saveData.shop.bulletOfferAssetNames[index]);
            bool purchased = saveData.shop.purchasedBulletOffers[index];
            Button button = offerButtons[buttonIndex++];
            button.interactable = !purchased && bullet != null
                && saveData.money >= bullet.Price
                && saveData.bullets.Count < DeckManager.MaximumOwnedBulletCount;
            SetButtonText(button, purchased ? "PURCHASED" :
                bullet == null ? "UNAVAILABLE" : $"{bullet.DisplayName}\n$ {bullet.Price}");
        }

        for (int index = 0; index < saveData.shop.itemOfferAssetNames.Count; index++)
        {
            ItemData item = catalog.FindItem(saveData.shop.itemOfferAssetNames[index]);
            bool purchased = saveData.shop.purchasedItemOffers[index];
            Button button = offerButtons[buttonIndex++];
            button.interactable = !purchased && item != null
                && saveData.money >= item.Price && FindEmptyInventorySlot() >= 0;
            SetButtonText(button, purchased ? "PURCHASED" :
                item == null ? "UNAVAILABLE" : $"{item.DisplayName}\n$ {item.Price}");
        }
    }

    private Button CreateOfferButton(
        Transform parent,
        Vector2 position,
        string label,
        UnityEngine.Events.UnityAction clicked,
        Vector2? size = null)
    {
        Image image = CreateImage(parent, "Button | Offer",
            new Color(0.72f, 0.16f, 0.09f, 1f));
        image.rectTransform.anchorMin = image.rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size ?? new Vector2(400f, 180f);
        Button button = image.gameObject.AddComponent<Button>();
        button.onClick.AddListener(clicked);
        CreateText(image.transform, label, 28, Vector2.zero,
            image.rectTransform.sizeDelta);
        return button;
    }

    private Text CreateText(
        Transform parent,
        string value,
        int size,
        Vector2 position,
        Vector2 dimensions)
    {
        Text text = new GameObject("Text", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(parent, false);
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.96f, 0.9f, 0.78f, 1f);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = dimensions;
        return text;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetButtonText(Button button, string value)
    {
        Text text = button == null ? null : button.GetComponentInChildren<Text>();

        if (text != null)
        {
            text.text = value;
        }
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

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(EventSystem),
                typeof(StandaloneInputModule));
        }
    }
}
