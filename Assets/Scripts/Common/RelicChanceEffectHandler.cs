using System;
using System.Collections.Generic;

internal sealed class RelicChanceEffectHandler
{
    private readonly IReadOnlyList<RelicInstance> ownedRelics;
    private readonly Action<RelicInstance, double> probabilityEvaluated;
    private readonly Func<double, bool> rollPercent;
    private readonly Action<RelicInstance, RelicEffectData> triggered;

    public RelicChanceEffectHandler(
        IReadOnlyList<RelicInstance> ownedRelics,
        Action<RelicInstance, double> probabilityEvaluated,
        Func<double, bool> rollPercent,
        Action<RelicInstance, RelicEffectData> triggered)
    {
        this.ownedRelics = ownedRelics;
        this.probabilityEvaluated = probabilityEvaluated;
        this.rollPercent = rollPercent;
        this.triggered = triggered;
    }

    public bool TryWaiveReloadTurn()
    {
        return TryTrigger(
            RelicEffectType.EmptyBeat,
            effect => effect.PrimerBaseChance,
            out _);
    }

    public bool TryWaiveMovementTurn()
    {
        return TryTrigger(
            RelicEffectType.RunningSpur,
            effect => effect.PrimerBaseChance,
            out _);
    }

    public int GetEnemyGoldDropMultiplier()
    {
        return TryTrigger(
            RelicEffectType.GoldPanner,
            effect => effect.GoldNuggetChance,
            out RelicEffectData effect)
                ? effect.NuggetsRequired
                : 1;
    }

    public bool TryReuseFiredBullet()
    {
        return TryTrigger(
            RelicEffectType.CrackedPrimer,
            effect => effect.PrimerBaseChance,
            out _);
    }

    private bool TryTrigger(
        RelicEffectType effectType,
        Func<RelicEffectData, double> chanceSelector,
        out RelicEffectData triggeredEffect)
    {
        triggeredEffect = null;

        if (ownedRelics == null || chanceSelector == null
            || rollPercent == null)
        {
            return false;
        }

        foreach (RelicInstance relic in ownedRelics)
        {
            if (relic?.Data == null || relic.IsSpent)
            {
                continue;
            }

            foreach (RelicEffectData effect in relic.Data.Effects)
            {
                if (effect == null || effect.EffectType != effectType)
                {
                    continue;
                }

                double chance = chanceSelector(effect);
                probabilityEvaluated?.Invoke(relic, chance);

                if (!rollPercent(chance))
                {
                    return false;
                }

                triggered?.Invoke(relic, effect);
                triggeredEffect = effect;
                return true;
            }
        }

        return false;
    }
}
