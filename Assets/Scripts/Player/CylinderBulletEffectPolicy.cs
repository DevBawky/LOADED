using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides whether a loaded bullet has a currently active stat/effect bonus.
/// The cylinder UI only renders this decision.
/// </summary>
internal static class CylinderBulletEffectPolicy
{
    public static bool ShouldShow(
        IReadOnlyList<BulletInstance> loadedBullets,
        int bulletIndex,
        DeckManager deckManager,
        PlayerShoot playerShoot,
        CurrencyManager currencyManager,
        PlayerHealth playerHealth)
    {
        if (loadedBullets == null
            || bulletIndex < 0
            || bulletIndex >= loadedBullets.Count)
        {
            return false;
        }

        BulletInstance bullet = loadedBullets[bulletIndex];

        if (bullet == null)
        {
            return false;
        }

        // Stored damage belongs to a distributor and only lights bullets the
        // distributor will actually enhance.
        if (bullet.TemporaryDamageBonus > 0f
            || bullet.TemporaryCriticalChanceBonus > 0f
            || HasActiveStackStatBonus(bullet)
            || HasActiveConditionalStatBonus(
                bullet,
                loadedBullets,
                bulletIndex,
                deckManager,
                playerShoot,
                currencyManager,
                playerHealth))
        {
            return true;
        }

        return WillReceiveEarlierBulletBuff(loadedBullets, bulletIndex);
    }

    private static bool WillReceiveEarlierBulletBuff(
        IReadOnlyList<BulletInstance> loadedBullets,
        int targetIndex)
    {
        float pendingStackBonus = 0f;

        // The cylinder fires from the highest index down. Simulate only
        // ordering effects that can enhance a later bullet before it fires.
        for (int sourceIndex = loadedBullets.Count - 1;
             sourceIndex > targetIndex;
             sourceIndex--)
        {
            BulletInstance source = loadedBullets[sourceIndex];

            if (source == null)
            {
                continue;
            }

            BulletEffectData powderEffect = BulletEffectUtility.Find(
                source,
                BulletEffectType.PowderPouch);

            if (powderEffect != null && powderEffect.Amount > 0f)
            {
                return true;
            }

            BulletEffectData stackEffect = BulletEffectUtility.Find(
                source,
                BulletEffectType.StackNextShot);

            if (stackEffect != null)
            {
                pendingStackBonus += Mathf.Max(0f, stackEffect.Amount);
                continue;
            }

            BulletEffectData distributorEffect = BulletEffectUtility.Find(
                source,
                BulletEffectType.Distributor);

            if (distributorEffect != null)
            {
                if (distributorEffect.Amount > 0f
                    && (pendingStackBonus > 0f
                        || source.StoredDamageBonus > 0f))
                {
                    return true;
                }

                pendingStackBonus = 0f;
                continue;
            }

            pendingStackBonus = 0f;
        }

        BulletInstance target = loadedBullets[targetIndex];
        return pendingStackBonus > 0f
            && !HasEffect(target, BulletEffectType.PowderPouch)
            && !HasEffect(target, BulletEffectType.StackNextShot)
            && !HasEffect(target, BulletEffectType.Distributor);
    }

    private static bool HasActiveStackStatBonus(BulletInstance bullet)
    {
        if (bullet.AbilityStacks > 0
            && (HasPositiveEffect(bullet, BulletEffectType.Focus)
                || HasPositiveEffect(
                    bullet,
                    BulletEffectType.Accumulator)))
        {
            return true;
        }

        if (bullet.PermanentStacks > 0
            && (HasPositiveEffect(bullet, BulletEffectType.Devourer)
                || HasPositiveEffect(bullet, BulletEffectType.Legacy)))
        {
            return true;
        }

        return bullet.ShotsObservedWhileLoaded > 0
            && HasPositiveEffect(bullet, BulletEffectType.Charge);
    }

