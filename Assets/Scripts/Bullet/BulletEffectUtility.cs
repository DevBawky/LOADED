using System;
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
            || effectType == BulletEffectType.Monopoly
            || effectType == BulletEffectType.Seismometer
            || effectType == BulletEffectType.ReverseShot
            || effectType == BulletEffectType.RecoilShot
            || effectType == BulletEffectType.Finale
            || effectType == BulletEffectType.Spread
            || effectType == BulletEffectType.Alzheimer
            || effectType == BulletEffectType.Concentration
            || effectType == BulletEffectType.Ritual
            || effectType == BulletEffectType.Immersion
            || effectType == BulletEffectType.Tracking
            || effectType == BulletEffectType.Assassination
            || effectType == BulletEffectType.FleshForBone
            || effectType == BulletEffectType.HighRoller;
    }

    public static int ResolveShotDirection(
        BulletInstance bullet,
        int facingDirection)
    {
        int direction = facingDirection >= 0 ? 1 : -1;
        return Find(bullet, BulletEffectType.ReverseShot) == null
            ? direction
            : -direction;
    }

    public static float GetMissingHealthDamageMultiplier(
        int currentHealth,
        int maxHealth,
        float maximumBonusPercent)
    {
        if (maxHealth <= 0 || maximumBonusPercent <= 0f)
        {
            return 1f;
        }

        float missingHealthRatio = Mathf.Clamp01(
            (float)(maxHealth - Mathf.Max(0, currentHealth)) / maxHealth);
        return 1f + missingHealthRatio * maximumBonusPercent / 100f;
    }

    public static int GetFleshForBoneBonusDamage(float healthCost)
    {
        int normalizedCost = Mathf.Max(0, Mathf.RoundToInt(healthCost));
        long bonusDamage = (long)normalizedCost * 3L;
        return bonusDamage >= int.MaxValue
            ? int.MaxValue
            : (int)bonusDamage;
    }

    public static int SaturatingAdd(int left, int right)
    {
        long result = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
        return result >= int.MaxValue ? int.MaxValue : (int)result;
    }
}
