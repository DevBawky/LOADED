using NUnit.Framework;
using System.Collections.Generic;
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
    public void EnemyAndBombLaneSave_NormalizesAndRoundTrips()
    {
        RunSaveData saveData = new RunSaveData();
        saveData.enemies.Add(new RunEnemySaveData
        {
            laneIndex = 1,
            preparedTargetLaneIndex = 1,
            preparedBigBarrelLaneIndex = 1
        });
        saveData.bombs.Add(new RunBombSaveData { laneIndex = 1 });

        string json = JsonUtility.ToJson(saveData);
        RunSaveData restored = JsonUtility.FromJson<RunSaveData>(json);
        RunSaveSystem.NormalizeSaveData(restored);

        Assert.That(restored.enemies[0].laneIndex, Is.EqualTo(1));
        Assert.That(
            restored.enemies[0].preparedTargetLaneIndex,
            Is.EqualTo(1));
        Assert.That(
            restored.enemies[0].preparedBigBarrelLaneIndex,
            Is.EqualTo(1));
        Assert.That(restored.bombs[0].laneIndex, Is.EqualTo(1));

        restored.enemies[0].laneIndex = -1;
        restored.enemies[0].preparedTargetLaneIndex = -1;
        restored.enemies[0].preparedBigBarrelLaneIndex = -1;
        restored.bombs[0].laneIndex = -1;
        RunSaveSystem.NormalizeSaveData(restored);

        Assert.That(restored.enemies[0].laneIndex, Is.Zero);
        Assert.That(restored.enemies[0].preparedTargetLaneIndex, Is.Zero);
        Assert.That(restored.enemies[0].preparedBigBarrelLaneIndex, Is.Zero);
        Assert.That(restored.bombs[0].laneIndex, Is.Zero);
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

    [Test]
    public void ApprovedLaneMoveCommitsDestinationLaneBeforeVisualMotionCompletes()
    {
        GameObject playerObject = new GameObject("PlayerMoveLaneCommitTest");

        try
        {
            ActorMotion actorMotion = playerObject.AddComponent<ActorMotion>();
            PlayerMove playerMove = playerObject.AddComponent<PlayerMove>();
            SerializedObject serializedPlayerMove = new SerializedObject(playerMove);
            serializedPlayerMove.FindProperty("actorMotion").objectReferenceValue = actorMotion;
            serializedPlayerMove.ApplyModifiedPropertiesWithoutUndo();

            playerMove.SetLaneIndex(0);

            System.Reflection.MethodInfo moveRoutineMethod = typeof(PlayerMove).GetMethod(
                "MoveRoutine",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(moveRoutineMethod, Is.Not.Null);

            System.Collections.IEnumerator moveRoutine = moveRoutineMethod.Invoke(
                playerMove,
                new object[] { Vector3.up, 2, 0, 2, 1 }) as System.Collections.IEnumerator;
            Assert.That(moveRoutine, Is.Not.Null);
            Assert.That(moveRoutine.MoveNext(), Is.True);
            Assert.That(playerMove.CurrentLaneIndex, Is.EqualTo(1));

            (moveRoutine as System.IDisposable)?.Dispose();
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void BigBarrelExplosionRangeRejectsTargetsInAnotherLane()
    {
        Assert.That(
            BossBombManager.IsCellInExplosionRange(3, 1, 4, 1, 1),
            Is.True);
        Assert.That(
            BossBombManager.IsCellInExplosionRange(3, 1, 3, 0, 1),
            Is.False);
        Assert.That(
            BossBombManager.IsCellInExplosionRange(3, 1, 5, 1, 1),
            Is.False);
    }

    [Test]
    public void BigBarrelBombTargetSelectionCanChooseEitherLane()
    {
        Random.State previousState = Random.state;

        try
        {
            Random.InitState(20260923);
            HashSet<int> selectedLanes = new HashSet<int>();

            for (int selectionIndex = 0;
                 selectionIndex < 32;
                 selectionIndex++)
            {
                selectedLanes.Add(
                    EnemyController.SelectBigBarrelBombTargetLane(
                        new[] { 0, 1 }));
            }

            Assert.That(selectedLanes, Is.EquivalentTo(new[] { 0, 1 }));
        }
        finally
        {
            Random.state = previousState;
        }
    }

    [Test]
    public void DirectionalEnemyQuery_ReturnsOnlyRequestedLane()
    {
        GameObject boardObject = new GameObject("Board Manager");
        GameObject waveObject = new GameObject("Wave Manager");
        GameObject tileParentObject = new GameObject("Tile Parent");
        GameObject tileTemplateObject = new GameObject("Tile Template");
        GameObject lowerEnemyObject = new GameObject("Lower Enemy");
        GameObject upperEnemyObject = new GameObject("Upper Enemy");

        try
        {
            BoardManager board = boardObject.AddComponent<BoardManager>();
            BoardTile tileTemplate =
                tileTemplateObject.AddComponent<BoardTile>();
            SerializedObject serializedBoard = new SerializedObject(board);
            serializedBoard.FindProperty("tileParent").objectReferenceValue =
                tileParentObject.transform;
            serializedBoard.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(board.ConfigureBoard(5, 2, tileTemplate), Is.True);

            EnemyController lowerEnemy =
                lowerEnemyObject.AddComponent<EnemyController>();
            EnemyController upperEnemy =
                upperEnemyObject.AddComponent<EnemyController>();
            ConfigureEnemyLane(lowerEnemy, 0);
            ConfigureEnemyLane(upperEnemy, 1);
            Assert.That(
                board.TryGetTilePosition(3, 0, out Vector3 lowerPosition),
                Is.True);
            Assert.That(
                board.TryGetTilePosition(3, 1, out Vector3 upperPosition),
                Is.True);
            lowerEnemy.transform.position = lowerPosition;
            upperEnemy.transform.position = upperPosition;

            WaveManager waveManager = waveObject.AddComponent<WaveManager>();
            SerializedObject serializedWave = new SerializedObject(waveManager);
            serializedWave.FindProperty("boardManager").objectReferenceValue =
                board;
            SerializedProperty activeEnemies =
                serializedWave.FindProperty("activeEnemies");
            activeEnemies.arraySize = 2;
            activeEnemies.GetArrayElementAtIndex(0).objectReferenceValue =
                lowerEnemy;
            activeEnemies.GetArrayElementAtIndex(1).objectReferenceValue =
                upperEnemy;
            serializedWave.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                board.TryGetTilePosition(1, 1, out Vector3 originPosition),
                Is.True);
            List<EnemyController> results = new List<EnemyController>();
            waveManager.GetEnemiesInDirection(
                originPosition,
                1,
                1,
                5,
                results);

            Assert.That(results, Is.EqualTo(new[] { upperEnemy }));
        }
        finally
        {
            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(waveObject);
            Object.DestroyImmediate(tileParentObject);
            Object.DestroyImmediate(tileTemplateObject);
            Object.DestroyImmediate(lowerEnemyObject);
            Object.DestroyImmediate(upperEnemyObject);
        }
    }

    [Test]
    public void TileWarningOwners_DoNotClearEachOtherOrPersistentWarnings()
    {
        GameObject managerObject = new GameObject("Board Manager");
        GameObject tileParentObject = new GameObject("Tile Parent");
        GameObject tileTemplateObject = new GameObject("Tile Template");
        GameObject warningObject = new GameObject("Warning");
        GameObject firstOwnerObject = new GameObject("First Warning Owner");
        GameObject secondOwnerObject = new GameObject("Second Warning Owner");

        try
        {
            warningObject.transform.SetParent(tileTemplateObject.transform);
            BoardTile tileTemplate =
                tileTemplateObject.AddComponent<BoardTile>();
            SerializedObject serializedTile = new SerializedObject(tileTemplate);
            serializedTile.FindProperty("warningObject").objectReferenceValue =
                warningObject;
            serializedTile.ApplyModifiedPropertiesWithoutUndo();

            BoardManager manager = managerObject.AddComponent<BoardManager>();
            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("tileParent").objectReferenceValue =
                tileParentObject.transform;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(manager.ConfigureBoard(2, 2, tileTemplate), Is.True);

            BoxCollider2D firstOwner =
                firstOwnerObject.AddComponent<BoxCollider2D>();
            BoxCollider2D secondOwner =
                secondOwnerObject.AddComponent<BoxCollider2D>();
            GameObject spawnedWarning = tileParentObject.transform
                .GetChild(3)
                .Find("Warning")
                .gameObject;

            Assert.That(
                manager.SetTileWarningActive(1, 1, firstOwner, true),
                Is.True);
            Assert.That(
                manager.SetTileWarningActive(1, 1, secondOwner, true),
                Is.True);
            manager.ReleaseTileWarnings(firstOwner);
            Assert.That(spawnedWarning.activeSelf, Is.True);

            Assert.That(manager.SetTileWarningActive(1, 1, true), Is.True);
            manager.ReleaseTileWarnings(secondOwner);
            Assert.That(spawnedWarning.activeSelf, Is.True);

            Assert.That(manager.SetTileWarningActive(1, 1, false), Is.True);
            Assert.That(spawnedWarning.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(tileParentObject);
            Object.DestroyImmediate(tileTemplateObject);
            Object.DestroyImmediate(firstOwnerObject);
            Object.DestroyImmediate(secondOwnerObject);
        }
    }

    private static void ConfigureEnemyLane(
        EnemyController enemy,
        int laneIndex)
    {
        SerializedObject serializedEnemy = new SerializedObject(enemy);
        serializedEnemy.FindProperty("currentHealth").intValue = 1;
        serializedEnemy.FindProperty("currentLaneIndex").intValue = laneIndex;
        serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
    }
}
