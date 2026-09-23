using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class BoardManagerTests
{
    [Test]
    public void StageData_DefaultsToTwoLanes()
    {
        StageData stage = ScriptableObject.CreateInstance<StageData>();

        try
        {
            Assert.That(stage.LaneCount, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(stage);
        }
    }

    [Test]
    public void ConfigureBoard_GeneratesEveryTileInEveryLane()
    {
        GameObject managerObject = new GameObject("Board Manager");
        GameObject tileParentObject = new GameObject("Tile Parent");
        GameObject tileTemplateObject = new GameObject("Tile Template");

        try
        {
            BoardManager manager = managerObject.AddComponent<BoardManager>();
            BoardTile tileTemplate =
                tileTemplateObject.AddComponent<BoardTile>();
            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("tileParent").objectReferenceValue =
                tileParentObject.transform;
            serializedManager.FindProperty("boardDistance").floatValue = 2f;
            serializedManager.FindProperty("laneDistance").floatValue = 0.92f;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                manager.ConfigureBoard(3, 2, tileTemplate),
                Is.True);
            Assert.That(manager.BoardCount, Is.EqualTo(3));
            Assert.That(manager.LaneCount, Is.EqualTo(2));
            Assert.That(manager.TotalTileCount, Is.EqualTo(6));
            Assert.That(tileParentObject.transform.childCount, Is.EqualTo(6));

            Assert.That(
                manager.TryGetTilePosition(0, 0, out Vector3 lowerLeft),
                Is.True);
            Assert.That(
                manager.TryGetTilePosition(2, 1, out Vector3 upperRight),
                Is.True);
            Assert.That(lowerLeft, Is.EqualTo(new Vector3(-2f, -0.46f, 0f)));
            Assert.That(upperRight, Is.EqualTo(new Vector3(2f, 0.46f, 0f)));

            Assert.That(
                manager.TryGetTilePosition(1, out Vector3 defaultLane),
                Is.True);
            Assert.That(defaultLane, Is.EqualTo(
                new Vector3(0f, -0.46f, 0f)));

            Assert.That(
                manager.TryGetAdjacentLanePosition(
                    1,
                    0,
                    1,
                    out int upperLaneIndex,
                    out Vector3 upperLanePosition),
                Is.True);
            Assert.That(upperLaneIndex, Is.EqualTo(1));
            Assert.That(upperLanePosition, Is.EqualTo(
                new Vector3(0f, 0.46f, 0f)));
            Assert.That(
                manager.TryGetRangedTilePosition(
                    upperLanePosition,
                    1,
                    1,
                    out Vector3 upperLaneRangedPosition),
                Is.True);
            Assert.That(upperLaneRangedPosition, Is.EqualTo(
                new Vector3(2f, 0.46f, 0f)));
            Assert.That(
                manager.TryGetAdjacentLanePosition(
                    1,
                    1,
                    1,
                    out _,
                    out _),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(tileParentObject);
            Object.DestroyImmediate(tileTemplateObject);
        }
    }

    [Test]
    public void PlayerLaneSave_NormalizesAndRoundTrips()
    {
        RunSaveData saveData = new RunSaveData
        {
            playerTileIndex = 3,
            playerLaneIndex = 1
        };

        string json = JsonUtility.ToJson(saveData);
        RunSaveData restored = JsonUtility.FromJson<RunSaveData>(json);
        RunSaveSystem.NormalizeSaveData(restored);

        Assert.That(restored.playerTileIndex, Is.EqualTo(3));
        Assert.That(restored.playerLaneIndex, Is.EqualTo(1));

        restored.playerLaneIndex = -5;
        RunSaveSystem.NormalizeSaveData(restored);
        Assert.That(restored.playerLaneIndex, Is.Zero);
    }

    [Test]
    public void VerticalMovementContext_CountsAsOneTile()
    {
        PlayerMovementContext movement = new PlayerMovementContext(
            2,
            0,
            2,
            1,
            1,
            PlayerMovementSource.NormalMove);

        Assert.That(movement.StartTileIndex, Is.EqualTo(2));
        Assert.That(movement.StartLaneIndex, Is.Zero);
        Assert.That(movement.EndTileIndex, Is.EqualTo(2));
        Assert.That(movement.EndLaneIndex, Is.EqualTo(1));
        Assert.That(movement.Distance, Is.EqualTo(1));
    }
}
