using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MainButtonShaderPrefabBuilder
{
    internal const string PrefabPath =
        "Assets/Prefabs/UI/Button/Button _ Main.prefab";
    internal const string ShaderPath =
        "Assets/Shaders/MainButtonLoaded.shader";
    internal const string MaterialPath =
        "Assets/Materials/UI/MainButtonLoaded.mat";

    [MenuItem("Tools/LOADED/UI/Rebuild Main Button Shader Prefab")]
    public static void RebuildFromMenu()
    {
        Rebuild();
    }

    public static void RebuildFromCommandLine()
    {
        Rebuild();
    }

    private static void Rebuild()
    {
        Material material = CreateOrUpdateMaterial();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            RemoveLegacyVisual(root.transform, "Image | Hover BG");
            Image legacyTintImage = ConfigureLegacyTintSource(root.transform);

            Animator animator = root.GetComponent<Animator>();

            if (animator != null)
            {
                Object.DestroyImmediate(animator);
            }

            Image image = root.GetComponent<Image>();
            Button button = root.GetComponent<Button>();

            if (image == null || button == null)
            {
                throw new MissingComponentException(
                    "Button _ Main requires Image and Button components.");
            }

            SerializedObject serializedImage = new SerializedObject(image);
            serializedImage.FindProperty("m_Sprite")
                .objectReferenceValue = null;
            serializedImage.ApplyModifiedPropertiesWithoutUndo();
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.material = material;
            image.raycastTarget = true;

            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;

            MainButtonShaderFeedback feedback =
                root.GetComponent<MainButtonShaderFeedback>();

            if (feedback == null)
            {
                feedback = root.AddComponent<MainButtonShaderFeedback>();
            }

            SerializedObject serializedFeedback =
                new SerializedObject(feedback);
            serializedFeedback.FindProperty("targetImage")
                .objectReferenceValue = image;
            serializedFeedback.FindProperty("legacyTintImage")
                .objectReferenceValue = legacyTintImage;
            serializedFeedback.ApplyModifiedPropertiesWithoutUndo();

            TMP_Text label = root.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.raycastTarget = false;
                label.transform.SetAsLastSibling();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
        Debug.Log(
            "Rebuilt Button _ Main with hover/click-only shader feedback.");
    }

    private static Material CreateOrUpdateMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

        if (shader == null)
        {
            throw new MissingReferenceException(
                "Main button shader was not imported: " + ShaderPath);
        }

        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material == null)
        {
            material = new Material(shader)
            {
                name = "MainButtonLoaded"
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetColor(
            "_PlateTop",
            new Color(0.72f, 0.49f, 0.24f, 1f));
        material.SetColor(
            "_PlateBottom",
            new Color(0.42f, 0.22f, 0.075f, 1f));
        material.SetColor(
            "_BorderColor",
            new Color(0.075f, 0.032f, 0.014f, 1f));
        material.SetColor(
            "_HoverColor",
            new Color(1f, 0.21f, 0.015f, 1f));
        material.SetColor(
            "_ClickColor",
            new Color(1f, 0.78f, 0.27f, 1f));
        material.SetFloat("_BorderWidth", 0.038f);
        material.SetFloat("_Chamfer", 0.075f);
        material.SetFloat("_GrainStrength", 0.075f);
        material.SetColor("_InstanceTint", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Image ConfigureLegacyTintSource(Transform root)
    {
        Transform legacyTint = root.Find("Data | Legacy Tint")
            ?? root.Find("Image | Normal BG");

        if (legacyTint == null)
        {
            return null;
        }

        legacyTint.name = "Data | Legacy Tint";
        Image image = legacyTint.GetComponent<Image>();

        if (image != null)
        {
            image.enabled = false;
            image.raycastTarget = false;
        }

        legacyTint.gameObject.SetActive(false);
        return image;
    }

    private static void RemoveLegacyVisual(
        Transform root,
        string childName)
    {
        Transform child = root.Find(childName);

        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }
}
