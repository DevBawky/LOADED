#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NodeMapSetupBuilder
{
    private const string SettingsPath =
        "Assets/Resources/NodeMapSettings.asset";
    private const string StagePath =
        "Assets/Scripts/Manager/Stage SO/Stage 1.asset";
    private const string ElitePath =
        "Assets/Scripts/Manager/Battle SO/Stage 1 Elite.asset";

    [MenuItem("Loaded/Setup Node Map")]
    public static void Setup()
    {
        EnsureResourceFolder();
        StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(StagePath);
        if (stage == null)
        {
            throw new System.InvalidOperationException(
                $"Stage data was not found at {StagePath}.");
        }

        NodeMapSettings settings =
            AssetDatabase.LoadAssetAtPath<NodeMapSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<NodeMapSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        BattleData elite = AssetDatabase.LoadAssetAtPath<BattleData>(ElitePath);
        List<BattleData> normalBattles = stage.Battles
            .Where(battle => battle != null && !battle.IsBoss
                && battle != elite)
            .ToList();
        BattleData boss = stage.Battles.LastOrDefault(
            battle => battle != null && battle.IsBoss);
        SerializedObject serialized = new SerializedObject(settings);
        serialized.FindProperty("stage").objectReferenceValue = stage;
        SetArray(serialized.FindProperty("normalBattles"), normalBattles);

        SetArray(
            serialized.FindProperty("eliteBattles"),
            elite == null
                ? new List<BattleData>()
                : new List<BattleData> { elite });
        serialized.FindProperty("bossBattle").objectReferenceValue = boss;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);

        string[] scenePaths =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/NodeMap.unity",
            "Assets/Scenes/Battle.unity",
            "Assets/Scenes/Shop.unity",
            "Assets/Scenes/Treasure.unity",
            "Assets/Scenes/Event.unity",
            "Assets/Scenes/Ending.unity"
        };
        EditorBuildSettings.scenes = scenePaths
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();

        AssetDatabase.SaveAssets();
        Debug.Log("Node map settings and build scenes are ready.");
    }

    private static void EnsureResourceFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }

    private static void SetArray(
        SerializedProperty property,
        IReadOnlyList<BattleData> values)
    {
        property.arraySize = values.Count;
        for (int index = 0; index < values.Count; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue =
                values[index];
        }
    }
}
#endif
