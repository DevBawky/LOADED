using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class LoadingTransitionPrefabBuilder
{
    private const string PrefabFolder = "Assets/Resources/UI";
    private const string PrefabPath = PrefabFolder + "/Canvas _ Loading Transition.prefab";

    [InitializeOnLoadMethod]
    private static void BuildIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                BuildPrefab();
            }
        };
    }

    [MenuItem("Tools/LOADED/Build Loading Transition Prefab")]
    public static void BuildPrefab()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "UI");

        GameObject root = CreateUIObject("Canvas | Loading Transition", null);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();

        Image background = CreateImage("Image | Screen Fill", root.transform, new Color(0.075f, 0.055f, 0.07f, 1f));
        Stretch(background.rectTransform);
        background.type = Image.Type.Filled;
        background.fillMethod = Image.FillMethod.Horizontal;
        background.fillOrigin = (int)Image.OriginHorizontal.Left;
        background.fillAmount = 0f;

        Image shadow = CreateImage("Image | Cylinder Shadow", root.transform, new Color(0f, 0f, 0f, 0.42f));
        SetCenteredRect(shadow.rectTransform, new Vector2(390f, 390f), new Vector2(12f, -14f));
        shadow.sprite = GetRoundSprite();
        CanvasGroup shadowGroup = shadow.gameObject.AddComponent<CanvasGroup>();
        shadowGroup.alpha = 0f;

        Image cylinder = CreateImage("Image | Cylinder", root.transform, new Color(0.20f, 0.19f, 0.21f, 1f));
        SetCenteredRect(cylinder.rectTransform, new Vector2(390f, 390f), Vector2.zero);
        cylinder.sprite = GetRoundSprite();
        CanvasGroup cylinderGroup = cylinder.gameObject.AddComponent<CanvasGroup>();
        cylinderGroup.alpha = 0f;

        Image innerRing = CreateImage("Image | Inner Ring", cylinder.transform, new Color(0.09f, 0.085f, 0.10f, 1f));
        SetCenteredRect(innerRing.rectTransform, new Vector2(285f, 285f), Vector2.zero);
        innerRing.sprite = GetRoundSprite();

        Image hub = CreateImage("Image | Hub", cylinder.transform, new Color(0.42f, 0.38f, 0.36f, 1f));
        SetCenteredRect(hub.rectTransform, new Vector2(72f, 72f), Vector2.zero);
        hub.sprite = GetRoundSprite();

        List<Sprite> projectBulletSprites = FindProjectBulletSprites();
        Sprite bulletSprite = projectBulletSprites.Count > 0 ? projectBulletSprites[0] : GetRoundSprite();
        List<Image> bullets = new List<Image>();

        for (int index = 0; index < 6; index++)
        {
            float angle = (90f - index * 60f) * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 116f;
            Image chamber = CreateImage($"Image | Chamber {index + 1}", cylinder.transform, new Color(0.035f, 0.03f, 0.04f, 1f));
            SetCenteredRect(chamber.rectTransform, new Vector2(88f, 88f), position);
            chamber.sprite = GetRoundSprite();

            Image bullet = CreateImage($"Image | Bullet {index + 1}", cylinder.transform, Color.white);
            SetCenteredRect(bullet.rectTransform, new Vector2(78f, 78f), position);
            bullet.sprite = bulletSprite;
            bullet.preserveAspect = true;
            bullet.raycastTarget = false;
            bullets.Add(bullet);
        }

        GameObject copyRoot = CreateUIObject("Group | Loading Copy", root.transform);
        RectTransform copyRect = copyRoot.GetComponent<RectTransform>();
        copyRect.anchorMin = new Vector2(0.15f, 0.08f);
        copyRect.anchorMax = new Vector2(0.85f, 0.30f);
        copyRect.offsetMin = Vector2.zero;
        copyRect.offsetMax = Vector2.zero;
        CanvasGroup copyGroup = copyRoot.AddComponent<CanvasGroup>();

        TextMeshProUGUI loading = CreateText("Text | Loading", copyRoot.transform, "LOADING", 42f, FontStyles.Bold);
        SetAnchors(loading.rectTransform, new Vector2(0f, 0.55f), Vector2.one);
        TextMeshProUGUI tip = CreateText("Text | Tip", copyRoot.transform, "탄환의 장전 순서를 확인하세요.", 24f, FontStyles.Normal);
        SetAnchors(tip.rectTransform, Vector2.zero, new Vector2(1f, 0.52f));
        tip.color = new Color(0.82f, 0.80f, 0.78f, 1f);

        LoadingTransitionController controller = root.AddComponent<LoadingTransitionController>();
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("transitionCanvasGroup").objectReferenceValue = rootGroup;
        serialized.FindProperty("backgroundFillImage").objectReferenceValue = background;
        serialized.FindProperty("cylinderTransform").objectReferenceValue = cylinder.rectTransform;
        serialized.FindProperty("cylinderCanvasGroup").objectReferenceValue = cylinderGroup;
        serialized.FindProperty("cylinderShadowCanvasGroup").objectReferenceValue = shadowGroup;
        serialized.FindProperty("loadingTextGroup").objectReferenceValue = copyGroup;
        serialized.FindProperty("loadingText").objectReferenceValue = loading;
        serialized.FindProperty("tipText").objectReferenceValue = tip;
        SetObjectArray(serialized.FindProperty("bulletImages"), bullets);
        SetObjectArray(serialized.FindProperty("bulletSprites"), projectBulletSprites);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Built loading transition prefab: {PrefabPath}");
    }

    public static void BuildFromCommandLine()
    {
        BuildPrefab();
    }

    private static List<Sprite> FindProjectBulletSprites()
    {
        List<Sprite> sprites = new List<Sprite>();
        string[] guids = AssetDatabase.FindAssets("t:BulletData", new[] { "Assets/Scripts/Bullet/SO" });
        foreach (string guid in guids)
        {
            BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null && data.CylinderIcon != null && !sprites.Contains(data.CylinderIcon))
            {
                sprites.Add(data.CylinderIcon);
            }
        }
        return sprites;
    }

    private static void SetObjectArray<T>(SerializedProperty property, List<T> values) where T : Object
    {
        property.arraySize = values.Count;
        for (int index = 0; index < values.Count; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        Image image = CreateUIObject(name, parent).AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, FontStyles style)
    {
        TextMeshProUGUI text = CreateUIObject(name, parent).AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static Sprite GetRoundSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
