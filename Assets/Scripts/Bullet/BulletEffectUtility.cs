using UnityEngine;

internal static class BulletEffectUtility
{
    public static BulletEffectData Find(
        BulletInstance bullet,
        BulletEffectType effectType)
    {
        if (bullet == null)
        {
            return null;
        }

        foreach (BulletEffectData effect in bullet.Effects)
        {
            if (effect != null && effect.EffectType == effectType)
            {
                return effect;
            }
        }

        return null;
    }

    public static BulletInstance ResolveShot(
        BulletInstance loadedBullet,
        BulletInstance previousResolvedBullet)
    {
        if (loadedBullet == null || previousResolvedBullet == null)
        {
            return loadedBullet;
        }

        return Find(loadedBullet, BulletEffectType.ClonePreviousShot) == null
            ? loadedBullet
            : previousResolvedBullet;
    }

    public static bool IsBoardWideShot(BulletInstance bullet)
    {
        return Find(bullet, BulletEffectType.QuickDraw) != null;
    }

    public static float GetWallImpactTransferPercent(
        BulletEffectData effect,
        int distance)
    {
        if (effect == null)
        {
            return 0f;
        }

        return distance switch
        {
            1 => effect.Amount,
            2 => effect.SecondTransferPercent,
            3 => effect.ThirdTransferPercent,
            _ => 0f
        };
    }

    public static bool IsShotScoped(BulletEffectType effectType)
    {
        return effectType == BulletEffectType.DestroyBullet;
    }

    public static bool IsManagedSpecial(BulletEffectType effectType)
    {
        return effectType == BulletEffectType.Jackpot
            || effectType == BulletEffectType.PowderPouch
            || effectType == BulletEffectType.StackNextShot
            || effectType == BulletEffectType.ClonePreviousShot
            || effectType == BulletEffectType.ChainFire
            || effectType == BulletEffectType.Resonance
            || effectType == BulletEffectType.Gilded
            || effectType == BulletEffectType.Coagulation
            || effectType == BulletEffectType.Heart
            || effectType == BulletEffectType.Saver
            || effectType == BulletEffectType.QuickDraw
            || effectType == BulletEffectType.Loader
            || effectType == BulletEffectType.Rangefinder
            || effectType == BulletEffectType.WallImpact
            || effectType == BulletEffectType.Judgment
            || effectType == BulletEffectType.StatusAmplifier
            || effectType == BulletEffectType.VenomBurst
            || effectType == BulletEffectType.Crescendo
            || effectType == BulletEffectType.Rebate
            || effectType == BulletEffectType.Distributor
            || effectType == BulletEffectType.Focus
            || effectType == BulletEffectType.Charge
            || effectType == BulletEffectType.Accumulator
            || effectType == BulletEffectType.ShellCollector
            || effectType == BulletEffectType.Devourer
            || effectType == BulletEffectType.Legacy
            || effectType == BulletEffectType.Collection
            || effectType == BulletEffectType.MixedGrade
            || effectType == BulletEffectType.Masterpiece
            || effectType == BulletEffectType.MassProduced
            || effectType == BulletEffectType.Monopoly;
    }
}
