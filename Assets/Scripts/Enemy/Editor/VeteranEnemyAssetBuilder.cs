using UnityEditor;
using UnityEngine;

public static class VeteranEnemyAssetBuilder
{
    private const string EnemyFolder = "Assets/Scripts/Enemy/Enemy SO";
    private const string ActionFolder =
        "Assets/Scripts/Enemy/Enemy Action SO";
    private const string AttackFolder =
        "Assets/Scripts/Enemy/Enemy Attack Action SO";
    private const string BattleFolder =
        "Assets/Scripts/Manager/Battle SO/Stage 1";
    private static readonly Color VeteranMeleeTint =
        new Color(1f, 0.58f, 0.35f, 1f);
    private static readonly Color VeteranGunnerTint =
        new Color(1f, 0.72f, 0.36f, 1f);
    private static readonly Color VeteranThrowerTint =
        new Color(1f, 0.48f, 0.28f, 1f);

    [MenuItem("Tools/LOADED/Create Stage 1 Veteran Enemies")]
    public static void CreateRequiredAssets()
    {
        EnemyData baseMelee = LoadRequired<EnemyData>(
            $"{EnemyFolder}/Test Enemy.asset");
        EnemyData baseGunner = LoadRequired<EnemyData>(
            $"{EnemyFolder}/Test Gunner.asset");
        EnemyData baseThrower = LoadRequired<EnemyData>(
            $"{EnemyFolder}/Test Thrower.asset");

        EnemyActionData veteranMeleeAction = CreateVeteranAction(
            "Veteran Melee Attack",
            $"{ActionFolder}/Melee Attack.asset",
            $"{AttackFolder}/Test Melee Attack.asset",
            "stage1_veteran_melee_attack",
            "강한 베기",
            "베테랑 칼잡이가 강하게 베어 피해 15를 줍니다.",
            15);
        EnemyActionData veteranGunnerAction = CreateVeteranAction(
            "Veteran Ranged Attack",
            $"{ActionFolder}/RangedAttack.asset",
            $"{AttackFolder}/Test Range Attack.asset",
            "stage1_veteran_gunner_attack",
            "정밀 사격",
            "베테랑 총잡이가 정밀하게 조준해 피해 22를 줍니다.",
            22);
        EnemyActionData veteranThrowerAction = CreateVeteranAction(
            "Veteran Thrower",
            $"{ActionFolder}/Thrower.asset",
            $"{AttackFolder}/Test Throw Attack.asset",
            "stage1_veteran_thrower_attack",
            "강화 화염병",
            "베테랑 투척병이 강화 화염병을 던져 피해 22를 줍니다.",
            22);

        EnemyData veteranMelee = CreateVeteranEnemy(
            "Veteran Melee",
            baseMelee,
            "stage1_veteran_melee",
            "베테랑 칼잡이",
            "거친 싸움에서 살아남아 더 단단하고 강한 공격을 사용하는 칼잡이.",
            100,
            4,
            7,
            VeteranMeleeTint,
            veteranMeleeAction);
        EnemyData veteranGunner = CreateVeteranEnemy(
            "Veteran Gunner",
            baseGunner,
            "stage1_veteran_gunner",
            "베테랑 총잡이",
            "노련한 조준으로 더 위력적인 사격을 가하는 총잡이.",
            40,
            2,
            5,
            VeteranGunnerTint,
            veteranGunnerAction);
        EnemyData veteranThrower = CreateVeteranEnemy(
            "Veteran Thrower",
            baseThrower,
            "stage1_veteran_thrower",
            "베테랑 투척병",
            "강화된 화염병으로 더 큰 피해를 노리는 투척병.",
            75,
            4,
            8,
            VeteranThrowerTint,
            veteranThrowerAction);

        ApplyBattlePlacements(
            baseMelee,
            baseGunner,
            baseThrower,
            veteranMelee,
            veteranGunner,
            veteranThrower);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = veteranMelee;
        Debug.Log("Stage 1 veteran enemies and battle placements are ready.");
    }

