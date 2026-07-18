using System;
using System.Collections.Generic;
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
}

[CreateAssetMenu(fileName = "New Bullet", menuName = "Loaded/Bullet")]
public class BulletData : ScriptableObject
{
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

    public bool RollPenetrationAfterHit(int hitCount)
    {
        return CanPenetrateAfterHit(
            hitCount,
            UnityEngine.Random.Range(0f, 100f));
    }

    public bool CanPenetrateAfterHit(int hitCount, float roll)
    {
        int chanceIndex = hitCount - 1;

        if (chanceIndex < 0 || chanceIndex >= PenetrationChances.Count)
        {
            return false;
        }

        PenetrationChanceData chanceData = PenetrationChances[chanceIndex];

        if (chanceData == null)
        {
            return false;
        }

        float chance = chanceData.Chance;
        return chance >= 100f || chance > 0f && roll >= 0f && roll < chance;
    }
}
