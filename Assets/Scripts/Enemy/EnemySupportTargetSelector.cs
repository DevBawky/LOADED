using System.Collections.Generic;
using UnityEngine;

internal readonly struct EnemySupportTargetCandidate<TTarget>
{
    public EnemySupportTargetCandidate(
        TTarget target,
        bool isEligible,
        int currentHealth,
        int maxHealth,
        int currentShield,
        int playerDistance,
        int tileIndex)
    {
        Target = target;
        IsEligible = isEligible;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        CurrentShield = currentShield;
        PlayerDistance = playerDistance;
        TileIndex = tileIndex;
    }

    public TTarget Target { get; }
    public bool IsEligible { get; }
    public int CurrentHealth { get; }
    public int MaxHealth { get; }
    public int CurrentShield { get; }
    public int PlayerDistance { get; }
    public int TileIndex { get; }
}

internal static class EnemySupportTargetSelector
{
    public static bool TrySelect<TTarget>(
        IReadOnlyList<EnemySupportTargetCandidate<TTarget>> candidates,
        float healThreshold,
        out TTarget selectedTarget,
        out EnemySupportType supportType)
    {
        selectedTarget = default;
        supportType = EnemySupportType.None;

        if (candidates == null)
        {
            return false;
        }

        float lowestHealthRatio = float.MaxValue;
        int bestHealPlayerDistance = int.MaxValue;
        int bestHealTileIndex = int.MaxValue;

        foreach (EnemySupportTargetCandidate<TTarget> candidate in candidates)
        {
            if (!candidate.IsEligible
                || candidate.CurrentHealth >= candidate.MaxHealth)
            {
                continue;
            }

            float healthRatio = candidate.MaxHealth <= 0
                ? 1f
                : (float)candidate.CurrentHealth / candidate.MaxHealth;

            if (healthRatio > healThreshold)
            {
                continue;
            }

            if (healthRatio < lowestHealthRatio
                || Mathf.Approximately(healthRatio, lowestHealthRatio)
                && (candidate.PlayerDistance < bestHealPlayerDistance
                    || candidate.PlayerDistance == bestHealPlayerDistance
                    && candidate.TileIndex < bestHealTileIndex))
            {
                selectedTarget = candidate.Target;
                lowestHealthRatio = healthRatio;
                bestHealPlayerDistance = candidate.PlayerDistance;
                bestHealTileIndex = candidate.TileIndex;
            }
        }

        if (lowestHealthRatio < float.MaxValue)
        {
            supportType = EnemySupportType.Heal;
            return true;
        }

        int bestShieldPlayerDistance = int.MaxValue;
        int bestShieldTileIndex = int.MaxValue;
        bool foundShieldTarget = false;

        foreach (EnemySupportTargetCandidate<TTarget> candidate in candidates)
        {
            if (!candidate.IsEligible || candidate.CurrentShield > 0)
            {
                continue;
            }

            if (candidate.PlayerDistance < bestShieldPlayerDistance
                || candidate.PlayerDistance == bestShieldPlayerDistance
                && candidate.TileIndex < bestShieldTileIndex)
            {
                selectedTarget = candidate.Target;
                bestShieldPlayerDistance = candidate.PlayerDistance;
                bestShieldTileIndex = candidate.TileIndex;
                foundShieldTarget = true;
            }
        }

        if (!foundShieldTarget)
        {
            return false;
        }

        supportType = EnemySupportType.Shield;
        return true;
    }
}
