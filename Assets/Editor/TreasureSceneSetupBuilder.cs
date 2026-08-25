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
    private const string MainButtonMaterialPath =
        "Assets/Materials/UI/MainButtonLoaded.mat";
    private const string LoadedFontAssetPath =
        "Assets/Package/Galmuri9 SDF.asset";

    private static readonly Color BackdropColor =
        new Color(0.018f, 0.014f, 0.012f, 0.995f);
    private static readonly Color PanelColor =
        new Color(0.065f, 0.043f, 0.028f, 0.985f);
    private static readonly Color GunmetalColor =
        new Color(0.105f, 0.072f, 0.05f, 1f);
    private static readonly Color BrassColor =
        new Color(0.76f, 0.47f, 0.18f, 1f);
    private static readonly Color EmberColor =
        new Color(0.95f, 0.25f, 0.045f, 1f);
    private static readonly Color PaperColor =
        new Color(0.94f, 0.87f, 0.74f, 1f);

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
        ApplyTreasureCanvasPresentation();
        BuildTreasureManagers();
        BuildTreasureScene();
        EnsureSceneInBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "Dedicated Treasure scene, canvas, and managers were built successfully.");
    }

    [MenuItem("Tools/LOADED/UI/Rebuild Treasure Presentation")]
    public static void RebuildPresentation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before rebuilding the Treasure presentation.");
        }

        EnsureFolder("Assets/Prefabs/UI", "Treasure");
        BuildTreasurePanel();
        ApplyTreasureCanvasPresentation();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Treasure presentation prefab was rebuilt successfully.");
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
        background.color = BackdropColor;

        CreateVaultRays(root.transform);
        CreateImage(
            "Image | Top Rail",
            root.transform,
            new Vector2(0f, 0.945f),
            Vector2.one,
            GunmetalColor);
        CreateImage(
            "Image | Top Ember Line",
            root.transform,
            new Vector2(0.04f, 0.942f),
            new Vector2(0.96f, 0.947f),
            EmberColor);

        TMP_Text kicker = CreateText(
            "Text | Treasure Kicker",
            root.transform,
            new Vector2(0.07f, 0.9f),
            new Vector2(0.93f, 0.94f),
            "SECURE CACHE  //  CLAIM ONE RELIC",
            15f,
            TextAlignmentOptions.Center);
        kicker.color = BrassColor;
        kicker.characterSpacing = 3f;

        TMP_Text title = CreateText(
            "Text | Treasure Title",
            root.transform,
            new Vector2(0.12f, 0.81f),
            new Vector2(0.88f, 0.9f),
            "VAULT RECOVERY",
            43f,
            TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.74f, 0.32f, 1f);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 4f;

        GameObject chestShadow = CreateFramedPanel(
            "Image | Chest Housing Shadow",
            root.transform,
            new Vector2(0.302f, 0.165f),
            new Vector2(0.698f, 0.745f),
            new Color(0f, 0f, 0f, 0.72f),
            Color.black,
            1f);
        chestShadow.GetComponent<RectTransform>().anchoredPosition =
            new Vector2(8f, -8f);
        GameObject chestHousing = CreateFramedPanel(
            "Panel | Chest Housing",
            root.transform,
            new Vector2(0.295f, 0.175f),
            new Vector2(0.69f, 0.755f),
            PanelColor,
            BrassColor,
            2f);
        CreateImage(
            "Image | Chest Housing Header",
            chestHousing.transform,
            new Vector2(0f, 0.91f),
            Vector2.one,
            new Color(0.22f, 0.105f, 0.04f, 1f));
        TMP_Text cacheLabel = CreateText(
            "Text | Chest Housing Header",
            chestHousing.transform,
            new Vector2(0.06f, 0.925f),
            new Vector2(0.94f, 0.99f),
            "RECOVERED CACHE  //  UNSEALED",
            13f,
            TextAlignmentOptions.Center);
        cacheLabel.color = new Color(1f, 0.67f, 0.27f, 1f);
        cacheLabel.fontStyle = FontStyles.Bold;
        CreateImage(
            "Image | Chest Plinth",
            chestHousing.transform,
            new Vector2(0.12f, 0.08f),
            new Vector2(0.88f, 0.13f),
            new Color(0.34f, 0.16f, 0.055f, 1f));

        Button chestButton = CreateButton(
            "Button | Treasure Chest",
            chestHousing.transform,
            new Vector2(0.08f, 0.14f),
            new Vector2(0.92f, 0.9f),
            new Color(0.48f, 0.27f, 0.09f, 1f));
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
            new Vector2(0.055f, 0.19f),
            new Vector2(0.945f, 0.79f));
        choices.SetActive(false);

        for (int index = 0; index < 3; index++)
        {
            CreateRelicChoice(choices.transform, index);
        }

        TMP_Text instruction = CreateText(
            "Text | Treasure Instruction",
            root.transform,
            new Vector2(0.18f, 0.09f),
            new Vector2(0.82f, 0.15f),
            "상자를 클릭해 여십시오.",
            17f,
            TextAlignmentOptions.Center);
        instruction.color = PaperColor;
        instruction.characterSpacing = 2f;

        CreateImage(
            "Image | Instruction Divider Left",
            root.transform,
            new Vector2(0.055f, 0.118f),
            new Vector2(0.17f, 0.122f),
            BrassColor);
        CreateImage(
            "Image | Instruction Divider Right",
            root.transform,
            new Vector2(0.83f, 0.118f),
            new Vector2(0.945f, 0.122f),
            BrassColor);

        Button continueButton = CreateButton(
            "Button | Treasure Continue",
            root.transform,
            new Vector2(0.39f, 0.025f),
            new Vector2(0.61f, 0.085f),
            new Color(0.92f, 0.72f, 0.5f, 1f));
        StyleLoadedButton(continueButton, continueButton.image);
        TMP_Text continueText = CreateText(
            "Text | Continue",
            continueButton.transform,
            new Vector2(0.04f, 0.06f),
            new Vector2(0.96f, 0.94f),
            "TO MAP",
            19f,
            TextAlignmentOptions.Center);
        continueText.fontStyle = FontStyles.Bold;
        continueButton.gameObject.SetActive(false);

        CreateImage(
            "Image | Bottom Rail",
            root.transform,
            Vector2.zero,
            new Vector2(1f, 0.018f),
            new Color(0.055f, 0.028f, 0.02f, 1f));

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
            new Color(0.92f, 0.72f, 0.5f, 1f));
        StyleLoadedButton(button, button.image);

        CreateImage(
            "Image | Relic Card Accent",
            button.transform,
            Vector2.zero,
            new Vector2(0.022f, 1f),
            index == 0 ? EmberColor : BrassColor);
        TMP_Text indexText = CreateText(
            "Text | Relic Card Index",
            button.transform,
            new Vector2(0.055f, 0.89f),
            new Vector2(0.26f, 0.97f),
            $"R-{number:00}",
            14f,
            TextAlignmentOptions.Left);
        indexText.color = new Color(1f, 0.58f, 0.19f, 1f);
        indexText.fontStyle = FontStyles.Bold;

        GameObject iconHousing = CreateFramedPanel(
            "Panel | Relic Icon Housing",
            button.transform,
            new Vector2(0.18f, 0.48f),
            new Vector2(0.82f, 0.88f),
            new Color(0.055f, 0.035f, 0.025f, 0.94f),
            new Color(0.62f, 0.36f, 0.13f, 0.9f),
            1f);

        GameObject iconObject = CreateUiObject(
            $"Image | Relic Icon {number}",
            iconHousing.transform);
        SetAnchors(
            iconObject.GetComponent<RectTransform>(),
            new Vector2(0.1f, 0.1f),
            new Vector2(0.9f, 0.9f));
        Image icon = iconObject.AddComponent<Image>();
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text name = CreateText(
            $"Text | Relic Name {number}",
            button.transform,
            new Vector2(0.07f, 0.345f),
            new Vector2(0.93f, 0.465f),
            $"RELIC {number}",
            22f,
            TextAlignmentOptions.Center);
        name.color = new Color(1f, 0.78f, 0.34f, 1f);
        name.fontStyle = FontStyles.Bold;

        CreateImage(
            "Image | Relic Name Divider",
            button.transform,
            new Vector2(0.12f, 0.325f),
            new Vector2(0.88f, 0.331f),
            new Color(0.72f, 0.42f, 0.15f, 0.9f));

        TMP_Text description = CreateText(
            $"Text | Relic Description {number}",
            button.transform,
            new Vector2(0.085f, 0.075f),
            new Vector2(0.915f, 0.305f),
            "유물 설명",
            16f,
            TextAlignmentOptions.TopLeft);
        description.color = PaperColor;
        description.lineSpacing = 5f;
    }

    private static void CreateVaultRays(Transform parent)
    {
        for (int index = 0; index < 8; index++)
        {
            float x = -0.08f + index * 0.165f;
            Image ray = CreateImage(
                $"Image | Vault Ray {index + 1}",
                parent,
                new Vector2(x, -0.08f),
                new Vector2(x + 0.035f, 1.08f),
                new Color(0.34f, 0.105f, 0.025f, 0.10f));
            ray.rectTransform.localEulerAngles = new Vector3(0f, 0f, 9f);
        }
    }

    private static GameObject CreateFramedPanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color fill,
        Color outlineColor,
        float outlineSize)
    {
        Image image = CreateImage(
            name,
            parent,
            anchorMin,
            anchorMax,
            fill);
        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(outlineSize, -outlineSize);
        outline.useGraphicAlpha = true;
        return image.gameObject;
    }

    private static Image CreateImage(
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
        image.raycastTarget = false;
        return image;
    }

    private static void StyleLoadedButton(Button button, Image image)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            MainButtonMaterialPath);
        if (material == null)
        {
            throw new MissingReferenceException(
                "LOADED main button material is missing: "
                + MainButtonMaterialPath);
        }

        image.material = material;
        image.raycastTarget = true;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        if (button.GetComponent<MainButtonShaderFeedback>() == null)
        {
            button.gameObject.AddComponent<MainButtonShaderFeedback>();
        }
    }

    private static void ApplyTreasureCanvasPresentation()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(
            TreasureCanvasPath);
        try
        {
            Transform choices = FindDescendant(
                root.transform,
                "Panel | Relic Choice");
            choices ??= FindDescendant(
                root.transform,
                "Panel | Relic Choices");
            if (choices == null)
            {
                throw new InvalidOperationException(
                    "TreasureCanvas is missing its relic choice panel.");
            }

            SetAnchors(
                choices.GetComponent<RectTransform>(),
                new Vector2(0.055f, 0.23f),
                new Vector2(0.945f, 0.75f));
            Image panelImage = choices.GetComponent<Image>();
            panelImage ??= choices.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.025f, 0.016f, 0.012f, 0.88f);
            panelImage.raycastTarget = false;
            Outline panelOutline = choices.GetComponent<Outline>();
            panelOutline ??= choices.gameObject.AddComponent<Outline>();
            panelOutline.effectColor = BrassColor;
            panelOutline.effectDistance = new Vector2(2f, -2f);
            panelOutline.useGraphicAlpha = true;

            Transform layout = FindDescendant(
                choices,
                "Layout | Relics");
            layout ??= choices;
            SetAnchors(
                layout.GetComponent<RectTransform>(),
                new Vector2(0.018f, 0.035f),
                new Vector2(0.982f, 0.965f));
            HorizontalLayoutGroup layoutGroup =
                layout.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.padding = new RectOffset(16, 16, 16, 16);
                layoutGroup.spacing = 24f;
                layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                layoutGroup.childControlWidth = true;
                layoutGroup.childControlHeight = true;
                layoutGroup.childForceExpandWidth = true;
                layoutGroup.childForceExpandHeight = true;
            }

            for (int index = 0; index < 3; index++)
            {
                int number = index + 1;
                Button button = FindDescendant(
                        choices,
                        $"Button | Relic {number}")
                    ?.GetComponent<Button>();
                button ??= FindDescendant(
                        choices,
                        $"Button | Relic Choice {number}")
                    ?.GetComponent<Button>();
                if (button == null)
                {
                    throw new InvalidOperationException(
                        $"TreasureCanvas is missing relic button {number}.");
                }

                Sprite legacyIcon = button.image.sprite;
                button.image.sprite = null;
                button.image.type = Image.Type.Simple;
                button.image.preserveAspect = false;
                button.image.color = new Color(0.92f, 0.72f, 0.5f, 1f);
                StyleLoadedButton(button, button.image);

                LayoutElement layoutElement =
                    button.GetComponent<LayoutElement>();
                layoutElement ??= button.gameObject.AddComponent<LayoutElement>();
                layoutElement.minWidth = 250f;
                layoutElement.minHeight = 300f;
                layoutElement.flexibleWidth = 1f;
                layoutElement.flexibleHeight = 1f;

                EnsureCanvasRelicCard(button, number, legacyIcon);
            }

            Button continueButton = FindDescendant(
                    root.transform,
                    "Button | Treasure Continue")
                ?.GetComponent<Button>();
            if (continueButton != null)
            {
                SetAnchors(
                    continueButton.GetComponent<RectTransform>(),
                    new Vector2(0.39f, 0.155f),
                    new Vector2(0.61f, 0.215f));
            }

            PrefabUtility.SaveAsPrefabAsset(root, TreasureCanvasPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureCanvasRelicCard(
        Button button,
        int number,
        Sprite legacyIcon)
    {
        Image accent = GetOrCreateImage(
            "Image | Relic Card Accent",
            button.transform);
        SetAnchors(
            accent.rectTransform,
            Vector2.zero,
            new Vector2(0.022f, 1f));
        accent.color = number == 1 ? EmberColor : BrassColor;

        TMP_Text indexText = GetOrCreateText(
            "Text | Relic Card Index",
            button.transform);
        SetAnchors(
            indexText.rectTransform,
            new Vector2(0.055f, 0.89f),
            new Vector2(0.28f, 0.97f));
        indexText.text = $"R-{number:00}";
        indexText.fontSize = 14f;
        indexText.alignment = TextAlignmentOptions.Left;
        indexText.color = new Color(1f, 0.58f, 0.19f, 1f);
        indexText.fontStyle = FontStyles.Bold;

        GameObject iconHousing = GetOrCreateFramedPanel(
            "Panel | Relic Icon Housing",
            button.transform,
            new Color(0.055f, 0.035f, 0.025f, 0.94f),
            new Color(0.62f, 0.36f, 0.13f, 0.9f));
        SetAnchors(
            iconHousing.GetComponent<RectTransform>(),
            new Vector2(0.18f, 0.49f),
            new Vector2(0.82f, 0.87f));

        string iconName = $"Image | Relic Icon {number}";
        Transform existingIcon = FindDescendant(button.transform, iconName);
        Image icon = existingIcon == null
            ? GetOrCreateImage(iconName, iconHousing.transform)
            : existingIcon.GetComponent<Image>();
        icon.transform.SetParent(iconHousing.transform, false);
        SetAnchors(
            icon.rectTransform,
            new Vector2(0.1f, 0.1f),
            new Vector2(0.9f, 0.9f));
        icon.material = null;
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        if (icon.sprite == null)
        {
            icon.sprite = legacyIcon;
        }

        TMP_Text name = GetOrCreateText(
            $"Text | Relic Name {number}",
            button.transform);
        SetAnchors(
            name.rectTransform,
            new Vector2(0.07f, 0.35f),
            new Vector2(0.93f, 0.47f));
        name.text = $"RELIC {number}";
        name.fontSize = 22f;
        name.alignment = TextAlignmentOptions.Center;
        name.color = new Color(1f, 0.78f, 0.34f, 1f);
        name.fontStyle = FontStyles.Bold;

        Image divider = GetOrCreateImage(
            "Image | Relic Name Divider",
            button.transform);
        SetAnchors(
            divider.rectTransform,
            new Vector2(0.12f, 0.325f),
            new Vector2(0.88f, 0.331f));
        divider.color = new Color(0.72f, 0.42f, 0.15f, 0.9f);

        TMP_Text description = GetOrCreateText(
            $"Text | Relic Description {number}",
            button.transform);
        SetAnchors(
            description.rectTransform,
            new Vector2(0.085f, 0.075f),
            new Vector2(0.915f, 0.305f));
        description.text = "RELIC DESCRIPTION";
        description.fontSize = 16f;
        description.alignment = TextAlignmentOptions.TopLeft;
        description.color = PaperColor;
        description.lineSpacing = 5f;
    }

    private static Image GetOrCreateImage(string name, Transform parent)
    {
        Transform child = FindDirectChild(parent, name);
        if (child != null)
        {
            Image existing = child.GetComponent<Image>();
            if (existing != null)
            {
                existing.raycastTarget = false;
                return existing;
            }
        }

        return CreateImage(
            name,
            parent,
            Vector2.zero,
            Vector2.one,
            Color.white);
    }

    private static TMP_Text GetOrCreateText(string name, Transform parent)
    {
        Transform child = FindDirectChild(parent, name);
        if (child != null)
        {
            TMP_Text existing = child.GetComponent<TMP_Text>();
            if (existing != null)
            {
                return existing;
            }
        }

        return CreateText(
            name,
            parent,
            Vector2.zero,
            Vector2.one,
            string.Empty,
            16f,
            TextAlignmentOptions.Center);
    }

    private static GameObject GetOrCreateFramedPanel(
        string name,
        Transform parent,
        Color fill,
        Color outlineColor)
    {
        Transform child = FindDirectChild(parent, name);
        if (child != null)
        {
            Image image = child.GetComponent<Image>();
            image ??= child.gameObject.AddComponent<Image>();
            image.color = fill;
            image.raycastTarget = false;
            Outline outline = child.GetComponent<Outline>();
            outline ??= child.gameObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(1f, -1f);
            return child.gameObject;
        }

        return CreateFramedPanel(
            name,
            parent,
            Vector2.zero,
            Vector2.one,
            fill,
            outlineColor,
            1f);
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
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
        TMP_FontAsset loadedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            LoadedFontAssetPath);
        if (loadedFont != null)
        {
            text.font = loadedFont;
        }
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
