using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct EventRunContext
{
    public EventRunContext(
        int eliteClears,
        int shopVisits,
        int eventClears,
        int money,
        int ownedBullets,
        float currentHealthPercent,
        int cumulativeBattleCount)
    {
        EliteClears = Mathf.Max(0, eliteClears);
        ShopVisits = Mathf.Max(0, shopVisits);
        EventClears = Mathf.Max(0, eventClears);
        Money = Mathf.Max(0, money);
        OwnedBullets = Mathf.Max(0, ownedBullets);
        CurrentHealthPercent = Mathf.Clamp(currentHealthPercent, 0f, 100f);
        CumulativeBattleCount = Mathf.Max(0, cumulativeBattleCount);
    }

    public int EliteClears { get; }
    public int ShopVisits { get; }
    public int EventClears { get; }
    public int Money { get; }
    public int OwnedBullets { get; }
    public float CurrentHealthPercent { get; }
    public int CumulativeBattleCount { get; }

    public static EventRunContext FromRunSave(RunSaveData runData)
    {
        if (runData == null)
        {
            return default;
        }

        int maximumHealth = Mathf.Max(1, runData.maxHealth);
        return new EventRunContext(
            NodeMapSaveSystem.GetCompletedNodeCount(
                NodeMapNodeType.EliteBattle),
            NodeMapSaveSystem.GetCompletedNodeCount(NodeMapNodeType.Shop),
            NodeMapSaveSystem.GetCompletedNodeCount(NodeMapNodeType.Event),
            runData.money,
            runData.bullets?.Count ?? 0,
            runData.currentHealth * 100f / maximumHealth,
            runData.cumulativeBattleTurnCount);
    }

    public float GetValue(EventRunStatistic statistic)
    {
        return statistic switch
        {
            EventRunStatistic.EliteClears => EliteClears,
            EventRunStatistic.ShopVisits => ShopVisits,
            EventRunStatistic.EventClears => EventClears,
            EventRunStatistic.Money => Money,
            EventRunStatistic.OwnedBullets => OwnedBullets,
            EventRunStatistic.CurrentHealthPercent => CurrentHealthPercent,
            EventRunStatistic.CumulativeBattleCount => CumulativeBattleCount,
            _ => 0f
        };
    }
}

public static class EventSelector
{
    public static EventDefinition Select(
        IReadOnlyList<EventDefinition> events,
        EventRunContext context,
        IReadOnlyCollection<string> completedEventIds)
    {
        if (events == null || events.Count == 0)
        {
            return null;
        }

        List<(EventDefinition Event, float Weight)> candidates =
            new List<(EventDefinition, float)>();
        float totalWeight = 0f;

        foreach (EventDefinition definition in events)
        {
            if (definition == null
                || completedEventIds != null
                && completedEventIds.Contains(definition.StableId))
            {
                continue;
            }

            float weight = definition.EvaluateWeight(context);
            if (weight <= 0f)
            {
                continue;
            }

            candidates.Add((definition, weight));
            totalWeight += weight;
        }

        if (candidates.Count == 0 || totalWeight <= 0f)
        {
            return null;
        }

        float roll = UnityEngine.Random.value * totalWeight;
        foreach ((EventDefinition definition, float weight) in candidates)
        {
            roll -= weight;
            if (roll <= 0f)
            {
                return definition;
            }
        }

        return candidates[candidates.Count - 1].Event;
    }
}
