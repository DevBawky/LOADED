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
}
