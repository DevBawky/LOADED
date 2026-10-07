#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class SpecialBattleSetupBuilder
{
    private const string SettingsPath =
        "Assets/Resources/NodeMapSettings.asset";
    private const string StartIconPath =
        "Assets/Sprites/UI/NodeMap/NodeIcon_Start.png";
    private const string StoreIconPath =
        "Assets/Sprites/UI/NodeMap/NodeIcon_Store.png";
    private const string RewardIconPath =
        "Assets/Sprites/UI/NodeMap/NodeIcon_Reward.png";
    private const string EventIconPath =
        "Assets/Sprites/UI/NodeMap/NodeIcon_Event.png";
    private const string SpecialIconPath =
        "Assets/Sprites/UI/NodeMap/NodeIcon_Special.png";
    private const string SourceEnemyPath =
        "Assets/Scripts/Enemy/Enemy SO/BigBarrel.asset";
    private const string ProfileFolder = "Assets/Resources/SpecialBattle";
    private const string ProfilePath = ProfileFolder
        + "/SpecialBattleBombProfile.asset";

    [MenuItem("Loaded/Setup Special Battles")]
    public static void Apply()
    {
        EnsureFolder("Assets/Resources", "SpecialBattle");
        SpecialBattleBombProfile profile = EnsureBombProfile();
        ConfigureNodeMapSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"Special battle authoring is ready with bomb profile '{profile.name}'.");
    }

    private static SpecialBattleBombProfile EnsureBombProfile()
    {
        EnemyData source = AssetDatabase.LoadAssetAtPath<EnemyData>(
            SourceEnemyPath);
        if (source == null || source.BigBarrel.BossBombPrefab == null)
        {
            throw new InvalidOperationException(
                $"Special battle bomb source is invalid: {SourceEnemyPath}");
        }

        SpecialBattleBombProfile profile =
            AssetDatabase.LoadAssetAtPath<SpecialBattleBombProfile>(
                ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<
                SpecialBattleBombProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("profileId").stringValue =
            "special.death-bomb";
        serialized.FindProperty("bombPrefab").objectReferenceValue =
            source.BigBarrel.BossBombPrefab;
        serialized.FindProperty("fuseTurns").intValue = 2;
        serialized.FindProperty("explosionRadius").intValue =
            source.BigBarrel.BombExplosionRadius;
        serialized.FindProperty("playerDamage").intValue =
            source.BigBarrel.BombDamage;
        serialized.FindProperty("enemyDamage").intValue =
            source.BigBarrel.BombDamage;
        serialized.FindProperty("bossDamage").intValue =
            source.BigBarrel.BossSelfExplosionDamage;
        serialized.FindProperty("dodgeWindowDuration").floatValue = 0.2f;
        serialized.FindProperty("explosionVfxPrefab").objectReferenceValue =
            source.ExplosionVfxPrefab;
        serialized.FindProperty("explosionVfxScale").floatValue =
            source.ExplosionVfxScale;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void ConfigureNodeMapSettings()
    {
        NodeMapSettings settings =
            AssetDatabase.LoadAssetAtPath<NodeMapSettings>(SettingsPath);
        Sprite startIcon = LoadIcon(StartIconPath);
        Sprite storeIcon = LoadIcon(StoreIconPath);
        Sprite rewardIcon = LoadIcon(RewardIconPath);
        Sprite eventIcon = LoadIcon(EventIconPath);
        Sprite specialIcon = LoadIcon(SpecialIconPath);
        if (settings == null)
        {
            throw new InvalidOperationException(
                "NodeMapSettings is missing.");
        }

        SerializedObject serialized = new SerializedObject(settings);
        serialized.FindProperty("startIcon").objectReferenceValue = startIcon;
        serialized.FindProperty("shopIcon").objectReferenceValue = storeIcon;
        serialized.FindProperty("treasureIcon").objectReferenceValue =
            rewardIcon;
        serialized.FindProperty("eventIcon").objectReferenceValue = eventIcon;
        serialized.FindProperty("specialIcon").objectReferenceValue =
            specialIcon;
        EnsureGenerationRule(serialized.FindProperty("generationRules"));
        EnsureNodeDescription(serialized.FindProperty("nodeDescriptions"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    private static Sprite LoadIcon(string path)
    {
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (icon == null)
        {
            throw new InvalidOperationException(
                $"Node-map icon is missing or invalid: {path}");
        }

        return icon;
    }

    private static void EnsureGenerationRule(SerializedProperty rules)
    {
        SerializedProperty specialRule = null;
        for (int index = 0; index < rules.arraySize; index++)
        {
            SerializedProperty candidate = rules.GetArrayElementAtIndex(index);
            if (candidate.FindPropertyRelative("nodeType").enumValueIndex
                == (int)NodeMapNodeType.SpecialBattle)
            {
                specialRule = candidate;
                break;
            }
        }

        if (specialRule == null)
        {
            rules.InsertArrayElementAtIndex(rules.arraySize);
            specialRule = rules.GetArrayElementAtIndex(rules.arraySize - 1);
        }

        specialRule.FindPropertyRelative("nodeType").enumValueIndex =
            (int)NodeMapNodeType.SpecialBattle;
        specialRule.FindPropertyRelative("weight").intValue = 8;
        specialRule.FindPropertyRelative("minimumCount").intValue = 2;
        specialRule.FindPropertyRelative("maximumCount").intValue = 3;
    }

    private static void EnsureNodeDescription(SerializedProperty descriptions)
    {
        SerializedProperty specialDescription = null;
        for (int index = 0; index < descriptions.arraySize; index++)
        {
            SerializedProperty candidate =
                descriptions.GetArrayElementAtIndex(index);
            if (candidate.FindPropertyRelative("nodeType").enumValueIndex
                == (int)NodeMapNodeType.SpecialBattle)
            {
                specialDescription = candidate;
                break;
            }
        }

        if (specialDescription == null)
        {
            descriptions.InsertArrayElementAtIndex(descriptions.arraySize);
            specialDescription = descriptions.GetArrayElementAtIndex(
                descriptions.arraySize - 1);
        }

        specialDescription.FindPropertyRelative("nodeType").enumValueIndex =
            (int)NodeMapNodeType.SpecialBattle;
        specialDescription.FindPropertyRelative("displayName").stringValue =
            "스페셜 전투";
        specialDescription.FindPropertyRelative("description").stringValue =
            "특수한 전투 규칙이 적용됩니다.";
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
