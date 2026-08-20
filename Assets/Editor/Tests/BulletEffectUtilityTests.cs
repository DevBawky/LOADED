using NUnit.Framework;
using UnityEditor;

public sealed class BulletEffectUtilityTests
{
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Seismometer.asset",
        BulletGrade.Rare, 6, 10, 20, 40)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Reverse Shot.asset",
        BulletGrade.Rare, 6, 10, 20, 40)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Evasion.asset",
        BulletGrade.Rare, 6, 10, 20, 40)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Immersion.asset",
        BulletGrade.Rare, 6, 10, 20, 40)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/Spread.asset",
        BulletGrade.Ace, 10, 20, 50, 100)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/Ritual.asset",
        BulletGrade.Ace, 10, 20, 50, 100)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/Tracking.asset",
        BulletGrade.Ace, 10, 20, 50, 100)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/Assassination.asset",
        BulletGrade.Ace, 10, 20, 50, 100)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/High Roller.asset",
        BulletGrade.Ace, 10, 20, 50, 100)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/Mastery.asset",
        BulletGrade.Legendary, 20, 50, 100, 200)]
    [TestCase("Assets/Scripts/Bullet/SO/Legendary/Finale.asset",
        BulletGrade.Legendary, 20, 50, 100, 200)]
    [TestCase("Assets/Scripts/Bullet/SO/Legendary/Flesh For Bone.asset",
        BulletGrade.Legendary, 20, 50, 100, 200)]
    [TestCase("Assets/Scripts/Bullet/SO/Legendary/Repeat_Mark.asset",
        BulletGrade.Legendary, 20, 50, 100, 200)]
    public void NewBulletEconomyMatchesGrade(
        string path,
        BulletGrade grade,
        int price,
        int levelZeroCost,
        int levelOneCost,
        int levelTwoCost)
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(path);

        Assert.That(data, Is.Not.Null);
        Assert.That(data.Grade, Is.EqualTo(grade));
        Assert.That(data.Price, Is.EqualTo(price));
        Assert.That(data.UpgradeLevels.Count,
            Is.EqualTo(BulletData.MaximumUpgradeLevel));
        Assert.That(data.GetUpgradeCost(0), Is.EqualTo(levelZeroCost));
        Assert.That(data.GetUpgradeCost(1), Is.EqualTo(levelOneCost));
        Assert.That(data.GetUpgradeCost(2), Is.EqualTo(levelTwoCost));
        Assert.That(data.GetUpgradeCost(3), Is.Zero);
    }

    [TestCase(100, 100, 100f, 1f)]
    [TestCase(50, 100, 100f, 1.5f)]
    [TestCase(0, 100, 100f, 2f)]
    [TestCase(25, 100, 40f, 1.3f)]
    public void HighRollerScalesWithMissingHealth(
        int currentHealth,
        int maxHealth,
        float maximumBonusPercent,
        float expectedMultiplier)
    {
        float multiplier =
            BulletEffectUtility.GetMissingHealthDamageMultiplier(
                currentHealth,
                maxHealth,
                maximumBonusPercent);

        Assert.That(multiplier, Is.EqualTo(expectedMultiplier).Within(0.0001f));
    }

    [Test]
    public void FleshForBoneDealsThreeTimesItsHealthCost()
    {
        Assert.That(
            BulletEffectUtility.GetFleshForBoneBonusDamage(7f),
            Is.EqualTo(21));
    }

    [Test]
    public void SaturatingAddDoesNotOverflow()
    {
        Assert.That(
            BulletEffectUtility.SaturatingAdd(int.MaxValue, 10),
            Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void ReverseShotAssetFlipsFacingDirection()
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(
            "Assets/Scripts/Bullet/SO/Rare/Reverse Shot.asset");

        Assert.That(data, Is.Not.Null);
        Assert.That(
            BulletEffectUtility.ResolveShotDirection(
                new BulletInstance(data, 0),
                1),
            Is.EqualTo(-1));
    }

    [Test]
    public void NewBulletCapstonesChangeTheirCorePlayPattern()
    {
        BulletData evasion = Load(
            "Assets/Scripts/Bullet/SO/Rare/Evasion.asset");
        BulletData immersion = Load(
            "Assets/Scripts/Bullet/SO/Rare/Immersion.asset");
        BulletData ritual = Load(
            "Assets/Scripts/Bullet/SO/Ace/Ritual.asset");
        BulletData finale = Load(
            "Assets/Scripts/Bullet/SO/Legendary/Finale.asset");

        Assert.That(Find(evasion.GetEffects(3), BulletEffectType.Knockback)
            .KnockbackDistance, Is.EqualTo(3));
        Assert.That(Find(evasion.GetEffects(3), BulletEffectType.RecoilShot)
            .KnockbackDistance, Is.EqualTo(2));
        Assert.That(Find(immersion.GetEffects(3),
            BulletEffectType.Concentration).Amount, Is.EqualTo(10f));
        Assert.That(Find(ritual.GetEffects(3), BulletEffectType.Ritual)
            .StackCount, Is.EqualTo(2));
        Assert.That(Find(ritual.GetEffects(3), BulletEffectType.Ritual)
            .ActivationChance, Is.Zero);
        Assert.That(Find(finale.GetEffects(3), BulletEffectType.Finale)
            .Amount, Is.EqualTo(100f));
    }

    private static BulletData Load(string path)
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(path);
        Assert.That(data, Is.Not.Null, path);
        return data;
    }

    private static BulletEffectData Find(
        System.Collections.Generic.IReadOnlyList<BulletEffectData> effects,
        BulletEffectType type)
    {
        foreach (BulletEffectData effect in effects)
        {
            if (effect != null && effect.EffectType == type)
            {
                return effect;
            }
        }

        Assert.Fail($"Effect {type} was not found.");
        return null;
    }
}
