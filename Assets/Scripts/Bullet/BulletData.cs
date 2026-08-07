using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

public enum BulletEffectType
{
    Poison = 0,
    Stun = 1,
    Mark = 2,
    Knockback = 3,
    PositionSwap = 4,
    LifeSteal = 5,
    Weakness = 6,
    IncreaseMaxHealth = 7,
    DestroyBullet = 8,
    GainGold = 9,
    Jackpot = 10,
    PowderPouch = 11,
    StackNextShot = 12,
    ClonePreviousShot = 13,
    ChainFire = 14,
    Resonance = 15,
    Gilded = 16,
    Coagulation = 17,
    Heart = 18,
    Saver = 19,
    QuickDraw = 20,
    Loader = 21,
    Rangefinder = 22,
    WallImpact = 23,
    Judgment = 24,
    StatusAmplifier = 25,
    VenomBurst = 26,
    Crescendo = 27,
    Rebate = 28,
    Distributor = 29,
    Focus = 30,
    Charge = 31,
    Accumulator = 32,
    ShellCollector = 33,
    Devourer = 34,
    Legacy = 35,
    Collection = 36,
    MixedGrade = 37,
    Masterpiece = 38,
    MassProduced = 39,
    Monopoly = 40
}

public enum BulletEffectTarget
{
    HitEnemy = 0,
    FiringPlayer = 1,
    AllEnemies = 2
}

public enum BulletConditionalTrigger
{
    EnemyDefeated = 0,
    CriticalHit = 1,
    Penetration = 2,
    EffectApplied = 3
}

public enum BulletGrade
{
    Normal = 0,
    Rare = 1,
    Ace = 2,
    Legendary = 3
}

public readonly struct BulletRuntimeTooltipStats
{
    public BulletRuntimeTooltipStats(
        float damageMultiplier,
        float criticalChanceBonus,
        IReadOnlyList<string> stateLines)
    {
        DamageMultiplier = Mathf.Max(0f, damageMultiplier);
        CriticalChanceBonus = Mathf.Max(0f, criticalChanceBonus);
        StateLines = stateLines ?? Array.Empty<string>();
    }

    public float DamageMultiplier { get; }
    public float CriticalChanceBonus { get; }
    public IReadOnlyList<string> StateLines { get; }
}

[Serializable]
public class BulletLevelData
{
    [SerializeField, TextArea] private string description;
    [Min(0)]
    [SerializeField] private int damage;
    [Range(1, 10)]
    [SerializeField] private int maxRange = 1;
    [Range(0f, 100f)]
    [SerializeField] private float criticalChance;
    [Min(1f)]
    [SerializeField] private float criticalDamageMultiplier = 2f;
    [SerializeField] private List<BulletEffectData> effects =
        new List<BulletEffectData>();
    [SerializeField] private List<BulletConditionalEventData> conditionalEvents =
        new List<BulletConditionalEventData>();
    [SerializeField] private List<PenetrationChanceData> penetrationChances =
        new List<PenetrationChanceData>();
    [SerializeField] private Material lineMaterial;
    [SerializeField] private bool doesNotConsumeTurn;
    [Min(0f)]
    [SerializeField] private float recoilStrength;
    [Min(0)]
    [Tooltip("Cost to upgrade from this level to the next level.")]
    [SerializeField] private int upgradeCost = 10;

