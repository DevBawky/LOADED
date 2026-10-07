#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class NewBulletExpansionBuilder
{
    private const string BulletRoot = "Assets/Scripts/Bullet/SO";
    private const string IconRoot = "Assets/Sprites/Bullet_LineArt";

    private readonly struct EffectSpec
    {
        public EffectSpec(
            BulletEffectType type,
            float amount = 0f,
            float chance = 100f,
            int stacks = 1)
        {
            Type = type;
            Amount = amount;
            Chance = chance;
            Stacks = stacks;
        }

        public BulletEffectType Type { get; }
        public float Amount { get; }
        public float Chance { get; }
        public int Stacks { get; }
    }

    private readonly struct LevelSpec
    {
        public LevelSpec(
            string description,
            int damage,
            int range,
            float criticalChance,
            float criticalMultiplier,
            int shotCount,
            params EffectSpec[] effects)
        {
            Description = description;
            Damage = damage;
            Range = range;
            CriticalChance = criticalChance;
            CriticalMultiplier = criticalMultiplier;
            ShotCount = shotCount;
            Effects = effects ?? Array.Empty<EffectSpec>();
        }

        public string Description { get; }
        public int Damage { get; }
        public int Range { get; }
        public float CriticalChance { get; }
        public float CriticalMultiplier { get; }
        public int ShotCount { get; }
        public EffectSpec[] Effects { get; }
    }

    [MenuItem("Tools/LOADED/Apply New Bullet Expansion")]
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureFolders();
        ConfigureSilentBullet();

        CreateBullet("Rare", "Focused Volley", "bullet_focused_volley", "집속탄",
            BulletGrade.Rare, BulletType.Shotgun, "focused_volley", 6,
            Levels(
                L("8개 팰릿. 적이 한 레인에만 있으면 한 적에게 집중하고, 여러 레인이면 기준 레인 4발과 나머지 균등 분배.", 3, 4, 5, 2f, 8, E(BulletEffectType.FocusedShotgun)),
                L("8개 팰릿. 기준 레인 4발, 나머지 레인 균등 분배.", 4, 4, 6, 2f, 8, E(BulletEffectType.FocusedShotgun)),
                L("8개 팰릿. 기준 레인 4발, 나머지 레인 균등 분배.", 5, 5, 7, 2f, 8, E(BulletEffectType.FocusedShotgun)),
                L("8개 팰릿. 기준 레인 4발, 나머지 레인 균등 분배.", 6, 5, 8, 2.2f, 8, E(BulletEffectType.FocusedShotgun))));

        CreateBullet("Ace", "Gambit", "bullet_gambit", "승부탄",
            BulletGrade.Ace, BulletType.Shotgun, "gambit", 10,
            Levels(
                L("12개 팰릿. 각 팰릿은 치명타 확률 8%, 치명타 배율 x5를 독립 판정.", 2, 4, 8, 5f, 12),
                L("12개 팰릿. 각 팰릿은 치명타 확률 8%, 치명타 배율 x5를 독립 판정.", 3, 4, 8, 5f, 12),
                L("12개 팰릿. 각 팰릿은 치명타 확률 8%, 치명타 배율 x5를 독립 판정.", 4, 5, 8, 5f, 12),
                L("12개 팰릿. 각 팰릿은 치명타 확률 8%, 치명타 배율 x5를 독립 판정.", 5, 5, 8, 5f, 12)));

        CreateBullet("Ace", "Dice Shot", "bullet_dice_shot", "주사위탄",
            BulletGrade.Ace, BulletType.Shotgun, "dice_shot", 10,
            Levels(
                L("6개 팰릿. 각 팰릿의 기본 피해가 1~10에서 독립 결정.", 10, 4, 10, 2f, 6, E(BulletEffectType.RandomPelletDamage, 1, 100, 10)),
                L("6개 팰릿. 각 팰릿의 기본 피해가 2~13에서 독립 결정.", 13, 4, 12, 2f, 6, E(BulletEffectType.RandomPelletDamage, 2, 100, 13)),
                L("6개 팰릿. 각 팰릿의 기본 피해가 3~17에서 독립 결정.", 17, 5, 15, 2.2f, 6, E(BulletEffectType.RandomPelletDamage, 3, 100, 17)),
                L("6개 팰릿. 각 팰릿의 기본 피해가 5~22에서 독립 결정.", 22, 5, 20, 2.5f, 6, E(BulletEffectType.RandomPelletDamage, 5, 100, 22))));

        CreateBullet("Rare", "Vanguard", "bullet_vanguard", "선봉탄",
            BulletGrade.Rare, BulletType.Normal, "vanguard", 6,
            PositionLevels(BulletEffectType.Vanguard, "첫 번째 물리 탄환이면", 30, 40, 55, 75));
        CreateBullet("Rare", "Finisher", "bullet_finisher", "종결탄",
            BulletGrade.Rare, BulletType.Normal, "finisher", 6,
            PositionLevels(BulletEffectType.Finisher, "마지막 물리 탄환이면", 40, 55, 70, 90));

        CreateBullet("Rare", "Wraith", "bullet_wraith", "망령탄",
            BulletGrade.Rare, BulletType.Ghost, "wraith", 6,
            ChanceLevels(BulletEffectType.SpecterReturn,
                "발사 후 무덤 대신 덱 맨 위로 귀환", 40, 50, 60, 75));
        CreateBullet("Ace", "Necromancy", "bullet_necromancy", "강령탄",
            BulletGrade.Ace, BulletType.Ghost, "necromancy", 10,
            Levels(
                L("함께 장전된 다른 유령 탄환 하나당 최종 피해 +20% (최대 3개).", 18, 5, 18, 2f, 1, E(BulletEffectType.Necromancy, 20, 100, 3)),
                L("함께 장전된 다른 유령 탄환 하나당 최종 피해 +25% (최대 3개).", 22, 5, 22, 2f, 1, E(BulletEffectType.Necromancy, 25, 100, 3)),
                L("함께 장전된 다른 유령 탄환 하나당 최종 피해 +30% (최대 3개).", 27, 6, 27, 2.2f, 1, E(BulletEffectType.Necromancy, 30, 100, 3)),
                L("함께 장전된 다른 유령 탄환 하나당 최종 피해 +40% (최대 3개).", 34, 6, 34, 2.5f, 1, E(BulletEffectType.Necromancy, 40, 100, 3))));

        CreateBullet("Normal", "Hunt", "bullet_hunt", "사냥탄",
            BulletGrade.Normal, BulletType.Sniper, "hunt", 3,
            TargetLevels(BulletEffectType.Hunt,
                "현재 플레이어 레인에서 현재 체력이 가장 낮은 적을 조준합니다.",
                new[] { 13, 16, 20, 25 }));
        CreateBullet("Rare", "Lock On", "bullet_lock_on", "고정탄",
            BulletGrade.Rare, BulletType.Sniper, "lock_on", 6,
            LockOnLevels());
        CreateBullet("Legendary", "Execution", "bullet_execution", "처형탄",
            BulletGrade.Legendary, BulletType.Sniper, "execution", 20,
            ExecutionLevels());
        CreateBullet("Rare", "Blink", "bullet_blink", "점멸탄",
            BulletGrade.Rare, BulletType.Kinetic, "blink", 6,
            TargetLevels(BulletEffectType.Blink,
                "발사 후 점유되지 않은 무작위 타일로 순간이동합니다.",
                new[] { 14, 18, 23, 30 }));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        BulletPoolSyncBuilder.SyncAllBulletPools();
        Debug.Log("Applied the 11-bullet expansion and Silent Bullet rename.");
    }

    public static void ApplyFromCommandLine() => Apply();

    private static void ConfigureSilentBullet()
    {
        const string source = BulletRoot + "/Ace/Ghost.asset";
        const string destination = BulletRoot + "/Normal/Silent.asset";
        if (AssetDatabase.LoadAssetAtPath<BulletData>(destination) == null)
        {
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }

        BulletData bullet = Load(destination);
        Sprite icon = EnsureArtworkIcon("Normal", "silent");
        Configure(bullet, "bullet_silent", "무음탄", BulletGrade.Normal,
            BulletType.Ghost, 3, icon,
            Levels(
                L("사격해도 턴을 소모하지 않습니다.", 8, 3, 5, 1.5f, 1),
                L("사격해도 턴을 소모하지 않습니다.", 11, 3, 7, 1.6f, 1),
                L("사격해도 턴을 소모하지 않습니다.", 14, 4, 9, 1.7f, 1),
                L("사격해도 턴을 소모하지 않습니다.", 18, 4, 12, 1.8f, 1)),
            true);
    }

    private static LevelSpec[] PositionLevels(
        BulletEffectType type, string prefix, params float[] bonuses)
    {
        int[] damage = { 16, 20, 25, 32 };
        LevelSpec[] levels = new LevelSpec[4];
        for (int i = 0; i < 4; i++)
            levels[i] = L($"{prefix} 최종 피해 +{bonuses[i]:0.##}%.",
                damage[i], 4 + i / 2, 12 + i * 4, 2f, 1,
                E(type, bonuses[i]));
        return levels;
    }

    private static LevelSpec[] ChanceLevels(
        BulletEffectType type, string prefix, params float[] chances)
    {
        int[] damage = { 14, 18, 23, 29 };
        LevelSpec[] levels = new LevelSpec[4];
        for (int i = 0; i < 4; i++)
            levels[i] = L($"{prefix} ({chances[i]:0.##}%).", damage[i],
                4 + i / 2, 12 + i * 4, 2f, 1,
                E(type, 0, chances[i]));
        return levels;
    }

    private static LevelSpec[] TargetLevels(
        BulletEffectType type, string description, int[] damage)
    {
        LevelSpec[] levels = new LevelSpec[4];
        for (int i = 0; i < 4; i++)
            levels[i] = L(description, damage[i], 4 + i / 2,
                12 + i * 4, 2f + i * 0.1f, 1, E(type));
        return levels;
    }

    private static LevelSpec[] LockOnLevels()
    {
        float[] bonus = { 25, 35, 45, 60 };
        int[] damage = { 16, 20, 25, 32 };
        LevelSpec[] levels = new LevelSpec[4];
        for (int i = 0; i < 4; i++)
            levels[i] = L("직전 물리 탄환의 주 대상이 생존해 있으면 다시 조준합니다. "
                + $"같은 대상에게 최종 피해 +{bonus[i]:0.##}%.",
                damage[i], 5 + i / 2, 15 + i * 5, 2f + i * 0.15f, 1,
                E(BulletEffectType.LockOn, bonus[i]));
        return levels;
    }

    private static LevelSpec[] ExecutionLevels()
    {
        int[] damage = { 28, 35, 44, 56 };
        LevelSpec[] levels = new LevelSpec[4];
        for (int i = 0; i < 4; i++)
            levels[i] = L("모든 레인에서 체력 비율이 가장 낮은 적을 조준합니다. "
                + "공격 전 보스가 아닌 적의 체력이 25% 미만이면 즉시 처형합니다.",
                damage[i], 6 + i / 2, 20 + i * 5, 2.2f + i * 0.2f, 1,
                E(BulletEffectType.Execution));
        return levels;
    }

    private static void CreateBullet(
        string gradeFolder, string assetName, string bulletId,
        string displayName, BulletGrade grade, BulletType type,
        string iconName, int price, LevelSpec[] levels)
    {
        string path = $"{BulletRoot}/{gradeFolder}/{assetName}.asset";
        BulletData bullet = AssetDatabase.LoadAssetAtPath<BulletData>(path);
        if (bullet == null)
        {
            BulletData template = FindTemplate(grade);
            bullet = UnityEngine.Object.Instantiate(template);
            bullet.name = assetName;
            AssetDatabase.CreateAsset(bullet, path);
        }

        Configure(bullet, bulletId, displayName, grade, type, price,
            EnsureArtworkIcon(gradeFolder, iconName), levels,
            false);
    }

    private static void Configure(
        BulletData bullet, string bulletId, string displayName,
        BulletGrade grade, BulletType type, int price, Sprite icon,
        LevelSpec[] levels, bool doesNotConsumeTurn)
    {
        SerializedObject serialized = new SerializedObject(bullet);
        serialized.FindProperty("bulletId").stringValue = bulletId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("price").intValue = price;
        serialized.FindProperty("grade").enumValueIndex = (int)grade;
        serialized.FindProperty("bulletType").enumValueIndex = (int)type;
        serialized.FindProperty("cylinderIcon").objectReferenceValue = icon;
        serialized.FindProperty("doesNotConsumeTurn").boolValue = doesNotConsumeTurn;
        ApplyLevel(serialized, null, levels[0], grade, 0, doesNotConsumeTurn);
        SerializedProperty upgrades = serialized.FindProperty("upgradeLevels");
        upgrades.arraySize = 3;
        for (int i = 0; i < 3; i++)
            ApplyLevel(serialized, upgrades.GetArrayElementAtIndex(i),
                levels[i + 1], grade, i + 1, doesNotConsumeTurn);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bullet);
    }

    private static void ApplyLevel(
        SerializedObject root, SerializedProperty level, LevelSpec spec,
        BulletGrade grade, int levelNumber, bool noTurn)
    {
        P(root, level, "description").stringValue = spec.Description;
        P(root, level, "damage").intValue = spec.Damage;
        P(root, level, "maxRange").intValue = spec.Range;
        P(root, level, "criticalChance").floatValue = spec.CriticalChance;
        P(root, level, "criticalDamageMultiplier").floatValue = spec.CriticalMultiplier;
        P(root, level, "shotgunShotCount").intValue = spec.ShotCount;
        P(root, level, "doesNotConsumeTurn").boolValue = noTurn;
        P(root, level, "penetrationChances").arraySize = 0;
        P(root, level, "conditionalEvents").arraySize = 0;
        P(root, level, "upgradeCost").intValue = UpgradeCost(grade, levelNumber);
        SerializedProperty effects = P(root, level, "effects");
        effects.arraySize = spec.Effects.Length;
        for (int i = 0; i < spec.Effects.Length; i++)
        {
            EffectSpec effect = spec.Effects[i];
            SerializedProperty target = effects.GetArrayElementAtIndex(i);
            target.FindPropertyRelative("effectType").enumValueIndex = (int)effect.Type;
            target.FindPropertyRelative("target").enumValueIndex = (int)BulletEffectTarget.HitEnemy;
            target.FindPropertyRelative("activationChance").floatValue = effect.Chance;
            target.FindPropertyRelative("stackCount").intValue = effect.Stacks;
            target.FindPropertyRelative("knockbackDistance").intValue = 1;
            target.FindPropertyRelative("amount").floatValue = effect.Amount;
            target.FindPropertyRelative("secondTransferPercent").floatValue = 0;
            target.FindPropertyRelative("thirdTransferPercent").floatValue = 0;
        }
    }

    private static SerializedProperty P(
        SerializedObject root, SerializedProperty level, string name) =>
        level == null ? root.FindProperty(name) : level.FindPropertyRelative(name);

    private static BulletData FindTemplate(BulletGrade grade)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:BulletData", new[] { $"{BulletRoot}/{grade}" });
        if (guids.Length == 0)
            throw new InvalidOperationException($"No template for {grade}.");
        return Load(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static BulletData Load(string path) =>
        AssetDatabase.LoadAssetAtPath<BulletData>(path)
        ?? throw new InvalidOperationException($"Missing bullet asset: {path}");

    private static LevelSpec[] Levels(params LevelSpec[] levels) => levels;
    private static LevelSpec L(string text, int damage, int range,
        float crit, float critMultiplier, int shots,
        params EffectSpec[] effects) =>
        new LevelSpec(text, damage, range, crit, critMultiplier, shots, effects);
    private static EffectSpec E(BulletEffectType type, float amount = 0,
        float chance = 100, int stacks = 1) =>
        new EffectSpec(type, amount, chance, stacks);

    private static int UpgradeCost(BulletGrade grade, int level)
    {
        if (level >= 3) return 0;
        return grade switch
        {
            BulletGrade.Normal => new[] { 5, 10, 20 }[level],
            BulletGrade.Rare => new[] { 10, 20, 40 }[level],
            BulletGrade.Ace => new[] { 20, 50, 100 }[level],
            _ => new[] { 50, 100, 200 }[level]
        };
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(IconRoot))
            AssetDatabase.CreateFolder("Assets/Sprites", "Bullet_LineArt");
        foreach (string grade in new[] { "Normal", "Rare", "Ace", "Legendary" })
        {
            string folder = $"{IconRoot}/{grade}";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(IconRoot, grade);
        }

        MigrateIcon("silent", "Normal");
        MigrateIcon("hunt", "Normal");
        MigrateIcon("focused_volley", "Rare");
        MigrateIcon("vanguard", "Rare");
        MigrateIcon("finisher", "Rare");
        MigrateIcon("wraith", "Rare");
        MigrateIcon("lock_on", "Rare");
        MigrateIcon("blink", "Rare");
        MigrateIcon("gambit", "Ace");
        MigrateIcon("dice", "Ace");
        MigrateIcon("necromancy", "Ace");
        MigrateIcon("execution", "Legendary");
        RenameIcon("Ace/Bullet_dice.png", "Ace/Bullet_dice_shot.png");
    }

    private static void MigrateIcon(string name, string grade)
    {
        string source = $"{IconRoot}/Bullet_{name}.png";
        string destination = $"{IconRoot}/{grade}/Bullet_{name}.png";
        if (AssetDatabase.LoadMainAssetAtPath(source) == null
            || AssetDatabase.LoadMainAssetAtPath(destination) != null)
            return;
        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException(error);
    }

    private static void RenameIcon(string relativeSource, string relativeDestination)
    {
        string source = $"{IconRoot}/{relativeSource}";
        string destination = $"{IconRoot}/{relativeDestination}";
        if (AssetDatabase.LoadMainAssetAtPath(source) == null
            || AssetDatabase.LoadMainAssetAtPath(destination) != null)
            return;
        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException(error);
    }

    private static Sprite EnsureArtworkIcon(string grade, string name)
    {
        string path = $"{IconRoot}/{grade}/Bullet_{name}.png";
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Missing authored bullet artwork: {path}");
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 256;
        importer.spritePixelsPerUnit = 256;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException($"Failed to import artwork: {path}");
        return sprite;
    }
}
#endif
