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

public static class EventSceneSetupBuilder
{
    private const string EventScenePath = "Assets/Scenes/Event.unity";
    private const string ShopCanvasPath =
        "Assets/Prefabs/UI/Shop/ShopCanvas.prefab";
    private const string ShopManagersPath =
        "Assets/Prefabs/UI/Shop/ShopSceneManagers.prefab";
    private const string EventFolder = "Assets/Prefabs/UI/Event";
    private const string EventPanelPath = EventFolder + "/Panel_Event.prefab";
    private const string EventCanvasPath = EventFolder + "/EventCanvas.prefab";
    private const string EventManagersPath =
        EventFolder + "/EventSceneManagers.prefab";
    private const string EventDataFolder = "Assets/Resources/Events";

    [MenuItem("Tools/LOADED/Build Dedicated Event Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before building the Event scene.");
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log(
                "Event scene build was cancelled to preserve unsaved scene changes.");
            return;
        }

        EnsurePrerequisites();
        EnsureFolder("Assets/Prefabs/UI", "Event");
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "Events");

        BuildEventPanel();
        BuildEventCanvas();
        BuildSampleEvents();
        BuildEventManagers();
        BuildEventScene();
        EnsureSceneInBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "Dedicated Event scene, conditional event data, and reusable UI were built successfully.");
    }

    [MenuItem("Tools/LOADED/Refresh Event Definition Pool")]
    public static void RefreshEventDefinitionPool()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before refreshing the Event pool.");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(
            EventManagersPath);
        try
        {
            EventSceneController controller =
                root.GetComponentInChildren<EventSceneController>(true);
            if (controller == null)
            {
                throw new InvalidOperationException(
                    "EventSceneManagers prefab is missing EventSceneController.");
            }

            AssignEveryEventDefinition(controller);
            PrefabUtility.SaveAsPrefabAsset(root, EventManagersPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            "Event pool refreshed from every EventDefinition asset, regardless of folder.");
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
                "Shop shared prefabs could not be prepared. Run the Shop scene builder first.");
        }
    }

    private static void BuildEventPanel()
    {
        GameObject root = CreateUiObject("Panel | Event", null);
        Stretch(root.GetComponent<RectTransform>());
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.055f, 0.05f, 0.08f, 0.97f);

        GameObject artFrame = CreateUiObject("Panel | Event Artwork", root.transform);
        SetAnchors(artFrame.GetComponent<RectTransform>(),
            new Vector2(0.045f, 0.10f), new Vector2(0.48f, 0.90f),
            Vector2.zero, Vector2.zero);
        Image artFrameImage = artFrame.AddComponent<Image>();
        artFrameImage.color = new Color(0.12f, 0.105f, 0.13f, 1f);

        GameObject artwork = CreateUiObject("Image | Event Artwork", artFrame.transform);
        Stretch(artwork.GetComponent<RectTransform>(), 14f);
        Image artworkImage = artwork.AddComponent<Image>();
        artworkImage.color = Color.white;
        artworkImage.preserveAspect = true;
        artworkImage.raycastTarget = false;

        GameObject dialoguePanel = CreateUiObject(
            "Panel | Event Dialogue",
            root.transform);
        SetAnchors(dialoguePanel.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.10f), new Vector2(0.955f, 0.90f),
            Vector2.zero, Vector2.zero);
        Image dialogueBackground = dialoguePanel.AddComponent<Image>();
        dialogueBackground.color = new Color(0.09f, 0.078f, 0.105f, 0.98f);

        CreateText(
            "Text | Event Title",
            dialoguePanel.transform,
            new Vector2(0.055f, 0.84f),
            new Vector2(0.945f, 0.965f),
            "EVENT",
            34f,
            TextAlignmentOptions.Left);
        CreateText(
            "Text | Event Dialogue",
            dialoguePanel.transform,
            new Vector2(0.055f, 0.39f),
            new Vector2(0.945f, 0.82f),
            "Event dialogue",
            22f,
            TextAlignmentOptions.TopLeft);

        CreateChoiceButton(dialoguePanel.transform, 1, 0.275f, 0.365f);
        CreateChoiceButton(dialoguePanel.transform, 2, 0.165f, 0.255f);
        CreateChoiceButton(dialoguePanel.transform, 3, 0.055f, 0.145f);

        PrefabUtility.SaveAsPrefabAsset(root, EventPanelPath);
        Object.DestroyImmediate(root);
    }

    private static void BuildEventCanvas()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
            ShopCanvasPath);
        GameObject canvas = Object.Instantiate(source);
        canvas.name = "Canvas | Event";
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
            throw new InvalidOperationException(
                "ShopCanvas is missing Panel | Shop.");
        }

        Transform managementSource = FindDescendant(
            shopPanel,
            "Panel | Manage Bullets");
        if (managementSource == null)
        {
            throw new InvalidOperationException(
                "Shop panel is missing Panel | Manage Bullets.");
        }

        GameObject management = Object.Instantiate(
            managementSource.gameObject,
            canvas.transform);
        management.name = "Panel | Manage Bullets";
        management.SetActive(false);

        Transform tooltipSource = FindDescendant(
            shopPanel,
            "Panel | Upgrade Tooltip");
        if (tooltipSource != null)
        {
            GameObject tooltip = Object.Instantiate(
                tooltipSource.gameObject,
                canvas.transform);
            tooltip.name = "Panel | Upgrade Tooltip";
            tooltip.SetActive(false);
        }

        Object.DestroyImmediate(shopPanel.gameObject);
        GameObject eventPanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            EventPanelPath);
        GameObject eventPanel = PrefabUtility.InstantiatePrefab(
            eventPanelPrefab) as GameObject;
        eventPanel.transform.SetParent(canvas.transform, false);
        eventPanel.transform.SetAsFirstSibling();

        Canvas rootCanvas = canvas.GetComponent<Canvas>();
        if (rootCanvas != null)
        {
            rootCanvas.sortingOrder = 0;
        }

        PrefabUtility.SaveAsPrefabAsset(canvas, EventCanvasPath);
        Object.DestroyImmediate(canvas);
    }

    private static void BuildEventManagers()
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

        EventSceneController eventController =
            root.GetComponent<EventSceneController>();
        if (eventController == null)
        {
            eventController = root.AddComponent<EventSceneController>();
        }

        AssignEveryEventDefinition(eventController);

        PrefabUtility.SaveAsPrefabAsset(root, EventManagersPath);
        Object.DestroyImmediate(root);
    }

    private static void BuildSampleEvents()
    {
        BulletData sampleBullet = AssetDatabase.FindAssets("t:BulletData")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BulletData>)
            .FirstOrDefault(data => data != null);

        CreateOrUpdateEvent(
            EventDataFolder + "/Event_UnvisitedMerchant.asset",
            "unvisited-merchant",
            "길 잃은 행상인",
            "상인은 낡은 탄환 하나를 손가락 사이에서 굴리며 당신을 바라본다.\n\n“마을 상점에는 들르지도 않았군. 이건 내가 맡아 두기엔 아까운 물건이야.”",
            8f,
            true,
            new[]
            {
                new EventWeightRule
                {
                    statistic = EventRunStatistic.ShopVisits,
                    comparison = EventComparison.Equal,
                    threshold = 0f,
                    operation = EventWeightOperation.Add,
                    value = 22f
                },
                new EventWeightRule
                {
                    statistic = EventRunStatistic.ShopVisits,
                    comparison = EventComparison.GreaterThanOrEqual,
                    threshold = 1f,
                    operation = EventWeightOperation.Multiply,
                    value = 0.25f
                }
            },
            new[]
            {
                new EventChoiceData
                {
                    buttonText = "[받는다] 탄환을 한 발 얻는다.",
                    outcomeText = "행상인은 짧게 웃고 탄환을 건넸다.",
                    requirements = new[]
                    {
                        new EventChoiceRequirement
                        {
                            type = EventChoiceRequirementType.BulletSpaceExists,
                            unavailableReason = "탄환 보유 한도에 도달했습니다."
                        }
                    },
                    effects = new[]
                    {
                        new EventEffect
                        {
                            type = EventEffectType.AddBullet,
                            bullet = sampleBullet
                        }
                    }
                },
                new EventChoiceData
                {
                    buttonText = "[떠난다] 대신 여비를 챙긴다.",
                    outcomeText = "당신은 작은 돈주머니만 챙겨 길을 재촉했다.",
                    effects = new[]
                    {
                        new EventEffect
                        {
                            type = EventEffectType.GainMoney,
                            amount = 10
                        }
                    }
                }
            });

        CreateOrUpdateEvent(
            EventDataFolder + "/Event_EliteGunsmith.asset",
            "elite-gunsmith",
            "상처투성이 총공",
            "총공은 당신의 총집에 남은 강적의 흔적을 알아본다.\n\n“그 정도 놈들을 쓰러뜨렸다면, 내 손을 빌릴 자격은 있겠지.”",
            7f,
            true,
            new[]
            {
                new EventWeightRule
                {
                    statistic = EventRunStatistic.EliteClears,
                    comparison = EventComparison.GreaterThanOrEqual,
                    threshold = 2f,
                    operation = EventWeightOperation.Add,
                    value = 28f
                }
            },
            new[]
            {
                new EventChoiceData
                {
                    buttonText = "[손질을 맡긴다] 탄환 하나를 무료로 강화한다.",
                    outcomeText = "총공의 망치질이 끝나자 탄환은 전보다 날카로운 기척을 띠었다.",
                    requirements = new[]
                    {
                        new EventChoiceRequirement
                        {
                            type = EventChoiceRequirementType.UpgradableBulletExists,
                            unavailableReason = "강화 가능한 탄환이 없습니다."
                        }
                    },
                    effects = new[]
                    {
                        new EventEffect
                        {
                            type = EventEffectType.UpgradeChosenBullet
                        }
                    }
                },
                new EventChoiceData
                {
                    buttonText = "[분해를 맡긴다] 탄환 하나를 제거한다.",
                    outcomeText = "불필요한 탄환은 조각으로 흩어졌다.",
                    requirements = new[]
                    {
                        new EventChoiceRequirement
                        {
                            type = EventChoiceRequirementType.RemovableBulletExists,
                            unavailableReason = "제거할 수 있는 탄환이 없습니다."
                        }
                    },
                    effects = new[]
                    {
                        new EventEffect
                        {
                            type = EventEffectType.RemoveChosenBullet
                        }
                    }
                },
                new EventChoiceData
                {
                    buttonText = "[거절한다] 그대로 떠난다.",
                    outcomeText = "총공은 어깨를 으쓱하고 다시 작업대로 돌아갔다."
                }
            });

        CreateOrUpdateEvent(
            EventDataFolder + "/Event_QuietCamp.asset",
            "quiet-camp",
            "고요한 야영지",
            "바람을 피할 수 있는 작은 야영지가 보인다. 아직 온기가 남은 모닥불 곁에서 잠시 숨을 돌릴 수 있을 것 같다.",
            12f,
            false,
            new[]
            {
                new EventWeightRule
                {
                    statistic = EventRunStatistic.CurrentHealthPercent,
                    comparison = EventComparison.LessThanOrEqual,
                    threshold = 40f,
                    operation = EventWeightOperation.Add,
                    value = 18f
                }
            },
            new[]
            {
                new EventChoiceData
                {
                    buttonText = "[휴식한다] 체력을 15 회복한다.",
                    outcomeText = "짧은 휴식이었지만 몸은 한결 가벼워졌다.",
                    effects = new[]
                    {
                        new EventEffect
                        {
                            type = EventEffectType.Heal,
                            amount = 15
                        }
                    }
                },
                new EventChoiceData
                {
                    buttonText = "[지나친다] 갈 길을 재촉한다.",
                    outcomeText = "당신은 모닥불을 뒤로하고 다시 길에 올랐다."
                }
            });
    }

    private static void CreateOrUpdateEvent(
        string path,
        string eventId,
        string title,
        string dialogue,
        float baseWeight,
        bool oncePerRun,
        EventWeightRule[] rules,
        EventChoiceData[] choices)
    {
        EventDefinition existingDefinition = AssetDatabase
            .FindAssets("t:EventDefinition")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<EventDefinition>)
            .FirstOrDefault(definition => definition != null
                && definition.StableId == eventId);
        if (existingDefinition != null)
        {
            return;
        }

        EventDefinition definition =
            AssetDatabase.LoadAssetAtPath<EventDefinition>(path);
        if (definition != null)
        {
            return;
        }

        definition = ScriptableObject.CreateInstance<EventDefinition>();
        AssetDatabase.CreateAsset(definition, path);

        definition.eventId = eventId;
        definition.displayName = title;
        definition.dialogue = dialogue;
        definition.baseWeight = baseWeight;
        definition.oncePerRun = oncePerRun;
        definition.weightRules = rules;
        definition.choices = choices;
        EditorUtility.SetDirty(definition);
    }

    private static void AssignEveryEventDefinition(
        EventSceneController controller)
    {
        EventDefinition[] definitions = AssetDatabase
            .FindAssets("t:EventDefinition")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<EventDefinition>)
            .Where(definition => definition != null)
            .GroupBy(definition => definition.StableId)
            .Select(group => group.First())
            .OrderBy(definition => definition.StableId,
                StringComparer.Ordinal)
            .ToArray();

        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty pool = serialized.FindProperty("eventPool");
        pool.arraySize = definitions.Length;
        for (int index = 0; index < definitions.Length; index++)
        {
            pool.GetArrayElementAtIndex(index).objectReferenceValue =
                definitions[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void BuildEventScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            EventScenePath,
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
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.04f, 0.035f, 0.06f, 1f);
        }

        GameObject canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            EventCanvasPath);
        GameObject canvasObject = PrefabUtility.InstantiatePrefab(
            canvasPrefab,
            scene) as GameObject;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.worldCamera = mainCamera;
        canvas.planeDistance = 10f;
        canvasObject.transform.localScale = Vector3.one;

        GameObject managersPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            EventManagersPath);
        PrefabUtility.InstantiatePrefab(managersPrefab, scene);

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, EventScenePath);
    }

    private static void EnsureSceneInBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == EventScenePath))
        {
            return;
        }

        EditorBuildSettings.scenes = scenes.Concat(new[]
        {
            new EditorBuildSettingsScene(EventScenePath, true)
        }).ToArray();
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        return gameObject;
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
            anchorMax,
            Vector2.zero,
            Vector2.zero);
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = new Color(0.93f, 0.90f, 0.84f, 1f);
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void CreateChoiceButton(
        Transform parent,
        int index,
        float minY,
        float maxY)
    {
        GameObject gameObject = CreateUiObject(
            $"Button | Event Choice {index}",
            parent);
        SetAnchors(
            gameObject.GetComponent<RectTransform>(),
            new Vector2(0.055f, minY),
            new Vector2(0.945f, maxY),
            Vector2.zero,
            Vector2.zero);
        Image image = gameObject.AddComponent<Image>();
        image.color = new Color(0.24f, 0.205f, 0.17f, 1f);
        Button button = gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.08f, 0.95f, 1f);
        colors.pressedColor = new Color(0.78f, 0.75f, 0.7f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        button.colors = colors;

        TMP_Text text = CreateText(
            "Text | Choice",
            gameObject.transform,
            new Vector2(0.035f, 0.05f),
            new Vector2(0.965f, 0.95f),
            $"Choice {index}",
            20f,
            TextAlignmentOptions.Left);
        text.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.localScale = Vector3.one;
    }

    private static void SetAnchors(
        RectTransform rect,
        Vector2 min,
        Vector2 max,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
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