    public string Description => description;
    public int Damage => Mathf.Max(0, damage);
    public int MaxRange => Mathf.Clamp(maxRange, 1, 10);
    public float CriticalChance => Mathf.Clamp(criticalChance, 0f, 100f);
    public float CriticalDamageMultiplier =>
        Mathf.Max(1f, criticalDamageMultiplier);
    public IReadOnlyList<BulletEffectData> Effects =>
        effects ?? (IReadOnlyList<BulletEffectData>)Array.Empty<BulletEffectData>();
    public IReadOnlyList<BulletConditionalEventData> ConditionalEvents =>
        conditionalEvents
        ?? (IReadOnlyList<BulletConditionalEventData>)Array.Empty<BulletConditionalEventData>();
    public IReadOnlyList<PenetrationChanceData> PenetrationChances =>
        penetrationChances
        ?? (IReadOnlyList<PenetrationChanceData>)Array.Empty<PenetrationChanceData>();
    public Material LineMaterial => lineMaterial;
    public bool DoesNotConsumeTurn => doesNotConsumeTurn;
    public float RecoilStrength => Mathf.Max(0f, recoilStrength);
    public int UpgradeCost => Mathf.Max(0, upgradeCost);

    public BulletLevelData()
    {
    }

    public BulletLevelData(
        string description,
        int damage,
        int maxRange,
        float criticalChance,
        float criticalDamageMultiplier,
        List<BulletEffectData> effects,
        List<BulletConditionalEventData> conditionalEvents,
        List<PenetrationChanceData> penetrationChances,
        Material lineMaterial,
        bool doesNotConsumeTurn,
        float recoilStrength,
        int upgradeCost)
    {
        this.description = description;
        this.damage = damage;
        this.maxRange = maxRange;
        this.criticalChance = criticalChance;
        this.criticalDamageMultiplier = criticalDamageMultiplier;
        this.effects = CloneEffects(effects);
        this.conditionalEvents = CloneConditionalEvents(conditionalEvents);
        this.penetrationChances = ClonePenetrationChances(
            penetrationChances);
        this.lineMaterial = lineMaterial;
        this.doesNotConsumeTurn = doesNotConsumeTurn;
        this.recoilStrength = recoilStrength;
        this.upgradeCost = upgradeCost;
    }

    private static List<BulletEffectData> CloneEffects(
        List<BulletEffectData> source)
    {
        List<BulletEffectData> copies = new List<BulletEffectData>();

        if (source == null)
        {
            return copies;
        }

        foreach (BulletEffectData effect in source)
        {
            copies.Add(effect == null ? null : new BulletEffectData(effect));
        }

        return copies;
    }

    private static List<PenetrationChanceData> ClonePenetrationChances(
        List<PenetrationChanceData> source)
    {
        List<PenetrationChanceData> copies =
            new List<PenetrationChanceData>();

        if (source == null)
        {
            return copies;
        }

        foreach (PenetrationChanceData chance in source)
        {
            copies.Add(chance == null
                ? null
                : new PenetrationChanceData(chance));
        }

        return copies;
    }

    private static List<BulletConditionalEventData> CloneConditionalEvents(
        List<BulletConditionalEventData> source)
    {
        List<BulletConditionalEventData> copies =
            new List<BulletConditionalEventData>();

        if (source == null)
        {
            return copies;
        }

        foreach (BulletConditionalEventData conditionalEvent in source)
        {
            copies.Add(conditionalEvent == null
                ? null
                : new BulletConditionalEventData(conditionalEvent));
        }

        return copies;
    }
}

[Serializable]
public class BulletEffectData
{
    [SerializeField] private BulletEffectType effectType;
    [SerializeField] private BulletEffectTarget target;
    [Range(0f, 100f)]
    [SerializeField] private float activationChance = 100f;
    [Min(1)]
    [Tooltip("Poison, Stun, Mark, and Weakness stack count. Poison deals damage equal to its current stacks each turn, then loses 1 stack. Ignored by other effects.")]
    [SerializeField] private int stackCount = 1;
    [Min(1)]
    [Tooltip("Maximum travel tiles for Knockback, or transfer tiles for WallImpact (clamped to 1-3).")]
    [SerializeField] private int knockbackDistance = 1;
    [Min(0f)]
    [Tooltip("Numeric value used by effects. Special bullets use this as a multiplier or percentage depending on their effect type.")]
    [SerializeField] private float amount = 1f;
    [Range(0f, 100f)]
    [Tooltip("Damage percentage transferred to an enemy 2 tiles behind the primary WallImpact target.")]
    [SerializeField] private float secondTransferPercent;
    [Range(0f, 100f)]
    [Tooltip("Damage percentage transferred to an enemy 3 tiles behind the primary WallImpact target.")]
    [SerializeField] private float thirdTransferPercent;

