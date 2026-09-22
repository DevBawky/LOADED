using System;
using System.Collections.Generic;

internal sealed class RelicLethalDamageCandidate
{
    public RelicLethalDamageCandidate(
        RelicInstance relic,
        RelicEffectData effect,
        int remainingUses)
    {
        Relic = relic;
        Effect = effect;
        RemainingUses = remainingUses;
    }

    public RelicInstance Relic { get; }
    public RelicEffectData Effect { get; }
    public int RemainingUses { get; private set; }

    public bool TryUse()
    {
        if (RemainingUses <= 0)
        {
            return false;
        }

        if (RemainingUses != int.MaxValue)
        {
            RemainingUses--;
        }

        return true;
    }
}

internal static class RelicLethalDamagePolicy
{
    public static List<RelicLethalDamageCandidate> CaptureCandidates(
        IReadOnlyList<RelicInstance> relics)
    {
        List<RelicLethalDamageCandidate> candidates =
            new List<RelicLethalDamageCandidate>();

        if (relics == null)
        {
            return candidates;
        }

        foreach (RelicInstance relic in relics)
        {
            if (!TryCreateCandidate(relic, out RelicLethalDamageCandidate candidate))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        return candidates;
    }

    public static bool TryPrevent(
        IReadOnlyList<RelicLethalDamageCandidate> candidates,
        int incomingDamage,
        int currentHealth,
        out RelicLethalDamageCandidate selectedCandidate,
        out int survivingHealth)
    {
        selectedCandidate = null;
        survivingHealth = 0;

        if (incomingDamage < currentHealth || currentHealth <= 0
            || candidates == null)
        {
            return false;
        }

        foreach (RelicLethalDamageCandidate candidate in candidates)
        {
            if (candidate == null || !candidate.TryUse())
            {
                continue;
            }

            selectedCandidate = candidate;
            survivingHealth = Math.Min(
                currentHealth,
                Math.Max(1, candidate.Effect.SurvivingHealth));
            return true;
        }

        return false;
    }

    private static bool TryCreateCandidate(
        RelicInstance relic,
        out RelicLethalDamageCandidate candidate)
    {
        candidate = null;

        if (relic?.Data == null || relic.IsSpent)
        {
            return false;
        }

        foreach (RelicEffectData effect in relic.Data.Effects)
        {
            if (effect == null || effect.EffectType
                != RelicEffectType.PreventLethalDamage)
            {
                continue;
            }

            int remainingUses = relic.Data.LifetimeType
                == RelicLifetimeType.Consumable
                    ? relic.RemainingCharges
                    : int.MaxValue;
            candidate = new RelicLethalDamageCandidate(
                relic,
                effect,
                remainingUses);
            return true;
        }

        return false;
    }
}

internal sealed class RelicLethalDamagePreviewState
{
    private readonly List<RelicLethalDamageCandidate> candidates;

    public RelicLethalDamagePreviewState(
        IReadOnlyList<RelicInstance> relics)
    {
        candidates = RelicLethalDamagePolicy.CaptureCandidates(relics);
    }

    public bool TryPrevent(
        int incomingDamage,
        int currentHealth,
        out int survivingHealth)
    {
        return RelicLethalDamagePolicy.TryPrevent(
            candidates,
            incomingDamage,
            currentHealth,
            out _,
            out survivingHealth);
    }
}