    public static void BuildFromCommandLine()
    {
        CreateRequiredAssets();
    }

    private static EnemyActionData CreateVeteranAction(
        string fileName,
        string baseActionPath,
        string baseAttackPath,
        string skillId,
        string displayName,
        string description,
        int damage)
    {
        EnemyActionData baseAction =
            LoadRequired<EnemyActionData>(baseActionPath);
        EnemyAttackData baseAttack =
            LoadRequired<EnemyAttackData>(baseAttackPath);
        string attackPath = $"{AttackFolder}/{fileName}.asset";
        EnemyAttackData attack = LoadOrCreate<EnemyAttackData>(attackPath);
        EditorUtility.CopySerialized(baseAttack, attack);
        attack.name = fileName;
        SerializedObject serializedAttack = new SerializedObject(attack);
        serializedAttack.FindProperty("skillId").stringValue = skillId;
        serializedAttack.FindProperty("displayName").stringValue = displayName;
        serializedAttack.FindProperty("description").stringValue = description;
        serializedAttack.FindProperty("damage").intValue = damage;
        serializedAttack.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attack);

        string actionPath = $"{ActionFolder}/{fileName}.asset";
        EnemyActionData action = LoadOrCreate<EnemyActionData>(actionPath);
        EditorUtility.CopySerialized(baseAction, action);
        action.name = fileName;
        SerializedObject serializedAction = new SerializedObject(action);
        serializedAction.FindProperty("displayName").stringValue = displayName;
        serializedAction.FindProperty("description").stringValue = description;
        serializedAction.FindProperty("attackData").objectReferenceValue =
            attack;
        serializedAction.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(action);
        return action;
    }

    private static EnemyData CreateVeteranEnemy(
        string fileName,
        EnemyData baseData,
        string enemyId,
        string displayName,
        string description,
        int maxHealth,
        int minimumGold,
        int maximumGold,
        Color avatarTint,
        EnemyActionData attackAction)
    {
        string path = $"{EnemyFolder}/{fileName}.asset";
        EnemyData data = LoadOrCreate<EnemyData>(path);
        EditorUtility.CopySerialized(baseData, data);
        data.name = fileName;
        SerializedObject serialized = new SerializedObject(data);
        serialized.FindProperty("enemyId").stringValue = enemyId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("maxHealth").intValue = maxHealth;
        serialized.FindProperty("minimumGoldDrop").intValue = minimumGold;
        serialized.FindProperty("maximumGoldDrop").intValue = maximumGold;
        serialized.FindProperty("avatarMaterialOverride")
            .objectReferenceValue = null;
        serialized.FindProperty("avatarTint").colorValue = avatarTint;

        SerializedProperty actions = serialized.FindProperty("actions");
        bool replacedAttack = false;

        for (int index = 0; index < actions.arraySize; index++)
        {
            SerializedProperty element = actions.GetArrayElementAtIndex(index);
            EnemyActionData current =
                element.objectReferenceValue as EnemyActionData;

            if (current != null
                && current.ActionType == attackAction.ActionType)
            {
                element.objectReferenceValue = attackAction;
                replacedAttack = true;
                break;
            }
        }

        if (!replacedAttack)
        {
            int index = actions.arraySize;
            actions.InsertArrayElementAtIndex(index);
            actions.GetArrayElementAtIndex(index).objectReferenceValue =
                attackAction;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        return data;
    }

    private static void ApplyBattlePlacements(
        EnemyData baseMelee,
        EnemyData baseGunner,
        EnemyData baseThrower,
        EnemyData veteranMelee,
        EnemyData veteranGunner,
        EnemyData veteranThrower)
    {
        EnsureReplacement(
            $"{BattleFolder}/2 Middle/Stage 1 Middle 1.asset",
            3,
            baseMelee,
            veteranMelee);
        EnsureReplacement(
            $"{BattleFolder}/2 Middle/Stage 1 Middle 2.asset",
            1,
            baseGunner,
            veteranGunner);
        EnsureReplacement(
            $"{BattleFolder}/2 Middle/Stage 1 Middle 2.asset",
            3,
            baseThrower,
            veteranThrower);

        string[] finalePaths =
        {
            $"{BattleFolder}/3 Finale/Stage 1 Finale 1.asset",
            $"{BattleFolder}/3 Finale/Stage 1 Finale 2.asset",
            $"{BattleFolder}/3 Finale/Stage 1 Finale 3.asset"
        };

        foreach (string path in finalePaths)
        {
            BattleData battle = LoadRequired<BattleData>(path);
            int finalWaveIndex = battle.Waves.Count - 1;
            EnsureReplacement(path, finalWaveIndex, baseMelee, veteranMelee);
            EnsureReplacement(path, finalWaveIndex, baseGunner, veteranGunner);
            EnsureReplacement(
                path,
                finalWaveIndex,
                baseThrower,
                veteranThrower);
        }

        string elitePath = $"{BattleFolder}/Stage 1 Elite.asset";
        BattleData elite = LoadRequired<BattleData>(elitePath);

        for (int waveIndex = 0; waveIndex < elite.Waves.Count; waveIndex++)
        {
            EnsureReplacement(
                elitePath,
                waveIndex,
                baseMelee,
                veteranMelee);
            EnsureReplacement(
                elitePath,
                waveIndex,
                baseGunner,
                veteranGunner);
            EnsureReplacement(
                elitePath,
                waveIndex,
                baseThrower,
                veteranThrower);
        }
    }

    private static void EnsureReplacement(
        string battlePath,
        int waveIndex,
        EnemyData baseEnemy,
        EnemyData veteranEnemy)
    {
        BattleData battle = LoadRequired<BattleData>(battlePath);
        SerializedObject serialized = new SerializedObject(battle);
        SerializedProperty waves = serialized.FindProperty("waves");

        if (waveIndex < 0 || waveIndex >= waves.arraySize)
        {
            throw new System.InvalidOperationException(
                $"Battle '{battlePath}' has no wave at index {waveIndex}.");
        }

        SerializedProperty enemies = waves.GetArrayElementAtIndex(waveIndex)
            .FindPropertyRelative("enemies");

        for (int index = 0; index < enemies.arraySize; index++)
        {
            SerializedProperty entry = enemies.GetArrayElementAtIndex(index);

            if (entry.FindPropertyRelative("enemyData").objectReferenceValue
                == veteranEnemy)
            {
                return;
            }
        }

        for (int index = 0; index < enemies.arraySize; index++)
        {
            SerializedProperty entry = enemies.GetArrayElementAtIndex(index);
            SerializedProperty enemyData =
                entry.FindPropertyRelative("enemyData");

            if (enemyData.objectReferenceValue != baseEnemy)
            {
                continue;
            }

            SerializedProperty count = entry.FindPropertyRelative("count");

            if (count.intValue <= 1)
            {
                enemyData.objectReferenceValue = veteranEnemy;
            }
            else
            {
                count.intValue--;
                int veteranIndex = enemies.arraySize;
                enemies.InsertArrayElementAtIndex(veteranIndex);
                SerializedProperty veteranEntry =
                    enemies.GetArrayElementAtIndex(veteranIndex);
                veteranEntry.FindPropertyRelative("enemyData")
                    .objectReferenceValue = veteranEnemy;
                veteranEntry.FindPropertyRelative("count").intValue = 1;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(battle);
            return;
        }
    }

    private static T LoadRequired<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset == null)
        {
            throw new System.InvalidOperationException(
                $"Required asset is missing: {path}");
        }

        return asset;
    }

    private static T LoadOrCreate<T>(string path)
        where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
