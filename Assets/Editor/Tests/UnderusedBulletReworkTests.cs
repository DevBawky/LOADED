using NUnit.Framework;
using UnityEditor;

public sealed class UnderusedBulletReworkTests
{
    [TestCase("Assets/Scripts/Bullet/SO/Legendary/Harvest.asset", "수확탄", BulletType.Combo, BulletEffectType.Collection)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/Return.asset", "귀환탄", BulletType.Piercing, BulletEffectType.Monopoly)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Emergency.asset", "비상탄", BulletType.Ghost, BulletEffectType.Loader)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Jackpot.asset", "잭팟탄", BulletType.Economy, BulletEffectType.Jackpot)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Crescendo.asset", "고조탄", BulletType.Debuff, BulletEffectType.Crescendo)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Resonance.asset", "공명탄", BulletType.Combo, BulletEffectType.Resonance)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Mixed Grade.asset", "혼합탄", BulletType.Debuff, BulletEffectType.MixedGrade)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Mass Produced.asset", "양산탄", BulletType.Growth, BulletEffectType.MassProduced)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/Masterpiece.asset", "명품탄", BulletType.Growth, BulletEffectType.Masterpiece)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Coagulation.asset", "응고탄", BulletType.Blood, BulletEffectType.Coagulation)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/Ritual.asset", "의식", BulletType.Blood, BulletEffectType.Ritual)]
    public void RenamedOrRetypedBulletsHaveExpectedIdentity(
        string path,
        string displayName,
        BulletType bulletType,
        BulletEffectType effectType)
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(path);

        Assert.That(data, Is.Not.Null, path);
        Assert.That(data.DisplayName, Is.EqualTo(displayName));
        Assert.That(data.BulletType, Is.EqualTo(bulletType));
        Assert.That(data.GetEffects(0), Has.Count.EqualTo(1));
        Assert.That(data.GetEffects(0)[0].EffectType, Is.EqualTo(effectType));
        Assert.That(data.CylinderIcon, Is.Not.Null);
    }

    [Test]
    public void MassProducedAndHarvestUseApprovedLevelCaps()
    {
        BulletData massProduced = Load(
            "Assets/Scripts/Bullet/SO/Rare/Mass Produced.asset");
        BulletData harvest = Load(
            "Assets/Scripts/Bullet/SO/Legendary/Harvest.asset");

        Assert.That(EffectStacks(massProduced), Is.EqualTo(new[] { 2, 3, 4, 5 }));
        Assert.That(EffectStacks(harvest), Is.EqualTo(new[] { 1, 2, 3, 5 }));
    }

    [Test]
    public void CrescendoUsesApprovedRandomDebuffStacks()
    {
        BulletData crescendo = Load(
            "Assets/Scripts/Bullet/SO/Rare/Crescendo.asset");

        Assert.That(EffectStacks(crescendo), Is.EqualTo(new[] { 2, 3, 4, 5 }));
        Assert.That(
            Effects(crescendo, effect => effect.Amount),
            Is.EqualTo(new[] { 0f, 0f, 0f, 0f }));
        Assert.That(
            Effects(crescendo, effect => effect.KnockbackDistance),
            Is.EqualTo(new[] { 0, 0, 0, 0 }));
    }

    [Test]
    public void JackpotUsesOneChanceMultiplierAndGoldPayloadPerLevel()
    {
        BulletData jackpot = Load(
            "Assets/Scripts/Bullet/SO/Rare/Jackpot.asset");

        Assert.That(
            Effects(jackpot, effect => effect.ActivationChance),
            Is.EqualTo(new[] { 10f, 12f, 15f, 20f }));
        Assert.That(
            Effects(jackpot, effect => effect.Amount),
            Is.EqualTo(new[] { 300f, 400f, 500f, 700f }));
        Assert.That(
            EffectStacks(jackpot),
            Is.EqualTo(new[] { 3, 4, 5, 7 }));
    }

    [Test]
    public void ChangedIconsMatchTheirRarityFolders()
    {
        AssertIconPath(
            "Assets/Scripts/Bullet/SO/Legendary/Harvest.asset",
            "/Bullet_LineArt/Legendary/Bullet_harvest.png");
        AssertIconPath(
            "Assets/Scripts/Bullet/SO/Ace/Return.asset",
            "/Bullet_LineArt/Ace/Bullet_return.png");
        AssertIconPath(
            "Assets/Scripts/Bullet/SO/Rare/Emergency.asset",
            "/Bullet_LineArt/Rare/Bullet_emergency.png");
        AssertIconPath(
            "Assets/Scripts/Bullet/SO/Rare/Jackpot.asset",
            "/Bullet_LineArt/Rare/Bullet_jackpot.png");
        AssertIconPath(
            "Assets/Scripts/Bullet/SO/Rare/Crescendo.asset",
            "/Bullet_LineArt/Rare/Bullet_crescendo.png");
        AssertIconPath(
            "Assets/Scripts/Bullet/SO/Rare/Resonance.asset",
            "/Bullet_LineArt/Rare/Bullet_resonance.png");
        AssertIconPath(
            "Assets/Scripts/Bullet/SO/Rare/Mixed Grade.asset",
            "/Bullet_LineArt/Rare/Bullet_mixed_grade.png");
        AssertIconPath(
            "Assets/Scripts/Bullet/SO/Rare/Mass Produced.asset",
            "/Bullet_LineArt/Rare/Bullet_mass_produced.png");
        AssertIconPath(
            "Assets/Scripts/Bullet/SO/Ace/Masterpiece.asset",
            "/Bullet_LineArt/Ace/Bullet_masterpiece.png");
        AssertIconPath(
            "Assets/Scripts/Bullet/SO/Rare/Coagulation.asset",
            "/Bullet_LineArt/Rare/Bullet_coagulation.png");
        AssertIconPath(
            "Assets/Scripts/Bullet/SO/Ace/Ritual.asset",
            "/Bullet_LineArt/Ace/Bullet_ritual.png");
    }

    [Test]
    public void CoagulationPreservesItsOriginalCriticalChanceProgression()
    {
        BulletData coagulation = Load(
            "Assets/Scripts/Bullet/SO/Rare/Coagulation.asset");

        Assert.That(
            new[]
            {
                coagulation.GetCriticalChance(0),
                coagulation.GetCriticalChance(1),
                coagulation.GetCriticalChance(2),
                coagulation.GetCriticalChance(3)
            },
            Is.EqualTo(new[] { 10f, 14f, 18f, 22f }));
    }

    private static BulletData Load(string path)
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(path);
        Assert.That(data, Is.Not.Null, path);
        return data;
    }

    private static int[] EffectStacks(BulletData data)
    {
        return Effects(data, effect => effect.StackCount);
    }

    private static T[] Effects<T>(
        BulletData data,
        System.Func<BulletEffectData, T> selector)
    {
        T[] values = new T[4];

        for (int level = 0; level < values.Length; level++)
        {
            values[level] = selector(data.GetEffects(level)[0]);
        }

        return values;
    }

    private static void AssertIconPath(string bulletPath, string suffix)
    {
        BulletData data = Load(bulletPath);
        string iconPath = AssetDatabase.GetAssetPath(data.CylinderIcon)
            .Replace('\\', '/');
        Assert.That(iconPath, Does.EndWith(suffix));
    }
}
