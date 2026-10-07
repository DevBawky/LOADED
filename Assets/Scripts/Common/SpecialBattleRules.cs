using System;
using UnityEngine;

public enum SpecialBattleRule
{
    None = 0,
    DeathBombs = 1,
    DamageSurge = 2,
    ReverseCylinder = 3,
    BloodReload = 4
}

public readonly struct BattleRuleContext
{
    public const float DamageSurgeMultiplier = 1.5f;
    public const int BloodReloadHealthCost = 1;
    public const int BloodReloadKillHeal = 3;

    public BattleRuleContext(SpecialBattleRule rule)
    {
        Rule = Enum.IsDefined(typeof(SpecialBattleRule), rule)
            ? rule
            : SpecialBattleRule.None;
    }

    public static BattleRuleContext None => new BattleRuleContext(
        SpecialBattleRule.None);

    public SpecialBattleRule Rule { get; }
    public bool CreatesDeathBombs => Rule == SpecialBattleRule.DeathBombs;
    public bool ReversesCylinder => Rule == SpecialBattleRule.ReverseCylinder;
    public bool UsesBloodReload => Rule == SpecialBattleRule.BloodReload;
    public float DamageMultiplier => Rule == SpecialBattleRule.DamageSurge
        ? DamageSurgeMultiplier
        : 1f;

    public int ScaleDamage(int damage)
    {
        if (damage <= 0)
        {
            return 0;
        }

        double scaled = Math.Ceiling(damage * (double)DamageMultiplier);
        return scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
    }

    public static string GetDisplayName(SpecialBattleRule rule)
    {
        return rule switch
        {
            SpecialBattleRule.DeathBombs => "연쇄 폭발",
            SpecialBattleRule.DamageSurge => "과잉 화력",
            SpecialBattleRule.ReverseCylinder => "역회전 실린더",
            SpecialBattleRule.BloodReload => "피의 재장전",
            _ => ""
        };
    }

    public static string GetDescription(SpecialBattleRule rule)
    {
        return rule switch
        {
            SpecialBattleRule.DeathBombs =>
                "적이 죽은 자리에 폭탄이 설치됩니다. 폭발로 죽은 적도 새 폭탄을 남깁니다.",
            SpecialBattleRule.DamageSurge =>
                "플레이어와 적이 주고받는 모든 피해가 1.5배가 됩니다.",
            SpecialBattleRule.ReverseCylinder =>
                "실린더의 탄환이 평소와 반대 순서로 발사됩니다.",
            SpecialBattleRule.BloodReload =>
                "재장전할 때 체력 1을 잃고, 적을 처치할 때 체력 3을 회복합니다. 체력이 1이면 재장전할 수 없습니다.",
            _ => string.Empty
        };
    }
}

public readonly struct CylinderFiringOrder
{
    public CylinderFiringOrder(bool reversed)
    {
        IsReversed = reversed;
    }

    public bool IsReversed { get; }
    public int Step => IsReversed ? 1 : -1;

    public int GetFirstIndex(int loadedCount)
    {
        return loadedCount <= 0 ? -1 : IsReversed ? 0 : loadedCount - 1;
    }

    public int GetNextRemovalIndex(int currentLoadedCount)
    {
        return GetFirstIndex(currentLoadedCount);
    }

    public bool IsFirstIndex(int index, int initialLoadedCount)
    {
        return index == GetFirstIndex(initialLoadedCount);
    }

    public bool IsLastIndex(int index, int initialLoadedCount)
    {
        if (initialLoadedCount <= 0)
        {
            return false;
        }

        return index == (IsReversed ? initialLoadedCount - 1 : 0);
    }

    public bool IsAfter(int candidateIndex, int currentIndex)
    {
        return IsReversed
            ? candidateIndex > currentIndex
            : candidateIndex < currentIndex;
    }

    public bool IsValidIndex(int index, int loadedCount)
    {
        return index >= 0 && index < loadedCount;
    }
}
