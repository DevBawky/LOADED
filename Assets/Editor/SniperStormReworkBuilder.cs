#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class SniperStormReworkBuilder
{
    private const string SnipingPath =
        "Assets/Scripts/Bullet/SO/Rare/Sniping.asset";
    private const string AssassinationPath =
        "Assets/Scripts/Bullet/SO/Ace/Assassination.asset";
    private const string MasteryPath =
        "Assets/Scripts/Bullet/SO/Legendary/Mastery.asset";
    private const string TrackingPath =
        "Assets/Scripts/Bullet/SO/Ace/Tracking.asset";
    private const string TyphoonPath =
        "Assets/Scripts/Bullet/SO/Ace/Typhoon.asset";
    private const string CataclysmPath =
        "Assets/Scripts/Bullet/SO/Legendary/Cataclysm.asset";
    private const string CataclysmIconPath =
        "Assets/Sprites/Bullet_Cylinder/Legendary/Bullet_Cataclysm.png";

    private readonly struct EffectSpec
    {
        public EffectSpec(
            BulletEffectType type,
            float amount = 0f,
            float chance = 100f,
            int stacks = 1,
            BulletEffectTarget target = BulletEffectTarget.FiringPlayer)
        {
            Type = type;
            Amount = amount;
            Chance = chance;
            Stacks = stacks;
            Target = target;
        }

        public BulletEffectType Type { get; }
        public float Amount { get; }
        public float Chance { get; }
        public int Stacks { get; }
        public BulletEffectTarget Target { get; }
    }

    private readonly struct LevelSpec
    {
        public LevelSpec(
            string description,
            int damage,
            int range,
            float criticalChance,
            float criticalMultiplier,
            EffectSpec[] effects,
            int defeatMarkStacks = 0)
        {
            Description = description;
            Damage = damage;
            Range = range;
            CriticalChance = criticalChance;
            CriticalMultiplier = criticalMultiplier;
            Effects = effects ?? Array.Empty<EffectSpec>();
            DefeatMarkStacks = defeatMarkStacks;
        }

        public string Description { get; }
        public int Damage { get; }
        public int Range { get; }
        public float CriticalChance { get; }
        public float CriticalMultiplier { get; }
        public EffectSpec[] Effects { get; }
        public int DefeatMarkStacks { get; }
    }

    [MenuItem("Tools/LOADED/Apply Sniper And Storm Rework")]
    public static void Apply()
    {
        MoveAssetIfNeeded(
            "Assets/Scripts/Bullet/SO/Rare/Rangefinder.asset",
            SnipingPath);
        MoveAssetIfNeeded(
            "Assets/Scripts/Bullet/SO/Ace/Mastery.asset",
            MasteryPath);

        ConfigureIcon();

        ConfigureBullet(
            Load(SnipingPath),
            "스나이핑",
            BulletGrade.Rare,
            BulletType.Sniper,
            new[]
            {
                new LevelSpec("전장에서 가장 먼 적을 조준합니다.",
                    20, 4, 20f, 1.7f,
                    Effects(new EffectSpec(BulletEffectType.Rangefinder))),
                new LevelSpec("전장에서 가장 먼 적을 조준합니다.",
                    20, 5, 23f, 1.8f,
                    Effects(new EffectSpec(BulletEffectType.Rangefinder))),
                new LevelSpec("전장에서 가장 먼 적을 조준합니다.\n표식 +1 (40%)",
                    22, 6, 27f, 2f,
                    Effects(
                        new EffectSpec(BulletEffectType.Rangefinder),
                        new EffectSpec(BulletEffectType.Mark, chance: 40f,
                            target: BulletEffectTarget.HitEnemy))),
                new LevelSpec("전장에서 가장 먼 적을 조준합니다.\n표식 +1",
                    24, 7, 32f, 2.2f,
                    Effects(
                        new EffectSpec(BulletEffectType.Rangefinder),
                        new EffectSpec(BulletEffectType.Mark,
                            target: BulletEffectTarget.HitEnemy)))
            });

        ConfigureBullet(
            Load(AssassinationPath),
            "암살",
            BulletGrade.Ace,
            BulletType.Sniper,
            new[]
            {
                AssassinationLevel(28, 5, 25f, 2f, 25f, 1),
                AssassinationLevel(32, 5, 30f, 2f, 35f, 1),
                AssassinationLevel(37, 6, 35f, 2.2f, 50f, 2),
                AssassinationLevel(43, 6, 45f, 2.5f, 75f, 3)
            });

        ConfigureBullet(
            Load(MasteryPath),
            "통달",
            BulletGrade.Legendary,
            BulletType.Sniper,
            new[]
            {
                MasteryLevel(16, 5, 15f, 2f, 1.2f),
                MasteryLevel(20, 5, 20f, 2f, 1.35f),
                MasteryLevel(25, 6, 25f, 2.2f, 1.5f),
                MasteryLevel(30, 6, 30f, 2.5f, 2f)
            });

        ConfigureBullet(
            Load(TrackingPath),
            "추적탄",
            BulletGrade.Ace,
            BulletType.Storm,
            new[]
            {
                TrackingLevel(12, 5, 20f, 2f),
                TrackingLevel(16, 5, 24f, 2f),
                TrackingLevel(20, 6, 28f, 2f),
                TrackingLevel(25, 6, 35f, 2f)
            });

        ConfigureBullet(
            Load(TyphoonPath),
            "태풍",
            BulletGrade.Ace,
            BulletType.Storm,
            new[]
            {
                TyphoonLevel(20, 4, 80f, 1.8f, 50f),
                TyphoonLevel(30, 4, 85f, 2f, 30f),
                TyphoonLevel(40, 5, 90f, 2.2f, 20f),
                TyphoonLevel(50, 5, 100f, 3f, 10f)
            });

        BulletData masteryTemplate = Load(MasteryPath);
        BulletData cataclysm = AssetDatabase.LoadAssetAtPath<BulletData>(
            CataclysmPath);
        if (cataclysm == null)
        {
            cataclysm = UnityEngine.Object.Instantiate(masteryTemplate);
            cataclysm.name = "Cataclysm";
            AssetDatabase.CreateAsset(cataclysm, CataclysmPath);
        }

        ConfigureBullet(
            cataclysm,
            "대재앙",
            BulletGrade.Legendary,
            BulletType.Storm,
            new[]
            {
                CataclysmLevel(24, 6, 20f, 2f),
                CataclysmLevel(32, 6, 24f, 2f),
                CataclysmLevel(42, 7, 28f, 2.2f),
                CataclysmLevel(55, 7, 35f, 2.5f)
            },
            "bullet_cataclysm",
            AssetDatabase.LoadAssetAtPath<Sprite>(CataclysmIconPath));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        BulletPoolSyncBuilder.SyncAllBulletPools();
        Debug.Log("Applied the sniper and storm bullet rework.");
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    private static LevelSpec AssassinationLevel(
        int damage,
        int range,
        float criticalChance,
        float criticalMultiplier,
        float stackDamagePercent,
        int defeatMarkStacks)
    {
        return new LevelSpec(
            "상태이상 스택이 가장 많은 적을 조준합니다.\n"
            + $"상태이상 스택당 최종 피해 +{stackDamagePercent:0.##}%\n"
            + $"처치 시 생존한 모든 적에게 표식 +{defeatMarkStacks}",
            damage,
            range,
            criticalChance,
            criticalMultiplier,
            Effects(new EffectSpec(
                BulletEffectType.Assassination,
                stackDamagePercent)),
            defeatMarkStacks);
    }

    private static LevelSpec MasteryLevel(
        int damage,
        int range,
        float criticalChance,
        float criticalMultiplier,
        float factor)
    {
        return new LevelSpec(
            "최대 체력이 가장 높은 적을 조준합니다.\n"
            + $"앞서 발사한 저격 탄환 1발당 최종 피해 x{factor:0.##}",
            damage,
            range,
            criticalChance,
            criticalMultiplier,
            Effects(new EffectSpec(BulletEffectType.Mastery, factor)));
    }

    private static LevelSpec TrackingLevel(
        int damage,
        int range,
        float criticalChance,
        float criticalMultiplier)
    {
        return new LevelSpec(
            "모든 레인에서 상태이상이 있는 모든 적을 공격합니다.",
            damage,
            range,
            criticalChance,
            criticalMultiplier,
            Effects(new EffectSpec(BulletEffectType.Tracking)));
    }

    private static LevelSpec TyphoonLevel(
        int damage,
        int range,
        float criticalChance,
        float criticalMultiplier,
        float destroyChance)
    {
        return new LevelSpec(
            "현재 플레이어 레인의 모든 적을 공격합니다.\n"
            + $"발사 후 파괴 ({destroyChance:0.##}%)",
            damage,
            range,
            criticalChance,
            criticalMultiplier,
            Effects(
                new EffectSpec(BulletEffectType.QuickDraw, 1f),
                new EffectSpec(BulletEffectType.DestroyBullet, 1f,
                    destroyChance)));
    }

    private static LevelSpec CataclysmLevel(
        int damage,
        int range,
        float criticalChance,
        float criticalMultiplier)
    {
        return new LevelSpec(
            "모든 레인의 모든 적을 공격합니다.",
            damage,
            range,
            criticalChance,
            criticalMultiplier,
            Effects(new EffectSpec(BulletEffectType.Cataclysm)));
    }

    private static EffectSpec[] Effects(params EffectSpec[] effects)
    {
        return effects;
    }

    private static BulletData Load(string path)
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(path);
        if (data == null)
        {
            throw new InvalidOperationException(
                $"Bullet asset was not found at '{path}'.");
        }

        return data;
    }

    private static void MoveAssetIfNeeded(string source, string destination)
    {
        if (AssetDatabase.LoadAssetAtPath<BulletData>(destination) != null)
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<BulletData>(source) == null)
        {
            throw new InvalidOperationException(
                $"Bullet asset was not found at '{source}'.");
        }

        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static void ConfigureIcon()
    {
        AssetDatabase.ImportAsset(
            CataclysmIconPath,
            ImportAssetOptions.ForceSynchronousImport
            | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(
            CataclysmIconPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException(
                "The Cataclysm icon could not be imported as a texture.");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 64;
        importer.spritePixelsPerUnit = 64f;
        importer.SaveAndReimport();
    }

    private static void ConfigureBullet(
        BulletData bullet,
        string displayName,
        BulletGrade grade,
        BulletType type,
        LevelSpec[] levels,
        string bulletId = null,
        Sprite icon = null)
    {
        if (levels == null || levels.Length != 4)
        {
            throw new ArgumentException("Exactly four levels are required.");
        }

        bullet.name = System.IO.Path.GetFileNameWithoutExtension(
            AssetDatabase.GetAssetPath(bullet));
        SerializedObject serialized = new SerializedObject(bullet);
        if (!string.IsNullOrWhiteSpace(bulletId))
        {
            serialized.FindProperty("bulletId").stringValue = bulletId;
        }
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("price").intValue = Price(grade);
        serialized.FindProperty("grade").enumValueIndex = (int)grade;
        serialized.FindProperty("bulletType").enumValueIndex = (int)type;
        if (icon != null)
        {
            serialized.FindProperty("cylinderIcon").objectReferenceValue =
                icon;
        }

        ApplyLevel(serialized, null, levels[0], grade, 0);
        SerializedProperty upgrades = serialized.FindProperty(
            "upgradeLevels");
        upgrades.arraySize = BulletData.MaximumUpgradeLevel;
        for (int index = 0; index < BulletData.MaximumUpgradeLevel; index++)
        {
            ApplyLevel(
                serialized,
                upgrades.GetArrayElementAtIndex(index),
                levels[index + 1],
                grade,
                index + 1);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bullet);
    }

    private static void ApplyLevel(
        SerializedObject serialized,
        SerializedProperty level,
        LevelSpec spec,
        BulletGrade grade,
        int levelNumber)
    {
        Property(serialized, level, "description").stringValue =
            spec.Description;
        Property(serialized, level, "damage").intValue = spec.Damage;
        Property(serialized, level, "maxRange").intValue = spec.Range;
        Property(serialized, level, "criticalChance").floatValue =
            spec.CriticalChance;
        Property(serialized, level, "criticalDamageMultiplier").floatValue =
            spec.CriticalMultiplier;
        ApplyEffects(Property(serialized, level, "effects"), spec.Effects);
        ApplyDefeatMarks(
            Property(serialized, level, "conditionalEvents"),
            spec.DefeatMarkStacks);
        Property(serialized, level, "penetrationChances").arraySize = 0;
        Property(serialized, level, "upgradeCost").intValue =
            UpgradeCost(grade, levelNumber);

        if (level != null)
        {
            level.FindPropertyRelative("lineMaterial").objectReferenceValue =
                serialized.FindProperty("lineMaterial").objectReferenceValue;
            level.FindPropertyRelative("doesNotConsumeTurn").boolValue =
                serialized.FindProperty("doesNotConsumeTurn").boolValue;
            level.FindPropertyRelative("recoilStrength").floatValue =
                serialized.FindProperty("recoilStrength").floatValue;
        }
    }

    private static SerializedProperty Property(
        SerializedObject serialized,
        SerializedProperty level,
        string name)
    {
        return level == null
            ? serialized.FindProperty(name)
            : level.FindPropertyRelative(name);
    }

    private static void ApplyEffects(
        SerializedProperty effects,
        EffectSpec[] specs)
    {
        effects.arraySize = specs.Length;
        for (int index = 0; index < specs.Length; index++)
        {
            ApplyEffect(effects.GetArrayElementAtIndex(index), specs[index]);
        }
    }

    private static void ApplyEffect(
        SerializedProperty property,
        EffectSpec spec)
    {
        property.FindPropertyRelative("effectType").enumValueIndex =
            (int)spec.Type;
        property.FindPropertyRelative("target").enumValueIndex =
            (int)spec.Target;
        property.FindPropertyRelative("activationChance").floatValue =
            spec.Chance;
        property.FindPropertyRelative("stackCount").intValue = spec.Stacks;
        property.FindPropertyRelative("knockbackDistance").intValue = 1;
        property.FindPropertyRelative("amount").floatValue = spec.Amount;
        property.FindPropertyRelative("secondTransferPercent").floatValue = 0f;
        property.FindPropertyRelative("thirdTransferPercent").floatValue = 0f;
    }

    private static void ApplyDefeatMarks(
        SerializedProperty conditionalEvents,
        int stacks)
    {
        conditionalEvents.arraySize = stacks > 0 ? 1 : 0;
        if (stacks <= 0)
        {
            return;
        }

        SerializedProperty conditional =
            conditionalEvents.GetArrayElementAtIndex(0);
        conditional.FindPropertyRelative("trigger").enumValueIndex =
            (int)BulletConditionalTrigger.EnemyDefeated;
        SerializedProperty events = conditional.FindPropertyRelative("events");
        events.arraySize = 1;
        ApplyEffect(
            events.GetArrayElementAtIndex(0),
            new EffectSpec(
                BulletEffectType.Mark,
                stacks: stacks,
                target: BulletEffectTarget.AllEnemies));
    }

    private static int Price(BulletGrade grade)
    {
        return grade switch
        {
            BulletGrade.Rare => 6,
            BulletGrade.Ace => 10,
            BulletGrade.Legendary => 20,
            _ => 3
        };
    }

    private static int UpgradeCost(BulletGrade grade, int level)
    {
        if (level >= BulletData.MaximumUpgradeLevel)
        {
            return 0;
        }

        return grade switch
        {
            BulletGrade.Rare => new[] { 10, 20, 40 }[level],
            BulletGrade.Ace => new[] { 20, 50, 100 }[level],
            BulletGrade.Legendary => new[] { 50, 100, 200 }[level],
            _ => new[] { 5, 10, 20 }[level]
        };
    }
}
#endif
