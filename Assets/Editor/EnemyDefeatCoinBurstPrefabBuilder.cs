using UnityEditor;
using UnityEngine;

public static class EnemyDefeatCoinBurstPrefabBuilder
{
    private const string PrefabDirectory = "Assets/Prefabs/VFX";
    private const string MaterialDirectory = "Assets/Materials/VFX";
    private const string PrefabPath =
        PrefabDirectory + "/VFX_EnemyDefeatCoinBurst.prefab";
    private const string MaterialPath =
        MaterialDirectory + "/EnemyDefeatCoin.mat";
    private const string PlayerPrefabPath =
        "Assets/Prefabs/Player/Player.prefab";
    private const string CoinPrefabPath =
        "Assets/Package/LiquidFire Package 4 - BSH games/"
        + "Devtoid - Gold Coins/3D Assets/Gold Coin - Single/"
        + "Prefab/GoldCoin.prefab";

    [MenuItem("Tools/LOADED/Rebuild Enemy Defeat Coin Burst")]
    public static void Build()
    {
        EnsureFolder("Assets/Prefabs", "VFX");
        EnsureFolder("Assets/Materials", "VFX");
        GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CoinPrefabPath);

        if (coinPrefab == null)
        {
            throw new System.InvalidOperationException(
                $"Missing GoldCoin prefab at {CoinPrefabPath}.");
        }

        Material coinMaterial = CreateOrUpdateCoinMaterial(coinPrefab);

        GameObject root = new GameObject("VFX_EnemyDefeatCoinBurst");

        try
        {
            EnemyDefeatCoinBurstEffect effect =
                root.AddComponent<EnemyDefeatCoinBurstEffect>();
            effect.ConfigureForEditor(coinPrefab, coinMaterial);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        WirePlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Rebuilt enemy defeat coin burst prefab at {PrefabPath}.");
    }

    public static void BuildFromCommandLine()
    {
        Build();
    }

    private static Material CreateOrUpdateCoinMaterial(GameObject coinPrefab)
    {
        Renderer sourceRenderer =
            coinPrefab.GetComponentInChildren<Renderer>(true);
        Material sourceMaterial = sourceRenderer == null
            ? null
            : sourceRenderer.sharedMaterial;

        if (sourceMaterial == null)
        {
            throw new System.InvalidOperationException(
                "GoldCoin prefab does not contain a material-backed Renderer.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            MaterialPath);

        if (material == null)
        {
            material = new Material(sourceMaterial);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            EditorUtility.CopySerialized(sourceMaterial, material);
        }

        material.name = "EnemyDefeatCoin";
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void WirePlayerPrefab()
    {
        GameObject playerRoot = PrefabUtility.LoadPrefabContents(
            PlayerPrefabPath);

        try
        {
            CombatFeedbackController feedback =
                playerRoot.GetComponent<CombatFeedbackController>();
            EnemyDefeatCoinBurstEffect coinBurst =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                    ?.GetComponent<EnemyDefeatCoinBurstEffect>();

            if (feedback == null || coinBurst == null)
            {
                throw new System.InvalidOperationException(
                    "Player combat feedback or coin burst prefab is missing.");
            }

            SerializedObject serializedFeedback =
                new SerializedObject(feedback);
            serializedFeedback.FindProperty("defeatCoinBurstPrefab")
                .objectReferenceValue = coinBurst;
            serializedFeedback.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(playerRoot);
        }
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
