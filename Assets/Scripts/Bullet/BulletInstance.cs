using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct BulletTooltipContext
{
    public BulletTooltipContext(
        int currentGold,
        int currentHealth,
        int maxHealth,
        int initialLoadedCount,
        int maxChambers,
        int bulletsFired,
        int criticalShots,
        IReadOnlyList<BulletInstance> deckBullets,
        IReadOnlyList<BulletInstance> loadedBullets,
        IReadOnlyList<BulletInstance> graveyardBullets)
    {
        CurrentGold = Mathf.Max(0, currentGold);
        CurrentHealth = Mathf.Max(0, currentHealth);
        MaxHealth = Mathf.Max(0, maxHealth);
        InitialLoadedCount = Mathf.Max(0, initialLoadedCount);
        MaxChambers = Mathf.Max(0, maxChambers);
        BulletsFired = Mathf.Max(0, bulletsFired);
        CriticalShots = Mathf.Max(0, criticalShots);
        DeckBullets = deckBullets ?? Array.Empty<BulletInstance>();
        LoadedBullets = loadedBullets ?? Array.Empty<BulletInstance>();
        GraveyardBullets = graveyardBullets ?? Array.Empty<BulletInstance>();
    }

    public int CurrentGold { get; }
    public int CurrentHealth { get; }
    public int MaxHealth { get; }
    public int InitialLoadedCount { get; }
    public int MaxChambers { get; }
    public int BulletsFired { get; }
    public int CriticalShots { get; }
    public IReadOnlyList<BulletInstance> DeckBullets { get; }
    public IReadOnlyList<BulletInstance> LoadedBullets { get; }
    public IReadOnlyList<BulletInstance> GraveyardBullets { get; }

    public static BulletTooltipContext Create(
        DeckManager deckManager,
        CurrencyManager currencyManager,
        PlayerHealth playerHealth,
        PlayerShoot playerShoot)
    {
        IReadOnlyList<BulletInstance> loadedBullets = deckManager == null
            ? Array.Empty<BulletInstance>()
            : deckManager.LoadedBullets;
        int initialLoadedCount = playerShoot == null
            ? loadedBullets.Count
            : playerShoot.InitialLoadedBulletCount;

        return new BulletTooltipContext(
            currencyManager == null ? 0 : currencyManager.CurrentMoney,
            playerHealth == null ? 0 : playerHealth.CurrentHealth,
            playerHealth == null ? 0 : playerHealth.MaxHealth,
            initialLoadedCount,
            deckManager == null ? 0 : deckManager.MaxReloadAmount,
            playerShoot == null ? 0 : playerShoot.BulletsFiredThisCylinder,
            playerShoot == null ? 0 : playerShoot.CriticalShotsThisCylinder,
            deckManager == null
                ? Array.Empty<BulletInstance>()
                : deckManager.Deck,
            loadedBullets,
            deckManager == null
                ? Array.Empty<BulletInstance>()
                : deckManager.Graveyard);
    }
}

[Serializable]
public readonly struct BulletRuntimeStateSnapshot
{
    public BulletRuntimeStateSnapshot(
        int abilityStacks,
        int permanentStacks,
        float storedDamageBonus,
        float temporaryCriticalChanceBonus,
        float temporaryDamageBonus,
        int shotsObservedWhileLoaded)
    {
        AbilityStacks = Mathf.Max(0, abilityStacks);
        PermanentStacks = Mathf.Max(0, permanentStacks);
        StoredDamageBonus = Mathf.Max(0f, storedDamageBonus);
        TemporaryCriticalChanceBonus = Mathf.Max(
            0f,
            temporaryCriticalChanceBonus);
        TemporaryDamageBonus = Mathf.Max(0f, temporaryDamageBonus);
        ShotsObservedWhileLoaded = Mathf.Max(
            0,
            shotsObservedWhileLoaded);
    }

    public int AbilityStacks { get; }
    public int PermanentStacks { get; }
    public float StoredDamageBonus { get; }
    public float TemporaryCriticalChanceBonus { get; }
    public float TemporaryDamageBonus { get; }
    public int ShotsObservedWhileLoaded { get; }
}

