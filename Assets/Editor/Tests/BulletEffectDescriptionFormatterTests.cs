using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

public sealed class BulletEffectDescriptionFormatterTests
{
    private const string BulletAssetRoot = "Assets/Scripts/Bullet/SO";

    [Test]
    public void EveryAuthoredEffectIsIncludedInDetailedDescription()
    {
        HashSet<BulletEffectType> authoredTypes =
            new HashSet<BulletEffectType>();
        string[] guids = AssetDatabase.FindAssets(
            "t:BulletData",
            new[] { BulletAssetRoot });

        Assert.That(guids, Is.Not.Empty);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BulletData bullet = AssetDatabase.LoadAssetAtPath<BulletData>(path);

            Assert.That(bullet, Is.Not.Null, path);

            for (int level = 0;
                 level <= BulletData.MaximumUpgradeLevel;
                 level++)
            {
                IReadOnlyList<BulletEffectData> effects =
                    bullet.GetEffects(level);
                IReadOnlyList<BulletConditionalEventData> conditionalEvents =
                    bullet.GetConditionalEvents(level);
                IReadOnlyList<PenetrationChanceData> penetrationChances =
                    bullet.GetPenetrationChances(level);
                string generatedDescription =
                    BulletEffectDescriptionFormatter.Build(
                        effects,
                        conditionalEvents,
                        penetrationChances);

                CollectAndAssertDescriptions(
                    authoredTypes,
                    effects,
                    generatedDescription,
                    path,
                    level);

                foreach (BulletConditionalEventData conditionalEvent
                         in conditionalEvents)
                {
                    if (conditionalEvent != null)
                    {
                        CollectAndAssertDescriptions(
                            authoredTypes,
                            conditionalEvent.Events,
                            generatedDescription,
                            path,
                            level);
                    }
                }

                if (!string.IsNullOrEmpty(generatedDescription))
                {
                    string tooltip = StripRichText(
                        bullet.GetDetailedDescription(level));
                    Assert.That(
                        tooltip,
                        Does.Contain(StripRichText(generatedDescription)),
                        $"{path} level {level}");
                }
            }
        }

        CollectionAssert.AreEquivalent(
            (BulletEffectType[])Enum.GetValues(typeof(BulletEffectType)),
            authoredTypes);
    }

    [Test]
    public void PenetrationDescriptionListsEveryStepChance()
    {
        BulletData judgment = Load(
            "Assets/Scripts/Bullet/SO/Ace/Judgment.asset");

        string description = StripRichText(
            judgment.GetDetailedDescription(3));

        Assert.That(
            description,
            Does.Contain("관통: 최대 2회 (1차 75% / 2차 50%)"));
    }

    [Test]
    public void DebuffDescriptionUsesActualTargetStacksAndChance()
    {
        BulletData venom = Load(
            "Assets/Scripts/Bullet/SO/Rare/Venom.asset");
        BulletData weakness = Load(
            "Assets/Scripts/Bullet/SO/Rare/Weakness.asset");

        string venomDescription = StripRichText(
            venom.GetDetailedDescription(1));
        string weaknessDescription = StripRichText(
            weakness.GetDetailedDescription(3));

        Assert.That(
            venomDescription,
            Does.Contain("명중한 적에게 독 +5"));
        Assert.That(venomDescription, Does.Contain("발동 70%"));
        Assert.That(
            weaknessDescription,
            Does.Contain("명중한 적에게 약화 +6"));
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

    private static void CollectAndAssertDescriptions(
        HashSet<BulletEffectType> authoredTypes,
        IReadOnlyList<BulletEffectData> effects,
        string generatedDescription,
        string path,
        int level)
    {
        foreach (BulletEffectData effect in effects)
        {
            if (effect == null)
            {
                continue;
            }

            authoredTypes.Add(effect.EffectType);
            string effectDescription =
                BulletEffectDescriptionFormatter.DescribeEffect(effect);
            Assert.That(
                effectDescription,
                Is.Not.Empty,
                $"{path} level {level}: {effect.EffectType}");
            Assert.That(
                generatedDescription,
                Does.Contain(effectDescription),
                $"{path} level {level}: {effect.EffectType}");
        }
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
        return Regex.Replace(value ?? string.Empty, "<[^>]+>", string.Empty);
    }
}
