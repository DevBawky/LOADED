#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Extracts the reusable panels from the legacy Battle canvas and builds the
/// dedicated Shop scene without modifying Battle.unity.
/// </summary>
public static class ShopSceneSetupBuilder
{
    private const string BattleScenePath = "Assets/Scenes/Battle.unity";
    private const string ShopScenePath = "Assets/Scenes/Shop.unity";
    private const string SourceCanvasPath = "Assets/Prefabs/UI/Canvas.prefab";
    private const string SharedFolder = "Assets/Prefabs/UI/Shared";
    private const string TooltipFolder = SharedFolder + "/Tooltips";
    private const string ShopFolder = "Assets/Prefabs/UI/Shop";
    private const string ShopPanelPath = ShopFolder + "/Panel_Shop.prefab";
    private const string FloatingPanelPath = SharedFolder + "/Panel_Floating.prefab";
    private const string TooltipsPanelPath = SharedFolder + "/Panel_Tooltips.prefab";
    private const string PausedPanelPath = SharedFolder + "/Panel_Paused.prefab";
    private const string GameOverPanelPath = SharedFolder + "/Panel_GameOver.prefab";
    private const string ShopCanvasPath = ShopFolder + "/ShopCanvas.prefab";
    private const string ShopManagersPath = ShopFolder + "/ShopSceneManagers.prefab";
    private const string TreasureCanvasPath =
        "Assets/Prefabs/UI/Treasure/TreasureCanvas.prefab";
    private const string EventCanvasPath =
        "Assets/Prefabs/UI/Event/EventCanvas.prefab";
    private const string UpgradeTooltipName = "Panel | Upgrade Tooltip";
    private const string BulletManageLayoutName = "Layout | Bullet Manage";

    private readonly struct TooltipPrefabDefinition
    {
        public TooltipPrefabDefinition(
            string objectName,
            string fileName,
            bool belongsToCanvasRoot)
        {
            ObjectName = objectName;
            AssetPath = TooltipFolder + "/" + fileName + ".prefab";
            BelongsToCanvasRoot = belongsToCanvasRoot;
        }

        public string ObjectName { get; }
        public string AssetPath { get; }
        public bool BelongsToCanvasRoot { get; }
    }

    private static readonly TooltipPrefabDefinition[] TooltipPrefabs =
    {
        new TooltipPrefabDefinition(
            "Panel | Item Tooltip", "Panel_ItemTooltip", true),
        new TooltipPrefabDefinition(
            "Panel | Bullet Tooltip", "Panel_BulletTooltip", true),
        new TooltipPrefabDefinition(
            "Panel | Cylinder Bullet Tooltip",
            "Panel_CylinderBulletTooltip",
            true),
        new TooltipPrefabDefinition(
            "Panel | Action Tooltip", "Panel_ActionTooltip", true),
        new TooltipPrefabDefinition(
            "Panel | Relic Tooltip", "Panel_RelicTooltip", true),
        new TooltipPrefabDefinition(
            "Panel | Debuff Desciption",
            "Panel_DebuffDescription",
            true),
        new TooltipPrefabDefinition(
            "Panel | Bullet Type Desciption",
            "Panel_BulletTypeDescription",
            true),
        new TooltipPrefabDefinition(
            UpgradeTooltipName, "Panel_UpgradeTooltip", false)
    };

    private static readonly string[] DedicatedCanvasPaths =
    {
        ShopCanvasPath,
        TreasureCanvasPath,
        EventCanvasPath
    };

    [MenuItem("Tools/LOADED/Build Dedicated Shop Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before building the Shop scene.");
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("Shop scene build was cancelled to preserve unsaved scene changes.");
            return;
        }

        EnsureFolder("Assets/Prefabs/UI", "Shared");
        EnsureFolder(SharedFolder, "Tooltips");
        EnsureFolder("Assets/Prefabs/UI", "Shop");

        Scene battleScene = EditorSceneManager.OpenScene(
            BattleScenePath,
            OpenSceneMode.Single);
        GameObject sourceCanvas = FindSceneObject(battleScene, "Canvas");

        if (sourceCanvas == null)
        {
            throw new InvalidOperationException(
                "Battle scene does not contain the Canvas prefab instance.");
        }

