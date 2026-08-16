#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TreasureSceneSetupBuilder
{
    private const string TreasureScenePath = "Assets/Scenes/Treasure.unity";
    private const string ShopCanvasPath =
        "Assets/Prefabs/UI/Shop/ShopCanvas.prefab";
    private const string ShopManagersPath =
        "Assets/Prefabs/UI/Shop/ShopSceneManagers.prefab";
    private const string TreasureFolder = "Assets/Prefabs/UI/Treasure";
    private const string TreasurePanelPath =
        TreasureFolder + "/Panel_Treasure.prefab";
    private const string TreasureCanvasPath =
        TreasureFolder + "/TreasureCanvas.prefab";
    private const string TreasureManagersPath =
        TreasureFolder + "/TreasureSceneManagers.prefab";

    [MenuItem("Tools/LOADED/Build Dedicated Treasure Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before building the Treasure scene.");
        }

        EnsurePrerequisites();
        EnsureFolder("Assets/Prefabs/UI", "Treasure");
        BuildTreasurePanel();
        BuildTreasureCanvas();
        BuildTreasureManagers();
        BuildTreasureScene();
        EnsureSceneInBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "Dedicated Treasure scene, canvas, and managers were built successfully.");
    }

    private static void EnsurePrerequisites()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ShopCanvasPath) != null
            && AssetDatabase.LoadAssetAtPath<GameObject>(ShopManagersPath)
                != null)
        {
            return;
        }

        ShopSceneSetupBuilder.Build();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ShopCanvasPath) == null
            || AssetDatabase.LoadAssetAtPath<GameObject>(ShopManagersPath)
                == null)
        {
            throw new InvalidOperationException(
                "Shop shared prefabs are required before building Treasure.");
        }
    }

    private static void BuildTreasurePanel()
    {
        GameObject root = CreateUiObject("Panel | Treasure", null);
        Stretch(root.GetComponent<RectTransform>());
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.035f, 0.028f, 0.045f, 0.985f);

        CreateText(
            "Text | Treasure Title",
            root.transform,
            new Vector2(0.12f, 0.86f),
            new Vector2(0.88f, 0.96f),
            "TREASURE",
            42f,
            TextAlignmentOptions.Center);

        Button chestButton = CreateButton(
            "Button | Treasure Chest",
            root.transform,
            new Vector2(0.31f, 0.22f),
            new Vector2(0.69f, 0.80f),
            new Color(0.42f, 0.24f, 0.10f, 1f));
        chestButton.image.preserveAspect = true;
        TMP_Text chestLabel = CreateText(
            "Text | Chest Label",
            chestButton.transform,
            new Vector2(0.08f, 0.08f),
            new Vector2(0.92f, 0.92f),
            "보물 상자",
            32f,
            TextAlignmentOptions.Center);
        chestLabel.color = new Color(1f, 0.86f, 0.48f, 1f);

        GameObject choices = CreateUiObject(
            "Panel | Relic Choices",
            root.transform);
        SetAnchors(
            choices.GetComponent<RectTransform>(),
            new Vector2(0.055f, 0.18f),
            new Vector2(0.945f, 0.80f));
        choices.SetActive(false);

        for (int index = 0; index < 3; index++)
        {
            CreateRelicChoice(choices.transform, index);
        }

        TMP_Text instruction = CreateText(
            "Text | Treasure Instruction",
            root.transform,
            new Vector2(0.18f, 0.075f),
            new Vector2(0.82f, 0.15f),
            "상자를 클릭해 여십시오.",
            22f,
            TextAlignmentOptions.Center);
        instruction.color = new Color(0.90f, 0.82f, 0.67f, 1f);

        Button continueButton = CreateButton(
            "Button | Treasure Continue",
            root.transform,
            new Vector2(0.40f, 0.025f),
            new Vector2(0.60f, 0.085f),
            new Color(0.34f, 0.27f, 0.16f, 1f));
        CreateText(
            "Text | Continue",
            continueButton.transform,
            new Vector2(0.04f, 0.06f),
            new Vector2(0.96f, 0.94f),
            "TO MAP",
            22f,
            TextAlignmentOptions.Center);
        continueButton.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, TreasurePanelPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateRelicChoice(Transform parent, int index)
    {
        const float gap = 0.025f;
        float width = (1f - gap * 2f) / 3f;
        float minX = index * (width + gap);
        float maxX = minX + width;
        int number = index + 1;

        Button button = CreateButton(
            $"Button | Relic Choice {number}",
            parent,
            new Vector2(minX, 0f),
            new Vector2(maxX, 1f),
            new Color(0.105f, 0.085f, 0.12f, 1f));

        GameObject iconObject = CreateUiObject(
            $"Image | Relic Icon {number}",
            button.transform);
        SetAnchors(
            iconObject.GetComponent<RectTransform>(),
            new Vector2(0.22f, 0.50f),
            new Vector2(0.78f, 0.91f));
        Image icon = iconObject.AddComponent<Image>();
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text name = CreateText(
            $"Text | Relic Name {number}",
            button.transform,
            new Vector2(0.07f, 0.37f),
            new Vector2(0.93f, 0.49f),
            $"RELIC {number}",
            24f,
            TextAlignmentOptions.Center);
        name.color = new Color(1f, 0.78f, 0.34f, 1f);

        TMP_Text description = CreateText(
            $"Text | Relic Description {number}",
            button.transform,
            new Vector2(0.075f, 0.08f),
            new Vector2(0.925f, 0.36f),
            "유물 설명",
            17f,
            TextAlignmentOptions.TopLeft);
        description.color = new Color(0.88f, 0.85f, 0.80f, 1f);
    }

    private static void BuildTreasureCanvas()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
            ShopCanvasPath);
        GameObject canvas = Object.Instantiate(source);
        canvas.name = "Canvas | Treasure";
        if (PrefabUtility.IsPartOfPrefabInstance(canvas))
        {
            PrefabUtility.UnpackPrefabInstance(
                canvas,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
        }

        Transform shopPanel = FindDescendant(canvas.transform, "Panel | Shop");
        if (shopPanel == null)
        {
            Object.DestroyImmediate(canvas);
            throw new InvalidOperationException(
                "ShopCanvas is missing Panel | Shop.");
        }
        Object.DestroyImmediate(shopPanel.gameObject);

        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            TreasurePanelPath);
        GameObject panel = PrefabUtility.InstantiatePrefab(panelPrefab)
            as GameObject;
        panel.transform.SetParent(canvas.transform, false);
        panel.transform.SetAsFirstSibling();

        Canvas rootCanvas = canvas.GetComponent<Canvas>();
        if (rootCanvas != null)
        {
            rootCanvas.sortingOrder = 0;
        }

        PrefabUtility.SaveAsPrefabAsset(canvas, TreasureCanvasPath);
        Object.DestroyImmediate(canvas);
    }

    private static void BuildTreasureManagers()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
            ShopManagersPath);
        GameObject root = Object.Instantiate(source);
        root.name = "##--MANAGERS--##";
        root.transform.SetParent(null, false);
        if (PrefabUtility.IsPartOfPrefabInstance(root))
        {
            PrefabUtility.UnpackPrefabInstance(
                root,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
        }

        foreach (ShopSceneController controller in
                 root.GetComponentsInChildren<ShopSceneController>(true))
        {
            Object.DestroyImmediate(controller);
        }
        foreach (EventSceneController controller in
                 root.GetComponentsInChildren<EventSceneController>(true))
        {
            Object.DestroyImmediate(controller);
        }

        if (root.GetComponentInChildren<RelicManager>(true) == null)
        {
            root.AddComponent<RelicManager>();
        }
        if (root.GetComponent<TreasureSceneController>() == null)
        {
            root.AddComponent<TreasureSceneController>();
        }

        PrefabUtility.SaveAsPrefabAsset(root, TreasureManagersPath);
        Object.DestroyImmediate(root);
    }

    private static void BuildTreasureScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            TreasureScenePath,
            OpenSceneMode.Single);
        Camera mainCamera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault();

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
        }
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.025f, 0.02f, 0.035f, 1f);

        GameObject canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            TreasureCanvasPath);
        GameObject canvasObject = PrefabUtility.InstantiatePrefab(
            canvasPrefab,
            scene) as GameObject;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.worldCamera = mainCamera;
        canvas.planeDistance = 10f;
        canvasObject.transform.localScale = Vector3.one;

        GameObject managersPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            TreasureManagersPath);
        PrefabUtility.InstantiatePrefab(managersPrefab, scene);

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TreasureScenePath);
    }

    private static void EnsureSceneInBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == TreasureScenePath))
        {
            return;
        }
        EditorBuildSettings.scenes = scenes.Concat(new[]
        {
            new EditorBuildSettingsScene(TreasureScenePath, true)
        }).ToArray();
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        SetAnchors(
            gameObject.GetComponent<RectTransform>(),
            anchorMin,
            anchorMax);
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        Button button = gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.08f, 0.95f, 1f);
        colors.pressedColor = new Color(0.78f, 0.74f, 0.68f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.65f);
        button.colors = colors;
        return button;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string value,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        SetAnchors(
            gameObject.GetComponent<RectTransform>(),
            anchorMin,
            anchorMax);
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = new Color(0.94f, 0.91f, 0.85f, 1f);
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetAnchors(
        RectTransform rect,
        Vector2 min,
        Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(transform => transform.name == name);
    }

    private static void EnsureFolder(string parent, string folder)
    {
        string path = parent + "/" + folder;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif
