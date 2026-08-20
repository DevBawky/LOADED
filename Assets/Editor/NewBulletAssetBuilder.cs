#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class NewBulletAssetBuilder
{
    private const string TemplatePath =
        "Assets/Scripts/Bullet/SO/Normal/Normal.asset";

    private readonly struct EffectSpec
    {
        public EffectSpec(
            BulletEffectType type,
            float amount = 0f,
            float activationChance = 100f,
            int stackCount = 1,
            int knockbackDistance = 1,
            BulletEffectTarget target = BulletEffectTarget.FiringPlayer)
        {
            Type = type;
            Amount = amount;
            ActivationChance = activationChance;
            StackCount = stackCount;
            KnockbackDistance = knockbackDistance;
            Target = target;
        }

        public BulletEffectType Type { get; }
        public float Amount { get; }
        public float ActivationChance { get; }
        public int StackCount { get; }
        public int KnockbackDistance { get; }
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
            params float[] penetrationChances)
        {
            Description = description;
            Damage = damage;
            Range = range;
            CriticalChance = criticalChance;
            CriticalMultiplier = criticalMultiplier;
            Effects = effects ?? Array.Empty<EffectSpec>();
            PenetrationChances = penetrationChances ?? Array.Empty<float>();
        }

        public string Description { get; }
        public int Damage { get; }
        public int Range { get; }
        public float CriticalChance { get; }
        public float CriticalMultiplier { get; }
        public IReadOnlyList<EffectSpec> Effects { get; }
        public IReadOnlyList<float> PenetrationChances { get; }
    }

    private readonly struct BulletSpec
    {
        public BulletSpec(
            string assetPath,
            string id,
            string displayName,
            BulletGrade grade,
            LevelSpec baseLevel,
            LevelSpec levelOne,
            LevelSpec levelTwo,
            LevelSpec levelThree)
        {
            AssetPath = assetPath;
            Id = id;
            DisplayName = displayName;
            Grade = grade;
            Levels = new[] { baseLevel, levelOne, levelTwo, levelThree };
        }

        public string AssetPath { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public BulletGrade Grade { get; }
        public IReadOnlyList<LevelSpec> Levels { get; }
    }

    [MenuItem("Tools/LOADED/Balance New Bullet Set")]
    public static void Build()
    {
        BulletData template = AssetDatabase.LoadAssetAtPath<BulletData>(
            TemplatePath);

        if (template == null)
        {
            throw new InvalidOperationException(
                $"Bullet template was not found at '{TemplatePath}'.");
        }

        foreach (BulletSpec spec in GetSpecs())
        {
            CreateOrUpdate(template, spec);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Balanced the 13 new bullet assets and upgrade levels.");
    }

    private static IEnumerable<BulletSpec> GetSpecs()
    {
        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Rare/Seismometer.asset",
            "bullet_seismometer",
            "지진계",
            BulletGrade.Rare,
            Level("이동한 칸마다 스택 +1. 스택당 최종 피해 +10%.",
                20, 4, 20f, 2f, Effect(BulletEffectType.Seismometer, 10f)),
            Level("이동한 칸마다 스택 +1. 스택당 최종 피해 +12%.",
                22, 4, 22f, 2f, Effect(BulletEffectType.Seismometer, 12f)),
            Level("이동한 칸마다 스택 +1. 스택당 최종 피해 +15%. 50% 확률로 적 1명 관통.",
                24, 5, 25f, 2f, Effect(BulletEffectType.Seismometer, 15f), 50f),
            Level("이동한 칸마다 스택 +1. 스택당 최종 피해 +20%. 적 1명 관통.",
                28, 5, 30f, 2.2f, Effect(BulletEffectType.Seismometer, 20f), 100f));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Rare/Reverse Shot.asset",
            "bullet_reverse_shot",
            "리버스탄",
            BulletGrade.Rare,
            Level("바라보는 방향의 반대로 발사합니다.",
                35, 5, 20f, 2f, Effect(BulletEffectType.ReverseShot)),
            Level("바라보는 방향의 반대로 발사합니다.",
                40, 6, 22f, 2f, Effect(BulletEffectType.ReverseShot)),
            Level("바라보는 방향의 반대로 발사합니다. 50% 확률로 적 1명 관통.",
                45, 6, 25f, 2f, Effect(BulletEffectType.ReverseShot), 50f),
            Level("바라보는 방향의 반대로 발사합니다. 적 1명 관통.",
                50, 7, 30f, 2.2f, Effect(BulletEffectType.ReverseShot), 100f));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Rare/Evasion.asset",
            "bullet_evasion",
            "회피탄",
            BulletGrade.Rare,
            EvasionLevel("적과 플레이어를 반대 방향으로 1칸 이동시킵니다.",
                18, 4, 20f, 1, 1),
            EvasionLevel("적을 2칸 밀치고 플레이어를 반대 방향으로 1칸 이동시킵니다.",
                21, 4, 22f, 2, 1),
            EvasionLevel("적과 플레이어를 반대 방향으로 2칸 이동시킵니다.",
                24, 5, 25f, 2, 2),
            EvasionLevel("적을 3칸 밀치고 플레이어를 반대 방향으로 2칸 이동시킵니다.",
                28, 5, 30f, 3, 2));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Rare/Immersion.asset",
            "bullet_immersion",
            "몰두",
            BulletGrade.Rare,
            Level("다음 탄환의 치명타 배율 +1.",
                12, 4, 20f, 2f, Effect(BulletEffectType.Immersion, 1f)),
            Level("다음 탄환의 치명타 배율 +1.5.",
                14, 4, 22f, 2f, Effect(BulletEffectType.Immersion, 1.5f)),
            Level("다음 탄환의 치명타 배율 +2. 이후 탄환의 치명타 확률 +5%p.",
                17, 5, 25f, 2f,
                Effects(
                    new EffectSpec(BulletEffectType.Immersion, 2f),
                    new EffectSpec(BulletEffectType.Concentration, 5f))),
            Level("다음 탄환의 치명타 배율 +3. 이후 탄환의 치명타 확률 +10%p.",
                20, 5, 30f, 2.2f,
                Effects(
                    new EffectSpec(BulletEffectType.Immersion, 3f),
                    new EffectSpec(BulletEffectType.Concentration, 10f))));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Ace/Spread.asset",
            "bullet_spread",
            "확산탄",
            BulletGrade.Ace,
            Level("이후 탄환의 최종 피해 +15%.",
                12, 4, 20f, 2f, Effect(BulletEffectType.Spread, 15f)),
            Level("이후 탄환의 최종 피해 +20%.",
                16, 5, 24f, 2f, Effect(BulletEffectType.Spread, 20f)),
            Level("이후 탄환의 최종 피해 +30%. 50% 확률로 적 1명 관통.",
                20, 5, 28f, 2.2f, Effect(BulletEffectType.Spread, 30f), 50f),
            Level("이후 탄환의 최종 피해 +40%. 적 1명 관통.",
                25, 6, 35f, 2.5f, Effect(BulletEffectType.Spread, 40f), 100f));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Ace/Ritual.asset",
            "bullet_conscious",
            "의식",
            BulletGrade.Ace,
            RitualLevel("치명타 시 집중 +1. 집중당 치명타 배율 +0.25. 비치명타 시 집중을 잃고 25% 확률로 파괴됩니다.",
                12, 4, 25f, 0.25f, 25f, 1),
            RitualLevel("치명타 시 집중 +1. 집중당 치명타 배율 +0.3. 비치명타 시 집중을 잃고 20% 확률로 파괴됩니다.",
                15, 4, 30f, 0.3f, 20f, 1),
            RitualLevel("치명타 시 집중 +1. 집중당 치명타 배율 +0.4. 비치명타 시 집중을 잃고 10% 확률로 파괴됩니다.",
                18, 5, 38f, 0.4f, 10f, 1),
            RitualLevel("치명타 시 집중 +2. 집중당 치명타 배율 +0.5. 비치명타 시 집중만 잃습니다.",
                22, 5, 50f, 0.5f, 0f, 2));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Ace/Tracking.asset",
            "bullet_tracking",
            "추적탄",
            BulletGrade.Ace,
            TrackingLevel("탄환 효과로 이동할 때마다 추적 +1. 발사 시 추적만큼 무작위 적에게 표식 +1.",
                12, 5, 20f, 1),
            TrackingLevel("탄환 효과로 이동할 때마다 추적 +1. 발사 시 추적만큼 무작위 적에게 표식 +1.",
                16, 5, 24f, 1),
            TrackingLevel("탄환 효과로 이동할 때마다 추적 +1. 발사 시 추적만큼 무작위 적에게 표식 +2.",
                20, 6, 28f, 2, 50f),
            TrackingLevel("탄환 효과로 이동할 때마다 추적 +1. 발사 시 추적만큼 무작위 적에게 표식 +3. 적 1명 관통.",
                25, 6, 35f, 3, 100f));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Ace/Assassination.asset",
            "bullet_assassination",
            "암살",
            BulletGrade.Ace,
            Level("이번 턴에 이미 피격된 적에게 최종 피해 +10%.",
                28, 5, 25f, 2f, Effect(BulletEffectType.Assassination, 10f)),
            Level("이번 턴에 이미 피격된 적에게 최종 피해 +20%.",
                32, 5, 30f, 2f, Effect(BulletEffectType.Assassination, 20f)),
            Level("이번 턴에 이미 피격된 적에게 최종 피해 +35%. 50% 확률로 적 1명 관통.",
                37, 6, 35f, 2.2f, Effect(BulletEffectType.Assassination, 35f), 50f),
            Level("이번 턴에 이미 피격된 적에게 최종 피해 +50%. 적 2명 관통.",
                43, 6, 45f, 2.5f, Effect(BulletEffectType.Assassination, 50f), 100f, 100f));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Ace/High Roller.asset",
            "bullet_high_roller",
            "하이롤러",
            BulletGrade.Ace,
            Level("잃은 체력 비율에 따라 최종 피해가 최대 +100% 증가합니다.",
                20, 5, 20f, 2f, Effect(BulletEffectType.HighRoller, 100f)),
            Level("잃은 체력 비율에 따라 최종 피해가 최대 +130% 증가합니다.",
                24, 5, 24f, 2f, Effect(BulletEffectType.HighRoller, 130f)),
            Level("잃은 체력 비율에 따라 최종 피해가 최대 +160% 증가합니다. 50% 확률로 적 1명 관통.",
                28, 6, 30f, 2.2f, Effect(BulletEffectType.HighRoller, 160f), 50f),
            Level("잃은 체력 비율에 따라 최종 피해가 최대 +200% 증가합니다. 적 2명 관통.",
                34, 6, 38f, 2.5f, Effect(BulletEffectType.HighRoller, 200f), 100f, 100f));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Ace/Mastery.asset",
            "bullet_mastery",
            "통달",
            BulletGrade.Legendary,
            Level("치명타 확률 5%, 치명타 배율 x7.",
                16, 5, 5f, 7f),
            Level("치명타 확률 7%, 치명타 배율 x8.",
                20, 5, 7f, 8f),
            Level("치명타 확률 10%, 치명타 배율 x10. 50% 확률로 적 1명 관통.",
                25, 6, 10f, 10f, null, 50f),
            Level("치명타 확률 15%, 치명타 배율 x12. 적 2명 관통.",
                30, 6, 15f, 12f, null, 100f, 100f));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Legendary/Finale.asset",
            "bullet_finale",
            "피날레",
            BulletGrade.Legendary,
            Level("실린더의 마지막 탄환이 35% 확률로 한 번 더 발사됩니다.",
                18, 4, 20f, 2f, Effect(BulletEffectType.Finale, 35f)),
            Level("실린더의 마지막 탄환이 50% 확률로 한 번 더 발사됩니다.",
                22, 5, 25f, 2f, Effect(BulletEffectType.Finale, 50f)),
            Level("실린더의 마지막 탄환이 70% 확률로 한 번 더 발사됩니다. 적 1명 관통.",
                28, 5, 30f, 2.3f, Effect(BulletEffectType.Finale, 70f), 100f),
            Level("실린더의 마지막 탄환이 반드시 한 번 더 발사됩니다. 적 2명 관통.",
                35, 6, 40f, 2.7f, Effect(BulletEffectType.Finale, 100f), 100f, 100f));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Legendary/Flesh For Bone.asset",
            "bullet_flesh_for_bone",
            "육참골단",
            BulletGrade.Legendary,
            FleshForBoneLevel("발사 시 체력 8을 잃고 기본 피해 +24.",
                10, 4, 20f, 8f),
            FleshForBoneLevel("발사 시 체력 10을 잃고 기본 피해 +30.",
                12, 5, 25f, 10f),
            FleshForBoneLevel("발사 시 체력 12를 잃고 기본 피해 +36. 50% 확률로 적 1명 관통.",
                15, 5, 30f, 12f, 50f),
            FleshForBoneLevel("발사 시 체력 15를 잃고 기본 피해 +45. 적 2명 관통.",
                20, 6, 40f, 15f, 100f, 100f));

        yield return new BulletSpec(
            "Assets/Scripts/Bullet/SO/Legendary/Repeat_Mark.asset",
            "bullet_repeat_mark",
            "도돌이표",
            BulletGrade.Legendary,
            RepeatLevel("25% 확률로 이 실린더에서 앞서 발사한 모든 탄환을 다시 발사합니다.",
                15, 5, 15f, 2.5f, 25f),
            RepeatLevel("35% 확률로 이 실린더에서 앞서 발사한 모든 탄환을 다시 발사합니다.",
                18, 5, 20f, 2.5f, 35f),
            RepeatLevel("50% 확률로 이 실린더에서 앞서 발사한 모든 탄환을 다시 발사합니다. 적 1명 관통.",
                22, 6, 25f, 2.8f, 50f, 100f),
            RepeatLevel("75% 확률로 이 실린더에서 앞서 발사한 모든 탄환을 다시 발사합니다. 적 2명 관통.",
                28, 6, 35f, 3f, 75f, 100f, 100f));
    }

    private static LevelSpec Level(
        string description,
        int damage,
        int range,
        float criticalChance,
        float criticalMultiplier,
        EffectSpec[] effects = null,
        params float[] penetrationChances)
    {
        return new LevelSpec(description, damage, range, criticalChance,
            criticalMultiplier, effects, penetrationChances);
    }

    private static EffectSpec[] Effect(BulletEffectType type, float amount = 0f)
    {
        return new[] { new EffectSpec(type, amount) };
    }

    private static EffectSpec[] Effects(params EffectSpec[] effects)
    {
        return effects;
    }

    private static LevelSpec EvasionLevel(string description, int damage,
        int range, float criticalChance, int enemyDistance,
        int playerDistance)
    {
        return Level(description, damage, range, criticalChance, 2f,
            Effects(
                new EffectSpec(BulletEffectType.Knockback,
                    knockbackDistance: enemyDistance,
                    target: BulletEffectTarget.HitEnemy),
                new EffectSpec(BulletEffectType.RecoilShot,
                    knockbackDistance: playerDistance)));
    }

    private static LevelSpec RitualLevel(string description, int damage,
        int range, float criticalChance, float multiplierPerStack,
        float destructionChance, int stacksPerCritical)
    {
        return Level(description, damage, range, criticalChance, 2f,
            new[] { new EffectSpec(BulletEffectType.Ritual,
                multiplierPerStack, destructionChance, stacksPerCritical) });
    }

    private static LevelSpec TrackingLevel(string description, int damage,
        int range, float criticalChance, int markStacks,
        params float[] penetrationChances)
    {
        return Level(description, damage, range, criticalChance, 2f,
            new[] { new EffectSpec(BulletEffectType.Tracking,
                stackCount: markStacks) }, penetrationChances);
    }

    private static LevelSpec FleshForBoneLevel(string description, int damage,
        int range, float criticalChance, float healthCost,
        params float[] penetrationChances)
    {
        return Level(description, damage, range, criticalChance, 2f,
            Effect(BulletEffectType.FleshForBone, healthCost),
            penetrationChances);
    }

    private static LevelSpec RepeatLevel(string description, int damage,
        int range, float criticalChance, float criticalMultiplier,
        float activationChance, params float[] penetrationChances)
    {
        return Level(description, damage, range, criticalChance,
            criticalMultiplier,
            new[] { new EffectSpec(BulletEffectType.Alzheimer,
                activationChance: activationChance) },
            penetrationChances);
    }

    private static void CreateOrUpdate(BulletData template, BulletSpec spec)
    {
        BulletData asset = AssetDatabase.LoadAssetAtPath<BulletData>(
            spec.AssetPath);

        if (asset == null)
        {
            asset = UnityEngine.Object.Instantiate(template);
            asset.name = System.IO.Path.GetFileNameWithoutExtension(
                spec.AssetPath);
            AssetDatabase.CreateAsset(asset, spec.AssetPath);
        }

        SerializedObject serialized = new SerializedObject(asset);
        SetString(serialized, "bulletId", spec.Id);
        SetString(serialized, "displayName", spec.DisplayName);
        serialized.FindProperty("price").intValue = GetPrice(spec.Grade);
        serialized.FindProperty("grade").enumValueIndex = (int)spec.Grade;
        ApplyBaseLevel(serialized, spec.Levels[0], spec.Grade);

        SerializedProperty upgradeLevels = serialized.FindProperty(
            "upgradeLevels");
        upgradeLevels.arraySize = BulletData.MaximumUpgradeLevel;

        for (int index = 0; index < BulletData.MaximumUpgradeLevel; index++)
        {
            ApplyUpgradeLevel(serialized,
                upgradeLevels.GetArrayElementAtIndex(index),
                spec.Levels[index + 1], spec.Grade, index + 1);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void ApplyBaseLevel(SerializedObject serialized,
        LevelSpec level, BulletGrade grade)
    {
        SetString(serialized, "description", level.Description);
        serialized.FindProperty("damage").intValue = level.Damage;
        serialized.FindProperty("maxRange").intValue = level.Range;
        serialized.FindProperty("criticalChance").floatValue =
            level.CriticalChance;
        serialized.FindProperty("criticalDamageMultiplier").floatValue =
            level.CriticalMultiplier;
        serialized.FindProperty("upgradeCost").intValue =
            GetUpgradeCost(grade, 0);
        ApplyEffects(serialized.FindProperty("effects"), level.Effects);
        serialized.FindProperty("conditionalEvents").arraySize = 0;
        ApplyPenetrationChances(
            serialized.FindProperty("penetrationChances"),
            level.PenetrationChances);
    }

    private static void ApplyUpgradeLevel(SerializedObject owner,
        SerializedProperty property, LevelSpec level, BulletGrade grade,
        int levelNumber)
    {
        property.FindPropertyRelative("description").stringValue =
            level.Description;
        property.FindPropertyRelative("damage").intValue = level.Damage;
        property.FindPropertyRelative("maxRange").intValue = level.Range;
        property.FindPropertyRelative("criticalChance").floatValue =
            level.CriticalChance;
        property.FindPropertyRelative("criticalDamageMultiplier").floatValue =
            level.CriticalMultiplier;
        ApplyEffects(property.FindPropertyRelative("effects"), level.Effects);
        property.FindPropertyRelative("conditionalEvents").arraySize = 0;
        ApplyPenetrationChances(
            property.FindPropertyRelative("penetrationChances"),
            level.PenetrationChances);
        property.FindPropertyRelative("lineMaterial").objectReferenceValue =
            owner.FindProperty("lineMaterial").objectReferenceValue;
        property.FindPropertyRelative("doesNotConsumeTurn").boolValue =
            owner.FindProperty("doesNotConsumeTurn").boolValue;
        property.FindPropertyRelative("recoilStrength").floatValue =
            owner.FindProperty("recoilStrength").floatValue;
        property.FindPropertyRelative("upgradeCost").intValue =
            GetUpgradeCost(grade, levelNumber);
    }

    private static void ApplyEffects(SerializedProperty effects,
        IReadOnlyList<EffectSpec> specs)
    {
        effects.arraySize = specs.Count;

        for (int index = 0; index < specs.Count; index++)
        {
            EffectSpec effect = specs[index];
            SerializedProperty property = effects.GetArrayElementAtIndex(index);
            property.FindPropertyRelative("effectType").enumValueIndex =
                (int)effect.Type;
            property.FindPropertyRelative("target").enumValueIndex =
                (int)effect.Target;
            property.FindPropertyRelative("activationChance").floatValue =
                effect.ActivationChance;
            property.FindPropertyRelative("stackCount").intValue =
                effect.StackCount;
            property.FindPropertyRelative("knockbackDistance").intValue =
                effect.KnockbackDistance;
            property.FindPropertyRelative("amount").floatValue = effect.Amount;
            property.FindPropertyRelative("secondTransferPercent").floatValue = 0f;
            property.FindPropertyRelative("thirdTransferPercent").floatValue = 0f;
        }
    }

    private static void ApplyPenetrationChances(SerializedProperty chances,
        IReadOnlyList<float> values)
    {
        chances.arraySize = values.Count;

        for (int index = 0; index < values.Count; index++)
        {
            chances.GetArrayElementAtIndex(index)
                .FindPropertyRelative("chance").floatValue = values[index];
        }
    }

    private static int GetPrice(BulletGrade grade)
    {
        return grade switch
        {
            BulletGrade.Normal => 3,
            BulletGrade.Rare => 6,
            BulletGrade.Ace => 10,
            BulletGrade.Legendary => 20,
            _ => 0
        };
    }

    private static int GetUpgradeCost(BulletGrade grade, int currentLevel)
    {
        if (currentLevel >= BulletData.MaximumUpgradeLevel)
        {
            return 0;
        }

        return grade switch
        {
            BulletGrade.Normal => new[] { 5, 10, 20 }[currentLevel],
            BulletGrade.Rare => new[] { 10, 20, 40 }[currentLevel],
            BulletGrade.Ace => new[] { 20, 50, 100 }[currentLevel],
            BulletGrade.Legendary => new[] { 50, 100, 200 }[currentLevel],
            _ => 0
        };
    }

    private static void SetString(SerializedObject serialized,
        string propertyName, string value)
    {
        serialized.FindProperty(propertyName).stringValue = value;
    }
}
#endif
