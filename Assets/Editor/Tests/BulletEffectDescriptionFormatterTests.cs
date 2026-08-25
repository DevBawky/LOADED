using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BulletEffectDescriptionFormatterTests
{
    private const string BulletAssetRoot = "Assets/Scripts/Bullet/SO";

    [Test]
    public void EveryDetailedDescriptionUsesSerializedLevelDescriptionAndStats()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:BulletData",
            new[] { BulletAssetRoot });

        Assert.That(guids, Is.Not.Empty);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BulletData bullet = AssetDatabase.LoadAssetAtPath<BulletData>(path);

            Assert.That(bullet, Is.Not.Null, path);

            SerializedObject serialized = new SerializedObject(bullet);

            for (int level = 0;
                 level <= BulletData.MaximumUpgradeLevel;
                 level++)
            {
                string expectedDescription = GetSerializedDescription(
                    serialized,
                    level);
                string tooltip = StripRichText(
                    bullet.GetDetailedDescription(level));

                Assert.That(
                    bullet.GetDescription(level),
                    Is.EqualTo(expectedDescription),
                    $"{path} level {level}");
                Assert.That(
                    expectedDescription,
                    Is.Not.Empty,
                    $"{path} level {level}");
                Assert.That(
                    tooltip,
                    Does.StartWith(
                        NormalizeNewlines(expectedDescription)
                        + "\n\n피해:"),
                    $"{path} level {level}");
                Assert.That(
                    tooltip,
                    Does.Not.Contain("효과 상세"),
                    $"{path} level {level}");
                Assert.That(
                    tooltip,
                    Does.Contain("유효 범위:"),
                    $"{path} level {level}");
                Assert.That(
                    tooltip,
                    Does.Contain("치명타 확률:"),
                    $"{path} level {level}");
                Assert.That(
                    tooltip,
                    Does.Contain("치명타 배율:"),
                    $"{path} level {level}");
            }
        }
    }

    [Test]
    public void GradeSynergyBulletsUseAuthoredKoreanTerminology()
    {
        BulletData massProduced = Load(
            "Assets/Scripts/Bullet/SO/Rare/Mass Produced.asset");
        BulletData masterpiece = Load(
            "Assets/Scripts/Bullet/SO/Ace/Masterpiece.asset");

        for (int level = 0;
             level <= BulletData.MaximumUpgradeLevel;
             level++)
        {
            string massProducedDescription =
                massProduced.GetDescription(level);
            string masterpieceDescription =
                masterpiece.GetDescription(level);
            string massProducedTooltip = StripRichText(
                massProduced.GetDetailedDescription(level));
            string masterpieceTooltip = StripRichText(
                masterpiece.GetDetailedDescription(level));

            Assert.That(
                massProducedDescription,
                Does.Contain("일반·레어 탄환"),
                $"양산탄 level {level}");
            Assert.That(
                masterpieceDescription,
                Does.Contain("에이스·레전드리 탄환"),
                $"명품탄 level {level}");
            Assert.That(
                massProducedTooltip,
                Does.Not.Contain("매스프로듀스"),
                $"양산탄 level {level}");
            Assert.That(
                masterpieceTooltip,
                Does.Not.Contain("마스터피스 브랜드"),
                $"명품탄 level {level}");
        }
    }

    [Test]
    public void BulletTypeDescriptionsUseFormalSentenceEndings()
    {
        foreach (BulletType bulletType in Enum.GetValues(typeof(BulletType)))
        {
            if (bulletType == BulletType.Normal)
            {
                continue;
            }

            BulletData bullet = ScriptableObject.CreateInstance<BulletData>();
            SerializedObject serialized = new SerializedObject(bullet);
            serialized.FindProperty("bulletType").enumValueIndex =
                (int)bulletType;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                Assert.That(
                    bullet.GetBulletTypeDescription(0),
                    Does.EndWith("합니다."),
                    bulletType.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bullet);
            }
        }
    }

    [Test]
    public void PlayerOnlyEffectsUsePlayerTargetInAllBulletAssets()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:BulletData",
            new[] { BulletAssetRoot });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BulletData bullet = AssetDatabase.LoadAssetAtPath<BulletData>(path);

            for (int level = 0;
                 level <= BulletData.MaximumUpgradeLevel;
                 level++)
            {
                AssertPlayerOnlyTargets(
                    bullet.GetEffects(level),
                    path,
                    level);

                foreach (BulletConditionalEventData conditionalEvent
                         in bullet.GetConditionalEvents(level))
                {
                    if (conditionalEvent != null)
                    {
                        AssertPlayerOnlyTargets(
                            conditionalEvent.Events,
                            path,
                            level);
                    }
                }
            }
        }
    }

    private static string GetSerializedDescription(
        SerializedObject bullet,
        int level)
    {
        if (level <= 0)
        {
            return bullet.FindProperty("description").stringValue;
        }

        SerializedProperty levels = bullet.FindProperty("upgradeLevels");
        Assert.That(
            levels.arraySize,
            Is.GreaterThanOrEqualTo(level),
            bullet.targetObject.name);
        return levels.GetArrayElementAtIndex(level - 1)
            .FindPropertyRelative("description")
            .stringValue;
    }

    private static void AssertPlayerOnlyTargets(
        IReadOnlyList<BulletEffectData> effects,
        string path,
        int level)
    {
        foreach (BulletEffectData effect in effects)
        {
            if (effect != null && IsPlayerOnly(effect.EffectType))
            {
                Assert.That(
                    effect.Target,
                    Is.EqualTo(BulletEffectTarget.FiringPlayer),
                    $"{path} level {level}: {effect.EffectType}");
            }
        }
    }

    private static bool IsPlayerOnly(BulletEffectType effectType)
    {
        return effectType == BulletEffectType.LifeSteal
            || effectType == BulletEffectType.IncreaseMaxHealth
            || effectType == BulletEffectType.DestroyBullet
            || effectType == BulletEffectType.GainGold;
    }

    private static BulletData Load(string path)
    {
        BulletData bullet = AssetDatabase.LoadAssetAtPath<BulletData>(path);
        Assert.That(bullet, Is.Not.Null, path);
        return bullet;
    }

    private static string StripRichText(string value)
    {
        return NormalizeNewlines(
            Regex.Replace(value ?? string.Empty, "<[^>]+>", string.Empty));
    }

    private static string NormalizeNewlines(string value)
    {
        return (value ?? string.Empty).Replace("\r\n", "\n");
    }
}
