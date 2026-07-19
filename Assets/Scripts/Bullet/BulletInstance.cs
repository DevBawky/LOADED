using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BulletInstance
{
    [SerializeField] private BulletData data;
    [Range(0, BulletData.MaximumUpgradeLevel)]
    [SerializeField] private int level;
    [SerializeField] private int acquisitionOrder;

    public BulletData Data => data;
    public int Level => Mathf.Clamp(level, 0, BulletData.MaximumUpgradeLevel);
    public int AcquisitionOrder => acquisitionOrder;
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
    public Sprite BulletIcon => data == null ? null : data.BulletIcon;
    public Sprite CylinderIcon => data == null ? null : data.CylinderIcon;
    public BulletGrade Grade => data == null ? BulletGrade.Common : data.Grade;
    public Color GradeNameColor => data == null
        ? Color.white
        : data.GradeNameColor;
    public int Damage => data == null ? 0 : data.GetDamage(Level);
    public int MaxRange => data == null ? 1 : data.GetMaxRange(Level);
    public float CriticalDamageMultiplier => data == null
        ? 1f
        : data.GetCriticalDamageMultiplier(Level);
    public IReadOnlyList<BulletEffectData> Effects => data == null
        ? Array.Empty<BulletEffectData>()
        : data.GetEffects(Level);
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
    public bool DoesNotConsumeTurn => data != null
        && data.GetDoesNotConsumeTurn(Level);
    public float RecoilStrength => data == null
        ? 0f
        : data.GetRecoilStrength(Level);
    public int RemoveCost => data == null ? 0 : data.GetRemoveCost(Level);
    public int UpgradeCost => data == null ? 0 : data.GetUpgradeCost(Level);

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
}
