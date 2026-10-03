#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class UnderusedBulletReworkBuilder
{
    private readonly struct EffectSpec
    {
        public EffectSpec(
            BulletEffectType type,
            float amount = 0f,
            float chance = 100f,
            int stacks = 1,
            int cap = 0)
        {
            Type = type;
            Amount = amount;
            Chance = chance;
            Stacks = stacks;
            Cap = cap;
        }

        public BulletEffectType Type { get; }
        public float Amount { get; }
        public float Chance { get; }
        public int Stacks { get; }
        public int Cap { get; }
    }

    private readonly struct LevelSpec
    {
        public LevelSpec(
            string description,
            int damage,
            float criticalChance,
            EffectSpec effect)
        {
            Description = description;
            Damage = damage;
            CriticalChance = criticalChance;
            Effect = effect;
        }

        public string Description { get; }
        public int Damage { get; }
        public float CriticalChance { get; }
        public EffectSpec Effect { get; }
    }

    [MenuItem("Tools/LOADED/Apply Underused Bullet Rework")]
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        MoveAssetIfNeeded(
            "Assets/Scripts/Bullet/SO/Legendary/Collection.asset",
            "Assets/Scripts/Bullet/SO/Legendary/Harvest.asset");
        MoveAssetIfNeeded(
            "Assets/Scripts/Bullet/SO/Ace/Monopoly.asset",
            "Assets/Scripts/Bullet/SO/Ace/Return.asset");
        MoveAssetIfNeeded(
            "Assets/Scripts/Bullet/SO/Rare/Loader.asset",
            "Assets/Scripts/Bullet/SO/Rare/Emergency.asset");

        ConfigureIcon(
            "Assets/Sprites/Bullet_Cylinder/Legendary/Bullet_Harvest_Cylinder.png");
        ConfigureIcon(
            "Assets/Sprites/Bullet_Cylinder/Ace/Bullet_Return_Cylinder.png");
        ConfigureIcon(
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Emergency_Cylinder.png");
        ConfigureIcon(
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Jackpot_Reworked_Cylinder.png");
        ConfigureIcon(
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Crescendo_RandomDebuff_Cylinder.png");
        ConfigureIcon(
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Resonance_Reworked_Cylinder.png");
        ConfigureIcon(
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Mixed_Reworked_Cylinder.png");
        ConfigureIcon(
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_MassProduced_Reworked_Cylinder.png");
        ConfigureIcon(
            "Assets/Sprites/Bullet_Cylinder/Ace/Bullet_Masterpiece_Reworked_Cylinder.png");
        ConfigureIcon(
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Coagulation_Reworked_Cylinder.png");
        ConfigureIcon(
            "Assets/Sprites/Bullet_Cylinder/Ace/Bullet_Ritual_Reworked_Cylinder.png");

        ConfigureBullet(
            "Assets/Scripts/Bullet/SO/Rare/Crescendo.asset",
            "고조탄",
            BulletType.Debuff,
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Crescendo_RandomDebuff_Cylinder.png",
            new[]
            {
                Crescendo(50, 25f, 2),
                Crescendo(60, 28f, 3),
                Crescendo(70, 32f, 4),
                Crescendo(100, 38f, 5)
            });
        ConfigureBullet(
            "Assets/Scripts/Bullet/SO/Rare/Resonance.asset",
            "공명탄",
            BulletType.Combo,
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Resonance_Reworked_Cylinder.png",
            new[]
            {
                Resonance(30, 15f, 1),
                Resonance(35, 21f, 2),
                Resonance(40, 29f, 3),
                Resonance(50, 40f, 4)
            });
        ConfigureBullet(
            "Assets/Scripts/Bullet/SO/Rare/Mixed Grade.asset",
            "혼합탄",
            BulletType.Debuff,
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Mixed_Reworked_Cylinder.png",
            new[]
            {
                Mixed(20, 16f, 1),
                Mixed(16, 20f, 1),
                Mixed(21, 26f, 2),
                Mixed(27, 34f, 3)
            });
        ConfigureBullet(
            "Assets/Scripts/Bullet/SO/Rare/Mass Produced.asset",
            "양산탄",
            BulletType.Growth,
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_MassProduced_Reworked_Cylinder.png",
            new[]
            {
                MassProduced(15, 15f, 2),
                MassProduced(18, 18f, 3),
                MassProduced(22, 22f, 4),
                MassProduced(28, 28f, 5)
            });
        ConfigureBullet(
            "Assets/Scripts/Bullet/SO/Legendary/Harvest.asset",
            "수확탄",
            BulletType.Combo,
            "Assets/Sprites/Bullet_Cylinder/Legendary/Bullet_Harvest_Cylinder.png",
            new[]
            {
                Harvest(10, 25f, 1),
                Harvest(15, 30f, 2),
                Harvest(22, 35f, 3),
                Harvest(30, 45f, 5)
            });
        ConfigureBullet(
            "Assets/Scripts/Bullet/SO/Ace/Return.asset",
            "귀환탄",
            BulletType.Piercing,
            "Assets/Sprites/Bullet_Cylinder/Ace/Bullet_Return_Cylinder.png",
            new[]
            {
                Return(20, 20f, 40f),
                Return(24, 24f, 60f),
                Return(28, 30f, 80f),
                Return(35, 38f, 100f)
            });
        ConfigureBullet(
            "Assets/Scripts/Bullet/SO/Ace/Masterpiece.asset",
            "명품탄",
            BulletType.Growth,
            "Assets/Sprites/Bullet_Cylinder/Ace/Bullet_Masterpiece_Reworked_Cylinder.png",
            new[]
            {
                Masterpiece(16, 0.15f, 5),
                Masterpiece(20, 0.20f, 6),
                Masterpiece(26, 0.25f, 7),
                Masterpiece(34, 0.30f, 8)
            });
        ConfigureBullet(
            "Assets/Scripts/Bullet/SO/Rare/Coagulation.asset",
            "응고탄",
            BulletType.Blood,
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Coagulation_Reworked_Cylinder.png",
            new[]
            {
                Coagulation(14, 10f, 10f, 3, 30),
                Coagulation(17, 14f, 12f, 4, 40),
                Coagulation(20, 18f, 15f, 5, 50),
                Coagulation(25, 22f, 20f, 5, 60)
            });
        ConfigureBullet(
            "Assets/Scripts/Bullet/SO/Rare/Emergency.asset",
            "비상탄",
            BulletType.Ghost,
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Emergency_Cylinder.png",
            new[]
            {
                Emergency(20, 15f),
                Emergency(25, 18f),
                Emergency(30, 22f),
                Emergency(35, 28f)
            });
        ConfigureBullet(
            "Assets/Scripts/Bullet/SO/Rare/Jackpot.asset",
            "잭팟탄",
            BulletType.Economy,
            "Assets/Sprites/Bullet_Cylinder/Rare/Bullet_Jackpot_Reworked_Cylinder.png",
            new[]
            {
                Jackpot(12, 30f, 10f, 300f, 3),
                Jackpot(15, 40f, 12f, 400f, 4),
                Jackpot(19, 50f, 15f, 500f, 5),
                Jackpot(24, 60f, 20f, 700f, 7)
            });
        ConfigureBullet(
            "Assets/Scripts/Bullet/SO/Ace/Ritual.asset",
            "의식",
            BulletType.Blood,
            "Assets/Sprites/Bullet_Cylinder/Ace/Bullet_Ritual_Reworked_Cylinder.png",
            new[]
            {
                Ritual(12, 25f, 2f),
                Ritual(15, 30f, 3f),
                Ritual(18, 38f, 5f),
                Ritual(22, 50f, 8f)
            });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        BulletPoolSyncBuilder.SyncAllBulletPools();
        Debug.Log("Applied the underused bullet rework.");
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    private static LevelSpec Crescendo(
        int damage,
        float critical,
        int stacks)
    {
        return new LevelSpec(
            $"명중 시 독·표식·약화·기절 중 하나를 무작위로 {stacks}스택 부여합니다.",
            damage,
            critical,
            new EffectSpec(BulletEffectType.Crescendo, stacks: stacks));
    }

    private static LevelSpec Resonance(int damage, float critical, int stacks)
    {
        return new LevelSpec(
            $"직전 탄환이 부여한 상태이상 종류를 명중 대상에게 각각 {stacks}스택 부여합니다.",
            damage,
            critical,
            new EffectSpec(BulletEffectType.Resonance, stacks: stacks));
    }

    private static LevelSpec Mixed(int damage, float critical, int stacks)
    {
        return new LevelSpec(
            $"독·표식·약화를 각각 {stacks}스택 부여합니다.",
            damage,
            critical,
            new EffectSpec(BulletEffectType.MixedGrade, stacks: stacks));
    }

    private static LevelSpec MassProduced(
        int damage,
        float critical,
        int cap)
    {
        return new LevelSpec(
            $"보유한 노멀·레어 탄환 하나당 추가 공격합니다 (최대 {cap}회).",
            damage,
            critical,
            new EffectSpec(BulletEffectType.MassProduced, stacks: cap));
    }

    private static LevelSpec Harvest(int damage, float critical, int cap)
    {
        return new LevelSpec(
            $"처치 시 현재 체력이 가장 낮은 적에게 즉시 다시 발사합니다 (최대 {cap}회).",
            damage,
            critical,
            new EffectSpec(BulletEffectType.Collection, stacks: cap));
    }

    private static LevelSpec Return(
        int damage,
        float critical,
        float returnPercent)
    {
        return new LevelSpec(
            $"명중 후 돌아오며 같은 레인의 귀환 경로에 원래 피해의 {returnPercent:0.##}%를 줍니다.",
            damage,
            critical,
            new EffectSpec(BulletEffectType.Monopoly, returnPercent));
    }

    private static LevelSpec Masterpiece(
        int damage,
        float bonus,
        int cap)
    {
        return new LevelSpec(
            $"치명타 확정. 보유한 에이스·레전드리 탄환 하나당 치명타 배율 +{bonus:0.##} (최대 {cap}개).",
            damage,
            100f,
            new EffectSpec(BulletEffectType.Masterpiece, bonus, stacks: cap));
    }

    private static LevelSpec Coagulation(
        int damage,
        float critical,
        float basePercent,
        int perBlood,
        int cap)
    {
        return new LevelSpec(
            $"잃은 체력의 {basePercent:0.##}% 회복. 혈투 탄환 하나당 +{perBlood}%p (최대 {cap}%).",
            damage,
            critical,
            new EffectSpec(
                BulletEffectType.Coagulation,
                basePercent,
                stacks: perBlood,
                cap: cap));
    }

    private static LevelSpec Emergency(int damage, float critical)
    {
        return new LevelSpec(
            "발사 후 다음 재장전이 턴을 소모하지 않습니다.",
            damage,
            critical,
            new EffectSpec(BulletEffectType.Loader));
    }

    private static LevelSpec Jackpot(
        int damage,
        float critical,
        float chance,
        float multiplier,
        int gold)
    {
        return new LevelSpec(
            $"발사 시 {chance:0.##}% 확률로 대박: 최종 피해 x{multiplier / 100f:0.##}, 골드 +{gold}.",
            damage,
            critical,
            new EffectSpec(
                BulletEffectType.Jackpot,
                multiplier,
                chance,
                gold));
    }

    private static LevelSpec Ritual(int damage, float critical, float growth)
    {
        return new LevelSpec(
            $"발사 시 최대 체력 1 지불. 이번 런 동안 이 탄환의 기본 피해 영구 +{growth:0.##}.",
            damage,
            critical,
            new EffectSpec(BulletEffectType.Ritual, growth, stacks: 1));
    }

    private static void ConfigureBullet(
        string path,
        string displayName,
        BulletType bulletType,
        string iconPath,
        LevelSpec[] levels)
    {
        if (levels == null || levels.Length != 4)
        {
            throw new ArgumentException("Exactly four levels are required.");
        }

        BulletData bullet = AssetDatabase.LoadAssetAtPath<BulletData>(path);
        if (bullet == null)
        {
            throw new InvalidOperationException($"Missing bullet asset: {path}");
        }

        bullet.name = System.IO.Path.GetFileNameWithoutExtension(path);
        SerializedObject serialized = new SerializedObject(bullet);
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("bulletType").enumValueIndex = (int)bulletType;

        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (icon == null)
            {
                throw new InvalidOperationException($"Missing icon: {iconPath}");
            }

            serialized.FindProperty("cylinderIcon").objectReferenceValue = icon;
        }

        ApplyLevel(serialized, null, levels[0]);
        SerializedProperty upgrades = serialized.FindProperty("upgradeLevels");
        upgrades.arraySize = BulletData.MaximumUpgradeLevel;

        for (int index = 0; index < BulletData.MaximumUpgradeLevel; index++)
        {
            ApplyLevel(
                serialized,
                upgrades.GetArrayElementAtIndex(index),
                levels[index + 1]);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bullet);
    }

    private static void ApplyLevel(
        SerializedObject serialized,
        SerializedProperty level,
        LevelSpec spec)
    {
        Property(serialized, level, "description").stringValue =
            spec.Description;
        Property(serialized, level, "damage").intValue = spec.Damage;
        Property(serialized, level, "criticalChance").floatValue =
            spec.CriticalChance;
        SerializedProperty effects = Property(
            serialized,
            level,
            "effects");
        effects.arraySize = 1;
        ApplyEffect(effects.GetArrayElementAtIndex(0), spec.Effect);
        Property(serialized, level, "conditionalEvents").arraySize = 0;
    }

    private static void ApplyEffect(
        SerializedProperty property,
        EffectSpec spec)
    {
        property.FindPropertyRelative("effectType").enumValueIndex =
            (int)spec.Type;
        property.FindPropertyRelative("target").enumValueIndex =
            (int)BulletEffectTarget.HitEnemy;
        property.FindPropertyRelative("activationChance").floatValue =
            spec.Chance;
        property.FindPropertyRelative("stackCount").intValue = spec.Stacks;
        property.FindPropertyRelative("knockbackDistance").intValue = spec.Cap;
        property.FindPropertyRelative("amount").floatValue = spec.Amount;
        property.FindPropertyRelative("secondTransferPercent").floatValue = 0f;
        property.FindPropertyRelative("thirdTransferPercent").floatValue = 0f;
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

    private static void MoveAssetIfNeeded(string source, string destination)
    {
        if (AssetDatabase.LoadAssetAtPath<BulletData>(destination) != null)
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<BulletData>(source) == null)
        {
            throw new InvalidOperationException($"Missing bullet asset: {source}");
        }

        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static void ConfigureIcon(string path)
    {
        AssetDatabase.ImportAsset(
            path,
            ImportAssetOptions.ForceSynchronousImport
            | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path)
            as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"Could not import icon: {path}");
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
}
#endif
