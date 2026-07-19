using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BulletData))]
public class BulletDataEditor : Editor
{
    private readonly bool[] levelFoldouts = { true, true, false, false };

    private void OnEnable()
    {
        foreach (Object inspectedTarget in targets)
        {
            if (inspectedTarget is BulletData bullet
                && bullet.EnsureUpgradeLevels())
            {
                EditorUtility.SetDirty(bullet);
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptReference();
        DrawBasicInformation();
        DrawDisplayColors();
        DrawBaseLevel();
        DrawUpgradeLevels();

        serializedObject.ApplyModifiedProperties();
        DrawValidationMessages();
    }

    private void DrawScriptReference()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("m_Script"));
        }
    }

    private void DrawBasicInformation()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Basic Information", EditorStyles.boldLabel);
        Draw("bulletId");
        Draw("displayName");
        Draw("bulletIcon");
        Draw("cylinderIcon");
        Draw("price");
        Draw("grade");
    }

    private void DrawDisplayColors()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Name Colors", EditorStyles.boldLabel);
        Draw("useCustomGradeNameColor");

        if (serializedObject.FindProperty("useCustomGradeNameColor").boolValue)
        {
            Draw("customGradeNameColor");
        }

        Draw("levelOneColor", "Level +1 Rich Text Color");
        Draw("levelTwoColor", "Level +2 Rich Text Color");
        Draw("levelThreeColor", "Level +3 Rich Text Color");
    }

    private void DrawBaseLevel()
    {
        EditorGUILayout.Space();
        levelFoldouts[0] = EditorGUILayout.Foldout(
            levelFoldouts[0],
            "Level 0 (Base)",
            true,
            EditorStyles.foldoutHeader);

        if (!levelFoldouts[0])
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            Draw("description");
            Draw("damage");
            Draw("maxRange");
            Draw("criticalDamageMultiplier");
            Draw("effects", includeChildren: true);
            Draw("penetrationChances", includeChildren: true);
            Draw("lineMaterial");
            Draw("primaryLineColor");
            Draw("secondaryLineColor");
            Draw("doesNotConsumeTurn");
            Draw("recoilStrength");
            EditorGUILayout.Space(2f);
            Draw("removeCost", "Remove Cost at Level 0");
            Draw("upgradeCost", "Upgrade Cost: 0 -> +1");
        }
    }

    private void DrawUpgradeLevels()
    {
        SerializedProperty levels = serializedObject.FindProperty(
            "upgradeLevels");

        if (levels.arraySize != BulletData.MaximumUpgradeLevel)
        {
            levels.arraySize = BulletData.MaximumUpgradeLevel;
        }

        for (int index = 0; index < BulletData.MaximumUpgradeLevel; index++)
        {
            int level = index + 1;
            EditorGUILayout.Space();
            levelFoldouts[level] = EditorGUILayout.Foldout(
                levelFoldouts[level],
                $"Level +{level}",
                true,
                EditorStyles.foldoutHeader);

            if (!levelFoldouts[level])
            {
                continue;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                SerializedProperty levelData = levels.GetArrayElementAtIndex(
                    index);
                EditorGUILayout.PropertyField(levelData, GUIContent.none, true);
            }
        }
    }

    private void DrawValidationMessages()
    {
        BulletData bullet = (BulletData)target;

        if (string.IsNullOrWhiteSpace(bullet.BulletId))
        {
            EditorGUILayout.HelpBox(
                "Bullet Id is empty. Assign a stable unique id.",
                MessageType.Warning);
        }

        if (bullet.BulletIcon == null && bullet.CylinderIcon == null)
        {
            EditorGUILayout.HelpBox(
                "Both Bullet Icon and Cylinder Icon are empty. UI cannot display this bullet.",
                MessageType.Warning);
        }

        EditorGUILayout.HelpBox(
            "Upgrade Cost belongs to the current level. For example, the Level +1 Upgrade Cost is paid when upgrading +1 to +2.",
            MessageType.Info);
    }

    private void Draw(
        string propertyName,
        string label = null,
        bool includeChildren = false)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            return;
        }

        EditorGUILayout.PropertyField(
            property,
            string.IsNullOrEmpty(label) ? null : new GUIContent(label),
            includeChildren);
    }
}
