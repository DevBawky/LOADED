using System.Collections.Generic;
using UnityEngine;

internal readonly struct BulletCombatResourceSnapshot
{
    public BulletCombatResourceSnapshot(
        int currentGold,
        int currentHealth,
        int maxHealth)
    {
        CurrentGold = Mathf.Max(0, currentGold);
        CurrentHealth = Mathf.Max(0, currentHealth);
        MaxHealth = Mathf.Max(0, maxHealth);
    }

    public int CurrentGold { get; }
    public int CurrentHealth { get; }
    public int MaxHealth { get; }
}

internal readonly struct BulletChamberSnapshot
{
    public BulletChamberSnapshot(
        int initialLoadedCount,
        int maxChambers,
        bool isLoaded,
        bool isLastChamber,
        bool isClone)
    {
        InitialLoadedCount = Mathf.Max(0, initialLoadedCount);
        MaxChambers = Mathf.Max(0, maxChambers);
        IsLoaded = isLoaded;
        IsLastChamber = isLastChamber;
        IsClone = isClone;
    }

    public int InitialLoadedCount { get; }
    public int MaxChambers { get; }
    public bool IsLoaded { get; }
    public bool IsLastChamber { get; }
    public bool IsClone { get; }
}

internal readonly struct BulletRuntimeCombatSnapshot
{
    public BulletRuntimeCombatSnapshot(
        int abilityStacks,
        int permanentStacks,
        int shotsObservedWhileLoaded,
        float temporaryDamageBonus,
        float temporaryCriticalChanceBonus)
    {
        AbilityStacks = Mathf.Max(0, abilityStacks);
        PermanentStacks = Mathf.Max(0, permanentStacks);
        ShotsObservedWhileLoaded = Mathf.Max(0, shotsObservedWhileLoaded);
        TemporaryDamageBonus = Mathf.Max(0f, temporaryDamageBonus);
        TemporaryCriticalChanceBonus = Mathf.Max(
            0f,
            temporaryCriticalChanceBonus);
    }

    public int AbilityStacks { get; }
    public int PermanentStacks { get; }
    public int ShotsObservedWhileLoaded { get; }
    public float TemporaryDamageBonus { get; }
    public float TemporaryCriticalChanceBonus { get; }
}