[Serializable]
public sealed class BulletInstance
{
    [SerializeField] private BulletData data;
    [Range(0, BulletData.MaximumUpgradeLevel)]
    [SerializeField] private int level;
    [SerializeField] private int acquisitionOrder;
    [SerializeField] private int abilityStacks;
    [SerializeField] private int permanentStacks;
    [SerializeField] private float storedDamageBonus;
    [NonSerialized] private float temporaryCriticalChanceBonus;
    [NonSerialized] private float temporaryDamageBonus;
    [NonSerialized] private int shotsObservedWhileLoaded;

    public BulletData Data => data;
    public int Level => Mathf.Clamp(level, 0, BulletData.MaximumUpgradeLevel);
    public int AcquisitionOrder => acquisitionOrder;
    public int AbilityStacks => Mathf.Max(0, abilityStacks);
    public int PermanentStacks => Mathf.Max(0, permanentStacks);
    public int CurrentStackCount => (int)Math.Min(
        int.MaxValue,
        (long)AbilityStacks + PermanentStacks);
    public float StoredDamageBonus => Mathf.Max(0f, storedDamageBonus);
    public float TemporaryDamageBonus => Mathf.Max(0f, temporaryDamageBonus);
    public float TemporaryCriticalChanceBonus => Mathf.Max(
        0f,
        temporaryCriticalChanceBonus);
    public int ShotsObservedWhileLoaded => Mathf.Max(
        0,
        shotsObservedWhileLoaded);
    public bool CanUpgrade => data != null
        && Level < BulletData.MaximumUpgradeLevel;
    public string DisplayName => data == null
        ? string.Empty
        : data.GetDisplayName(Level);
    public string RichDisplayName => data == null
        ? string.Empty
        : data.GetRichDisplayName(Level);
    public string Description => data == null
        ? string.Empty
        : data.GetDescription(Level);
    public string DetailedDescription => data == null
        ? string.Empty
        : data.GetDetailedDescription(Level);
    public Sprite CylinderIcon => data == null ? null : data.CylinderIcon;
    public BulletGrade Grade => data == null ? BulletGrade.Normal : data.Grade;
    public BulletType BulletType => data == null
        ? BulletType.Normal
        : data.BulletType;
    public string BulletTypeDisplayName => data == null
        ? BulletData.GetBulletTypeDisplayName(BulletType.Normal)
        : data.BulletTypeDisplayName;
    public string BulletTypeDescription => data == null
        ? string.Empty
        : data.GetBulletTypeDescription(Level);
    public Color GradeNameColor => data == null
        ? Color.white
        : data.GradeNameColor;
    public int Damage => data == null ? 0 : data.GetDamage(Level);
    public int MaxRange => data == null ? 1 : data.GetMaxRange(Level);
    public float CriticalChance => data == null
        ? 0f
        : data.GetCriticalChance(Level);
    public float CriticalDamageMultiplier => data == null
        ? 1f
        : data.GetCriticalDamageMultiplier(Level);
    public IReadOnlyList<BulletEffectData> Effects => data == null
        ? Array.Empty<BulletEffectData>()
        : data.GetEffects(Level);
    public IReadOnlyList<BulletConditionalEventData> ConditionalEvents =>
        data == null
            ? Array.Empty<BulletConditionalEventData>()
            : data.GetConditionalEvents(Level);
    public IReadOnlyList<PenetrationChanceData> PenetrationChances => data == null
        ? Array.Empty<PenetrationChanceData>()
        : data.GetPenetrationChances(Level);
    public int MaxHitCount => PenetrationChances.Count + 1;
    public Material LineMaterial => data == null
        ? null
        : data.GetLineMaterial(Level);
    public Color PrimaryLineColor => data == null
        ? Color.white
        : data.GetPrimaryLineColor(Level);
    public Color SecondaryLineColor => data == null
        ? Color.white
        : data.GetSecondaryLineColor(Level);
    public float LineWidthMultiplier => data == null
        ? 1f
        : data.GetLineWidthMultiplier(Level);
    public bool DoesNotConsumeTurn => data != null
        && data.GetDoesNotConsumeTurn(Level);
    public bool DoesNotConsumeReloadTurn => data != null
        && (data.BulletType == BulletType.Ghost
            || data.GetDoesNotConsumeTurn(Level));
    public int ShotCount => data == null ? 1 : data.GetShotCount(Level);
    public float RecoilStrength => data == null
        ? 0f
        : data.GetRecoilStrength(Level);
    public int UpgradeCost => data == null ? 0 : data.GetUpgradeCost(Level);

