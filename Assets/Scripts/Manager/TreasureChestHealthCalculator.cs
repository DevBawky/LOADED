using System;
using System.Collections.Generic;

internal static class TreasureChestHealthCalculator
{
    internal static int CalculateRoundedAdpc(
        IReadOnlyList<BulletInstance> bullets,
        int cylinderCapacity)
    {
        if (bullets == null || bullets.Count == 0)
        {
            return 1;
        }

        long totalDamage = 0L;
        int bulletCount = 0;

        foreach (BulletInstance bullet in bullets)
        {
            if (bullet == null)
            {
                continue;
            }

            long damage = Math.Max(0, bullet.Damage);
            totalDamage = totalDamage > long.MaxValue - damage
                ? long.MaxValue
                : totalDamage + damage;
            bulletCount++;
        }

        return CalculateRoundedAdpc(
            totalDamage,
            bulletCount,
            cylinderCapacity);
    }

    internal static int CalculateRoundedAdpc(
        long totalDamage,
        int bulletCount,
        int cylinderCapacity)
    {
        int safeBulletCount = Math.Max(0, bulletCount);
        int safeCapacity = Math.Max(1, cylinderCapacity);
        int cylinderCount = Math.Max(
            1,
            (safeBulletCount + safeCapacity - 1) / safeCapacity);
        long averageDamage = Math.Max(
            1L,
            (Math.Max(0L, totalDamage) + cylinderCount - 1L)
                / cylinderCount);

        return RoundDownToLeadingDigit(averageDamage);
    }

    internal static int RoundDownToLeadingDigit(long value)
    {
        long clamped = Math.Clamp(value, 1L, int.MaxValue);
        long magnitude = 1L;

        while (clamped / magnitude >= 10L)
        {
            magnitude *= 10L;
        }

        long rounded = clamped / magnitude * magnitude;
        return (int)Math.Clamp(rounded, 1L, int.MaxValue);
    }
}