    public BulletEffectType EffectType => effectType;
    public BulletEffectTarget Target => target;
    public float ActivationChance => activationChance;
    public int StackCount => stackCount;
    public int KnockbackDistance => knockbackDistance;
    public float Amount => Mathf.Max(0f, amount);
    public float SecondTransferPercent => Mathf.Max(
        0f,
        secondTransferPercent);
    public float ThirdTransferPercent => Mathf.Max(
        0f,
        thirdTransferPercent);

    public BulletEffectData()
    {
    }

    public BulletEffectData(BulletEffectData source)
    {
        if (source == null)
        {
            return;
        }

        effectType = source.effectType;
        target = source.target;
        activationChance = source.activationChance;
        stackCount = source.stackCount;
        knockbackDistance = source.knockbackDistance;
        amount = source.amount;
        secondTransferPercent = source.secondTransferPercent;
        thirdTransferPercent = source.thirdTransferPercent;
    }

    public bool RollActivation()
    {
        return CanActivate(UnityEngine.Random.Range(0f, 100f));
    }

    public bool CanActivate(float roll)
    {
        float chance = Mathf.Clamp(activationChance, 0f, 100f);
        return chance >= 100f
            || chance > 0f && roll >= 0f && roll < chance;
    }
}

[Serializable]
public class BulletConditionalEventData
{
    [SerializeField] private BulletConditionalTrigger trigger;
    [SerializeField] private List<BulletEffectData> events =
        new List<BulletEffectData>();

    public BulletConditionalTrigger Trigger => trigger;
    public IReadOnlyList<BulletEffectData> Events =>
        events ?? (IReadOnlyList<BulletEffectData>)Array.Empty<BulletEffectData>();

    public BulletConditionalEventData()
    {
    }

    public BulletConditionalEventData(BulletConditionalEventData source)
    {
        if (source == null)
        {
            return;
        }

        trigger = source.trigger;
        events = new List<BulletEffectData>();

        if (source.events == null)
        {
            return;
        }

        foreach (BulletEffectData effect in source.events)
        {
            events.Add(effect == null ? null : new BulletEffectData(effect));
        }
    }
}

[System.Serializable]
public class PenetrationChanceData
{
    [Range(0f, 100f)]
    [SerializeField] private float chance;

    public float Chance => chance;

    public PenetrationChanceData()
    {
    }

    public PenetrationChanceData(PenetrationChanceData source)
    {
        if (source != null)
        {
            chance = source.chance;
        }
    }
}

[CreateAssetMenu(fileName = "New Bullet", menuName = "Loaded/Bullet")]
public class BulletData : ScriptableObject
{
    public const int MaximumUpgradeLevel = 3;

    [Header("Basic Information")]
    [SerializeField] private string bulletId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite cylinderIcon;
    [Min(0)]
    [SerializeField] private int price;
    [SerializeField] private BulletGrade grade;

    [Header("Display Colors")]
    [SerializeField] private bool useCustomGradeNameColor;
    [SerializeField] private Color customGradeNameColor = Color.white;
    [SerializeField] private Color levelOneColor =
        new Color(0.4f, 1f, 0.4f, 1f);
    [SerializeField] private Color levelTwoColor =
        new Color(0.35f, 0.75f, 1f, 1f);
    [SerializeField] private Color levelThreeColor =
        new Color(1f, 0.65f, 0.2f, 1f);

