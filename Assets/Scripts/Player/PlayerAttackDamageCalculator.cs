using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class PhysicalBulletCriticalScope<TTarget>
    where TTarget : class
{
    private readonly HashSet<TTarget> exposedTargets =
        new HashSet<TTarget>();

    internal void Begin()
    {
        exposedTargets.Clear();
    }

    internal bool Resolve(
        bool rolledCritical,
        TTarget target,
        bool targetIsExposed)
    {
        if (target != null && targetIsExposed)
        {
            exposedTargets.Add(target);
        }

        return PlayerAttackDamageCalculator.ResolveCriticalForTarget(
            rolledCritical,
            targetIsExposed
                || target != null && exposedTargets.Contains(target));
    }

    internal void Complete(Action<TTarget> consumeExposed)
    {
        try
        {
            if (consumeExposed == null)
            {
                return;
            }

            foreach (TTarget target in exposedTargets)
            {
                consumeExposed(target);
            }
        }
        finally
        {
            exposedTargets.Clear();
        }
    }
}

/// <summary>
/// Calculates final direct-shot damage without owning firing sequence state.
/// </summary>
internal static class PlayerAttackDamageCalculator
{
    internal static bool ResolveCriticalForTarget(
        bool rolledCritical,
        bool targetIsExposed)
    {
        return rolledCritical || targetIsExposed;
    }

    public static int Calculate(
        BulletInstance bullet,
        bool isCritical,
        float damageMultiplier,
        int shotIndex,
        bool isLastLoadedShot,
        PlayerHealth playerHealth,
        RelicManager relicManager,
        DeckManager deckManager,
        bool applyRuntimeRelicModifiers,
        float criticalDamageMultiplierBonus = 0f,
        float battleDamageMultiplier = 1f)
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
                bullet.CriticalDamageMultiplier
                    + Mathf.Max(0f, criticalDamageMultiplierBonus));
        }

        int modifiedDamage = playerHealth.ModifyOutgoingAttackDamage(damage);
        double combinedMultiplier = Math.Max(0d, damageMultiplier)
            * Math.Max(0d, battleDamageMultiplier);

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
        int baseDamage = bullet.Damage;
        BulletEffectData fleshForBoneEffect = BulletEffectUtility.Find(
            bullet,
            BulletEffectType.FleshForBone);

        if (fleshForBoneEffect != null)
        {
            baseDamage = BulletEffectUtility.SaturatingAdd(
                baseDamage,
                BulletEffectUtility.GetFleshForBoneBonusDamage(
                    fleshForBoneEffect.Amount));
        }

        BulletEffectData ritualEffect = BulletEffectUtility.Find(
            bullet,
            BulletEffectType.Ritual);

        if (ritualEffect != null)
        {
            baseDamage = BulletEffectUtility.SaturatingAdd(
                baseDamage,
                BulletEffectUtility.GetRitualDamageBonus(
                    ritualEffect,
                    bullet.PermanentStacks));
        }

        return baseDamage;
    }
}
