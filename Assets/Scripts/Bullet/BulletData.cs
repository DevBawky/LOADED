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
    Weakness = 6
}

public enum BulletGrade
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4
}

[Serializable]
public class BulletLevelData
{
    [SerializeField, TextArea] private string description;
    [Min(0)]
    [SerializeField] private int damage;
    [Range(1, 10)]
    [SerializeField] private int maxRange = 1;
    [Min(1f)]
    [SerializeField] private float criticalDamageMultiplier = 2f;
    [SerializeField] private List<BulletEffectData> effects =
        new List<BulletEffectData>();
    [SerializeField] private List<PenetrationChanceData> penetrationChances =
        new List<PenetrationChanceData>();
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color primaryLineColor = Color.white;
    [SerializeField] private Color secondaryLineColor = Color.white;
    [SerializeField] private bool doesNotConsumeTurn;
    [Min(0f)]
    [SerializeField] private float recoilStrength;
    [Min(0)]
    [SerializeField] private int removeCost = 5;
    [Min(0)]
    [Tooltip("Cost to upgrade from this level to the next level.")]
    [SerializeField] private int upgradeCost = 10;

    public string Description => description;
    public int Damage => Mathf.Max(0, damage);
    public int MaxRange => Mathf.Clamp(maxRange, 1, 10);
    public float CriticalDamageMultiplier =>
        Mathf.Max(1f, criticalDamageMultiplier);
    public IReadOnlyList<BulletEffectData> Effects =>
        effects ?? (IReadOnlyList<BulletEffectData>)Array.Empty<BulletEffectData>();
    public IReadOnlyList<PenetrationChanceData> PenetrationChances =>
        penetrationChances
        ?? (IReadOnlyList<PenetrationChanceData>)Array.Empty<PenetrationChanceData>();
    public Material LineMaterial => lineMaterial;
    public Color PrimaryLineColor => primaryLineColor;
    public Color SecondaryLineColor => secondaryLineColor;
    public bool DoesNotConsumeTurn => doesNotConsumeTurn;
    public float RecoilStrength => Mathf.Max(0f, recoilStrength);
    public int RemoveCost => Mathf.Max(0, removeCost);
    public int UpgradeCost => Mathf.Max(0, upgradeCost);

    public BulletLevelData()
    {
    }

    public BulletLevelData(
        string description,
        int damage,
        int maxRange,
        float criticalDamageMultiplier,
        List<BulletEffectData> effects,
        List<PenetrationChanceData> penetrationChances,
        Material lineMaterial,
        Color primaryLineColor,
        Color secondaryLineColor,
        bool doesNotConsumeTurn,
        float recoilStrength,
        int removeCost,
        int upgradeCost)
    {
        this.description = description;
        this.damage = damage;
        this.maxRange = maxRange;
        this.criticalDamageMultiplier = criticalDamageMultiplier;
        this.effects = CloneEffects(effects);
        this.penetrationChances = ClonePenetrationChances(
            penetrationChances);
        this.lineMaterial = lineMaterial;
        this.primaryLineColor = primaryLineColor;
        this.secondaryLineColor = secondaryLineColor;
        this.doesNotConsumeTurn = doesNotConsumeTurn;
        this.recoilStrength = recoilStrength;
        this.removeCost = removeCost;
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
}

[Serializable]
public class BulletEffectData
{
    [SerializeField] private BulletEffectType effectType;
    [Range(0f, 100f)]
    [SerializeField] private float activationChance = 100f;
    [Min(1)]
    [Tooltip("Poison, Stun, Mark, and Weakness stack count. Ignored by other effects.")]
    [SerializeField] private int stackCount = 1;
    [Min(1)]
    [Tooltip("Maximum travel tiles for Knockback. Ignored by other effects.")]
    [SerializeField] private int knockbackDistance = 1;

    public BulletEffectType EffectType => effectType;
    public float ActivationChance => activationChance;
    public int StackCount => stackCount;
    public int KnockbackDistance => knockbackDistance;

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
        activationChance = source.activationChance;
        stackCount = source.stackCount;
        knockbackDistance = source.knockbackDistance;
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
    [FormerlySerializedAs("sprite")]
    [SerializeField] private Sprite bulletIcon;
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
    [Min(1f)]
    [SerializeField] private float criticalDamageMultiplier = 2f;
    [SerializeField] private List<BulletEffectData> effects =
        new List<BulletEffectData>();
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
    [SerializeField] private int removeCost = 5;
    [Min(0)]
    [Tooltip("Cost to upgrade from level 0 to level 1.")]
    [SerializeField] private int upgradeCost = 10;

    [Header("Upgrade Levels (+1 to +3)")]
    [SerializeField] private List<BulletLevelData> upgradeLevels =
        new List<BulletLevelData>();

    public string BulletId => bulletId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite BulletIcon => bulletIcon;
    public Sprite CylinderIcon => cylinderIcon;
    public int Price => Mathf.Max(0, price);
    public BulletGrade Grade => grade;
    public int Damage => damage;
    public int MaxRange => maxRange;
    public float CriticalDamageMultiplier =>
        Mathf.Max(1f, criticalDamageMultiplier);
    public IReadOnlyList<BulletEffectData> Effects =>
        effects ?? (IReadOnlyList<BulletEffectData>)Array.Empty<BulletEffectData>();
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
    public int RemoveCost => Mathf.Max(0, removeCost);
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
        StringBuilder builder = new StringBuilder();
        string levelDescription = GetDescription(level);

        if (!string.IsNullOrWhiteSpace(levelDescription))
        {
            builder.AppendLine(levelDescription);
            builder.AppendLine();
        }

        builder.Append("대미지: ")
            .AppendLine(GetDamage(level).ToString());
        builder.Append("유효 범위: ")
            .Append(GetMaxRange(level))
            .AppendLine(" 칸");
        builder.Append("크리티컬 배율: x")
            .AppendLine(GetCriticalDamageMultiplier(level).ToString("0.##"));
        return builder.ToString().Trim();
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

    public IReadOnlyList<BulletEffectData> GetEffects(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null ? Effects : levelData.Effects;
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
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null
            ? primaryLineColor
            : levelData.PrimaryLineColor;
    }

    public Color GetSecondaryLineColor(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null
            ? secondaryLineColor
            : levelData.SecondaryLineColor;
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

    public int GetRemoveCost(int level)
    {
        BulletLevelData levelData = GetUpgradeLevelData(level);
        return levelData == null ? RemoveCost : levelData.RemoveCost;
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
            criticalDamageMultiplier,
            effects,
            penetrationChances,
            lineMaterial,
            primaryLineColor,
            secondaryLineColor,
            doesNotConsumeTurn,
            recoilStrength,
            removeCost,
            upgradeCost);
    }

    private static Color GetDefaultGradeColor(BulletGrade bulletGrade)
    {
        return bulletGrade switch
        {
            BulletGrade.Common => new Color(0.86f, 0.86f, 0.86f, 1f),
            BulletGrade.Uncommon => new Color(0.35f, 0.9f, 0.4f, 1f),
            BulletGrade.Rare => new Color(0.3f, 0.65f, 1f, 1f),
            BulletGrade.Epic => new Color(0.75f, 0.4f, 1f, 1f),
            BulletGrade.Legendary => new Color(1f, 0.62f, 0.16f, 1f),
            _ => Color.white
        };
    }
}
