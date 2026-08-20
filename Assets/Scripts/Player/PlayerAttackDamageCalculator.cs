using System;
using UnityEngine;

/// <summary>
/// Calculates final direct-shot damage without owning firing sequence state.
/// </summary>
internal static class PlayerAttackDamageCalculator
{
    public static int Calculate(
        BulletInstance bullet,
        bool isCritical,
        float damageMultiplier,
        int shotIndex,
        bool isLastLoadedShot,
        PlayerHealth playerHealth,
        RelicManager relicManager,
        DeckManager deckManager,
        bool applyRuntimeRelicModifiers)
    {
        if (bullet == null || bullet.Damage <= 0 || playerHealth == null)
        {
            return 0;
        }

        int damage = GetEffectiveBaseDamage(bullet, deckManager);

        if (isCritical)
        {
            damage = MultiplyCeiling(
                damage,
                bullet.CriticalDamageMultiplier);
        }

        int modifiedDamage = playerHealth.ModifyOutgoingAttackDamage(damage);
        double combinedMultiplier = Math.Max(0d, damageMultiplier);

        if (applyRuntimeRelicModifiers && relicManager != null)
        {
            combinedMultiplier *=
                relicManager.GetConditionalFinalDamageMultiplier(
                    shotIndex == 0,
                    isLastLoadedShot);
        }

        return MultiplyCeiling(modifiedDamage, combinedMultiplier);
    }

    internal static int MultiplyCeiling(int damage, double multiplier)
    {
        if (damage <= 0 || multiplier <= 0d || double.IsNaN(multiplier))
        {
            return 0;
        }

        double result = Math.Ceiling(damage * multiplier);
        return double.IsInfinity(result) || result >= int.MaxValue
            ? int.MaxValue
            : (int)Math.Max(0d, result);
    }

    private static int GetEffectiveBaseDamage(
        BulletInstance bullet,
        DeckManager deckManager)
    {
        BulletEffectData crescendoEffect = BulletEffectUtility.Find(
            bullet,
            BulletEffectType.Crescendo);

        if (crescendoEffect == null || deckManager == null)
        {
            return bullet.Damage;
        }

        int otherOwnedBulletCount = Mathf.Max(
            0,
            deckManager.TotalBulletCount
                - (deckManager.Contains(bullet) ? 1 : 0));

        return Mathf.Max(
            0,
            Mathf.CeilToInt(
                bullet.Damage
                - otherOwnedBulletCount * crescendoEffect.Amount));
    }
}