    [Header("Combat")]
    [Min(0)]
    [SerializeField] private int damage;
    [Range(1, 10)]
    [SerializeField] private int maxRange = 1;
    [Range(0f, 100f)]
    [SerializeField] private float criticalChance;
    [Min(1f)]
    [SerializeField] private float criticalDamageMultiplier = 2f;
    [SerializeField] private List<BulletEffectData> effects =
        new List<BulletEffectData>();
    [SerializeField] private List<BulletConditionalEventData> conditionalEvents =
        new List<BulletConditionalEventData>();
    [SerializeField] private List<PenetrationChanceData> penetrationChances = new List<PenetrationChanceData>();
    [FormerlySerializedAs("trailMaterial")]
    [SerializeField] private Material lineMaterial;
    [FormerlySerializedAs("trailColor")]
    [FormerlySerializedAs("lineColor")]
    [SerializeField] private Color primaryLineColor = Color.white;
    [SerializeField] private Color secondaryLineColor = Color.white;
    [SerializeField] private bool doesNotConsumeTurn;
    [Min(0f)]
    [SerializeField] private float recoilStrength;

    [Header("Level 0 Costs")]
    [Min(0)]
    [Tooltip("Cost to upgrade from level 0 to level 1.")]
    [SerializeField] private int upgradeCost = 10;

    [Header("Upgrade Levels (+1 to +3)")]
    [SerializeField] private List<BulletLevelData> upgradeLevels =
        new List<BulletLevelData>();

    public string BulletId => bulletId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite CylinderIcon => cylinderIcon;
    public int Price => Mathf.Max(0, price);
    public BulletGrade Grade => grade;
    public int Damage => damage;
    public int MaxRange => maxRange;
    public float CriticalChance => Mathf.Clamp(criticalChance, 0f, 100f);
    public float CriticalDamageMultiplier =>
        Mathf.Max(1f, criticalDamageMultiplier);
    public IReadOnlyList<BulletEffectData> Effects =>
        effects ?? (IReadOnlyList<BulletEffectData>)Array.Empty<BulletEffectData>();
    public IReadOnlyList<BulletConditionalEventData> ConditionalEvents =>
        conditionalEvents
        ?? (IReadOnlyList<BulletConditionalEventData>)Array.Empty<BulletConditionalEventData>();
    public IReadOnlyList<PenetrationChanceData> PenetrationChances =>
        penetrationChances
        ?? (IReadOnlyList<PenetrationChanceData>)Array.Empty<PenetrationChanceData>();
    public int MaxHitCount => PenetrationChances.Count + 1;
    public Material LineMaterial => lineMaterial;
    public Color PrimaryLineColor => primaryLineColor;
    public Color SecondaryLineColor => secondaryLineColor;
    public Color LineColor => primaryLineColor;
    public bool DoesNotConsumeTurn => doesNotConsumeTurn;
    public float RecoilStrength => recoilStrength;
    public Color GradeNameColor => useCustomGradeNameColor
        ? customGradeNameColor
        : GetDefaultGradeColor(grade);
    public int UpgradeCost => Mathf.Max(0, upgradeCost);
    public IReadOnlyList<BulletLevelData> UpgradeLevels => upgradeLevels;

    private void OnValidate()
    {
        EnsureUpgradeLevels();
    }

    public bool EnsureUpgradeLevels()
    {
        bool changed = false;

        if (upgradeLevels == null)
        {
            upgradeLevels = new List<BulletLevelData>();
            changed = true;
        }

        while (upgradeLevels.Count < MaximumUpgradeLevel)
        {
            upgradeLevels.Add(CreateBaseLevelCopy());
            changed = true;
        }

        if (upgradeLevels.Count > MaximumUpgradeLevel)
        {
            upgradeLevels.RemoveRange(
                MaximumUpgradeLevel,
                upgradeLevels.Count - MaximumUpgradeLevel);
            changed = true;
        }

        for (int index = 0; index < upgradeLevels.Count; index++)
        {
            if (upgradeLevels[index] != null)
            {
                continue;
            }

            upgradeLevels[index] = CreateBaseLevelCopy();
            changed = true;
        }

        return changed;
    }