internal readonly struct BulletOwnedCompositionSnapshot
{
    public BulletOwnedCompositionSnapshot(
        int otherResonanceCount,
        int distinctOwnedBulletTypeCount,
        int otherLoadedGradeCount,
        int ownedHighGradeCount,
        int ownedLowGradeCount,
        int mostCommonOwnedGradeCount)
    {
        OtherResonanceCount = Mathf.Max(0, otherResonanceCount);
        DistinctOwnedBulletTypeCount = Mathf.Max(
            0,
            distinctOwnedBulletTypeCount);
        OtherLoadedGradeCount = Mathf.Max(0, otherLoadedGradeCount);
        OwnedHighGradeCount = Mathf.Max(0, ownedHighGradeCount);
        OwnedLowGradeCount = Mathf.Max(0, ownedLowGradeCount);
        MostCommonOwnedGradeCount = Mathf.Max(
            0,
            mostCommonOwnedGradeCount);
    }

    public int OtherResonanceCount { get; }
    public int DistinctOwnedBulletTypeCount { get; }
    public int OtherLoadedGradeCount { get; }
    public int OwnedHighGradeCount { get; }
    public int OwnedLowGradeCount { get; }
    public int MostCommonOwnedGradeCount { get; }

    public static BulletOwnedCompositionSnapshot Capture(
        BulletInstance firedBullet,
        IReadOnlyList<BulletInstance> loadedBullets,
        IReadOnlyList<BulletInstance> ownedBullets)
    {
        return Capture(
            firedBullet,
            loadedBullets,
            ownedBullets,
            (HashSet<BulletData>)null,
            (int[])null);
    }

    public static BulletOwnedCompositionSnapshot Capture(
        BulletInstance firedBullet,
        IReadOnlyList<BulletInstance> loadedBullets,
        IReadOnlyList<BulletInstance> ownedBullets,
        HashSet<BulletData> ownedTypeBuffer,
        int[] ownedGradeCountBuffer)
    {
        HashSet<BulletData> ownedTypes = ownedTypeBuffer
            ?? new HashSet<BulletData>();
        int[] ownedGradeCounts = ownedGradeCountBuffer?.Length >= 4
            ? ownedGradeCountBuffer
            : new int[4];
        ResetBuffers(ownedTypes, ownedGradeCounts);
        AccumulateOwned(ownedBullets, ownedTypes, ownedGradeCounts);
        return Create(
            firedBullet,
            loadedBullets,
            ownedTypes.Count,
            ownedGradeCounts);
    }

    public static BulletOwnedCompositionSnapshot Capture(
        BulletInstance firedBullet,
        IReadOnlyList<BulletInstance> loadedBullets,
        IReadOnlyList<BulletInstance> deckBullets,
        IReadOnlyList<BulletInstance> loadedOwnedBullets,
        IReadOnlyList<BulletInstance> graveyardBullets)
    {
        HashSet<BulletData> ownedTypes = new HashSet<BulletData>();
        int[] ownedGradeCounts = new int[4];
        AccumulateOwned(deckBullets, ownedTypes, ownedGradeCounts);
        AccumulateOwned(loadedOwnedBullets, ownedTypes, ownedGradeCounts);
        AccumulateOwned(graveyardBullets, ownedTypes, ownedGradeCounts);
        return Create(
            firedBullet,
            loadedBullets,
            ownedTypes.Count,
            ownedGradeCounts);
    }

    private static BulletOwnedCompositionSnapshot Create(
        BulletInstance firedBullet,
        IReadOnlyList<BulletInstance> loadedBullets,
        int distinctOwnedBulletTypeCount,
        int[] ownedGradeCounts)
    {
        int otherResonanceCount = 0;
        int otherLoadedGradeCount = 0;

        if (loadedBullets != null)
        {
            foreach (BulletInstance bullet in loadedBullets)
            {
                if (bullet == null || ReferenceEquals(bullet, firedBullet))
                {
                    continue;
                }

                if (BulletEffectUtility.Find(
                        bullet,
                        BulletEffectType.Resonance) != null)
                {
                    otherResonanceCount++;
                }

                if (firedBullet != null && bullet.Grade != firedBullet.Grade)
                {
                    otherLoadedGradeCount++;
                }
            }
        }

        int ownedLowGradeCount = ownedGradeCounts[0] + ownedGradeCounts[1];
        int ownedHighGradeCount = ownedGradeCounts[2] + ownedGradeCounts[3];
        int mostCommonOwnedGradeCount = Mathf.Max(
            ownedGradeCounts[0],
            ownedGradeCounts[1],
            ownedGradeCounts[2],
            ownedGradeCounts[3]);
        return new BulletOwnedCompositionSnapshot(
            otherResonanceCount,
            distinctOwnedBulletTypeCount,
            otherLoadedGradeCount,
            ownedHighGradeCount,
            ownedLowGradeCount,
            mostCommonOwnedGradeCount);
    }

    private static void AccumulateOwned(
        IReadOnlyList<BulletInstance> bullets,
        HashSet<BulletData> ownedTypes,
        int[] ownedGradeCounts)
    {
        if (bullets == null)
        {
            return;
        }

        foreach (BulletInstance bullet in bullets)
        {
            if (bullet == null)
            {
                continue;
            }

            if (bullet.Data != null)
            {
                ownedTypes.Add(bullet.Data);
            }

            int gradeIndex = Mathf.Clamp((int)bullet.Grade, 0, 3);
            ownedGradeCounts[gradeIndex]++;
        }
    }

    private static void ResetBuffers(
        HashSet<BulletData> ownedTypes,
        int[] ownedGradeCounts)
    {
        ownedTypes.Clear();
        ownedGradeCounts[0] = 0;
        ownedGradeCounts[1] = 0;
        ownedGradeCounts[2] = 0;
        ownedGradeCounts[3] = 0;
    }
}

