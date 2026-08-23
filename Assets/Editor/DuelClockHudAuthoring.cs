#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class DuelClockHudAuthoring
{
    private const string CanvasPrefabPath =
        "Assets/Prefabs/UI/Canvas.prefab";
    private const string BattleScenePath =
        "Assets/Scenes/Battle.unity";
    private const string FloatingPanelName = "Panel | Floating";
    private const string HudRootName = "Layout | Duel Clock";

    private static readonly Color32 BackdropColor =
        new Color32(18, 13, 25, 235);
    private static readonly Color32 TitleColor =
        new Color32(247, 191, 62, 255);
    private static readonly Color32 PrimaryTextColor =
        new Color32(250, 245, 238, 255);
    private static readonly Color32 SecondaryTextColor =
        new Color32(208, 199, 216, 255);
    private static readonly Color32 BeatTextColor =
        new Color32(255, 226, 145, 255);
    private static readonly Color32 CompletedSpawnTextColor =
        new Color32(145, 148, 158, 255);
    private static readonly Color32 TrackColor =
        new Color32(60, 43, 66, 230);
    private static readonly Color32 FillColor =
        new Color32(231, 77, 42, 255);
    private static readonly Color32 MarkerColor =
        new Color32(255, 215, 92, 255);

    [MenuItem("Tools/LOADED/Build Duel Clock HUD")]
    public static void BuildFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before building the Duel Clock HUD.");
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log(
                "Duel Clock HUD build was cancelled to preserve unsaved scene changes.");
            return;
        }

        BuildInternal();
    }

    public static void ApplyFromCommandLine()
    {
        BuildInternal();
    }

    private static void BuildInternal()
    {
        bool prefabChanged = BuildCanvasPrefab();

        if (prefabChanged)
        {
            AssetDatabase.ImportAsset(
                CanvasPrefabPath,
                ImportAssetOptions.ForceUpdate);
        }

        int removedDuplicateCount = ReconcileBattleScene();
        Debug.Log(
            "Duel Clock HUD authoring complete. "
            + $"Prefab changed: {prefabChanged}, "
            + $"removed Battle duplicates: {removedDuplicateCount}.");
    }

    private static bool BuildCanvasPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(
            CanvasPrefabPath);

        try
        {
            Transform floatingPanel = FindDescendant(
                prefabRoot.transform,
                FloatingPanelName);
            Transform hudRoot = floatingPanel == null
                ? null
                : FindDirectChild(floatingPanel, HudRootName);

            if (hudRoot == null)
            {
                throw new InvalidOperationException(
                    $"Canvas prefab is missing '{FloatingPanelName}/{HudRootName}'.");
            }

            TMP_Text fontSource = hudRoot.GetComponentInChildren<TMP_Text>(
                true);
            fontSource ??= prefabRoot.GetComponentInChildren<TMP_Text>(true);

            if (fontSource == null || fontSource.font == null)
            {
                throw new InvalidOperationException(
                    "Canvas prefab does not contain a usable TMP font source.");
            }

            if (IsCurrentLayout(hudRoot))
            {
                return false;
            }

            ConfigureHud(hudRoot, fontSource.font);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CanvasPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureHud(
        Transform hudRoot,
        TMP_FontAsset font)
    {
        ClearChildren(hudRoot);
        hudRoot.gameObject.layer = 5;
        hudRoot.gameObject.SetActive(true);

        RectTransform rootRect = hudRoot as RectTransform;
        rootRect.anchorMin = new Vector2(0.015f, 0.71f);
        rootRect.anchorMax = rootRect.anchorMin;
        rootRect.pivot = new Vector2(0f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(320f, 124f);
        rootRect.localScale = Vector3.one;

        Image backdrop = GetOrAddComponent<Image>(hudRoot.gameObject);
        backdrop.color = BackdropColor;
        backdrop.raycastTarget = false;
        backdrop.type = backdrop.sprite != null
            && backdrop.sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;

        CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(
            hudRoot.gameObject);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        VerticalLayoutGroup rootLayout =
            GetOrAddComponent<VerticalLayoutGroup>(hudRoot.gameObject);
        rootLayout.padding = new RectOffset(14, 14, 10, 10);
        rootLayout.spacing = 6f;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        GameObject header = CreateLayoutRow(
            "Layout | Header",
            hudRoot,
            25f,
            8f);
        TMP_Text titleText = CreateText(
            "Text | Title",
            header.transform,
            font,
            "DUEL CLOCK",
            20f,
            FontStyles.Bold,
            TextAlignmentOptions.Left,
            TitleColor,
            1f,
            0f);
        TMP_Text enemyCountText = CreateText(
            "Text | Enemy Count",
            header.transform,
            font,
            "적 스폰까지 (0/5)",
            16f,
            FontStyles.Bold,
            TextAlignmentOptions.Right,
            BeatTextColor,
            0f,
            150f);

        GameObject meter = CreateContainer(
            "Layout | Meter",
            hudRoot,
            25f);
        Image track = CreateImage(
            "Image | Track",
            meter.transform,
            TrackColor);
        Stretch(track.rectTransform, 0f, 0f, 0f, 0f);
        Image progressFill = CreateImage(
            "Image | Progress Fill",
            meter.transform,
            FillColor);
        Stretch(progressFill.rectTransform, 3f, 3f, 3f, 3f);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0.62f;

        Image marker = CreateImage(
            "Image | Beat Marker",
            meter.transform,
            MarkerColor);
        RectTransform markerRect = marker.rectTransform;
        markerRect.anchorMin = new Vector2(1f, 0f);
        markerRect.anchorMax = new Vector2(1f, 1f);
        markerRect.pivot = new Vector2(1f, 0.5f);
        markerRect.anchoredPosition = new Vector2(-2f, 0f);
        markerRect.sizeDelta = new Vector2(2f, -2f);

        GameObject footer = CreateLayoutRow(
            "Layout | Footer",
            hudRoot,
            24f,
            8f);
        TMP_Text progressText = CreateText(
            "Text | Progress",
            footer.transform,
            font,
            "62%",
            16f,
            FontStyles.Bold,
            TextAlignmentOptions.Left,
            PrimaryTextColor,
            0f,
            84f);
        TMP_Text actionPreviewText = CreateText(
            "Text | Action Preview",
            footer.transform,
            font,
            "남은 적 수: 5",
            14f,
            FontStyles.Normal,
            TextAlignmentOptions.Right,
            SecondaryTextColor,
            1f,
            0f);

        DuelClockHUD hud = GetOrAddComponent<DuelClockHUD>(
            hudRoot.gameObject);
        SerializedObject serializedHud = new SerializedObject(hud);
        serializedHud.FindProperty("authoredLayoutVersion").intValue =
            DuelClockHUD.CurrentLayoutVersion;
        serializedHud.FindProperty("canvasGroup").objectReferenceValue =
            canvasGroup;
        serializedHud.FindProperty("progressFill").objectReferenceValue =
            progressFill;
        serializedHud.FindProperty("fillLerpSpeed").floatValue = 12f;
        serializedHud.FindProperty("beatFillLerpSpeed").floatValue = 28f;
        serializedHud.FindProperty("beatFullHoldDuration").floatValue =
            0.08f;
        serializedHud.FindProperty("beatPulseDuration").floatValue = 0.36f;
        serializedHud.FindProperty("beatPulseScale").floatValue = 1.12f;
        serializedHud.FindProperty("beatPulseColor").colorValue = TitleColor;
        serializedHud.FindProperty("titleText").objectReferenceValue =
            titleText;
        serializedHud.FindProperty("enemyCountText").objectReferenceValue =
            enemyCountText;
        serializedHud.FindProperty("allEnemiesSpawnedTextColor").colorValue =
            CompletedSpawnTextColor;
        serializedHud.FindProperty("progressText").objectReferenceValue =
            progressText;
        serializedHud.FindProperty("actionPreviewText")
            .objectReferenceValue = actionPreviewText;
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool IsCurrentLayout(Transform hudRoot)
    {
        DuelClockHUD hud = hudRoot.GetComponent<DuelClockHUD>();

        if (hud == null)
        {
            return false;
        }

        SerializedObject serializedHud = new SerializedObject(hud);
        SerializedProperty layoutVersion = serializedHud.FindProperty(
            "authoredLayoutVersion");
        Transform header = FindDirectChild(hudRoot, "Layout | Header");
        Transform meter = FindDirectChild(hudRoot, "Layout | Meter");
        Transform footer = FindDirectChild(hudRoot, "Layout | Footer");
        return layoutVersion != null
            && layoutVersion.intValue == DuelClockHUD.CurrentLayoutVersion
            && header != null
            && meter != null
            && footer != null
            && FindDirectChild(header, "Text | Title") != null
            && FindDirectChild(header, "Text | Enemy Count") != null
            && FindDirectChild(meter, "Image | Track") != null
            && FindDirectChild(meter, "Image | Progress Fill") != null
            && FindDirectChild(meter, "Image | Beat Marker") != null
            && FindDirectChild(footer, "Text | Progress") != null
            && FindDirectChild(footer, "Text | Action Preview") != null;
    }

    private static int ReconcileBattleScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            BattleScenePath,
            OpenSceneMode.Single);
        Transform floatingPanel = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            floatingPanel = FindDescendant(
                root.transform,
                FloatingPanelName);

            if (floatingPanel != null)
            {
                break;
            }
        }

        if (floatingPanel == null)
        {
            throw new InvalidOperationException(
                $"Battle scene is missing '{FloatingPanelName}'.");
        }

        List<Transform> hudRoots = FindDirectChildren(
            floatingPanel,
            HudRootName);
        Transform configuredRoot = null;

        foreach (Transform candidate in hudRoots)
        {
            if (candidate.GetComponent<DuelClockHUD>() != null)
            {
                configuredRoot = candidate;
                break;
            }
        }

        if (configuredRoot == null)
        {
            throw new InvalidOperationException(
                "Battle scene did not inherit the configured Duel Clock HUD from Canvas.prefab.");
        }

        int removedDuplicateCount = 0;

        foreach (Transform candidate in hudRoots)
        {
            if (candidate != configuredRoot)
            {
                Object.DestroyImmediate(candidate.gameObject);
                removedDuplicateCount++;
            }
        }

        if (removedDuplicateCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Battle scene could not be saved after Duel Clock HUD authoring.");
            }
        }

        return removedDuplicateCount;
    }

    private static GameObject CreateLayoutRow(
        string objectName,
        Transform parent,
        float preferredHeight,
        float spacing)
    {
        GameObject row = CreateContainer(
            objectName,
            parent,
            preferredHeight);
        HorizontalLayoutGroup layout = row.AddComponent<
            HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return row;
    }

    private static GameObject CreateContainer(
        string objectName,
        Transform parent,
        float preferredHeight)
    {
        GameObject container = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(LayoutElement));
        container.layer = 5;
        container.transform.SetParent(parent, false);
        LayoutElement element = container.GetComponent<LayoutElement>();
        element.preferredHeight = preferredHeight;
        element.flexibleWidth = 1f;
        return container;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        TMP_FontAsset font,
        string text,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color,
        float flexibleWidth,
        float preferredWidth)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(LayoutElement));
        textObject.layer = 5;
        textObject.transform.SetParent(parent, false);
        TMP_Text textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.font = font;
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.enableAutoSizing = true;
        textComponent.fontSizeMin = 10f;
        textComponent.fontSizeMax = fontSize;
        textComponent.textWrappingMode = TextWrappingModes.NoWrap;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.raycastTarget = false;
        LayoutElement element = textObject.GetComponent<LayoutElement>();
        element.flexibleWidth = flexibleWidth;

        if (preferredWidth > 0f)
        {
            element.preferredWidth = preferredWidth;
        }

        return textComponent;
    }

    private static Image CreateImage(
        string objectName,
        Transform parent,
        Color color)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform));
        imageObject.layer = 5;
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd");
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(
        RectTransform rect,
        float left,
        float right,
        float top,
        float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            Object.DestroyImmediate(parent.GetChild(index).gameObject);
        }
    }

    private static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Transform FindDescendant(
        Transform root,
        string objectName)
    {
        if (root.name == objectName)
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDescendant(
                root.GetChild(index),
                objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindDirectChild(
        Transform parent,
        string objectName)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);

            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    private static List<Transform> FindDirectChildren(
        Transform parent,
        string objectName)
    {
        List<Transform> results = new List<Transform>();

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);

            if (child.name == objectName)
            {
                results.Add(child);
            }
        }

        return results;
    }
}
#endif
