#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class BulletArtworkCatalogBuilder
{
    private const string BulletRoot = "Assets/Scripts/Bullet/SO";
    private const string ArtworkRoot = "Assets/Sprites/Bullet_LineArt";

    [MenuItem("Tools/LOADED/Apply Full Cartoon Bullet Artwork")]
    public static void Apply()
    {
        EnsureFolders();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string[] guids = AssetDatabase.FindAssets(
            "t:BulletData", new[] { BulletRoot });
        int assigned = 0;
        foreach (string guid in guids)
        {
            string dataPath = AssetDatabase.GUIDToAssetPath(guid);
            BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(dataPath);
            if (data == null)
                continue;

            string assetName = Path.GetFileNameWithoutExtension(dataPath);
            string slug = Regex.Replace(assetName.ToLowerInvariant(),
                "[^a-z0-9]+", "_").Trim('_');
            string artworkPath =
                $"{ArtworkRoot}/{data.Grade}/Bullet_{slug}.png";
            if (!File.Exists(artworkPath))
                throw new FileNotFoundException(
                    $"Missing cartoon artwork for {data.DisplayName}: {artworkPath}");

            ConfigureImporter(artworkPath);
            Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(artworkPath);
            if (artwork == null)
                throw new InvalidOperationException(
                    $"Failed to import artwork: {artworkPath}");

            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("cylinderIcon").objectReferenceValue = artwork;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            assigned++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"Assigned borderless cartoon artwork to {assigned} bullets.");
    }

    public static void ApplyFromCommandLine() => Apply();

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(ArtworkRoot))
            AssetDatabase.CreateFolder("Assets/Sprites", "Bullet_LineArt");
        foreach (string grade in new[] { "Normal", "Rare", "Ace", "Legendary" })
        {
            string path = $"{ArtworkRoot}/{grade}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(ArtworkRoot, grade);
        }
    }

    private static void ConfigureImporter(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 256;
        importer.spritePixelsPerUnit = 256;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }
}
#endif