    private static bool HasActiveConditionalStatBonus(
        BulletInstance bullet,
        IReadOnlyList<BulletInstance> loadedBullets,
        int bulletIndex,
        DeckManager deckManager,
        PlayerShoot playerShoot,
        CurrencyManager currencyManager,
        PlayerHealth playerHealth)
    {
        BulletEffectData effect = BulletEffectUtility.Find(
            bullet,
            BulletEffectType.Jackpot);

        if (effect != null && effect.Amount > 100f && bulletIndex == 0)
        {
            return true;
        }

        effect = BulletEffectUtility.Find(bullet, BulletEffectType.Resonance);

        if (effect != null && effect.Amount > 0f
            && CountOtherLoadedEffects(
                loadedBullets,
                bulletIndex,
                BulletEffectType.Resonance) > 0)
        {
            return true;
        }

        effect = BulletEffectUtility.Find(bullet, BulletEffectType.Loader);

        if (effect != null && effect.Amount > 0f
            && deckManager != null
            && deckManager.MaxReloadAmount
                > (playerShoot == null
                    ? loadedBullets.Count
                    : playerShoot.InitialLoadedBulletCount))
        {
            return true;
        }

        effect = BulletEffectUtility.Find(bullet, BulletEffectType.Crescendo);

        if (effect != null && effect.Amount > 0f
            && deckManager != null
            && deckManager.TotalBulletCount
                < DeckManager.MaximumOwnedBulletCount)
        {
            return true;
        }

        effect = BulletEffectUtility.Find(bullet, BulletEffectType.MixedGrade);

        if (effect != null && effect.Amount > 0f
            && HasOtherLoadedGrade(loadedBullets, bullet, bulletIndex))
        {
            return true;
        }

        effect = BulletEffectUtility.Find(bullet, BulletEffectType.Gilded);

        if (effect != null && effect.Amount > 0f
            && currencyManager != null
            && currencyManager.CurrentMoney
                >= Mathf.Max(1, effect.StackCount))
        {
            return true;
        }

        effect = BulletEffectUtility.Find(bullet, BulletEffectType.Coagulation);

        if (effect != null && effect.Amount > 0f
            && playerHealth != null
            && playerHealth.MaxHealth > 0
            && 100f * (playerHealth.MaxHealth - playerHealth.CurrentHealth)
                / playerHealth.MaxHealth
                >= Mathf.Max(1, effect.StackCount))
        {
            return true;
        }

        effect = BulletEffectUtility.Find(bullet, BulletEffectType.Heart);

        if (effect != null && effect.Amount > 0f
            && playerHealth != null
            && playerHealth.MaxHealth >= Mathf.Max(1, effect.StackCount))
        {
            return true;
        }

        return HasActiveOwnedCollectionBonus(bullet, deckManager);
    }

    private static bool HasActiveOwnedCollectionBonus(
        BulletInstance bullet,
        DeckManager deckManager)
    {
        if (deckManager == null)
        {
            return false;
        }

        foreach (BulletEffectData effect in bullet.Effects)
        {
            if (effect == null || effect.Amount <= 0f)
            {
                continue;
            }

            switch (effect.EffectType)
            {
                case BulletEffectType.Collection:
                    return CountDistinctOwnedBulletTypes(deckManager) > 0;
                case BulletEffectType.Masterpiece:
                    return CountOwnedGrades(
                        deckManager,
                        BulletGrade.Ace,
                        BulletGrade.Legendary) > 0;
                case BulletEffectType.MassProduced:
                    return CountOwnedGrades(
                        deckManager,
                        BulletGrade.Normal,
                        BulletGrade.Rare) > 0;
                case BulletEffectType.Monopoly:
                    return deckManager.Deck.Count
                        + deckManager.LoadedBullets.Count
                        + deckManager.Graveyard.Count > 0;
            }
        }

        return false;
    }

    private static int CountDistinctOwnedBulletTypes(DeckManager deckManager)
    {
        HashSet<BulletData> types = new HashSet<BulletData>();
        AddOwnedBulletTypes(types, deckManager.Deck);
        AddOwnedBulletTypes(types, deckManager.LoadedBullets);
        AddOwnedBulletTypes(types, deckManager.Graveyard);
        return types.Count;
    }

    private static void AddOwnedBulletTypes(
        HashSet<BulletData> types,
        IReadOnlyList<BulletInstance> bullets)
    {
        foreach (BulletInstance bullet in bullets)
        {
            if (bullet?.Data != null)
            {
                types.Add(bullet.Data);
            }
        }
    }

    private static int CountOwnedGrades(
        DeckManager deckManager,
        BulletGrade first,
        BulletGrade second)
    {
        return CountGrades(deckManager.Deck, first, second)
            + CountGrades(deckManager.LoadedBullets, first, second)
            + CountGrades(deckManager.Graveyard, first, second);
    }

    private static int CountGrades(
        IReadOnlyList<BulletInstance> bullets,
        BulletGrade first,
        BulletGrade second)
    {
        int count = 0;

        foreach (BulletInstance bullet in bullets)
        {
            if (bullet != null
                && (bullet.Grade == first || bullet.Grade == second))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountOtherLoadedEffects(
        IReadOnlyList<BulletInstance> bullets,
        int targetIndex,
        BulletEffectType effectType)
    {
        int count = 0;

        // When targetIndex fires it has already been removed, so only lower
        // indices are still in DeckManager.LoadedBullets.
        for (int index = 0; index < targetIndex; index++)
        {
            if (HasEffect(bullets[index], effectType))
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasOtherLoadedGrade(
        IReadOnlyList<BulletInstance> bullets,
        BulletInstance target,
        int targetIndex)
    {
        for (int index = 0; index < targetIndex; index++)
        {
            BulletInstance bullet = bullets[index];

            if (bullet != null && bullet.Grade != target.Grade)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPositiveEffect(
        BulletInstance bullet,
        BulletEffectType effectType)
    {
        BulletEffectData effect = BulletEffectUtility.Find(
            bullet,
            effectType);
        return effect != null && effect.Amount > 0f;
    }

    private static bool HasEffect(
        BulletInstance bullet,
        BulletEffectType effectType)
    {
        return BulletEffectUtility.Find(bullet, effectType) != null;
    }
}
