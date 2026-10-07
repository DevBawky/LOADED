using System.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class SniperStormReworkTests
{
    [Test]
    public void SniperAssetsUseTheirNewTargetingMarkers()
    {
        AssertSniper(
            "Assets/Scripts/Bullet/SO/Rare/Sniping.asset",
            "스나이핑",
            BulletEffectType.Rangefinder);
        AssertSniper(
            "Assets/Scripts/Bullet/SO/Ace/Assassination.asset",
            "암살",
            BulletEffectType.Assassination);
        AssertSniper(
            "Assets/Scripts/Bullet/SO/Legendary/Mastery.asset",
            "통달",
            BulletEffectType.Mastery);
    }

    [Test]
    public void MasteryUsesApprovedPerShotFactors()
    {
        BulletData data = Load(
            "Assets/Scripts/Bullet/SO/Legendary/Mastery.asset");
        float[] expected = { 1.2f, 1.35f, 1.5f, 2f };

        for (int level = 0; level <= BulletData.MaximumUpgradeLevel; level++)
        {
            BulletInstance bullet = new BulletInstance(data, 0);
            for (int upgrade = 0; upgrade < level; upgrade++)
            {
                Assert.That(bullet.TryUpgrade(), Is.True);
            }

            BulletEffectData effect = BulletEffectUtility.Find(
                bullet,
                BulletEffectType.Mastery);
            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.Amount, Is.EqualTo(expected[level])
                .Within(0.0001f));
        }
    }

    [Test]
    public void AssassinationKillMarksAllEnemies()
    {
        BulletData data = Load(
            "Assets/Scripts/Bullet/SO/Ace/Assassination.asset");
        int[] expectedStacks = { 1, 1, 2, 3 };

        for (int level = 0; level <= BulletData.MaximumUpgradeLevel; level++)
        {
            BulletConditionalEventData conditional = data
                .GetConditionalEvents(level)
                .Single();
            BulletEffectData mark = conditional.Events.Single();

            Assert.That(conditional.Trigger,
                Is.EqualTo(BulletConditionalTrigger.EnemyDefeated));
            Assert.That(mark.EffectType, Is.EqualTo(BulletEffectType.Mark));
            Assert.That(mark.Target, Is.EqualTo(BulletEffectTarget.AllEnemies));
            Assert.That(mark.StackCount, Is.EqualTo(expectedStacks[level]));
        }
    }

    [Test]
    public void StormPoliciesMatchTrackingTyphoonAndCataclysmRoles()
    {
        BulletInstance tracking = Instance(
            "Assets/Scripts/Bullet/SO/Ace/Tracking.asset");
        BulletInstance typhoon = Instance(
            "Assets/Scripts/Bullet/SO/Ace/Typhoon.asset");
        BulletInstance cataclysm = Instance(
            "Assets/Scripts/Bullet/SO/Legendary/Cataclysm.asset");

        Assert.That(BulletEffectUtility.CanStormTarget(
            tracking, 0, 2, 1), Is.True);
        Assert.That(BulletEffectUtility.CanStormTarget(
            tracking, 0, 0, 0), Is.False);
        Assert.That(BulletEffectUtility.CanStormTarget(
            typhoon, 1, 1, 0), Is.True);
        Assert.That(BulletEffectUtility.CanStormTarget(
            typhoon, 1, 2, 5), Is.False);
        Assert.That(BulletEffectUtility.CanStormTarget(
            cataclysm, 1, 2, 0), Is.True);
    }

    private static void AssertSniper(
        string path,
        string expectedName,
        BulletEffectType marker)
    {
        BulletData data = Load(path);
        BulletInstance bullet = new BulletInstance(data, 0);

        Assert.That(data.DisplayName, Is.EqualTo(expectedName));
        Assert.That(data.BulletType, Is.EqualTo(BulletType.Sniper));
        Assert.That(BulletEffectUtility.Find(bullet, marker), Is.Not.Null);
    }

    private static BulletData Load(string path)
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(path);
        Assert.That(data, Is.Not.Null, path);
        return data;
    }

    private static BulletInstance Instance(string path)
    {
        return new BulletInstance(Load(path), 0);
    }
}
