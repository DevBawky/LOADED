#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DuelClockHudAuthoring
{
    private const string CanvasPrefabPath =
        "Assets/Prefabs/UI/Canvas.prefab";
    private const string BattleScenePath =
        "Assets/Scenes/Battle.unity";

    [MenuItem("Tools/LOADED/Validate Duel Clock HUD")]
    public static void ValidateFromMenu()
    {
        ValidateAssets();
    }

    public static void ApplyFromCommandLine()
    {
        ValidateAssets();
    }

    private static void ValidateAssets()
    {
        GameObject canvas = AssetDatabase.LoadAssetAtPath<GameObject>(
            CanvasPrefabPath);

        if (canvas == null)
        {
            throw new InvalidOperationException(
                $"Canvas prefab is missing at '{CanvasPrefabPath}'.");
        }

        DuelClockHUD[] huds = canvas.GetComponentsInChildren<DuelClockHUD>(
            true);

        if (huds.Length != 1)
        {
            throw new InvalidOperationException(
                "Canvas prefab must contain exactly one DuelClockHUD.");
        }

        ValidateHudReferences(huds[0]);

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BattleScenePath) == null)
        {
            throw new InvalidOperationException(
                $"Battle scene is missing at '{BattleScenePath}'.");
        }

        Debug.Log(
            "Duel Clock HUD validation complete. "
            + "No prefab or scene content was generated or modified.",
            huds[0]);
    }

    private static void ValidateHudReferences(DuelClockHUD hud)
    {
        SerializedObject serializedHud = new SerializedObject(hud);
        RequireReference<CanvasGroup>(serializedHud, "canvasGroup");
        RequireReference<Image>(serializedHud, "progressFill");
        RequireReference<Image>(serializedHud, "spawnProgressFill");
        RequireReference<TMP_Text>(serializedHud, "titleText");
        RequireReference<TMP_Text>(serializedHud, "progressText");
        RequireReference<TMP_Text>(serializedHud, "spawnGaugeLabelText");
        RequireReference<TMP_Text>(serializedHud, "unspawnedEnemyCountText");
    }

    private static void RequireReference<T>(
        SerializedObject serializedObject,
        string propertyName)
        where T : UnityEngine.Object
    {
        SerializedProperty property = serializedObject.FindProperty(
            propertyName);

        if (property?.objectReferenceValue is T)
        {
            return;
        }

        throw new InvalidOperationException(
            $"DuelClockHUD requires a valid '{propertyName}' reference.");
    }
}
#endif