    public string GetDisplayName(int level)
    {
        string baseName = string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName;
        int validLevel = Mathf.Clamp(level, 0, MaximumUpgradeLevel);
        return validLevel == 0 ? baseName : $"{baseName} (+{validLevel})";
    }

    public string GetRichDisplayName(int level)
    {
        string baseName = string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName;
        int validLevel = Mathf.Clamp(level, 0, MaximumUpgradeLevel);

        if (validLevel == 0)
        {
            return baseName;
        }

        string colorHex = ColorUtility.ToHtmlStringRGBA(
            GetUpgradeLevelColor(validLevel));
        return $"{baseName} <color=#{colorHex}>(+{validLevel})</color>";
    }

    public Color GetUpgradeLevelColor(int level)
    {
        return Mathf.Clamp(level, 1, MaximumUpgradeLevel) switch
        {
            1 => levelOneColor,
            2 => levelTwoColor,
            3 => levelThreeColor,
            _ => Color.white
        };
    }

    public string GetDescription(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null ? description : levelData.Description;
    }

    public string GetDetailedDescription(int level)
    {
        return GetDetailedDescription(
            level,
            new BulletRuntimeTooltipStats(1f, 0f, Array.Empty<string>()));
    }

    public string GetDetailedDescription(
        int level,
        BulletRuntimeTooltipStats runtimeStats)
    {
        StringBuilder builder = new StringBuilder();
        string levelDescription = GetDescription(level);

        if (!string.IsNullOrWhiteSpace(levelDescription))
        {
            builder.AppendLine(levelDescription);
            builder.AppendLine();
        }

        int baseDamage = GetDamage(level);
        int currentDamage = Mathf.Max(
            0,
            Mathf.RoundToInt(baseDamage * runtimeStats.DamageMultiplier));
        int damageDifference = currentDamage - baseDamage;

        builder.Append("대미지: ")
            .Append(baseDamage);

        if (damageDifference > 0)
        {
            builder.Append(" <color=#67E480>(+ ")
                .Append(damageDifference)
                .Append(")</color> = ")
                .Append(currentDamage);
        }
        else if (damageDifference < 0)
        {
            builder.Append(" <color=#FF8066>(- ")
                .Append(-damageDifference)
                .Append(")</color> = ")
                .Append(currentDamage);
        }

        builder.AppendLine();

        if (runtimeStats.DamageMultiplier > 1.0001f)
        {
            builder.Append("현재 대미지 배율: x")
                .AppendLine(runtimeStats.DamageMultiplier.ToString("0.##"));
        }

        builder.Append("유효 범위: ")
            .Append(GetMaxRange(level))
            .AppendLine(" 칸");
        float baseCriticalChance = GetCriticalChance(level);
        float currentCriticalChance = Mathf.Clamp(
            baseCriticalChance + runtimeStats.CriticalChanceBonus,
            0f,
            100f);
        builder.Append("크리티컬 확률: ")
            .Append(baseCriticalChance.ToString("0.##"));

        if (currentCriticalChance > baseCriticalChance + 0.001f)
        {
            builder.Append("% <color=#67E480>(+ ")
                .Append((currentCriticalChance - baseCriticalChance)
                    .ToString("0.##"))
                .Append("%p)</color> = ")
                .Append(currentCriticalChance.ToString("0.##"));
        }

        builder.AppendLine("%");
        builder.Append("크리티컬 배율: x")
            .AppendLine(GetCriticalDamageMultiplier(level).ToString("0.##"));

        if (runtimeStats.StateLines.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("<color=#B8C6D9>현재 누적 정보</color>");

            foreach (string stateLine in runtimeStats.StateLines)
            {
                if (!string.IsNullOrWhiteSpace(stateLine))
                {
                    builder.Append("• ").AppendLine(stateLine);
                }
            }
        }

        return TooltipTextFormatter.Format(builder.ToString().Trim());
    }

