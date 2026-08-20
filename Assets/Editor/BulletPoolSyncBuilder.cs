#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BulletPoolSyncBuilder
{
    private const string BulletDataRoot = "Assets/Scripts/Bullet/SO";

    private static readonly string[] PrefabRoots =
    {
        "Assets/Prefabs",
        "Assets/Resources/UI"
    };

    [MenuItem("Tools/LOADED/Sync All Bullet Pools")]
    public static void SyncAllBulletPools()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before synchronizing bullet pools.");
        }

        string[] bulletPaths = FindAllBulletDataPaths();
        if (bulletPaths.Length == 0)
        {
            throw new InvalidOperationException(
                $"No BulletData assets were found under {BulletDataRoot}.");
        }

        int changedAssets = 0;
        int changedEntries = 0;
        foreach (string path in FindPrefabPaths())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = SynchronizeOwners(
                    root, LoadBulletData(bulletPaths));
                if (changed <= 0)
                {
                    continue;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                changedAssets++;
                changedEntries += changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (EditorBuildSettingsScene buildScene in
                     EditorBuildSettings.scenes.Where(scene => scene.enabled))
            {
                Scene scene = EditorSceneManager.OpenScene(
                    buildScene.path, OpenSceneMode.Single);
                BulletData[] bullets = LoadBulletData(bulletPaths);
                int changed = scene.GetRootGameObjects()
                    .Sum(root => SynchronizeOwners(root, bullets));
                if (changed <= 0)
                {
                    continue;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, buildScene.path);
                changedAssets++;
                changedEntries += changed;
            }
        }
        finally
        {
            if (previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Bullet pool sync complete. Authored bullets: {bulletPaths.Length}, "
            + $"changed assets: {changedAssets}, changed entries: "
            + $"{changedEntries}.");
    }

    public static void SyncFromCommandLine()
    {
        SyncAllBulletPools();
    }

    internal static BulletData[] LoadAllBulletData()
    {
        return LoadBulletData(FindAllBulletDataPaths());
    }

    private static string[] FindAllBulletDataPaths()
    {
        return AssetDatabase.FindAssets(
                "t:BulletData", new[] { BulletDataRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static BulletData[] LoadBulletData(
        IEnumerable<string> assetPaths)
    {
        return assetPaths
            .Select(AssetDatabase.LoadAssetAtPath<BulletData>)
            .Where(data => data != null)
            .ToArray();
    }

    private static IEnumerable<string> FindPrefabPaths()
    {
        return AssetDatabase.FindAssets("t:Prefab", PrefabRoots)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static int SynchronizeOwners(
        GameObject root,
        IReadOnlyList<BulletData> bullets)
    {
        int changed = 0;
        foreach (ShopManager shop in
                 root.GetComponentsInChildren<ShopManager>(true))
        {
            changed += Synchronize(shop, "bulletPool", bullets);
        }

        foreach (BulletDictionaryController dictionary in
                 root.GetComponentsInChildren<BulletDictionaryController>(
                     true))
        {
            changed += Synchronize(dictionary, "bullets", bullets);
        }

        return changed;
    }

    private static int Synchronize(
        Object owner,
        string propertyName,
        IReadOnlyList<BulletData> bullets)
    {
        SerializedObject serialized = new(owner);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            throw new InvalidOperationException(
                $"{owner.GetType().Name}.{propertyName} is not an array.");
        }

        int originalSize = property.arraySize;
        List<Object> synchronized = new(originalSize + bullets.Count);
        HashSet<Object> existing = new();
        for (int index = 0; index < property.arraySize; index++)
        {
            Object value = property.GetArrayElementAtIndex(index)
                .objectReferenceValue;
            if (value != null && existing.Add(value))
            {
                synchronized.Add(value);
            }
        }

        foreach (BulletData bullet in bullets)
        {
            if (!existing.Add(bullet))
            {
                continue;
            }

            synchronized.Add(bullet);
        }

        int changed = Math.Abs(originalSize - synchronized.Count);
        for (int index = 0; index < synchronized.Count; index++)
        {
            if (index >= originalSize
                || property.GetArrayElementAtIndex(index)
                    .objectReferenceValue != synchronized[index])
            {
                changed++;
            }
        }

        if (changed > 0)
        {
            property.arraySize = synchronized.Count;
            for (int index = 0; index < synchronized.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    synchronized[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        return changed;
    }
}
#endif