    public int GetDisplayedStackCount(
        IReadOnlyList<BulletInstance> loadedBullets)
    {
        long stackCount = CurrentStackCount;

        if (!ContainsReference(loadedBullets, this))
        {
            return CurrentStackCount;
        }

        foreach (BulletEffectData effect in Effects)
        {
            if (effect == null)
            {
                continue;
            }

            switch (effect.EffectType)
            {
                case BulletEffectType.Resonance:
                    stackCount += CountOtherLoadedEffects(
                        loadedBullets,
                        BulletEffectType.Resonance);
                    break;
                case BulletEffectType.Charge:
                    stackCount += Mathf.Min(
                        ShotsObservedWhileLoaded,
                        Mathf.Max(0, effect.StackCount));
                    break;
            }
        }

        return (int)Math.Min(int.MaxValue, stackCount);
    }

    public string GetDetailedDescription(BulletTooltipContext context)
    {
        if (data == null)
        {
            return string.Empty;
        }

        return data.GetDetailedDescription(
            Level,
            GetRuntimeTooltipStats(context));
    }

    public BulletRuntimeTooltipStats GetRuntimeTooltipStats(
        BulletTooltipContext context)
    {
        if (data == null)
        {
            return new BulletRuntimeTooltipStats(
                1f,
                0f,
                Array.Empty<string>());
        }

        float damageMultiplier = 1f + TemporaryDamageBonus;
        float criticalChanceBonus = TemporaryCriticalChanceBonus;
        List<string> stateLines = new List<string>();
        bool isLoaded = ContainsReference(context.LoadedBullets, this);

        foreach (BulletEffectData effect in Effects)
        {
            if (effect == null)
            {
                continue;
            }

            switch (effect.EffectType)
            {
                case BulletEffectType.Jackpot:
                {
                    bool isLastChamber = context.LoadedBullets.Count > 0
                        && ReferenceEquals(context.LoadedBullets[0], this);

                    if (isLastChamber)
                    {
                        float jackpotMultiplier = Mathf.Max(
                            1f,
                            effect.Amount / 100f);
                        damageMultiplier *= jackpotMultiplier;
                        stateLines.Add(
                            $"마지막 약실 조건 충족 "
                            + $"(피해 x{jackpotMultiplier:0.##})");
                    }

                    break;
                }
                case BulletEffectType.Gilded:
                {
                    int units = context.CurrentGold
                        / Mathf.Max(1, effect.StackCount);
                    float bonus = units * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"보유 골드: {context.CurrentGold} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.Coagulation:
                {
                    float missingPercent = context.MaxHealth <= 0
                        ? 0f
                        : 100f * (context.MaxHealth - context.CurrentHealth)
                            / context.MaxHealth;
                    float bonus = Mathf.Floor(
                            missingPercent / Mathf.Max(1, effect.StackCount))
                        * effect.Amount;
                    criticalChanceBonus += bonus;
                    stateLines.Add(
                        $"잃은 체력: {missingPercent:0.##}% "
                        + $"(치명타 +{bonus:0.##}%p)");
                    break;
                }
                case BulletEffectType.Heart:
                {
                    int units = context.MaxHealth
                        / Mathf.Max(1, effect.StackCount);
                    float bonus = units * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"최대 체력: {context.MaxHealth} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.Loader:
                {
                    if (!isLoaded)
                    {
                        break;
                    }

                    int emptyChambers = Mathf.Max(
                        0,
                        context.MaxChambers - context.InitialLoadedCount);
                    float bonus = emptyChambers * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"빈 약실: {emptyChambers} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.Resonance:
                {
                    if (!isLoaded)
                    {
                        break;
                    }

                    int otherCount = CountOtherLoadedEffects(
                        context.LoadedBullets,
                        BulletEffectType.Resonance);
                    float bonus = otherCount * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"다른 공명탄: {otherCount} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.Crescendo:
                {
                    int ownedBulletCount = context.DeckBullets.Count
                        + context.LoadedBullets.Count
                        + context.GraveyardBullets.Count;

                    if (ContainsReference(context.DeckBullets, this)
                        || ContainsReference(context.LoadedBullets, this)
                        || ContainsReference(context.GraveyardBullets, this))
                    {
                        ownedBulletCount = Mathf.Max(0, ownedBulletCount - 1);
                    }

                    int effectiveBaseDamage = Mathf.Max(
                        0,
                        Mathf.CeilToInt(
                            Damage
                            - ownedBulletCount * effect.Amount));

                    if (Damage > 0)
                    {
                        damageMultiplier *= effectiveBaseDamage / (float)Damage;
                    }

                    stateLines.Add(
                        $"보유 탄환: {ownedBulletCount} "
                        + $"(기본 피해 {effectiveBaseDamage})");
                    break;
                }
                case BulletEffectType.Focus:
                {
                    float bonus = AbilityStacks * effect.Amount;
                    criticalChanceBonus += bonus;
                    stateLines.Add(
                        $"집중 스택: {AbilityStacks} "
                        + $"(치명타 +{bonus:0.##}%p)");
                    break;
                }
                case BulletEffectType.Charge:
                {
                    if (!isLoaded)
                    {
                        break;
                    }

                    int stacks = Mathf.Min(
                        ShotsObservedWhileLoaded,
                        Mathf.Max(0, effect.StackCount));
                    float bonus = stacks * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"충전 스택: {stacks}/{effect.StackCount} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.Accumulator:
                {
                    float bonus = AbilityStacks * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"축전 스택: {AbilityStacks} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.ShellCollector:
                {
                    int cost = Mathf.Max(1, effect.StackCount);
                    int extraShots = Mathf.Min(
                        Mathf.Max(1, effect.KnockbackDistance),
                        AbilityStacks / cost);
                    stateLines.Add(
                        $"탄피: {AbilityStacks} "
                        + $"(추가 발사 {extraShots}회)");
                    break;
                }
                case BulletEffectType.Distributor:
                    stateLines.Add(
                        $"저장된 피해 보너스: "
                        + $"+{StoredDamageBonus * 100f:0.##}%");
                    break;
                case BulletEffectType.Devourer:
                case BulletEffectType.Legacy:
                {
                    float bonus = PermanentStacks * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    string label = effect.EffectType == BulletEffectType.Devourer
                        ? "포식"
                        : "유산";
                    stateLines.Add(
                        $"{label} 스택: {PermanentStacks} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.Collection:
                {
                    int count = CountDistinctOwnedBulletTypes(context);
                    float bonus = count * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"보유 탄환 종류: {count} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.MixedGrade:
                {
                    int count = CountOtherLoadedGrades(
                        context.LoadedBullets);
                    float bonus = count * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"실린더의 다른 등급 탄환: {count} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.Masterpiece:
                {
                    int count = CountOwnedGrades(
                        context,
                        BulletGrade.Ace,
                        BulletGrade.Legendary);
                    float bonus = count * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"에이스 이상 탄환: {count} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.MassProduced:
                {
                    int count = CountOwnedGrades(
                        context,
                        BulletGrade.Normal,
                        BulletGrade.Rare);
                    float bonus = count * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"노멀·레어 탄환: {count} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.Monopoly:
                {
                    int count = GetMostCommonOwnedGradeCount(context);
                    float bonus = count * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"최다 보유 등급 탄환: {count} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.Seismometer:
                {
                    float bonus = AbilityStacks * effect.Amount / 100f;
                    damageMultiplier *= 1f + bonus;
                    stateLines.Add(
                        $"이동 스택: {AbilityStacks} "
                        + $"(피해 +{bonus * 100f:0.##}%)");
                    break;
                }
                case BulletEffectType.Ritual:
                    stateLines.Add(
                        $"집중 스택: {AbilityStacks} "
                        + $"(치명타 배율 +{AbilityStacks * effect.Amount:0.##})");
                    break;
                case BulletEffectType.Tracking:
                    stateLines.Add($"추적 횟수: {AbilityStacks}");
                    break;
                case BulletEffectType.HighRoller:
                {
                    float multiplier =
                        BulletEffectUtility.GetMissingHealthDamageMultiplier(
                            context.CurrentHealth,
                            context.MaxHealth,
                            effect.Amount);
                    damageMultiplier *= multiplier;
                    stateLines.Add(
                        $"잔여 체력: {context.CurrentHealth}/{context.MaxHealth} "
                        + $"(피해 +{(multiplier - 1f) * 100f:0.##}%)");
                    break;
                }
            }
        }

        if (TemporaryDamageBonus > 0f)
        {
            stateLines.Add(
                $"분배받은 피해 보너스: "
                + $"+{TemporaryDamageBonus * 100f:0.##}%");
        }

        if (TemporaryCriticalChanceBonus > 0f)
        {
            stateLines.Add(
                $"임시 치명타 보너스: "
                + $"+{TemporaryCriticalChanceBonus:0.##}%p");
        }

        return new BulletRuntimeTooltipStats(
            damageMultiplier,
            criticalChanceBonus,
            stateLines);
    }

    public string GetStatusDisplayText(BulletTooltipContext context)
    {
        // Clone borrows the previous shot's runtime state for execution only.
        // It does not own or present that state as a stack of its own.
        if (HasEffect(BulletEffectType.ClonePreviousShot))
        {
            return string.Empty;
        }

        if (TryGetEffectUnitCount(context, out int effectUnitCount)
            && effectUnitCount > 0)
        {
            return effectUnitCount.ToString();
        }

        int stackCount = CurrentStackCount;

        if (stackCount > 0)
        {
            return stackCount.ToString();
        }

        if (StoredDamageBonus > 0.0001f
            && HasEffect(BulletEffectType.Distributor))
        {
            return $"+{StoredDamageBonus * 100f:0.##}%";
        }

        BulletRuntimeTooltipStats stats = GetRuntimeTooltipStats(context);

        if (stats.DamageMultiplier > 1.0001f)
        {
            return $"+{(stats.DamageMultiplier - 1f) * 100f:0.##}%";
        }

        if (stats.CriticalChanceBonus > 0.0001f)
        {
            return $"+{stats.CriticalChanceBonus:0.##}%p";
        }

        return string.Empty;
    }

    private bool TryGetEffectUnitCount(
        BulletTooltipContext context,
        out int unitCount)
    {
        bool isLoaded = ContainsReference(context.LoadedBullets, this);

        foreach (BulletEffectData effect in Effects)
        {
            if (effect == null)
            {
                continue;
            }

            switch (effect.EffectType)
            {
                case BulletEffectType.Jackpot:
                    unitCount = isLoaded
                        && context.LoadedBullets.Count > 0
                        && ReferenceEquals(context.LoadedBullets[0], this)
                            ? 1
                            : 0;
                    return true;
                case BulletEffectType.Gilded:
                    unitCount = context.CurrentGold
                        / Mathf.Max(1, effect.StackCount);
                    return true;
                case BulletEffectType.Coagulation:
                {
                    float missingPercent = context.MaxHealth <= 0
                        ? 0f
                        : 100f * (context.MaxHealth - context.CurrentHealth)
                            / context.MaxHealth;
                    unitCount = Mathf.FloorToInt(
                        missingPercent / Mathf.Max(1, effect.StackCount));
                    return true;
                }
                case BulletEffectType.Heart:
                    unitCount = context.MaxHealth
                        / Mathf.Max(1, effect.StackCount);
                    return true;
                case BulletEffectType.Loader:
                    unitCount = isLoaded
                        ? Mathf.Max(
                            0,
                            context.MaxChambers - context.InitialLoadedCount)
                        : 0;
                    return true;
                case BulletEffectType.Resonance:
                    unitCount = isLoaded
                        ? CountOtherLoadedEffects(
                            context.LoadedBullets,
                            BulletEffectType.Resonance)
                        : 0;
                    return true;
                case BulletEffectType.Focus:
                case BulletEffectType.Accumulator:
                case BulletEffectType.Seismometer:
                case BulletEffectType.Ritual:
                case BulletEffectType.Tracking:
                    unitCount = AbilityStacks;
                    return true;
                case BulletEffectType.Charge:
                    unitCount = isLoaded
                        ? Mathf.Min(
                            ShotsObservedWhileLoaded,
                            Mathf.Max(0, effect.StackCount))
                        : 0;
                    return true;
                case BulletEffectType.Devourer:
                case BulletEffectType.Legacy:
                    unitCount = PermanentStacks;
                    return true;
                case BulletEffectType.Collection:
                    unitCount = CountDistinctOwnedBulletTypes(context);
                    return true;
                case BulletEffectType.MixedGrade:
                    unitCount = isLoaded
                        ? CountOtherLoadedGrades(context.LoadedBullets)
                        : 0;
                    return true;
                case BulletEffectType.Masterpiece:
                    unitCount = CountOwnedGrades(
                        context,
                        BulletGrade.Ace,
                        BulletGrade.Legendary);
                    return true;
                case BulletEffectType.MassProduced:
                    unitCount = CountOwnedGrades(
                        context,
                        BulletGrade.Normal,
                        BulletGrade.Rare);
                    return true;
                case BulletEffectType.Monopoly:
                    unitCount = GetMostCommonOwnedGradeCount(context);
                    return true;
            }
        }

        unitCount = 0;
        return false;
    }

    private bool HasEffect(BulletEffectType effectType)
    {
        foreach (BulletEffectData effect in Effects)
        {
            if (effect != null && effect.EffectType == effectType)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsReference(
        IReadOnlyList<BulletInstance> bullets,
        BulletInstance target)
    {
        if (bullets == null)
        {
            return false;
        }

        foreach (BulletInstance bullet in bullets)
        {
            if (ReferenceEquals(bullet, target))
            {
                return true;
            }
        }

        return false;
    }

    private int CountOtherLoadedGrades(
        IReadOnlyList<BulletInstance> bullets)
    {
        int count = 0;

        foreach (BulletInstance bullet in bullets)
        {
            if (bullet != null && !ReferenceEquals(bullet, this)
                && bullet.Grade != Grade)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountDistinctOwnedBulletTypes(
        BulletTooltipContext context)
    {
        HashSet<BulletData> types = new HashSet<BulletData>();
        AddOwnedTypes(types, context.DeckBullets);
        AddOwnedTypes(types, context.LoadedBullets);
        AddOwnedTypes(types, context.GraveyardBullets);
        return types.Count;
    }

    private static void AddOwnedTypes(
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
        BulletTooltipContext context,
        BulletGrade first,
        BulletGrade second)
    {
        return CountGrades(context.DeckBullets, first, second)
            + CountGrades(context.LoadedBullets, first, second)
            + CountGrades(context.GraveyardBullets, first, second);
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

    private static int GetMostCommonOwnedGradeCount(
        BulletTooltipContext context)
    {
        int[] counts = new int[4];
        CountOwnedGradeInstances(counts, context.DeckBullets);
        CountOwnedGradeInstances(counts, context.LoadedBullets);
        CountOwnedGradeInstances(counts, context.GraveyardBullets);
        return Mathf.Max(counts[0], counts[1], counts[2], counts[3]);
    }

    private static void CountOwnedGradeInstances(
        int[] counts,
        IReadOnlyList<BulletInstance> bullets)
    {
        foreach (BulletInstance bullet in bullets)
        {
            if (bullet != null)
            {
                int index = Mathf.Clamp((int)bullet.Grade, 0, 3);
                counts[index]++;
            }
        }
    }

    private int CountOtherLoadedEffects(
        IReadOnlyList<BulletInstance> loadedBullets,
        BulletEffectType effectType)
    {
        int count = 0;

        foreach (BulletInstance bullet in loadedBullets)
        {
            if (bullet == null || ReferenceEquals(bullet, this))
            {
                continue;
            }

            foreach (BulletEffectData effect in bullet.Effects)
            {
                if (effect != null && effect.EffectType == effectType)
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    public BulletInstance(BulletData data, int acquisitionOrder)
    {
        this.data = data;
        this.acquisitionOrder = acquisitionOrder;
        level = 0;
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade)
        {
            return false;
        }

        level++;
        return true;
    }

    public bool RollPenetrationAfterHit(int hitCount)
    {
        return data != null && data.CanPenetrateAfterHit(
            hitCount,
            Level,
            UnityEngine.Random.Range(0f, 100f));
    }

    public bool RollCritical()
    {
        return RollCritical(0f);
    }

    public bool RollCritical(float additionalChanceBonus)
    {
        float chanceBonus = temporaryCriticalChanceBonus
            + Mathf.Max(0f, additionalChanceBonus);
        temporaryCriticalChanceBonus = 0f;
        return CanTriggerCritical(
            UnityEngine.Random.Range(0f, 100f),
            chanceBonus);
    }

    public bool CanTriggerCritical(float roll)
    {
        return CanTriggerCritical(roll, 0f);
    }

    public bool CanTriggerCritical(float roll, float chanceBonus)
    {
        float chance = Mathf.Clamp(
            CriticalChance + Mathf.Max(0f, chanceBonus),
            0f,
            100f);
        return chance >= 100f
            || chance > 0f && roll >= 0f && roll < chance;
    }

    public void AddTemporaryCriticalChance(float chanceBonus)
    {
        temporaryCriticalChanceBonus = Mathf.Clamp(
            temporaryCriticalChanceBonus + Mathf.Max(0f, chanceBonus),
            0f,
            100f);
    }

    public float ConsumeTemporaryCriticalChanceBonus()
    {
        float chanceBonus = temporaryCriticalChanceBonus;
        temporaryCriticalChanceBonus = 0f;
        return chanceBonus;
    }

    public void AddTemporaryDamageBonus(float damageBonus)
    {
        temporaryDamageBonus = Mathf.Max(
            0f,
            temporaryDamageBonus + Mathf.Max(0f, damageBonus));
    }

    public float ConsumeTemporaryDamageBonus()
    {
        float damageBonus = temporaryDamageBonus;
        temporaryDamageBonus = 0f;
        return damageBonus;
    }

    public void AddAbilityStacks(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        abilityStacks = (int)Math.Min(
            int.MaxValue,
            (long)abilityStacks + amount);
    }

    public void ConsumeAbilityStacks(int amount)
    {
        abilityStacks = Mathf.Max(0, abilityStacks - Mathf.Max(0, amount));
    }

    public void SetAbilityStacks(int amount)
    {
        abilityStacks = Mathf.Max(0, amount);
    }

    internal void ResetAbilityStacks()
    {
        abilityStacks = 0;
    }

    public void BeginCylinderShotTracking()
    {
        shotsObservedWhileLoaded = 0;
    }

    public void RecordShotWhileLoaded()
    {
        shotsObservedWhileLoaded = shotsObservedWhileLoaded == int.MaxValue
            ? int.MaxValue
            : shotsObservedWhileLoaded + 1;
    }

    public BulletRuntimeStateSnapshot CaptureRuntimeState()
    {
        return new BulletRuntimeStateSnapshot(
            AbilityStacks,
            PermanentStacks,
            StoredDamageBonus,
            TemporaryCriticalChanceBonus,
            TemporaryDamageBonus,
            ShotsObservedWhileLoaded);
    }

    public void ApplyRuntimeState(BulletRuntimeStateSnapshot state)
    {
        abilityStacks = state.AbilityStacks;
        permanentStacks = state.PermanentStacks;
        storedDamageBonus = state.StoredDamageBonus;
        temporaryCriticalChanceBonus = state.TemporaryCriticalChanceBonus;
        temporaryDamageBonus = state.TemporaryDamageBonus;
        shotsObservedWhileLoaded = state.ShotsObservedWhileLoaded;
    }

    public void AddPermanentStacks(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        permanentStacks = (int)Math.Min(
            int.MaxValue,
            (long)permanentStacks + amount);
    }

    public void AddStoredDamageBonus(float damageBonus)
    {
        storedDamageBonus = Mathf.Max(
            0f,
            storedDamageBonus + Mathf.Max(0f, damageBonus));
    }

    public void ResetStageState()
    {
        abilityStacks = 0;
        shotsObservedWhileLoaded = 0;
        temporaryCriticalChanceBonus = 0f;
        temporaryDamageBonus = 0f;
    }
}