    public int GetDamage(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null ? Mathf.Max(0, damage) : levelData.Damage;
    }

    public int GetMaxRange(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null
            ? Mathf.Clamp(maxRange, 1, 10)
            : levelData.MaxRange;
    }

    public float GetCriticalDamageMultiplier(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null
            ? Mathf.Max(1f, criticalDamageMultiplier)
            : levelData.CriticalDamageMultiplier;
    }

    public float GetCriticalChance(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null
            ? Mathf.Clamp(criticalChance, 0f, 100f)
            : levelData.CriticalChance;
    }

    public IReadOnlyList<BulletEffectData> GetEffects(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null ? Effects : levelData.Effects;
    }

    public IReadOnlyList<BulletConditionalEventData> GetConditionalEvents(
        int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null
            ? ConditionalEvents
            : levelData.ConditionalEvents;
    }

    public IReadOnlyList<PenetrationChanceData> GetPenetrationChances(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null
            ? PenetrationChances
            : levelData.PenetrationChances;
    }

    public Material GetLineMaterial(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null ? lineMaterial : levelData.LineMaterial;
    }

    public Color GetPrimaryLineColor(int level)
    {
        return primaryLineColor;
    }

    public Color GetSecondaryLineColor(int level)
    {
        return secondaryLineColor;
    }

    public bool GetDoesNotConsumeTurn(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null
            ? doesNotConsumeTurn
            : levelData.DoesNotConsumeTurn;
    }

    public float GetRecoilStrength(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null
            ? Mathf.Max(0f, recoilStrength)
            : levelData.RecoilStrength;
    }

    public int GetUpgradeCost(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null ? UpgradeCost : levelData.UpgradeCost;
    }

    public bool RollPenetrationAfterHit(int hitCount)
    {
        return CanPenetrateAfterHit(
            hitCount,
            UnityEngine.Random.Range(0f, 100f));
    }

    public bool CanPenetrateAfterHit(int hitCount, float roll)
    {
        return CanPenetrateAfterHit(hitCount, 0, roll);
    }

    public bool CanPenetrateAfterHit(int hitCount, int level, float roll)
    {
        IReadOnlyList<PenetrationChanceData> chances =
            GetPenetrationChances(level);
        int chanceIndex = hitCount - 1;

        if (chanceIndex < 0 || chanceIndex >= chances.Count)
        {
            return false;
        }

        PenetrationChanceData chanceData = chances[chanceIndex];

        if (chanceData == null)
        {
            return false;
        }

        float chance = chanceData.Chance;
        return chance >= 100f || chance > 0f && roll >= 0f && roll < chance;
    }

    private BulletLevelData GetUpgradeLevelData(int level)
    {
        int index = Mathf.Clamp(level, 0, MaximumUpgradeLevel) - 1;

        if (index < 0 || upgradeLevels == null || index >= upgradeLevels.Count)
        {
            return null;
        }

        return upgradeLevels[index];
    }

    private BulletLevelData CreateBaseLevelCopy()
    {
        return new BulletLevelData(
            description,
            damage,
            maxRange,
            criticalChance,
            criticalDamageMultiplier,
            effects,
            conditionalEvents,
            penetrationChances,
            lineMaterial,
            doesNotConsumeTurn,
            recoilStrength,
            upgradeCost);
    }

    private static Color GetDefaultGradeColor(BulletGrade bulletGrade)
    {
        return bulletGrade switch
        {
            BulletGrade.Normal => new Color(0.86f, 0.86f, 0.86f, 1f),
            BulletGrade.Rare => new Color(0.3f, 0.65f, 1f, 1f),
            BulletGrade.Ace => new Color(0.75f, 0.4f, 1f, 1f),
            BulletGrade.Legendary => new Color(1f, 0.62f, 0.16f, 1f),
            _ => Color.white
        };
    }
}
