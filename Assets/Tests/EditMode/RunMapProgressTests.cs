#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class RunMapProgressTests
{
    private ActMapData map;

    [SetUp]
    public void SetUp()
    {
        map = ScriptableObject.CreateInstance<ActMapData>();
        SetPrivate(map, "actId", "test_act");
        SetPrivate(map, "startNodeId", "start");
        SetPrivate(map, "nodes", new List<MapNodeData>
        {
            new MapNodeData("start", MapNodeType.Start,
                new[] { "battle" }, Vector2.zero),
            new MapNodeData("battle", MapNodeType.NormalBattle,
                new[] { "shop", "treasure" }, Vector2.up),
            new MapNodeData("shop", MapNodeType.Shop,
                new[] { "boss" }, Vector2.left),
            new MapNodeData("treasure", MapNodeType.Treasure,
                new[] { "boss" }, Vector2.right),
            new MapNodeData("boss", MapNodeType.Boss,
                new string[0], Vector2.up * 2f)
        });
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(map);
    }

    [Test]
    public void OnlyConnectedNodesCanBeEntered()
    {
        RunMapProgress progress = new RunMapProgress(map, new RunMapState());

        Assert.That(progress.CanEnter("battle"), Is.True);
        Assert.That(progress.CanEnter("shop"), Is.False);
        Assert.That(progress.TryEnter("battle"), Is.True);
        Assert.That(progress.CanEnter("battle"), Is.False);
    }

    [Test]
    public void CompletingNodeUnlocksItsOutgoingPaths()
    {
        RunMapProgress progress = new RunMapProgress(map, new RunMapState());

        Assert.That(progress.TryEnter("battle"), Is.True);
        Assert.That(progress.TryCompleteActiveNode(), Is.True);
        Assert.That(progress.State.currentNodeId, Is.EqualTo("battle"));
        Assert.That(progress.CanEnter("shop"), Is.True);
        Assert.That(progress.CanEnter("treasure"), Is.True);
        Assert.That(progress.CanEnter("boss"), Is.False);
    }

    [Test]
    public void CompletedNodeCannotBeRepeated()
    {
        RunMapState state = new RunMapState
        {
            currentNodeId = "start",
            completedNodeIds = new List<string> { "battle" }
        };
        RunMapProgress progress = new RunMapProgress(map, state);

        Assert.That(progress.CanEnter("battle"), Is.False);
    }

    [Test]
    public void StateNormalizationRemovesDuplicateProgress()
    {
        RunMapState state = new RunMapState
        {
            currentNodeId = "battle",
            visitedNodeIds = new List<string> { "start", "start", "battle" },
            completedNodeIds = new List<string> { "battle", "battle" }
        };

        _ = new RunMapProgress(map, state);

        Assert.That(state.visitedNodeIds.Count, Is.EqualTo(2));
        Assert.That(state.completedNodeIds.Count, Is.EqualTo(1));
    }

    private static void SetPrivate<T>(object target, string fieldName, T value)
    {
        target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic)?.SetValue(target, value);
    }
}
#endif
