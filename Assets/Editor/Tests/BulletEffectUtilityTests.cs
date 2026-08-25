using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BulletEffectUtilityTests
{
    [Test]
    public void CloneDoesNotExposeBorrowedRuntimeStateAsStackText()
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(
            "Assets/Scripts/Bullet/SO/Ace/Clone.asset");
        BulletInstance bullet = new BulletInstance(data, 0);
        bullet.AddAbilityStacks(3);

        Assert.That(data, Is.Not.Null);
        Assert.That(bullet.CurrentStackCount, Is.EqualTo(3));
        Assert.That(
            bullet.GetStatusDisplayText(default),
            Is.Empty);
    }

    [Test]
    public void SeismometerAbilityStacksResetAfterFiring()
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(
            "Assets/Scripts/Bullet/SO/Rare/Seismometer.asset");
        BulletInstance bullet = new BulletInstance(data, 0);
        bullet.AddAbilityStacks(4);

        Assert.That(data, Is.Not.Null);
        Assert.That(
            BulletEffectUtility.Find(
                bullet,
                BulletEffectType.Seismometer),
            Is.Not.Null);

        PlayerShoot.ResetPostFireAbilityStacks(bullet, bullet);

        Assert.That(bullet.AbilityStacks, Is.Zero);
    }

    [TestCase("Assets/Scripts/Bullet/SO/Rare/Seismometer.asset",
        BulletGrade.Rare, 6, 10, 20, 40)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Reverse Shot.asset",
        BulletGrade.Rare, 6, 10, 20, 40)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Rotation Shot.asset",
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
    public void RotatePlayerEffectFlipsFacingDirectionAfterShot()
    {
        BulletData data = CreateBulletWithEffect(
            BulletEffectType.RotatePlayer);

        try
        {
            BulletInstance bullet = new BulletInstance(data, 0);

            Assert.That(
                BulletEffectUtility.ResolveFacingDirectionAfterShot(
                    bullet,
                    1),
                Is.EqualTo(-1));
            Assert.That(
                BulletEffectUtility.ResolveFacingDirectionAfterShot(
                    bullet,
                    -1),
                Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void RotationShotAssetRotatesPlayerAfterShot()
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(
            "Assets/Scripts/Bullet/SO/Rare/Rotation Shot.asset");

        Assert.That(data, Is.Not.Null);
        Assert.That(
            BulletEffectUtility.ResolveFacingDirectionAfterShot(
                new BulletInstance(data, 0),
                1),
            Is.EqualTo(-1));
    }

    [Test]
    public void ShotRangePreviewUsesReverseShotDirection()
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(
            "Assets/Scripts/Bullet/SO/Rare/Reverse Shot.asset");
        Assert.That(data, Is.Not.Null);
        BulletInstance bullet = new BulletInstance(data, 0);

        bool resolved = PlayerShotRangePreview.TryResolveLoadedShot(
            new[] { bullet },
            0,
            1,
            out BulletInstance resolvedBullet,
            out int shotDirection);

        Assert.That(resolved, Is.True);
        Assert.That(resolvedBullet, Is.SameAs(bullet));
        Assert.That(shotDirection, Is.EqualTo(-1));
    }

    [Test]
    public void ShotRangePreviewAccountsForEarlierRotationBullet()
    {
        BulletData rotationData = CreateBulletWithEffect(
            BulletEffectType.RotatePlayer);
        BulletData reverseData = AssetDatabase.LoadAssetAtPath<BulletData>(
            "Assets/Scripts/Bullet/SO/Rare/Reverse Shot.asset");
        Assert.That(reverseData, Is.Not.Null);

        try
        {
            BulletInstance reverseBullet = new BulletInstance(reverseData, 0);
            BulletInstance rotationBullet = new BulletInstance(rotationData, 0);

            bool resolved = PlayerShotRangePreview.TryResolveLoadedShot(
                new[] { reverseBullet, rotationBullet },
                0,
                1,
                out BulletInstance resolvedBullet,
                out int shotDirection);

            Assert.That(resolved, Is.True);
            Assert.That(resolvedBullet, Is.SameAs(reverseBullet));
            Assert.That(shotDirection, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(rotationData);
        }
    }

    [Test]
    public void NewBulletDataDefaultsToNormalType()
    {
        BulletData data = ScriptableObject.CreateInstance<BulletData>();

        try
        {
            Assert.That(data.BulletType, Is.EqualTo(BulletType.Normal));
            Assert.That(data.BulletTypeDisplayName, Is.EqualTo("일반"));
            Assert.That(data.ShotCount, Is.EqualTo(1));
            Assert.That(data.DoesNotConsumeReloadTurn, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void GhostBulletReloadDoesNotConsumeTurnButShotStillDoes()
    {
        BulletData data = CreateBulletOfType(BulletType.Ghost);

        try
        {
            BulletInstance bullet = new BulletInstance(data, 0);

            Assert.That(bullet.DoesNotConsumeReloadTurn, Is.True);
            Assert.That(bullet.DoesNotConsumeTurn, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void SniperBulletExposesPenetrationTypeDescription()
    {
        BulletData data = CreateBulletOfType(BulletType.Sniper);

        try
        {
            Assert.That(data.BulletTypeDisplayName, Is.EqualTo("저격"));
            Assert.That(data.GetBulletTypeDescription(0), Does.Contain("관통 확률"));
        }
        finally
        {
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StormBulletUsesBoardWideTargeting()
    {
        BulletData data = CreateBulletOfType(BulletType.Storm);

        try
        {
            Assert.That(
                BulletEffectUtility.IsBoardWideShot(
                    new BulletInstance(data, 0)),
                Is.True);
        }
        finally
        {
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void TyphoonAssetUsesStormBulletType()
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(
            "Assets/Scripts/Bullet/SO/Ace/Typhoon.asset");

        Assert.That(data, Is.Not.Null);
        Assert.That(data.BulletType, Is.EqualTo(BulletType.Storm));
        Assert.That(
            BulletEffectUtility.IsBoardWideShot(
                new BulletInstance(data, 0)),
            Is.True);
    }

    [TestCase("Assets/Scripts/Bullet/SO/Ace/Ghost.asset", BulletType.Ghost)]
    [TestCase("Assets/Scripts/Bullet/SO/Legendary/Pierce.asset", BulletType.Piercing)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Venom.asset", BulletType.Debuff)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Stun.asset", BulletType.Debuff)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Mark.asset", BulletType.Debuff)]
    [TestCase("Assets/Scripts/Bullet/SO/Rare/Weakness.asset", BulletType.Debuff)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/Amplifier.asset", BulletType.Debuff)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/Venom Burst.asset", BulletType.Debuff)]
    [TestCase("Assets/Scripts/Bullet/SO/Ace/Tracking.asset", BulletType.Debuff)]
    public void AuthoredBulletUsesExpectedType(string path, BulletType expected)
    {
        BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(path);

        Assert.That(data, Is.Not.Null);
        Assert.That(data.BulletType, Is.EqualTo(expected));
        Assert.That(data.BulletTypeDisplayName, Does.Not.EndWith("탄"));
        Assert.That(data.GetBulletTypeDescription(0), Is.Not.Empty);
    }

    [Test]
    public void ShotgunUsesConfiguredShotCount()
    {
        BulletData data = CreateBulletOfType(BulletType.Shotgun);
        SerializedObject serialized = new SerializedObject(data);
        serialized.FindProperty("shotgunShotCount").intValue = 4;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        try
        {
            Assert.That(new BulletInstance(data, 0).ShotCount, Is.EqualTo(4));
            Assert.That(data.GetBulletTypeDescription(0), Does.Contain("4발"));
        }
        finally
        {
            Object.DestroyImmediate(data);
        }
    }

    [TestCase(true, 0.2f, false)]
    [TestCase(true, 0f, false)]
    [TestCase(false, 0.2f, true)]
    [TestCase(false, 0f, false)]
    public void RequiredShotgunShotsSkipTheAdditionalShotInterval(
        bool hasRequiredShotgunShot,
        float interval,
        bool expected)
    {
        Assert.That(
            PlayerShoot.ShouldWaitBeforeAdditionalShot(
                hasRequiredShotgunShot,
                interval),
            Is.EqualTo(expected));
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

    private static BulletData CreateBulletOfType(BulletType bulletType)
    {
        BulletData data = ScriptableObject.CreateInstance<BulletData>();
        SerializedObject serialized = new SerializedObject(data);
        serialized.FindProperty("bulletType").enumValueIndex = (int)bulletType;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return data;
    }

    private static BulletData CreateBulletWithEffect(BulletEffectType effectType)
    {
        BulletData data = CreateBulletOfType(BulletType.Normal);
        SerializedObject serialized = new SerializedObject(data);
        SerializedProperty effects = serialized.FindProperty("effects");
        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("effectType").enumValueIndex =
            (int)effectType;
        effect.FindPropertyRelative("target").enumValueIndex =
            (int)BulletEffectTarget.FiringPlayer;
        effect.FindPropertyRelative("activationChance").floatValue = 100f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
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