internal readonly struct BulletDynamicCombatContext
{
    public BulletDynamicCombatContext(
        BulletCombatResourceSnapshot resources,
        BulletChamberSnapshot chamber,
        BulletRuntimeCombatSnapshot runtime,
        BulletOwnedCompositionSnapshot composition)
    {
        CurrentGold = resources.CurrentGold;
        CurrentHealth = resources.CurrentHealth;
        MaxHealth = resources.MaxHealth;
        InitialLoadedCount = chamber.InitialLoadedCount;
        MaxChambers = chamber.MaxChambers;
        IsLoaded = chamber.IsLoaded;
        IsLastChamber = chamber.IsLastChamber;
        IsClone = chamber.IsClone;
        AbilityStacks = runtime.AbilityStacks;
        PermanentStacks = runtime.PermanentStacks;
        ShotsObservedWhileLoaded = runtime.ShotsObservedWhileLoaded;
        TemporaryDamageBonus = runtime.TemporaryDamageBonus;
        TemporaryCriticalChanceBonus = runtime.TemporaryCriticalChanceBonus;
        OtherResonanceCount = composition.OtherResonanceCount;
        DistinctOwnedBulletTypeCount =
            composition.DistinctOwnedBulletTypeCount;
        OtherLoadedGradeCount = composition.OtherLoadedGradeCount;
        OwnedHighGradeCount = composition.OwnedHighGradeCount;
        OwnedLowGradeCount = composition.OwnedLowGradeCount;
        MostCommonOwnedGradeCount = composition.MostCommonOwnedGradeCount;
    }

    public int CurrentGold { get; }
    public int CurrentHealth { get; }
    public int MaxHealth { get; }
    public int InitialLoadedCount { get; }
    public int MaxChambers { get; }
    public bool IsLoaded { get; }
    public bool IsLastChamber { get; }
    public bool IsClone { get; }
    public int AbilityStacks { get; }
    public int PermanentStacks { get; }
    public int ShotsObservedWhileLoaded { get; }
    public float TemporaryDamageBonus { get; }
    public float TemporaryCriticalChanceBonus { get; }
    public int OtherResonanceCount { get; }
    public int DistinctOwnedBulletTypeCount { get; }
    public int OtherLoadedGradeCount { get; }
    public int OwnedHighGradeCount { get; }
    public int OwnedLowGradeCount { get; }
    public int MostCommonOwnedGradeCount { get; }
    public int EmptyChamberCount => Mathf.Max(
        0,
        MaxChambers - InitialLoadedCount);
    public float MissingHealthPercent => MaxHealth <= 0
        ? 0f
        : 100f * (MaxHealth - CurrentHealth) / MaxHealth;
}

internal readonly struct BulletDynamicCombatResult
{
    public BulletDynamicCombatResult(
        float damageMultiplier,
        float criticalChanceBonus)
    {
        DamageMultiplier = damageMultiplier;
        CriticalChanceBonus = criticalChanceBonus;
    }

    public float DamageMultiplier { get; }
    public float CriticalChanceBonus { get; }
}

internal readonly struct BulletTargetDamageContext
{
    public BulletTargetDamageContext(
        int tileDistance,
        int totalStatusStackCount,
        bool wasHitThisTurn)
    {
        TileDistance = tileDistance;
        TotalStatusStackCount = Mathf.Max(0, totalStatusStackCount);
        WasHitThisTurn = wasHitThisTurn;
    }

    public int TileDistance { get; }
    public int TotalStatusStackCount { get; }
    public bool WasHitThisTurn { get; }
}

internal static class BulletDynamicCombatRules
{
    public static BulletDynamicCombatResult Evaluate(
        BulletInstance firedBullet,
        BulletInstance resolvedBullet,
        BulletDynamicCombatContext context)
    {
        return new BulletDynamicCombatResult(
            CalculateDamageMultiplier(
                firedBullet,
                resolvedBullet,
                context),
            CalculateCriticalChanceBonus(
                resolvedBullet,
                context));
    }

