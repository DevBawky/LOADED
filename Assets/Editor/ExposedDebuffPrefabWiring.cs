using System;
using UnityEditor;
using UnityEngine;

public static class ExposedDebuffPrefabWiring
{
    private const string SessionKey =
        "LOADED.ExposedDebuffPrefabWiring.Completed";
    private const string EnemyPrefabPath =
        "Assets/Prefabs/Enemy/Enemy.prefab";
    private const string ExposedSpritePath =
        "Assets/Sprites/UI/Debuff/Debuff_Exposed.png";

    [InitializeOnLoadMethod]
    private static void ScheduleWireIfNeeded()
    {
        if (!Application.isBatchMode
            && !SessionState.GetBool(SessionKey, false))
        {
            EditorApplication.delayCall += WireIfNeeded;
        }
    }

    private static void WireIfNeeded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += WireIfNeeded;
            return;
        }

        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            EnemyPrefabPath);
        Sprite exposedSprite = LoadExposedSprite();
        StatusEffectController statusEffects = enemyPrefab == null
            ? null
            : enemyPrefab.GetComponent<StatusEffectController>();

        if (statusEffects == null || exposedSprite == null)
        {
            return;
        }

        SerializedObject serializedStatus =
            new SerializedObject(statusEffects);
        SerializedProperty exposedSpriteProperty =
            serializedStatus.FindProperty("exposedSprite");

        if (exposedSpriteProperty != null
            && exposedSpriteProperty.objectReferenceValue != exposedSprite)
        {
            Wire();
        }

        SessionState.SetBool(SessionKey, true);
    }

    [MenuItem("Tools/LOADED/Wire Exposed Debuff Icon")]
    public static void Wire()
    {
        Sprite exposedSprite = LoadExposedSprite();

        if (exposedSprite == null)
        {
            throw new MissingReferenceException(
                $"Exposed debuff sprite was not found at {ExposedSpritePath}.");
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(
            EnemyPrefabPath);

        try
        {
            StatusEffectController statusEffects =
                prefabRoot.GetComponent<StatusEffectController>();

            if (statusEffects == null)
            {
                throw new MissingComponentException(
                    $"StatusEffectController is missing from {EnemyPrefabPath}.");
            }

            SerializedObject serializedStatus =
                new SerializedObject(statusEffects);
            SerializedProperty exposedSpriteProperty =
                serializedStatus.FindProperty("exposedSprite");

            if (exposedSpriteProperty == null)
            {
                throw new MissingMemberException(
                    "StatusEffectController.exposedSprite was not found.");
            }

            exposedSpriteProperty.objectReferenceValue = exposedSprite;
            serializedStatus.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, EnemyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Wired Debuff_Exposed to the enemy status UI prefab.");
    }

    private static Sprite LoadExposedSprite()
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(
                     ExposedSpritePath))
        {
            if (asset is Sprite sprite)
            {
                return sprite;
            }
        }

        return null;
    }
}