        EnsureTooltipPrefabAssets(sourceCanvas);
        BuildTooltipsPrefab();
        BuildPanelPrefab(sourceCanvas, "Panel | Shop", ShopPanelPath);
        SyncShopUpgradeTooltip();
        BuildPanelPrefab(sourceCanvas, "Panel | Floating", FloatingPanelPath);
        BuildPanelPrefab(sourceCanvas, "Panel | Paused", PausedPanelPath);
        BuildPanelPrefab(sourceCanvas, "Panel | GameOver", GameOverPanelPath);
        BuildShopCanvas(sourceCanvas);
        BuildShopManagers(battleScene);
        BuildShopScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Dedicated Shop scene and shared UI prefabs were built successfully.");
    }

    [MenuItem("Tools/LOADED/Build Shared Tooltip Prefabs")]
    public static void BuildSharedTooltipPrefabs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before building shared tooltip prefabs.");
        }

        EnsureFolder("Assets/Prefabs/UI", "Shared");
        EnsureFolder(SharedFolder, "Tooltips");
        GameObject sourceCanvas = PrefabUtility.LoadPrefabContents(
            SourceCanvasPath);

        try
        {
            EnsureTooltipPrefabAssets(sourceCanvas);
            BuildTooltipsPrefab();
            ReplaceCanvasTooltipPrefabs(sourceCanvas);
            SyncCanvasUpgradeTooltips(sourceCanvas.transform);
            RewireTooltipReferences(sourceCanvas);
            PrefabUtility.SaveAsPrefabAsset(sourceCanvas, SourceCanvasPath);
            SyncShopUpgradeTooltip();

            foreach (string canvasPath in DedicatedCanvasPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(canvasPath)
                    != null)
                {
                    SyncDedicatedCanvasTooltips(canvasPath);
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(sourceCanvas);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "Shared tooltip prefabs were built and linked to every gameplay canvas.");
    }

    private static void EnsureTooltipPrefabAssets(GameObject sourceCanvas)
    {
        foreach (TooltipPrefabDefinition definition in TooltipPrefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(definition.AssetPath)
                != null)
            {
                continue;
            }

            Transform sourceTooltip = FindDescendant(
                sourceCanvas.transform,
                definition.ObjectName);

            if (sourceTooltip == null)
            {
                throw new InvalidOperationException(
                    $"Battle Canvas is missing '{definition.ObjectName}'.");
            }

            GameObject clone = Object.Instantiate(sourceTooltip.gameObject);
            clone.name = definition.ObjectName;
            clone.transform.SetParent(null, false);

            if (PrefabUtility.IsPartOfPrefabInstance(clone))
            {
                PrefabUtility.UnpackPrefabInstance(
                    clone,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            clone.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(clone, definition.AssetPath);
            Object.DestroyImmediate(clone);
        }
    }

    private static void BuildTooltipsPrefab()
    {
        int canvasTooltipCount = 0;

        foreach (TooltipPrefabDefinition definition in TooltipPrefabs)
        {
            if (definition.BelongsToCanvasRoot)
            {
                canvasTooltipCount++;
            }
        }

        if (canvasTooltipCount == 0)
        {
            throw new InvalidOperationException("No Canvas tooltips are defined.");
        }

        GameObject root = new GameObject(
            "Panel | Tooltips",
            typeof(RectTransform));
        root.layer = 5;
        RectTransform rootRect = root.transform as RectTransform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = Vector2.zero;
        rootRect.localScale = Vector3.one;

        foreach (TooltipPrefabDefinition definition in TooltipPrefabs)
        {
            if (!definition.BelongsToCanvasRoot)
            {
                continue;
            }

            GameObject tooltipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                definition.AssetPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(
                tooltipPrefab) as GameObject;
            instance.name = definition.ObjectName;
            instance.transform.SetParent(root.transform, false);
            instance.SetActive(false);
        }

        PrefabUtility.SaveAsPrefabAsset(root, TooltipsPanelPath);
        Object.DestroyImmediate(root);
    }

    private static void SyncShopUpgradeTooltip()
    {
        GameObject shopPanel = PrefabUtility.LoadPrefabContents(ShopPanelPath);

        try
        {
            Transform targetLayout = FindDescendant(
                shopPanel.transform,
                BulletManageLayoutName);
            ReplaceUpgradeTooltip(targetLayout);
            PrefabUtility.SaveAsPrefabAsset(shopPanel, ShopPanelPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(shopPanel);
        }
    }

    private static void SyncDedicatedCanvasTooltips(string canvasPath)
    {
        GameObject canvas = PrefabUtility.LoadPrefabContents(canvasPath);

        try
        {
            ReplaceCanvasTooltipPrefabs(canvas);

            if (!string.Equals(canvasPath, ShopCanvasPath,
                    StringComparison.Ordinal))
            {
                SyncCanvasUpgradeTooltips(canvas.transform);
            }

            RewireTooltipReferences(canvas);
            PrefabUtility.SaveAsPrefabAsset(canvas, canvasPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(canvas);
        }
    }

    private static void ReplaceCanvasTooltipPrefabs(GameObject canvas)
    {
        RemoveNamedDescendants(canvas.transform, "Panel | Tooltips");

        foreach (TooltipPrefabDefinition definition in TooltipPrefabs)
        {
            if (definition.BelongsToCanvasRoot)
            {
                RemoveNamedDescendants(
                    canvas.transform,
                    definition.ObjectName);
            }
        }

        GameObject tooltipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            TooltipsPanelPath);
        GameObject tooltipRoot = PrefabUtility.InstantiatePrefab(
            tooltipPrefab,
            canvas.transform) as GameObject;
        tooltipRoot.name = "Panel | Tooltips";
        tooltipRoot.SetActive(true);
        StretchToParent(tooltipRoot.transform as RectTransform);
        tooltipRoot.transform.SetAsLastSibling();
    }

    private static void SyncCanvasUpgradeTooltips(Transform targetCanvas)
    {
        List<Transform> targetLayouts = FindDescendants(
            targetCanvas,
            BulletManageLayoutName);

        RemoveNamedDescendants(targetCanvas, UpgradeTooltipName);

        foreach (Transform targetLayout in targetLayouts)
        {
            ReplaceUpgradeTooltip(targetLayout);
        }
    }

    private static void ReplaceUpgradeTooltip(Transform targetLayout)
    {
        if (targetLayout == null)
        {
            return;
        }

        RemoveNamedDescendants(targetLayout, UpgradeTooltipName);
        TooltipPrefabDefinition definition = GetTooltipDefinition(
            UpgradeTooltipName);
        GameObject tooltipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            definition.AssetPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(
            tooltipPrefab,
            targetLayout) as GameObject;
        instance.name = UpgradeTooltipName;
        instance.SetActive(false);
    }

    private static TooltipPrefabDefinition GetTooltipDefinition(
        string objectName)
    {
        foreach (TooltipPrefabDefinition definition in TooltipPrefabs)
        {
            if (string.Equals(
                    definition.ObjectName,
                    objectName,
                    StringComparison.Ordinal))
            {
                return definition;
            }
        }

        throw new InvalidOperationException(
            $"No tooltip prefab is defined for '{objectName}'.");
    }

    private static void RewireTooltipReferences(GameObject canvas)
    {
        InventoryTooltipUI inventoryTooltip =
            canvas.GetComponentInChildren<InventoryTooltipUI>(true);

        if (inventoryTooltip != null)
        {
            Transform itemTooltip = FindDescendant(
                canvas.transform,
                "Panel | Item Tooltip");
            Transform bulletTooltip = FindDescendant(
                canvas.transform,
                "Panel | Bullet Tooltip");
            Transform cylinderTooltip = FindDescendant(
                canvas.transform,
                "Panel | Cylinder Bullet Tooltip");
            Transform bulletTypeDescription = FindDescendant(
                canvas.transform,
                "Panel | Bullet Type Desciption");
            Transform debuffDescription = FindDescendant(
                canvas.transform,
                "Panel | Debuff Desciption");
            SerializedObject serialized = new SerializedObject(
                inventoryTooltip);

            AssignObject(serialized, "tooltip", itemTooltip);
            AssignObject(serialized, "itemIcon", FindNamedComponent<Image>(
                itemTooltip, "Image | Item Sprite"));
            AssignObject(serialized, "itemNameText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    itemTooltip, "Text | Item Name"));
            AssignObject(serialized, "itemDescriptionText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    itemTooltip, "Text | Item Description"));
            AssignObject(serialized, "bulletTooltip", bulletTooltip);
            AssignObject(serialized, "bulletIcon", FindNamedComponent<Image>(
                bulletTooltip, "Image | Bullet Sprite"));
            AssignObject(serialized, "bulletCylinderIcon",
                FindNamedComponent<Image>(
                    bulletTooltip, "Image | Bullet Cylinder Sprite"));
            AssignObject(serialized, "bulletNameText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    bulletTooltip, "Text | Bullet Name"));
            AssignObject(serialized, "bulletGradeText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    bulletTooltip, "Text | Bullet Grade"));
            AssignObject(serialized, "bulletDescriptionText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    bulletTooltip, "Text | Bullet Description"));
            AssignObject(serialized, "cylinderBulletTooltip", cylinderTooltip);
            AssignObject(serialized, "cylinderBulletNameText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    cylinderTooltip, "Text | Bullet Name"));
            AssignObject(serialized, "cylinderBulletGradeText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    cylinderTooltip, "Text | Bullet Grade"));
            AssignObject(serialized, "cylinderBulletDescriptionText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    cylinderTooltip, "Text | Bullet Description"));
            AssignObject(
                serialized,
                "bulletTypeDescriptionPanel",
                bulletTypeDescription);
            AssignObject(serialized, "bulletTypeDescriptionNameText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    bulletTypeDescription, "Text | Bullet Name"));
            AssignObject(serialized, "bulletTypeDescriptionBodyText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    bulletTypeDescription, "Text | Bullet Description"));
            AssignObject(
                serialized,
                "debuffDescriptionPanel",
                debuffDescription);
            AssignObject(serialized, "debuffDescriptionIcon",
                FindNamedComponent<Image>(
                    debuffDescription, "Image | Debuff Icon"));
            AssignObject(serialized, "debuffDescriptionNameText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    debuffDescription, "Text | Bullet Name"));
            AssignObject(serialized, "debuffDescriptionBodyText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    debuffDescription, "Text | Bullet Description"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (BulletManagementUI management in
                 canvas.GetComponentsInChildren<BulletManagementUI>(true))
        {
            Transform upgradeTooltip = FindDescendant(
                management.transform.root,
                UpgradeTooltipName);
            SerializedObject serialized = new SerializedObject(management);
            AssignObject(serialized, "upgradeTooltip", upgradeTooltip);
            AssignObject(serialized, "upgradeTooltipDescriptionText",
                FindNamedComponent<TMPro.TextMeshProUGUI>(
                    upgradeTooltip, "Text | Bullet Description"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AssignObject(
        SerializedObject serialized,
        string propertyName,
        Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static T FindNamedComponent<T>(
        Transform root,
        string objectName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component.name == objectName)
            {
                return component;
            }
        }

        return null;
    }

    private static List<Transform> FindDescendants(
        Transform root,
        string objectName)
    {
        List<Transform> matches = new List<Transform>();

        foreach (Transform candidate in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == objectName)
            {
                matches.Add(candidate);
            }
        }

        return matches;
    }

    private static void RemoveNamedDescendants(
        Transform root,
        string objectName)
    {
        List<Transform> matches = FindDescendants(root, objectName);

        for (int index = matches.Count - 1; index >= 0; index--)
        {
            Transform match = matches[index];

            if (match != root && match != null)
            {
                Object.DestroyImmediate(match.gameObject);
            }
        }
    }

    private static void StretchToParent(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void BuildPanelPrefab(
        GameObject sourceCanvas,
        string panelName,
        string assetPath)
    {
        Transform sourcePanel = FindDescendant(
            sourceCanvas.transform,
            panelName);

        if (sourcePanel == null)
        {
            throw new InvalidOperationException(
                $"Canvas is missing '{panelName}'.");
        }

        GameObject clone = Object.Instantiate(sourcePanel.gameObject);
        clone.name = panelName;
        clone.transform.SetParent(null, false);
        clone.SetActive(panelName == "Panel | Shop"
            || panelName == "Panel | Floating");
        PrefabUtility.SaveAsPrefabAsset(clone, assetPath);
        Object.DestroyImmediate(clone);
    }

    private static void BuildShopCanvas(GameObject sourceCanvas)
    {
        GameObject canvas = Object.Instantiate(sourceCanvas);
        canvas.name = "Canvas | Shop";

        if (PrefabUtility.IsPartOfPrefabInstance(canvas))
        {
            PrefabUtility.UnpackPrefabInstance(
                canvas,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
        }

        RectTransform canvasRect = canvas.transform as RectTransform;

        if (canvasRect != null)
        {
            canvasRect.localScale = Vector3.one;
        }

        for (int index = canvas.transform.childCount - 1; index >= 0; index--)
        {
            Object.DestroyImmediate(canvas.transform.GetChild(index).gameObject);
        }

        AddNestedPanel(
            canvas.transform, ShopPanelPath, "Panel | Shop", true);
        AddNestedPanel(
            canvas.transform, FloatingPanelPath, "Panel | Floating", true);
        AddNestedPanel(
            canvas.transform, TooltipsPanelPath, "Panel | Tooltips", true);
        AddNestedPanel(
            canvas.transform, GameOverPanelPath, "Panel | GameOver", false);
        AddNestedPanel(
            canvas.transform, PausedPanelPath, "Panel | Paused", false);

        ClearMissingSceneReferences(canvas);
        RewireTooltipReferences(canvas);
        ValidateTooltipCoverage(sourceCanvas, canvas);
        PrefabUtility.SaveAsPrefabAsset(canvas, ShopCanvasPath);
        Object.DestroyImmediate(canvas);
    }

    private static void ValidateTooltipCoverage(
        GameObject sourceCanvas,
        GameObject shopCanvas)
    {
        HashSet<string> sourceNames = GetTooltipNames(sourceCanvas.transform);
        HashSet<string> targetNames = GetTooltipNames(shopCanvas.transform);
        sourceNames.ExceptWith(targetNames);

        if (sourceNames.Count > 0)
        {
            throw new InvalidOperationException(
                "Shop Canvas is missing tooltip panels: "
                + string.Join(", ", sourceNames));
        }
    }

    private static HashSet<string> GetTooltipNames(Transform root)
    {
        HashSet<string> names = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (Transform transform in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (IsTooltipSupportPanel(transform))
            {
                names.Add(transform.name);
            }
        }

        return names;
    }

    private static bool IsTooltip(Transform transform)
    {
        return transform != null
            && transform.name.IndexOf(
                "Tooltip",
                StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsTooltipSupportPanel(Transform transform)
    {
        return IsTooltip(transform)
            || transform != null && string.Equals(
                transform.name,
                "Panel | Debuff Desciption",
                StringComparison.Ordinal)
            || transform != null && string.Equals(
                transform.name,
                "Panel | Bullet Type Desciption",
                StringComparison.Ordinal);
    }

    private static void AddNestedPanel(
        Transform parent,
        string prefabPath,
        string instanceName,
        bool active)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab)
            as GameObject;
        instance.name = instanceName;
        instance.transform.SetParent(parent, false);
        instance.SetActive(active);
    }

    private static void BuildShopManagers(Scene battleScene)
    {
        GameObject sourceManagers = FindSceneObject(
            battleScene,
            "##--MANAGERS--##");
        DeckManager sourceDeck = FindSceneComponent<DeckManager>(battleScene);
        CurrencyManager sourceCurrency =
            FindSceneComponent<CurrencyManager>(battleScene);
        PlayerInventory sourceInventory =
            FindSceneComponent<PlayerInventory>(battleScene);
        ShopManager sourceShop = FindSceneComponent<ShopManager>(battleScene);

        if (sourceManagers == null || sourceDeck == null || sourceCurrency == null
            || sourceInventory == null || sourceShop == null)
        {
            throw new InvalidOperationException(
                "Battle scene is missing ##--MANAGERS--## or a Shop run-data manager.");
        }

        GameObject root = Object.Instantiate(sourceManagers);
        root.name = "##--MANAGERS--##";
        root.transform.SetParent(null, false);

        if (PrefabUtility.IsPartOfPrefabInstance(root))
        {
            PrefabUtility.UnpackPrefabInstance(
                root,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
        }

        DeckManager deck = root.GetComponentInChildren<DeckManager>(true);
        CurrencyManager currency =
            root.GetComponentInChildren<CurrencyManager>(true);
        ShopManager shop = root.GetComponentInChildren<ShopManager>(true);
        PlayerInventory inventory = root.GetComponent<PlayerInventory>();

        if (deck == null || currency == null || shop == null)
        {
            Object.DestroyImmediate(root);
            throw new InvalidOperationException(
                "The copied ##--MANAGERS--## hierarchy is missing a required manager.");
        }

        inventory ??= root.AddComponent<PlayerInventory>();
        EditorUtility.CopySerialized(sourceInventory, inventory);
        ClearProperties(deck, "deck", "loadedBullets", "graveyard",
            "nextAcquisitionOrder", "paidBulletRemovalCount");
        ClearProperties(currency, "currentMoneyText", "currentMoney",
            "pendingAnimatedMoney");
        ClearProperties(inventory, "playerHealth", "deckManager",
            "playerMove", "waveManager");
        ClearProperties(shop, "currencyManager", "deckManager",
            "playerInventory", "stateManager", "myBulletCountText",
            "slots", "itemSlots", "refreshButton", "refreshCostText",
            "currentRefreshCost");

        DisableBattleOnlyManager<BoardManager>(root);
        DisableBattleOnlyManager<WaveManager>(root);
        DisableBattleOnlyManager<RewardManager>(root);
        DisableBattleOnlyManager<StateManager>(root);

        if (root.GetComponent<ShopSceneController>() == null)
        {
            root.AddComponent<ShopSceneController>();
        }

        ClearMissingSceneReferences(root);
        PrefabUtility.SaveAsPrefabAsset(root, ShopManagersPath);
        Object.DestroyImmediate(root);
    }

    private static void DisableBattleOnlyManager<T>(GameObject root)
        where T : Behaviour
    {
        foreach (T behaviour in root.GetComponentsInChildren<T>(true))
        {
            behaviour.enabled = false;
        }
    }

    private static void BuildShopScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ShopScenePath,
            OpenSceneMode.Single);
        Camera mainCamera = FindSceneComponent<Camera>(scene);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (mainCamera == null
                || root != mainCamera.transform.root.gameObject)
            {
                Object.DestroyImmediate(root);
            }
        }

        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            mainCamera = cameraObject.GetComponent<Camera>();
            mainCamera.orthographic = true;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.075f, 0.07f, 0.1f, 1f);
        }

        GameObject canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            ShopCanvasPath);
        GameObject canvasObject = PrefabUtility.InstantiatePrefab(
            canvasPrefab,
            scene) as GameObject;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.worldCamera = mainCamera;
        canvas.planeDistance = 10f;
        canvasObject.transform.localScale = Vector3.one;

        GameObject managersPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            ShopManagersPath);
        PrefabUtility.InstantiatePrefab(managersPrefab, scene);

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ShopScenePath);
    }

    private static void ClearMissingSceneReferences(GameObject root)
    {
        foreach (MonoBehaviour behaviour in
                 root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyType
                        != SerializedPropertyType.ObjectReference
                    || property.objectReferenceValue == null)
                {
                    continue;
                }

                Object value = property.objectReferenceValue;
                bool isSceneObject = value is GameObject gameObject
                    ? gameObject.scene.IsValid()
                    : value is Component component
                    && component.gameObject.scene.IsValid();

                if (isSceneObject && !IsPartOfRoot(value, root.transform))
                {
                    property.objectReferenceValue = null;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static bool IsPartOfRoot(Object value, Transform root)
    {
        Transform transform = value switch
        {
            GameObject gameObject => gameObject.transform,
            Component component => component.transform,
            _ => null
        };
        return transform != null
            && (transform == root || transform.IsChildOf(root));
    }

    private static void ClearProperties(
        Object target,
        params string[] propertyNames)
    {
        SerializedObject serialized = new SerializedObject(target);

        foreach (string propertyName in propertyNames)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                continue;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = 0;
                    break;
                default:
                    if (property.isArray && property.propertyType
                        != SerializedPropertyType.String)
                    {
                        property.arraySize = 0;
                    }
                    break;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDescendant(root.transform, name);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDescendant(root.GetChild(index), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
