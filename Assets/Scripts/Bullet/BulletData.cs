using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    [SerializeField] private string bulletId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite sprite;
    [Min(0)]
    [SerializeField] private int damage;
    [Range(1, 10)]
    [SerializeField] private int maxRange = 1;
    [Min(0)]
    [SerializeField] private int knockbackDistance;
    [Min(0)]
    [SerializeField] private int stunDurationTurns;
    [Min(0)]
    [SerializeField] private int markDurationTurns;
    [Range(1f, 3f)]
    [SerializeField] private float markDamageMultiplier = 1f;
    [SerializeField] private List<PenetrationChanceData> penetrationChances = new List<PenetrationChanceData>();
    [FormerlySerializedAs("trailMaterial")]
    [SerializeField] private Material lineMaterial;
    [FormerlySerializedAs("trailColor")]
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private bool doesNotConsumeTurn;
    [Min(0f)]
    [SerializeField] private float recoilStrength;

    public string BulletId => bulletId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Sprite => sprite;
    public int Damage => damage;
    public int MaxRange => maxRange;
    public int KnockbackDistance => knockbackDistance;
    public int StunDurationTurns => stunDurationTurns;
    public int MarkDurationTurns => markDurationTurns;
    public float MarkDamageMultiplier => markDamageMultiplier;
    public IReadOnlyList<PenetrationChanceData> PenetrationChances => penetrationChances;
    public int MaxHitCount => penetrationChances.Count + 1;
    public Material LineMaterial => lineMaterial;
    public Color LineColor => lineColor;
    public bool DoesNotConsumeTurn => doesNotConsumeTurn;
    public float RecoilStrength => recoilStrength;

    public bool RollPenetrationAfterHit(int hitCount)
    {
        return CanPenetrateAfterHit(hitCount, Random.Range(0f, 100f));
    }

    public bool CanPenetrateAfterHit(int hitCount, float roll)
    {
        int chanceIndex = hitCount - 1;

        if (chanceIndex < 0 || chanceIndex >= penetrationChances.Count)
        {
            return false;
        }

        PenetrationChanceData chanceData = penetrationChances[chanceIndex];

        if (chanceData == null)
        {
            return false;
        }

        float chance = chanceData.Chance;
        return chance >= 100f || chance > 0f && roll >= 0f && roll < chance;
    }
}
