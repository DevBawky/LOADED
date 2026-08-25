using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class RefactoringPolicyTests
{
    private readonly List<ScriptableObject> createdAssets =
        new List<ScriptableObject>();
    private readonly List<GameObject> createdObjects =
        new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
            {
                Object.DestroyImmediate(createdObjects[index]);
            }
        }

        createdObjects.Clear();

        foreach (ScriptableObject asset in createdAssets)
        {
            if (asset != null)
            {
                Object.DestroyImmediate(asset);
            }
        }

        createdAssets.Clear();
    }

    [Test]
    public void GenerateItems_RemovesDuplicatesAndRespectsCapacity()
    {
        ItemData first = CreateAsset<ItemData>();
        ItemData second = CreateAsset<ItemData>();
        ItemData third = CreateAsset<ItemData>();
        List<ItemData> pool = new List<ItemData>
        {
            first,
            null,
            second,
            first,
            third
        };
        List<ItemData> offers = new List<ItemData> { first };

        Random.InitState(1729);
        ShopOfferGenerator.GenerateItems(pool, 2, offers);

        Assert.That(offers, Has.Count.EqualTo(2));
        Assert.That(new HashSet<ItemData>(offers), Has.Count.EqualTo(2));
        Assert.That(offers, Has.All.Matches<ItemData>(pool.Contains));
    }

    [Test]
    public void GenerateItems_ClearsDestinationWhenCapacityIsZero()
    {
        ItemData item = CreateAsset<ItemData>();
        List<ItemData> offers = new List<ItemData> { item };

        ShopOfferGenerator.GenerateItems(
            new[] { item },
            0,
            offers);

        Assert.That(offers, Is.Empty);
    }

    [TestCase(-1, 0)]
    [TestCase(0, 0)]
    [TestCase(4, 4)]
    [TestCase(5, 5)]
    [TestCase(6, 5)]
    [TestCase(int.MaxValue, 5)]
    public void ClampRefreshCost_RestrictsCostToSupportedRange(
        int refreshCost,
        int expected)
    {
        Assert.That(
            ShopManager.ClampRefreshCost(refreshCost),
            Is.EqualTo(expected));
    }

    [TestCase(-1, 1)]
    [TestCase(0, 1)]
    [TestCase(4, 5)]
    [TestCase(5, 5)]
    [TestCase(6, 5)]
    [TestCase(int.MaxValue, 5)]
    public void CalculateNextRefreshCost_IncreasesByOneAndStopsAtFive(
        int currentRefreshCost,
        int expected)
    {
        Assert.That(
            ShopManager.CalculateNextRefreshCost(currentRefreshCost),
            Is.EqualTo(expected));
    }

    [Test]
    public void RefreshCostRule_StartsAtZeroAndStopsAtFive()
    {
        int refreshCost = ShopManager.InitialRefreshCost;

        Assert.That(refreshCost, Is.Zero);

        for (int expected = 1; expected <= 5; expected++)
        {
            refreshCost = ShopManager.CalculateNextRefreshCost(refreshCost);
            Assert.That(refreshCost, Is.EqualTo(expected));
        }

        Assert.That(
            ShopManager.CalculateNextRefreshCost(refreshCost),
            Is.EqualTo(5));
    }

    [Test]
    public void SuccessfulRefreshImmediatelyUpdatesDisplayedCostUpToFive()
    {
        GameObject currencyObject = CreateObject("Currency");
        CurrencyManager currencyManager =
            currencyObject.AddComponent<CurrencyManager>();
        currencyManager.RestoreRunMoney(100);
        GameObject costObject = CreateObject("Text | Refresh Cost");
        TMP_Text costText = costObject.AddComponent<TextMeshProUGUI>();
        GameObject shopObject = CreateObject("Shop");
        shopObject.SetActive(false);
        ShopManager shopManager = shopObject.AddComponent<ShopManager>();
        SerializedObject serializedShop = new SerializedObject(shopManager);
        serializedShop.FindProperty("currencyManager").objectReferenceValue =
            currencyManager;
        serializedShop.FindProperty("refreshCostText").objectReferenceValue =
            costText;
        serializedShop.FindProperty("refreshHideDuration").floatValue = 0f;
        serializedShop.FindProperty("refreshRevealDuration").floatValue = 0f;
        serializedShop.FindProperty("refreshRevealInterval").floatValue = 0f;
        serializedShop.ApplyModifiedPropertiesWithoutUndo();
        shopObject.SetActive(true);

        for (int refreshCount = 1; refreshCount <= 12; refreshCount++)
        {
            Assert.That(shopManager.TryRefreshOffers(), Is.True);
            int expectedCost = Mathf.Min(refreshCount, 5);
            Assert.That(shopManager.CurrentRefreshCost,
                Is.EqualTo(expectedCost));
            Assert.That(costText.text,
                Does.EndWith($"${expectedCost}"));
        }

        Assert.That(currencyManager.CurrentMoney, Is.EqualTo(55));
    }

    [Test]
    public void ShopRefreshGuideDoesNotClaimRefreshIsFree()
    {
        FirstRunGuideContent.GuidePage refreshPage = default;
        bool found = false;

        foreach (FirstRunGuideContent.GuidePage page
                 in FirstRunGuideContent.ShopPages)
        {
            if (page.TargetName == "Button | Refresh")
            {
                refreshPage = page;
                found = true;
                break;
            }
        }

        Assert.That(found, Is.True);
        Assert.That(refreshPage.Description, Does.Not.Contain("데모 버전"));
        Assert.That(refreshPage.Description, Does.Not.Contain("무료"));
    }

    [TestCase(GameFlowState.Initializing, false)]
    [TestCase(GameFlowState.Battle, true)]
    [TestCase(GameFlowState.BattleClear, false)]
    [TestCase(GameFlowState.Shop, true)]
    [TestCase(GameFlowState.RunComplete, false)]
    [TestCase(GameFlowState.RunFailed, false)]
    [TestCase(GameFlowState.Event, true)]
    [TestCase(GameFlowState.Treasure, true)]
    public void InventoryItemSaleAvailabilityMatchesFlowState(
        GameFlowState flowState,
        bool expected)
    {
        Assert.That(
            ShopManager.IsInventoryItemSaleState(flowState),
            Is.EqualTo(expected));
    }

    [TestCase(GameFlowState.Battle)]
    [TestCase(GameFlowState.Shop)]
    [TestCase(GameFlowState.Event)]
    [TestCase(GameFlowState.Treasure)]
    public void InventoryItemSaleRemovesItemAndAddsFixedPrice(
        GameFlowState flowState)
    {
        GameObject inventoryObject = CreateObject("Inventory");
        PlayerInventory inventory =
            inventoryObject.AddComponent<PlayerInventory>();
        ItemData item = CreateAsset<ItemData>();
        Assert.That(inventory.TryAdd(item), Is.True);

        GameObject currencyObject = CreateObject("Currency");
        CurrencyManager currency =
            currencyObject.AddComponent<CurrencyManager>();
        currency.RestoreRunMoney(7);

        GameObject stateObject = CreateObject("State");
        stateObject.SetActive(false);
        StateManager stateManager = stateObject.AddComponent<StateManager>();
        SerializedObject serializedState = new SerializedObject(stateManager);
        serializedState.FindProperty("currentState").enumValueIndex =
            (int)flowState;
        serializedState.ApplyModifiedPropertiesWithoutUndo();

        GameObject shopObject = CreateObject("Shop");
        shopObject.SetActive(false);
        ShopManager shop = shopObject.AddComponent<ShopManager>();
        SerializedObject serializedShop = new SerializedObject(shop);
        serializedShop.FindProperty("playerInventory").objectReferenceValue =
            inventory;
        serializedShop.FindProperty("currencyManager").objectReferenceValue =
            currency;
        serializedShop.FindProperty("stateManager").objectReferenceValue =
            stateManager;
        serializedShop.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(shop.CanSellInventoryItems, Is.True);
        Assert.That(shop.TrySellInventoryItem(0), Is.True);
        Assert.That(inventory.GetItem(0), Is.Null);
        Assert.That(
            currency.CurrentMoney,
            Is.EqualTo(7 + ShopManager.InventoryItemSellPrice));
    }

    [TestCase(
        "Assets/Prefabs/UI/Event/EventCanvas.prefab",
        "Assets/Prefabs/UI/Event/EventSceneManagers.prefab")]
    [TestCase(
        "Assets/Prefabs/UI/Treasure/TreasureCanvas.prefab",
        "Assets/Prefabs/UI/Treasure/TreasureSceneManagers.prefab")]
    public void DedicatedLocationPrefabsContainItemSaleDependencies(
        string canvasPath,
        string managersPath)
    {
        GameObject canvas = AssetDatabase.LoadAssetAtPath<GameObject>(
            canvasPath);
        GameObject managers = AssetDatabase.LoadAssetAtPath<GameObject>(
            managersPath);

        Assert.That(canvas, Is.Not.Null, canvasPath);
        Assert.That(managers, Is.Not.Null, managersPath);
        Assert.That(
            canvas.GetComponentInChildren<InventoryUI>(true),
            Is.Not.Null,
            canvasPath);
        Assert.That(
            canvas.GetComponentInChildren<InventoryTooltipUI>(true),
            Is.Not.Null,
            canvasPath);
        Assert.That(
            managers.GetComponentInChildren<ShopManager>(true),
            Is.Not.Null,
            managersPath);
        Assert.That(
            managers.GetComponentInChildren<CurrencyManager>(true),
            Is.Not.Null,
            managersPath);
        Assert.That(
            managers.GetComponentInChildren<PlayerInventory>(true),
            Is.Not.Null,
            managersPath);
        Assert.That(
            managers.GetComponentInChildren<StateManager>(true),
            Is.Not.Null,
            managersPath);
    }

    [Test]
    public void ShopInventoryGuideMentionsBattleItemSales()
    {
        FirstRunGuideContent.GuidePage inventoryPage = default;
        bool found = false;

        foreach (FirstRunGuideContent.GuidePage page
                 in FirstRunGuideContent.ShopPages)
        {
            if (page.TargetName == "Layout | Inventory")
            {
                inventoryPage = page;
                found = true;
                break;
            }
        }

        Assert.That(found, Is.True);
        Assert.That(inventoryPage.Description, Does.Contain("전투"));
        Assert.That(inventoryPage.Description, Does.Contain("이벤트"));
        Assert.That(inventoryPage.Description, Does.Contain("보물"));
        Assert.That(inventoryPage.Description, Does.Contain("우클릭"));
    }

    [Test]
    public void CylinderEffectPolicy_TracksDirectTemporaryDamageBonus()
    {
        BulletData data = CreateAsset<BulletData>();
        BulletInstance bullet = new BulletInstance(data, 0);
        List<BulletInstance> loadedBullets =
            new List<BulletInstance> { bullet };

        Assert.That(
            CylinderBulletEffectPolicy.ShouldShow(
                loadedBullets,
                0,
                null,
                null,
                null,
                null),
            Is.False);

        bullet.AddTemporaryDamageBonus(0.25f);

        Assert.That(
            CylinderBulletEffectPolicy.ShouldShow(
                loadedBullets,
                0,
                null,
                null,
                null,
                null),
            Is.True);
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void CylinderEffectPolicy_RejectsInvalidIndex(int index)
    {
        Assert.That(
            CylinderBulletEffectPolicy.ShouldShow(
                new BulletInstance[1],
                index,
                null,
                null,
                null,
                null),
            Is.False);
    }

    private T CreateAsset<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        createdAssets.Add(asset);
        return asset;
    }

    private GameObject CreateObject(string objectName)
    {
        GameObject gameObject = new GameObject(objectName);
        createdObjects.Add(gameObject);
        return gameObject;
    }
}