    public static float CalculateDamageMultiplier(
        BulletInstance firedBullet,
        BulletInstance resolvedBullet,
        BulletDynamicCombatContext context)
    {
        float multiplier = 1f;
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Seismometer),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.HighRoller),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Jackpot),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Resonance),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                firedBullet,
                BulletEffectType.ClonePreviousShot),
            context);
        multiplier *= 1f + context.TemporaryDamageBonus;
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Gilded),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Heart),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Loader),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Charge),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Accumulator),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Devourer),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Legacy),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Collection),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.MixedGrade),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Masterpiece),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.MassProduced),
            context);
        multiplier *= GetDamageFactor(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Monopoly),
            context);

        return multiplier;
    }

    public static float CalculateCriticalChanceBonus(
        BulletInstance resolvedBullet,
        BulletDynamicCombatContext context)
    {
        float criticalChanceBonus = context.TemporaryCriticalChanceBonus;
        criticalChanceBonus += GetCriticalChanceBonus(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Coagulation),
            context);
        criticalChanceBonus += GetCriticalChanceBonus(
            BulletEffectUtility.Find(
                resolvedBullet,
                BulletEffectType.Focus),
            context);

        return criticalChanceBonus;
    }

    public static float CalculateTargetDamageMultiplier(
        BulletInstance bullet,
        BulletTargetDamageContext context)
    {
        float multiplier = 1f;
        BulletEffectData effect = BulletEffectUtility.Find(
            bullet,
            BulletEffectType.Rangefinder);

        if (effect != null && context.TileDistance >= 0)
        {
            multiplier *= 1f
                + context.TileDistance * effect.Amount / 100f;
        }

        effect = BulletEffectUtility.Find(
            bullet,
            BulletEffectType.Judgment);

        if (effect != null)
        {
            multiplier *= 1f
                + context.TotalStatusStackCount * effect.Amount / 100f;
        }

        effect = BulletEffectUtility.Find(
            bullet,
            BulletEffectType.Assassination);

        if (effect != null && context.WasHitThisTurn)
        {
            multiplier *= 1f + Mathf.Max(0f, effect.Amount) / 100f;
        }

        return multiplier;
    }

    public static float GetDamageFactor(
        BulletEffectData effect,
        BulletDynamicCombatContext context)
    {
        if (effect == null)
        {
            return 1f;
        }

        if (effect.EffectType == BulletEffectType.HighRoller)
        {
            return BulletEffectUtility.GetMissingHealthDamageMultiplier(
                context.CurrentHealth,
                context.MaxHealth,
                effect.Amount);
        }

        if (!TryGetEffectUnitCount(effect, context, out int unitCount))
        {
            return 1f;
        }

        switch (effect.EffectType)
        {
            case BulletEffectType.Jackpot:
            case BulletEffectType.ClonePreviousShot:
                return unitCount > 0
                    ? Mathf.Max(1f, effect.Amount / 100f)
                    : 1f;
            case BulletEffectType.Gilded:
            case BulletEffectType.Heart:
            case BulletEffectType.Loader:
            case BulletEffectType.Resonance:
            case BulletEffectType.Charge:
            case BulletEffectType.Accumulator:
            case BulletEffectType.Devourer:
            case BulletEffectType.Legacy:
            case BulletEffectType.Collection:
            case BulletEffectType.MixedGrade:
            case BulletEffectType.Masterpiece:
            case BulletEffectType.MassProduced:
            case BulletEffectType.Monopoly:
                return 1f + unitCount * effect.Amount / 100f;
            case BulletEffectType.Seismometer:
                return 1f + unitCount * Mathf.Max(0f, effect.Amount) / 100f;
            default:
                return 1f;
        }
    }

    public static float GetCriticalChanceBonus(
        BulletEffectData effect,
        BulletDynamicCombatContext context)
    {
        if (effect == null
            || !TryGetEffectUnitCount(effect, context, out int unitCount))
        {
            return 0f;
        }

        return effect.EffectType == BulletEffectType.Coagulation
            || effect.EffectType == BulletEffectType.Focus
                ? unitCount * effect.Amount
                : 0f;
    }

    public static bool TryGetEffectUnitCount(
        BulletEffectData effect,
        BulletDynamicCombatContext context,
        out int unitCount)
    {
        if (effect == null)
        {
            unitCount = 0;
            return false;
        }

        switch (effect.EffectType)
        {
            case BulletEffectType.Jackpot:
                unitCount = context.IsLoaded && context.IsLastChamber ? 1 : 0;
                return true;
            case BulletEffectType.ClonePreviousShot:
                unitCount = context.IsClone ? 1 : 0;
                return true;
            case BulletEffectType.Gilded:
                unitCount = context.CurrentGold
                    / Mathf.Max(1, effect.StackCount);
                return true;
            case BulletEffectType.Coagulation:
                unitCount = Mathf.FloorToInt(
                    context.MissingHealthPercent
                    / Mathf.Max(1, effect.StackCount));
                return true;
            case BulletEffectType.Heart:
                unitCount = context.MaxHealth
                    / Mathf.Max(1, effect.StackCount);
                return true;
            case BulletEffectType.Loader:
                unitCount = context.IsLoaded ? context.EmptyChamberCount : 0;
                return true;
            case BulletEffectType.Resonance:
                unitCount = context.IsLoaded
                    ? context.OtherResonanceCount
                    : 0;
                return true;
            case BulletEffectType.Focus:
            case BulletEffectType.Accumulator:
            case BulletEffectType.Seismometer:
            case BulletEffectType.Ritual:
            case BulletEffectType.Tracking:
                unitCount = context.AbilityStacks;
                return true;
            case BulletEffectType.Charge:
                unitCount = context.IsLoaded
                    ? Mathf.Min(
                        context.ShotsObservedWhileLoaded,
                        Mathf.Max(0, effect.StackCount))
                    : 0;
                return true;
            case BulletEffectType.Devourer:
            case BulletEffectType.Legacy:
                unitCount = context.PermanentStacks;
                return true;
            case BulletEffectType.Collection:
                unitCount = context.DistinctOwnedBulletTypeCount;
                return true;
            case BulletEffectType.MixedGrade:
                unitCount = context.IsLoaded
                    ? context.OtherLoadedGradeCount
                    : 0;
                return true;
            case BulletEffectType.Masterpiece:
                unitCount = context.OwnedHighGradeCount;
                return true;
            case BulletEffectType.MassProduced:
                unitCount = context.OwnedLowGradeCount;
                return true;
            case BulletEffectType.Monopoly:
                unitCount = context.MostCommonOwnedGradeCount;
                return true;
            default:
                unitCount = 0;
                return false;
        }
    }
}
