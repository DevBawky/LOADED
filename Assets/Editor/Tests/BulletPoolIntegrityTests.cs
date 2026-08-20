#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class BulletPoolIntegrityTests
{
    private static readonly string[] PrefabRoots =
    {
        "Assets/Prefabs",
        "Assets/Resources/UI"
    };

    [Test]
    public void EveryAuthoredBullet_IsRegisteredInEveryPoolOwner()
    {
        string[] expected = BulletPoolSyncBuilder.LoadAllBulletData()
            .Select(AssetDatabase.GetAssetPath)
            .ToArray();
        Assert.That(expected.Length, Is.GreaterThan(0));

        int shopOwners = 0;
        int dictionaryOwners = 0;
        foreach (string path in AssetDatabase.FindAssets(
                     "t:Prefab", PrefabRoots)
                 .Select(AssetDatabase.GUIDToAssetPath)
                 .Distinct())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ValidateOwners(
                    root, path, expected, ref shopOwners,
                    ref dictionaryOwners);
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
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    ValidateOwners(
                        root, buildScene.path, expected, ref shopOwners,
                        ref dictionaryOwners);
                }
            }
        }
        finally
        {
            if (previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        Assert.That(shopOwners, Is.GreaterThan(0));
        Assert.That(dictionaryOwners, Is.GreaterThan(0));
    }

    private static void ValidateOwners(
        GameObject root,
        string assetPath,
        IReadOnlyCollection<string> expected,
        ref int shopOwners,
        ref int dictionaryOwners)
    {
        foreach (ShopManager shop in
                 root.GetComponentsInChildren<ShopManager>(true))
        {
            AssertComplete(shop, "bulletPool", assetPath, expected);
            shopOwners++;
        }

        foreach (BulletDictionaryController dictionary in
                 root.GetComponentsInChildren<BulletDictionaryController>(
                     true))
        {
            AssertComplete(dictionary, "bullets", assetPath, expected);
            dictionaryOwners++;
        }
    }

    private static void AssertComplete(
        Object owner,
        string propertyName,
        string assetPath,
        IReadOnlyCollection<string> expected)
    {
        SerializedProperty property = new SerializedObject(owner)
            .FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, assetPath);

        HashSet<string> registered = new();
        for (int index = 0; index < property.arraySize; index++)
        {
            Object value = property.GetArrayElementAtIndex(index)
                .objectReferenceValue;
            Assert.That(value, Is.Not.Null,
                $"{assetPath}: {propertyName}[{index}] is null.");
            Assert.That(value, Is.InstanceOf<BulletData>(),
                $"{assetPath}: {propertyName}[{index}] is not BulletData.");
            string registeredPath = AssetDatabase.GetAssetPath(value);
            Assert.That(registeredPath, Is.Not.Empty,
                $"{assetPath}: {propertyName}[{index}] is not an asset.");
            Assert.That(registered.Add(registeredPath), Is.True,
                $"{assetPath}: {value.name} is duplicated in {propertyName}.");
        }

        string[] missing = expected.Where(path =>
            !registered.Contains(path)).ToArray();
        string missingNames = string.Join(", ", missing);
        Assert.That(missing, Is.Empty,
            $"{assetPath}: missing {missingNames}.");
    }
}
#endif
