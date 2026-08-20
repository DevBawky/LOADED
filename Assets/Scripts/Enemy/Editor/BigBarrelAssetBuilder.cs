using UnityEditor;
using UnityEngine;

public static class BigBarrelAssetBuilder
{
    private const string EnemyFolder = "Assets/Scripts/Enemy/Enemy SO";
    private const string ActionFolder =
        "Assets/Scripts/Enemy/Enemy Action SO";
    private const string PrefabFolder = "Assets/Prefabs/Enemy";
    private const string MaterialFolder = "Assets/Materials/Enemy";
    private const string BattleFolder = "Assets/Scripts/Manager/Battle SO";
    private const string ExplosionVfxPath =
        "Assets/Sprites/VFX/VFX_Explode.prefab";

    [MenuItem("Tools/Loaded/Create Stage 1 Big Barrel Assets")]
    public static void CreateRequiredAssets()
    {
        EnsureFolder(MaterialFolder);

        Material bombMaterial = CreateMaterialIfMissing(
            $"{MaterialFolder}/BigBarrelBombTelegraph.mat",
            new Color(1f, 0.36f, 0.02f, 0.72f));
        Material shotgunMaterial = CreateMaterialIfMissing(
            $"{MaterialFolder}/BigBarrelShotgunTelegraph.mat",
            new Color(1f, 0.04f, 0.02f, 0.78f));
        GameObject bombPrefab = CreateBombPrefabIfMissing();
        GameObject explosionVfx =
            AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionVfxPath);
        EnemyActionData explosiveThrow = CreateActionIfMissing(
            "ExplosiveThrow",
            EnemyActionType.ExplosiveThrow,
            "폭탄 투척",
            "고정된 무작위 타일에 시한폭탄을 투척합니다.");
        EnemyActionData shotgunAttack = CreateActionIfMissing(
            "ShotgunAttack",
            EnemyActionType.ShotgunAttack,
            "산탄 사격",
            "보스의 현재 위치 양옆 타일을 동시에 공격합니다.");
        EnemyActionData bossReload = CreateActionIfMissing(
            "BossReload",
            EnemyActionType.Reload,
            "재장전",
            "폭탄 투척 패턴을 다시 시작하기 전에 재장전합니다.");
        EnemyData bossData = CreateBossDataIfMissing(
            bombPrefab,
            explosionVfx,
            bombMaterial,
            shotgunMaterial,
            explosiveThrow,
            shotgunAttack,
            bossReload);
        CreateBattleDataIfMissing(bossData);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = bossData;
        Debug.Log("Stage 1 Big Barrel assets are ready.", bossData);
    }

    private static EnemyActionData CreateActionIfMissing(
        string fileName,
        EnemyActionType type,
        string displayName,
        string description)
    {
        string path = $"{ActionFolder}/{fileName}.asset";
        EnemyActionData action = AssetDatabase.LoadAssetAtPath<
            EnemyActionData>(path);

        if (action != null)
        {
            return action;
        }

        action = ScriptableObject.CreateInstance<EnemyActionData>();
        AssetDatabase.CreateAsset(action, path);
        SerializedObject serialized = new SerializedObject(action);
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("actionType").enumValueIndex = (int)type;
        serialized.FindProperty("description").stringValue = description;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(action);
        return action;
    }

    private static Material CreateMaterialIfMissing(
        string path,
        Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Sprites/Default");
        material = new Material(shader)
        {
            color = color,
            name = System.IO.Path.GetFileNameWithoutExtension(path)
        };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject CreateBombPrefabIfMissing()
    {
        string path = $"{PrefabFolder}/BossBomb.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab != null)
        {
            return prefab;
        }

        GameObject root = new GameObject("BossBomb");
        root.AddComponent<BossBomb>();
        prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static EnemyData CreateBossDataIfMissing(
        GameObject bombPrefab,
        GameObject explosionVfx,
        Material bombMaterial,
        Material shotgunMaterial,
        params EnemyActionData[] actions)
    {
        string path = $"{EnemyFolder}/BigBarrel.asset";
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);

        if (data != null)
        {
            return data;
        }

        data = ScriptableObject.CreateInstance<EnemyData>();
        AssetDatabase.CreateAsset(data, path);
        SerializedObject serialized = new SerializedObject(data);
        serialized.FindProperty("enemyId").stringValue = "stage1_big_barrel";
        serialized.FindProperty("displayName").stringValue =
            "폭약왕 빅 베럴";
        serialized.FindProperty("description").stringValue =
            "시한폭탄과 양옆 산탄 사격을 고정 순서로 사용하는 스테이지 1 보스.";
        serialized.FindProperty("maxHealth").intValue = 300;
        serialized.FindProperty("behaviorType").enumValueIndex =
            (int)EnemyBehaviorType.BigBarrel;
        serialized.FindProperty("preferredDistance").intValue = 2;
        serialized.FindProperty("maxQueuedAttacks").intValue = 3;
        serialized.FindProperty("recoveryTurns").intValue = 2;
        serialized.FindProperty("thrownProjectileDuration").floatValue = 0.45f;
        serialized.FindProperty("explosionVfxPrefab").objectReferenceValue =
            explosionVfx;
        serialized.FindProperty("explosionVfxScale").floatValue = 1f;
        SerializedProperty actionList = serialized.FindProperty("actions");
        actionList.arraySize = actions.Length;

        for (int index = 0; index < actions.Length; index++)
        {
            actionList.GetArrayElementAtIndex(index).objectReferenceValue =
                actions[index];
        }

        SerializedProperty settings = serialized.FindProperty("bigBarrel");
        settings.FindPropertyRelative("phaseTwoHealthRatio").floatValue = 0.5f;
        settings.FindPropertyRelative("bombDamage").intValue = 20;
        settings.FindPropertyRelative("bossSelfExplosionDamage").intValue = 10;
        settings.FindPropertyRelative("bombExplosionRadius").intValue = 1;
        settings.FindPropertyRelative("bombFuseTurns").intValue = 3;
        settings.FindPropertyRelative("phaseTwoBombFuseTurns").intValue = 2;
        settings.FindPropertyRelative("shotgunDamage").intValue = 15;
        settings.FindPropertyRelative("bossBombPrefab").objectReferenceValue =
            bombPrefab;
        settings.FindPropertyRelative("bombArcHeight").floatValue = 2f;
        settings.FindPropertyRelative("bombTelegraphMaterial")
            .objectReferenceValue = bombMaterial;
        settings.FindPropertyRelative("shotgunTelegraphMaterial")
            .objectReferenceValue = shotgunMaterial;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        return data;
    }

    private static BattleData CreateBattleDataIfMissing(EnemyData bossData)
    {
        string path = $"{BattleFolder}/Stage 1 Boss.asset";
        BattleData battle = AssetDatabase.LoadAssetAtPath<BattleData>(path);

        if (battle != null)
        {
            return battle;
        }

        battle = ScriptableObject.CreateInstance<BattleData>();
        AssetDatabase.CreateAsset(battle, path);
        SerializedObject serialized = new SerializedObject(battle);
        serialized.FindProperty("battleId").stringValue = "stage1_boss";
        serialized.FindProperty("displayName").stringValue =
            "STAGE 1 BOSS";
        serialized.FindProperty("noticeDescription").stringValue =
            "폭약왕 빅 베럴";
        serialized.FindProperty("clearNoticeDescription").stringValue =
            "폭약왕 빅 베럴을 처치했습니다.";
        serialized.FindProperty("battleType").enumValueIndex =
            (int)BattleType.Boss;
        serialized.FindProperty("boardCount").intValue = 7;
        BoardTile tilePrefab = AssetDatabase.LoadAssetAtPath<BoardTile>(
            "Assets/Prefabs/Tiles/Tile.prefab");
        serialized.FindProperty("tilePrefab").objectReferenceValue = tilePrefab;
        serialized.FindProperty("spawnTerm").intValue = 0;
        SerializedProperty waves = serialized.FindProperty("waves");
        waves.arraySize = 1;
        SerializedProperty enemies = waves.GetArrayElementAtIndex(0)
            .FindPropertyRelative("enemies");
        enemies.arraySize = 1;
        SerializedProperty entry = enemies.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("enemyData").objectReferenceValue = bossData;
        entry.FindPropertyRelative("count").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(battle);
        return battle;
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int index = 1; index < parts.Length; index++)
        {
            string next = $"{current}/{parts[index]}";

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }
}
