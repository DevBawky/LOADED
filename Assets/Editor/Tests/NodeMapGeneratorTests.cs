using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class NodeMapGeneratorTests
{
    [Test]
    public void Generate_CreatesSingleStartAndSingleFinalBoss()
    {
        NodeMapRunData map = NodeMapGenerator.Generate(1234, 0, 12, 4, 5);

        Assert.That(map.nodes.Count(node => node.type == NodeMapNodeType.Start), Is.EqualTo(1));
        Assert.That(map.nodes.Count(node => node.type == NodeMapNodeType.Boss), Is.EqualTo(1));
        NodeMapNodeData boss = map.nodes.Single(node => node.type == NodeMapNodeType.Boss);
        Assert.That(boss.column, Is.EqualTo(map.nodes.Max(node => node.column)));
        Assert.That(boss.nextNodeIds, Is.Empty);
    }

    [Test]
    public void Generate_MakesEveryNodeReachableFromStart()
    {
        NodeMapRunData map = NodeMapGenerator.Generate(5678, 0, 12, 4, 5);
        NodeMapNodeData start = map.nodes.Single(node => node.type == NodeMapNodeType.Start);
        Dictionary<int, NodeMapNodeData> byId = map.nodes.ToDictionary(node => node.id);
        HashSet<int> visited = new HashSet<int> { start.id };
        Queue<int> pending = new Queue<int>();
        pending.Enqueue(start.id);

        while (pending.Count > 0)
        {
            foreach (int next in byId[pending.Dequeue()].nextNodeIds)
            {
                if (visited.Add(next))
                {
                    pending.Enqueue(next);
                }
            }
        }

        Assert.That(visited.Count, Is.EqualTo(map.nodes.Count));
    }

    [Test]
    public void Generate_IsDeterministicForSeed()
    {
        NodeMapRunData first = NodeMapGenerator.Generate(42, 0, 10, 4, 4);
        NodeMapRunData second = NodeMapGenerator.Generate(42, 0, 10, 4, 4);

        Assert.That(UnityEngine.JsonUtility.ToJson(first),
            Is.EqualTo(UnityEngine.JsonUtility.ToJson(second)));
    }

    [Test]
    public void Generate_MakesEveryFirstPlayableNodeANormalBattle()
    {
        NodeMapGenerationRule[] rules =
        {
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.Event,
                weight = 1,
                maximumCount = -1
            }
        };

        NodeMapRunData map = NodeMapGenerator.Generate(
            8142026, 0, 12, 4, 3, rules);
        List<NodeMapNodeData> firstNodes = map.nodes
            .Where(node => node.column == 1)
            .ToList();

        Assert.That(firstNodes, Is.Not.Empty);
        Assert.That(firstNodes.All(node =>
            node.type == NodeMapNodeType.NormalBattle), Is.True);
    }

    [Test]
    public void Generate_ForcesSingleTreasureColumnNearHalfProgress()
    {
        NodeMapGenerationRule[] rules =
        {
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.Treasure,
                weight = 0,
                minimumCount = 99,
                maximumCount = 0
            },
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.NormalBattle,
                weight = 1,
                maximumCount = -1
            }
        };

        NodeMapRunData map = NodeMapGenerator.Generate(
            20260814, 0, 12, 4, 3, rules);
        List<NodeMapNodeData> treasures = map.nodes
            .Where(node => node.type == NodeMapNodeType.Treasure)
            .ToList();

        Assert.That(treasures, Is.Not.Empty);
        Assert.That(treasures.Select(node => node.column).Distinct(),
            Is.EqualTo(new[] { 6 }));
        Assert.That(map.nodes.Where(node => node.column == 6).All(node =>
            node.type == NodeMapNodeType.Treasure), Is.True);
    }

    [Test]
    public void Generate_IgnoresTreasureRuleAndUsesOnlyHalfProgressColumn()
    {
        NodeMapGenerationRule[] rules =
        {
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.Treasure,
                weight = 100,
                minimumCount = 99,
                maximumCount = -1
            },
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.NormalBattle,
                weight = 1,
                maximumCount = -1
            }
        };

        NodeMapRunData map = NodeMapGenerator.Generate(
            20260815, 0, 12, 4, 3, rules);
        List<int> treasureColumns = map.nodes
            .Where(node => node.type == NodeMapNodeType.Treasure)
            .Select(node => node.column)
            .Distinct()
            .OrderBy(column => column)
            .ToList();

        Assert.That(treasureColumns, Is.EqualTo(new[] { 6 }));
        Assert.That(treasureColumns.All(column => map.nodes
            .Where(node => node.column == column)
            .All(node => node.type == NodeMapNodeType.Treasure)), Is.True);
    }

    [Test]
    public void Generate_MakesEveryPreBossNodeAShop()
    {
        for (int seed = 0; seed < 25; seed++)
        {
            NodeMapRunData map = NodeMapGenerator.Generate(
                seed, 0, 12, 4, 3);
            int bossColumn = map.nodes.Max(node => node.column);
            List<NodeMapNodeData> preBossNodes = map.nodes
                .Where(node => node.column == bossColumn - 1)
                .ToList();

            Assert.That(preBossNodes, Is.Not.Empty);
            Assert.That(preBossNodes.All(node =>
                node.type == NodeMapNodeType.Shop), Is.True);
        }
    }

    [Test]
    public void Generate_UsesOnlyThirdsAndPreBossShopColumns()
    {
        NodeMapGenerationRule[] rules =
        {
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.NormalBattle,
                weight = 1,
                maximumCount = -1
            },
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.Shop,
                weight = 100,
                minimumCount = 99,
                maximumCount = -1
            },
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.Treasure,
                weight = 100,
                minimumCount = 99,
                maximumCount = -1
            }
        };

        for (int seed = 0; seed < 25; seed++)
        {
            NodeMapRunData map = NodeMapGenerator.Generate(
                seed, 0, 15, 4, 3, rules);
            int bossColumn = map.nodes.Max(node => node.column);
            List<int> shopColumns = map.nodes
                .Where(node => node.type == NodeMapNodeType.Shop)
                .Select(node => node.column)
                .Distinct()
                .OrderBy(column => column)
                .ToList();
            List<int> treasureColumns = map.nodes
                .Where(node => node.type == NodeMapNodeType.Treasure)
                .Select(node => node.column)
                .Distinct()
                .ToList();

            Assert.That(shopColumns, Is.EqualTo(new[] { 5, 9, 13 }));
            Assert.That(treasureColumns, Is.EqualTo(new[] { 7 }));
            Assert.That(shopColumns.Contains(1), Is.False);
            Assert.That(shopColumns.Intersect(treasureColumns), Is.Empty);
            Assert.That(shopColumns.All(column => map.nodes
                .Where(node => node.column == column)
                .All(node => node.type == NodeMapNodeType.Shop)), Is.True);
        }
    }

    [Test]
    public void Generate_UsesWeightForSlotsOutsideForcedMinimums()
    {
        NodeMapGenerationRule[] rules =
        {
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.NormalBattle,
                weight = 0,
                maximumCount = -1
            },
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.Event,
                weight = 100,
                maximumCount = -1
            }
        };

        NodeMapRunData map = NodeMapGenerator.Generate(
            50100, 0, 10, 4, 3, rules);
        List<NodeMapNodeData> weightedNodes = map.nodes
            .Where(node => node.column > 1
                && node.type != NodeMapNodeType.Boss
                && node.type != NodeMapNodeType.Shop
                && node.type != NodeMapNodeType.Treasure)
            .ToList();

        Assert.That(weightedNodes, Is.Not.Empty);
        Assert.That(weightedNodes.All(node =>
            node.type == NodeMapNodeType.Event), Is.True);
    }

    [Test]
    public void Generate_RespectsConfiguredMinimumAndMaximumCounts()
    {
        NodeMapGenerationRule[] rules =
        {
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.NormalBattle,
                weight = 10,
                minimumCount = 3,
                maximumCount = 4
            },
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.EliteBattle,
                weight = 5,
                minimumCount = 2,
                maximumCount = 2
            },
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.Event,
                weight = 1,
                maximumCount = -1
            }
        };

        NodeMapRunData map = NodeMapGenerator.Generate(
            9912, 0, 10, 4, 5, rules);
        int normalCount = map.nodes.Count(
            node => node.type == NodeMapNodeType.NormalBattle);
        int eliteCount = map.nodes.Count(
            node => node.type == NodeMapNodeType.EliteBattle);

        Assert.That(normalCount, Is.InRange(3, 4));
        Assert.That(eliteCount, Is.EqualTo(2));
    }

    [TestCase(1, NodeMapBattleProgressSection.Early)]
    [TestCase(3, NodeMapBattleProgressSection.Early)]
    [TestCase(4, NodeMapBattleProgressSection.Middle)]
    [TestCase(6, NodeMapBattleProgressSection.Middle)]
    [TestCase(7, NodeMapBattleProgressSection.Late)]
    [TestCase(9, NodeMapBattleProgressSection.Late)]
    public void ProgressSection_UsesPlayableColumnsOnly(
        int column,
        NodeMapBattleProgressSection expected)
    {
        Assert.That(
            NodeMapGenerator.GetNormalBattleProgressSection(column, 10),
            Is.EqualTo(expected));
    }

    [Test]
    public void ProgressSection_UsesRequestedThirdBoundaries()
    {
        const int bossColumn = 14;

        Assert.That(NodeMapGenerator.GetNormalBattleProgressSection(
            5, bossColumn), Is.EqualTo(NodeMapBattleProgressSection.Early));
        Assert.That(NodeMapGenerator.GetNormalBattleProgressSection(
            6, bossColumn), Is.EqualTo(NodeMapBattleProgressSection.Middle));
        Assert.That(NodeMapGenerator.GetNormalBattleProgressSection(
            9, bossColumn), Is.EqualTo(NodeMapBattleProgressSection.Middle));
        Assert.That(NodeMapGenerator.GetNormalBattleProgressSection(
            10, bossColumn), Is.EqualTo(NodeMapBattleProgressSection.Late));
    }

    [Test]
    public void ProgressSection_UsesConfiguredBoundaries()
    {
        const int bossColumn = 11;

        Assert.That(NodeMapGenerator.GetNormalBattleProgressSection(
            3, bossColumn, 0.25f, 0.75f),
            Is.EqualTo(NodeMapBattleProgressSection.Early));
        Assert.That(NodeMapGenerator.GetNormalBattleProgressSection(
            4, bossColumn, 0.25f, 0.75f),
            Is.EqualTo(NodeMapBattleProgressSection.Middle));
        Assert.That(NodeMapGenerator.GetNormalBattleProgressSection(
            7, bossColumn, 0.25f, 0.75f),
            Is.EqualTo(NodeMapBattleProgressSection.Middle));
        Assert.That(NodeMapGenerator.GetNormalBattleProgressSection(
            8, bossColumn, 0.25f, 0.75f),
            Is.EqualTo(NodeMapBattleProgressSection.Late));
    }

    [Test]
    public void ProgressSection_ClampsMiddleBoundaryAfterEarlyBoundary()
    {
        Assert.That(NodeMapGenerator.GetNormalBattleProgressSection(
            7, 11, 0.7f, 0.3f),
            Is.EqualTo(NodeMapBattleProgressSection.Early));
        Assert.That(NodeMapGenerator.GetNormalBattleProgressSection(
            8, 11, 0.7f, 0.3f),
            Is.EqualTo(NodeMapBattleProgressSection.Late));
    }

    [Test]
    public void Generate_AssignsNormalBattleIndexFromProgressPool()
    {
        NodeMapGenerationRule[] rules =
        {
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.NormalBattle,
                weight = 1,
                maximumCount = -1
            }
        };
        NodeMapRunData map = NodeMapGenerator.Generate(
            20260814,
            0,
            11,
            4,
            2,
            rules,
            3,
            4,
            5,
            0.2f,
            0.8f);

        foreach (NodeMapNodeData node in map.nodes.Where(
                     node => node.type == NodeMapNodeType.NormalBattle))
        {
            int expectedPoolCount =
                NodeMapGenerator.GetNormalBattleProgressSection(
                    node.column,
                    10,
                    0.2f,
                    0.8f) switch
                {
                    NodeMapBattleProgressSection.Middle => 3,
                    NodeMapBattleProgressSection.Late => 4,
                    _ => 2
                };
            Assert.That(node.battleIndex, Is.InRange(0, expectedPoolCount - 1));
        }
    }

    [Test]
    public void Generate_EliteBattleIndexIgnoresProgressPools()
    {
        NodeMapGenerationRule[] rules =
        {
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.EliteBattle,
                weight = 1,
                maximumCount = -1
            }
        };
        NodeMapRunData map = NodeMapGenerator.Generate(
            777,
            0,
            11,
            4,
            2,
            rules,
            3,
            4,
            5);

        foreach (NodeMapNodeData node in map.nodes.Where(
                     node => node.type == NodeMapNodeType.EliteBattle))
        {
            Assert.That(node.battleIndex, Is.InRange(0, 4));
        }
    }
}
