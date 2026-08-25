using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class NodeMapGenerator
{
    private static readonly NodeMapGenerationRule[] DefaultRules =
    {
        new NodeMapGenerationRule
        {
            nodeType = NodeMapNodeType.NormalBattle,
            weight = 50,
            minimumCount = 1,
            maximumCount = -1
        },
        new NodeMapGenerationRule
        {
            nodeType = NodeMapNodeType.EliteBattle,
            weight = 10,
            maximumCount = 3
        },
        new NodeMapGenerationRule
        {
            nodeType = NodeMapNodeType.Shop,
            weight = 0,
            maximumCount = 0
        },
        new NodeMapGenerationRule
        {
            nodeType = NodeMapNodeType.Treasure,
            weight = 0,
            maximumCount = 0
        },
        new NodeMapGenerationRule
        {
            nodeType = NodeMapNodeType.Event,
            weight = 20,
            maximumCount = -1
        }
    };

    public static NodeMapRunData Generate(
        int seed,
        int stageIndex,
        int columnCount = 12,
        int maximumRows = 4,
        int battleCount = 1,
        IReadOnlyList<NodeMapGenerationRule> generationRules = null,
        int middleBattleCount = -1,
        int lateBattleCount = -1,
        int eliteBattleCount = -1,
        float earlyBattleEndProgress = 1f / 3f,
        float middleBattleEndProgress = 2f / 3f)
    {
        columnCount = Mathf.Max(3, columnCount);
        maximumRows = Mathf.Max(2, maximumRows);
        battleCount = Mathf.Max(1, battleCount);
        middleBattleCount = middleBattleCount < 0
            ? battleCount
            : Mathf.Max(1, middleBattleCount);
        lateBattleCount = lateBattleCount < 0
            ? battleCount
            : Mathf.Max(1, lateBattleCount);
        eliteBattleCount = eliteBattleCount < 0
            ? battleCount
            : Mathf.Max(1, eliteBattleCount);
        System.Random random = new System.Random(seed);
        NodeMapRunData map = new NodeMapRunData
        {
            seed = seed,
            stageIndex = Mathf.Max(0, stageIndex)
        };

        List<List<NodeMapNodeData>> columns =
            new List<List<NodeMapNodeData>>(columnCount);
        int nextId = 0;
        columns.Add(new List<NodeMapNodeData>
        {
            CreateNode(nextId++, 0, maximumRows / 2, NodeMapNodeType.Start)
        });

        for (int column = 1; column < columnCount - 1; column++)
        {
            int count = random.Next(2, maximumRows + 1);
            List<int> rows = Enumerable.Range(0, maximumRows)
                .OrderBy(_ => random.Next())
                .Take(count)
                .OrderBy(row => row)
                .ToList();
            List<NodeMapNodeData> nodes = new List<NodeMapNodeData>(count);

            foreach (int row in rows)
            {
                NodeMapNodeData node = CreateNode(
                    nextId++, column, row, NodeMapNodeType.NormalBattle);
                nodes.Add(node);
            }
            columns.Add(nodes);
        }

        columns.Add(new List<NodeMapNodeData>
        {
            CreateNode(
                nextId++,
                columnCount - 1,
                maximumRows / 2,
                NodeMapNodeType.Boss)
        });

        List<NodeMapNodeData> middleNodes = columns
            .Skip(1)
            .Take(columns.Count - 2)
            .SelectMany(column => column)
            .ToList();
        AssignMiddleNodeTypes(
            middleNodes,
            generationRules,
            battleCount,
            middleBattleCount,
            lateBattleCount,
            eliteBattleCount,
            columnCount - 1,
            earlyBattleEndProgress,
            middleBattleEndProgress,
            random);

        for (int column = 0; column < columns.Count - 1; column++)
        {
            ConnectColumns(columns[column], columns[column + 1]);
        }

        foreach (List<NodeMapNodeData> column in columns)
        {
            map.nodes.AddRange(column);
        }
        map.currentNodeId = columns[0][0].id;
        map.completedNodeIds.Add(map.currentNodeId);
        return map;
    }

    private static void AssignMiddleNodeTypes(
        List<NodeMapNodeData> nodes,
        IReadOnlyList<NodeMapGenerationRule> configuredRules,
        int earlyBattleCount,
        int middleBattleCount,
        int lateBattleCount,
        int eliteBattleCount,
        int maximumColumn,
        float earlyBattleEndProgress,
        float middleBattleEndProgress,
        System.Random random)
    {
        IReadOnlyList<NodeMapGenerationRule> sourceRules =
            configuredRules == null || configuredRules.Count == 0
                ? DefaultRules
                : configuredRules;
        HashSet<int> forcedTreasureColumns = GetForcedTreasureColumns(
            maximumColumn);
        HashSet<int> forcedShopColumns = GetForcedShopColumns(maximumColumn);

        // Shop and Treasure are exclusively assigned by fixed progress
        // columns and never participate in ordinary allocation.
        List<NodeMapGenerationRule> rules = sourceRules
            .Where(rule => rule != null
                && rule.nodeType != NodeMapNodeType.Start
                && rule.nodeType != NodeMapNodeType.Boss
                && rule.nodeType != NodeMapNodeType.Shop
                && rule.nodeType != NodeMapNodeType.Treasure)
            .GroupBy(rule => rule.nodeType)
            .Select(group => group.First())
            .ToList();
        if (rules.Count == 0)
        {
            rules.Add(DefaultRules[0]);
        }

        Dictionary<NodeMapNodeType, int> counts = rules.ToDictionary(
            rule => rule.nodeType, _ => 0);

        // Guaranteed columns take priority over configured rules. The first
        // playable column remains a battle in very short maps. If fixed
        // columns overlap, Treasure keeps its half-progress column.
        List<NodeMapNodeData> unassignedNodes = new List<NodeMapNodeData>();
        foreach (NodeMapNodeData node in nodes)
        {
            if (node.column == 1)
            {
                node.type = NodeMapNodeType.NormalBattle;
                IncrementCount(counts, node.type);
            }
            else if (forcedTreasureColumns.Contains(node.column))
            {
                node.type = NodeMapNodeType.Treasure;
            }
            else if (forcedShopColumns.Contains(node.column))
            {
                node.type = NodeMapNodeType.Shop;
            }
            else
            {
                unassignedNodes.Add(node);
            }
        }

        // Constrained types are placed first so their minimum count cannot be
        // consumed by types that are valid in every playable column.
        foreach (NodeMapGenerationRule rule in rules
                     .OrderBy(rule => unassignedNodes.Count(node =>
                         CanAssignType(node, rule.nodeType, maximumColumn))))
        {
            int maximum = rule.maximumCount < 0
                ? nodes.Count
                : Mathf.Max(0, rule.maximumCount);
            int required = Mathf.Min(
                Mathf.Max(0, rule.minimumCount), maximum);
            required = Mathf.Max(
                0,
                required - GetCount(counts, rule.nodeType));

            while (required > 0)
            {
                List<NodeMapNodeData> eligibleNodes = unassignedNodes
                    .Where(node => CanAssignType(
                        node,
                        rule.nodeType,
                        maximumColumn))
                    .ToList();
                if (eligibleNodes.Count == 0)
                {
                    break;
                }

                NodeMapNodeData selectedNode = eligibleNodes[
                    random.Next(eligibleNodes.Count)];
                selectedNode.type = rule.nodeType;
                unassignedNodes.Remove(selectedNode);
                IncrementCount(counts, rule.nodeType);
                required--;
            }
        }

        foreach (NodeMapNodeData node in unassignedNodes
                     .OrderBy(_ => random.Next()))
        {
            List<NodeMapGenerationRule> candidates = rules
                .Where(rule => rule.weight > 0
                    && CanAssignType(node, rule.nodeType, maximumColumn)
                    && (rule.maximumCount < 0
                        || GetCount(counts, rule.nodeType)
                            < rule.maximumCount))
                .ToList();
            if (candidates.Count == 0)
            {
                NodeMapGenerationRule fallback = rules.FirstOrDefault(
                    rule => rule.maximumCount < 0
                        && CanAssignType(
                            node,
                            rule.nodeType,
                            maximumColumn));
                node.type = fallback == null
                    ? NodeMapNodeType.NormalBattle
                    : fallback.nodeType;
                IncrementCount(counts, node.type);
                continue;
            }

            int totalWeight = candidates.Sum(rule => rule.weight);
            int roll = random.Next(totalWeight);
            NodeMapGenerationRule selected = candidates[0];
            foreach (NodeMapGenerationRule candidate in candidates)
            {
                if (roll < candidate.weight)
                {
                    selected = candidate;
                    break;
                }
                roll -= candidate.weight;
            }
            node.type = selected.nodeType;
            IncrementCount(counts, node.type);
        }

        foreach (NodeMapNodeData node in nodes)
        {
            if (node.type == NodeMapNodeType.NormalBattle)
            {
                int poolCount = GetNormalBattleProgressSection(
                    node.column,
                    maximumColumn,
                    earlyBattleEndProgress,
                    middleBattleEndProgress) switch
                {
                    NodeMapBattleProgressSection.Middle => middleBattleCount,
                    NodeMapBattleProgressSection.Late => lateBattleCount,
                    _ => earlyBattleCount
                };
                node.battleIndex = random.Next(Mathf.Max(1, poolCount));
            }
            else if (node.type == NodeMapNodeType.EliteBattle)
            {
                node.battleIndex = random.Next(
                    Mathf.Max(1, eliteBattleCount));
            }
        }
    }

    private static bool CanAssignType(
        NodeMapNodeData node,
        NodeMapNodeType type,
        int maximumColumn)
    {
        if (node == null)
        {
            return false;
        }

        if (node.column == 1)
        {
            return type == NodeMapNodeType.NormalBattle;
        }

        // Shop and Treasure are never part of the weighted/minimum allocation
        // pass.
        return type != NodeMapNodeType.Shop
            && type != NodeMapNodeType.Treasure;
    }

    private static HashSet<int> GetForcedTreasureColumns(
        int maximumColumn)
    {
        return new HashSet<int>
        {
            GetColumnNearestProgress(maximumColumn, 0.5f)
        };
    }

    private static HashSet<int> GetForcedShopColumns(int maximumColumn)
    {
        int preBossColumn = Mathf.Max(1, maximumColumn - 1);
        return new HashSet<int>
        {
            GetColumnNearestProgress(maximumColumn, 1f / 3f),
            GetColumnNearestProgress(maximumColumn, 2f / 3f),
            preBossColumn
        };
    }

    private static int GetColumnNearestProgress(
        int maximumColumn,
        float progress)
    {
        const int firstPlayableColumn = 1;
        int lastPlayableColumn = Mathf.Max(
            firstPlayableColumn,
            maximumColumn - 1);
        return Mathf.RoundToInt(Mathf.Lerp(
            firstPlayableColumn,
            lastPlayableColumn,
            Mathf.Clamp01(progress)));
    }

    private static int GetCount(
        IReadOnlyDictionary<NodeMapNodeType, int> counts,
        NodeMapNodeType type)
    {
        return counts.TryGetValue(type, out int count) ? count : 0;
    }

    private static void IncrementCount(
        IDictionary<NodeMapNodeType, int> counts,
        NodeMapNodeType type)
    {
        counts.TryGetValue(type, out int count);
        counts[type] = count + 1;
    }

    public static NodeMapBattleProgressSection GetNormalBattleProgressSection(
        int column,
        int maximumColumn,
        float earlyBattleEndProgress = 1f / 3f,
        float middleBattleEndProgress = 2f / 3f)
    {
        int firstPlayableColumn = 1;
        int lastPlayableColumn = Mathf.Max(
            firstPlayableColumn,
            maximumColumn - 1);
        float progress = lastPlayableColumn == firstPlayableColumn
            ? 0f
            : Mathf.InverseLerp(
                firstPlayableColumn,
                lastPlayableColumn,
                Mathf.Clamp(column, firstPlayableColumn, lastPlayableColumn));

        float earlyBoundary = Mathf.Clamp01(earlyBattleEndProgress);
        float middleBoundary = Mathf.Clamp(
            middleBattleEndProgress,
            earlyBoundary,
            1f);
        if (progress <= earlyBoundary)
        {
            return NodeMapBattleProgressSection.Early;
        }

        return progress <= middleBoundary
            ? NodeMapBattleProgressSection.Middle
            : NodeMapBattleProgressSection.Late;
    }

    private static NodeMapNodeData CreateNode(
        int id,
        int column,
        int row,
        NodeMapNodeType type)
    {
        return new NodeMapNodeData
        {
            id = id,
            column = column,
            row = row,
            type = type
        };
    }

    private static void ConnectColumns(
        IReadOnlyList<NodeMapNodeData> left,
        IReadOnlyList<NodeMapNodeData> right)
    {
        foreach (NodeMapNodeData source in left)
        {
            NodeMapNodeData nearest = right
                .OrderBy(node => Mathf.Abs(node.row - source.row))
                .ThenBy(node => node.row)
                .First();
            source.nextNodeIds.Add(nearest.id);

            NodeMapNodeData second = right
                .Where(node => node.id != nearest.id
                    && Mathf.Abs(node.row - source.row) <= 1)
                .OrderBy(node => Mathf.Abs(node.row - source.row))
                .FirstOrDefault();
            if (second != null)
            {
                source.nextNodeIds.Add(second.id);
            }
        }

        // Every room remains reachable from the previous column.
        foreach (NodeMapNodeData target in right)
        {
            if (left.Any(source => source.nextNodeIds.Contains(target.id)))
            {
                continue;
            }

            left.OrderBy(source => Mathf.Abs(source.row - target.row))
                .First().nextNodeIds.Add(target.id);
        }
    }
}
