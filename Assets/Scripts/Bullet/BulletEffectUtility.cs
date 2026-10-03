using System;
using UnityEngine;

internal static class BulletEffectUtility
{
    public const int MarkStatusMask = 1 << (int)StatusEffectType.Mark;
    public const int PoisonStatusMask = 1 << (int)StatusEffectType.Poison;
    public const int StunStatusMask = 1 << (int)StatusEffectType.Stun;
    public const int WeaknessStatusMask = 1 << (int)StatusEffectType.Weakness;
    public const int AllStackableStatusMask = MarkStatusMask
        | PoisonStatusMask
        | StunStatusMask
        | WeaknessStatusMask;
    public const int MixedGradeStatusMask = MarkStatusMask
        | PoisonStatusMask
        | WeaknessStatusMask;
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
        return bullet != null
            && (bullet.BulletType == BulletType.Storm
                || Find(bullet, BulletEffectType.QuickDraw) != null);
    }

    public static bool IsAutoTargetingShot(BulletInstance bullet)
    {
        return bullet != null && bullet.BulletType == BulletType.Sniper;
    }

    public static bool IsTrackingStorm(BulletInstance bullet)
    {
        return bullet != null
            && bullet.BulletType == BulletType.Storm
            && Find(bullet, BulletEffectType.Tracking) != null;
    }

    public static bool IsCataclysmStorm(BulletInstance bullet)
    {
        return bullet != null
            && bullet.BulletType == BulletType.Storm
            && Find(bullet, BulletEffectType.Cataclysm) != null;
    }

    public static bool CanStormTarget(
        BulletInstance bullet,
        int playerLaneIndex,
        int enemyLaneIndex,
        int totalStatusStackCount)
    {
        if (bullet == null || bullet.BulletType != BulletType.Storm)
        {
            return false;
        }

        if (IsCataclysmStorm(bullet))
        {
            return true;
        }

        if (IsTrackingStorm(bullet))
        {
            return totalStatusStackCount > 0;
        }

        return playerLaneIndex == enemyLaneIndex;
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
            || effectType == BulletEffectType.HighRoller
            || effectType == BulletEffectType.RotatePlayer
            || effectType == BulletEffectType.Mastery
            || effectType == BulletEffectType.Cataclysm
            || effectType == BulletEffectType.FocusedShotgun
            || effectType == BulletEffectType.Vanguard
            || effectType == BulletEffectType.Finisher
            || effectType == BulletEffectType.SpecterReturn
            || effectType == BulletEffectType.Necromancy
            || effectType == BulletEffectType.Hunt
            || effectType == BulletEffectType.LockOn
            || effectType == BulletEffectType.Execution
            || effectType == BulletEffectType.Blink
            || effectType == BulletEffectType.RandomPelletDamage;
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

    public static int ResolveFacingDirectionAfterShot(
        BulletInstance bullet,
        int facingDirection)
    {
        int direction = facingDirection >= 0 ? 1 : -1;
        return Find(bullet, BulletEffectType.RotatePlayer) == null
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

    public static int GetInflictedStatusMask(BulletInstance bullet)
    {
        if (bullet == null)
        {
            return 0;
        }

        int mask = 0;

        foreach (BulletEffectData effect in bullet.Effects)
        {
            if (effect == null)
            {
                continue;
            }

            switch (effect.EffectType)
            {
                case BulletEffectType.Mark:
                    mask |= MarkStatusMask;
                    break;
                case BulletEffectType.Poison:
                    mask |= PoisonStatusMask;
                    break;
                case BulletEffectType.Stun:
                    mask |= StunStatusMask;
                    break;
                case BulletEffectType.Weakness:
                    mask |= WeaknessStatusMask;
                    break;
                case BulletEffectType.MixedGrade:
                    mask |= MixedGradeStatusMask;
                    break;
            }
        }

        return mask;
    }

    public static bool IncludesStatus(int mask, StatusEffectType type)
    {
        return (mask & (1 << (int)type)) != 0;
    }

    public static int GetCrescendoStatusStacks(BulletEffectData effect)
    {
        return effect == null ? 0 : Mathf.Max(0, effect.StackCount);
    }

    public static int GetMassProducedAdditionalShots(
        BulletEffectData effect,
        int ownedLowGradeCount)
    {
        return effect == null
            ? 0
            : Mathf.Min(
                Mathf.Max(0, effect.StackCount),
                Mathf.Max(0, ownedLowGradeCount));
    }

    public static float GetMasterpieceCriticalDamageBonus(
        BulletEffectData effect,
        int ownedHighGradeCount)
    {
        if (effect == null)
        {
            return 0f;
        }

        int cappedCount = Mathf.Min(
            Mathf.Max(0, effect.StackCount),
            Mathf.Max(0, ownedHighGradeCount));
        return cappedCount * Mathf.Max(0f, effect.Amount);
    }

    public static float GetCoagulationRecoveryPercent(
        BulletEffectData effect,
        int ownedBloodBulletCount)
    {
        if (effect == null)
        {
            return 0f;
        }

        float recoveryPercent = Mathf.Max(0f, effect.Amount)
            + Mathf.Max(0, ownedBloodBulletCount)
            * Mathf.Max(0, effect.StackCount);
        return Mathf.Min(
            Mathf.Max(0, effect.KnockbackDistance),
            recoveryPercent);
    }

    public static int GetRitualDamageBonus(
        BulletEffectData effect,
        int permanentStacks)
    {
        if (effect == null)
        {
            return 0;
        }

        double bonus = Math.Ceiling(
            Mathf.Max(0, permanentStacks) * Mathf.Max(0f, effect.Amount));
        return bonus >= int.MaxValue ? int.MaxValue : (int)bonus;
    }

    public static float GetJackpotDamageMultiplier(BulletEffectData effect)
    {
        return effect == null ? 1f : Mathf.Max(1f, effect.Amount / 100f);
    }

    public static float GetReturnDamageMultiplier(BulletEffectData effect)
    {
        return effect == null ? 0f : Mathf.Max(0f, effect.Amount / 100f);
    }

    public static float GetRandomPelletDamageMultiplier(
        BulletInstance bullet,
        bool useExpectedValue)
    {
        BulletEffectData effect = Find(
            bullet,
            BulletEffectType.RandomPelletDamage);
        if (effect == null || bullet == null || bullet.Damage <= 0)
        {
            return 1f;
        }

        int minimum = Mathf.Max(0, Mathf.RoundToInt(effect.Amount));
        int maximum = Mathf.Max(minimum, effect.StackCount);
        float rolledDamage = useExpectedValue
            ? (minimum + maximum) * 0.5f
            : UnityEngine.Random.Range(minimum, maximum + 1);
        return rolledDamage / bullet.Damage;
    }

    public static float GetPositionDamageMultiplier(
        BulletInstance bullet,
        bool isFirstPhysicalBullet,
        bool isLastPhysicalBullet)
    {
        BulletEffectData effect = Find(
            bullet,
            BulletEffectType.Vanguard);
        if (effect != null && isFirstPhysicalBullet)
        {
            return 1f + Mathf.Max(0f, effect.Amount) / 100f;
        }

        effect = Find(bullet, BulletEffectType.Finisher);
        return effect != null && isLastPhysicalBullet
            ? 1f + Mathf.Max(0f, effect.Amount) / 100f
            : 1f;
    }

    public static int SaturatingAdd(int left, int right)
    {
        long result = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
        return result >= int.MaxValue ? int.MaxValue : (int)result;
    }
}
