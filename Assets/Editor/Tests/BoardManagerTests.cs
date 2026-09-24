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
            Assert.That(lowerLeft, Is.EqualTo(new Vector3(-3f, -0.46f, 0f)));
            Assert.That(upperRight, Is.EqualTo(new Vector3(3f, 0.46f, 0f)));

            Assert.That(
                manager.TryGetTilePosition(1, out Vector3 defaultLane),
                Is.True);
            Assert.That(defaultLane, Is.EqualTo(
                new Vector3(-1f, -0.46f, 0f)));

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
                new Vector3(-1f, 0.46f, 0f)));
            Assert.That(
                manager.TryGetRangedTilePosition(
                    upperLanePosition,
                    1,
                    1,
                    1,
                    out Vector3 upperLaneRangedPosition),
                Is.True);
            Assert.That(upperLaneRangedPosition, Is.EqualTo(
                new Vector3(1f, 0.46f, 0f)));
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
    public void LowerLaneRendersInFrontOfUpperLane()
    {
        int lowerLaneOrder = EnemyController.CalculateLaneSortingOrder(0, 2);
        int upperLaneOrder = EnemyController.CalculateLaneSortingOrder(1, 2);
        int lowerCanvasOrder =
            EnemyController.CalculateLaneCanvasSortingOrder(0, 2);
        int upperCanvasOrder =
            EnemyController.CalculateLaneCanvasSortingOrder(1, 2);

        Assert.That(lowerLaneOrder, Is.GreaterThan(upperLaneOrder));
        Assert.That(
            lowerLaneOrder - upperLaneOrder,
            Is.EqualTo(EnemyController.LaneSortingOrderStride));
        Assert.That(upperCanvasOrder, Is.GreaterThan(lowerLaneOrder));
        Assert.That(lowerCanvasOrder, Is.GreaterThan(upperCanvasOrder));
    }

    [Test]
    public void UpperLaneAttackQueueReceivesAStableVerticalOffset()
    {
        GameObject ownerObject = new GameObject("Enemy Queue Owner");
        GameObject queueObject = new GameObject(
            "Image | Queue",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image));

        try
        {
            queueObject.transform.SetParent(ownerObject.transform, false);
            RectTransform queueRect =
                queueObject.GetComponent<RectTransform>();
            queueRect.anchoredPosition = new Vector2(3f, 4f);
            EnemyActionQueueUI queueUI =
                ownerObject.AddComponent<EnemyActionQueueUI>();
            SerializedObject serializedQueue = new SerializedObject(queueUI);
            serializedQueue.FindProperty("queueImage").objectReferenceValue =
                queueObject.GetComponent<UnityEngine.UI.Image>();
            serializedQueue.FindProperty("laneVerticalSeparation").floatValue =
                2f;
            serializedQueue.ApplyModifiedPropertiesWithoutUndo();

            queueUI.ApplyLaneLayout(1);
            Assert.That(queueRect.anchoredPosition, Is.EqualTo(
                new Vector2(3f, 6f)));

            queueUI.ApplyLaneLayout(1);
            Assert.That(queueRect.anchoredPosition, Is.EqualTo(
                new Vector2(3f, 6f)));

            queueUI.ApplyLaneLayout(0);
            Assert.That(queueRect.anchoredPosition, Is.EqualTo(
                new Vector2(3f, 4f)));
        }
        finally
        {
            Object.DestroyImmediate(ownerObject);
        }
    }

    [Test]
    public void EnemyPrefabInitializationAppliesLaneSortingAndQueueLayout()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemy/Enemy.prefab");
        GameObject enemyObject = Object.Instantiate(prefab);
        GameObject boardObject = new GameObject("Board Manager");

        try
        {
            Assert.That(prefab, Is.Not.Null);
            EnemyController enemy = enemyObject.GetComponent<EnemyController>();
            EnemyActionQueueUI queueUI =
                enemyObject.GetComponent<EnemyActionQueueUI>();
            RectTransform queueRect = enemyObject.transform
                .Find("Canvas/Image | Queue") as RectTransform;
            Assert.That(enemy, Is.Not.Null);
            Assert.That(queueUI, Is.Not.Null);
            Assert.That(queueRect, Is.Not.Null);
            float baseQueueY = queueRect.anchoredPosition.y;

            BoardManager board = boardObject.AddComponent<BoardManager>();
            System.Reflection.FieldInfo boardField = typeof(EnemyController)
                .GetField(
                    "boardManager",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic);
            Assert.That(boardField, Is.Not.Null);
            boardField.SetValue(enemy, board);
            SerializedObject serializedEnemy = new SerializedObject(enemy);
            serializedEnemy.FindProperty("currentLaneIndex").intValue = 1;
            serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
            enemy.ApplyLaneSortingOrder();

            UnityEngine.Rendering.SortingGroup sortingGroup =
                enemy.GetComponent<UnityEngine.Rendering.SortingGroup>();
            Canvas canvas = enemy.GetComponentInChildren<Canvas>(true);
            Assert.That(sortingGroup, Is.Not.Null);
            Assert.That(sortingGroup.sortingOrder,
                Is.EqualTo(EnemyController.CalculateLaneSortingOrder(1, 2)));
            Assert.That(canvas.sortingOrder,
                Is.EqualTo(
                    EnemyController.CalculateLaneCanvasSortingOrder(1, 2)));
            Assert.That(queueRect.anchoredPosition.y,
                Is.GreaterThan(baseQueueY));
        }
        finally
        {
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(boardObject);
        }
    }

    [Test]
    public void EnemyHudChangesSidesWithoutAccumulatingLaneOffsets()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemy/Enemy.prefab");
        GameObject enemyObject = Object.Instantiate(prefab);
        try
        {
            EnemyActionQueueUI ui = enemyObject.GetComponent<EnemyActionQueueUI>();
            RectTransform health = enemyObject.transform.Find("Canvas/Panel | HP_BG") as RectTransform;
            RectTransform queue = enemyObject.transform.Find("Canvas/Image | Queue") as RectTransform;
            RectTransform ready = enemyObject.transform.Find("Canvas/Image | Queue Ready") as RectTransform;
            Vector3 authoredHealthPosition = health.localPosition;
            Vector3 authoredQueuePosition = queue.localPosition;
            ui.ApplyLaneLayout(0);
            Vector3 lowerHealth = health.localPosition;
            Vector3 lowerQueue = queue.localPosition;
            Assert.That(lowerHealth.y, Is.EqualTo(authoredQueuePosition.y));
            Assert.That(lowerQueue.y + queue.rect.height * 0.5f,
                Is.LessThan(lowerHealth.y - health.rect.height * 0.5f));
            ui.ApplyLaneLayout(1);
            Vector3 upperHealth = health.localPosition;
            Vector3 upperQueue = queue.localPosition;
            Assert.That(upperHealth, Is.EqualTo(authoredHealthPosition));
            Assert.That(upperQueue.y - queue.rect.height * 0.5f,
                Is.GreaterThan(upperHealth.y + health.rect.height * 0.5f));
            ui.ApplyLaneLayout(1);
            Assert.That(queue.localPosition, Is.EqualTo(upperQueue));
            ui.ApplyLaneLayout(0);
            Assert.That(health.localPosition, Is.EqualTo(lowerHealth));
            Assert.That(queue.localPosition, Is.EqualTo(lowerQueue));
            Assert.That(ready.anchoredPosition, Is.EqualTo(queue.anchoredPosition));
        }
        finally
        {
            Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void SpawnLaneSelectionPrefersTheLeastPopulatedLane()
    {
        Assert.That(
            WaveManager.SelectLeastPopulatedLane(
                new[] { 3, 1 },
                new[] { 0, 1 },
                0.5f),
            Is.EqualTo(1));
        Assert.That(
            WaveManager.SelectLeastPopulatedLane(
                new[] { 0, 0 },
                new[] { 0, 1 },
                0f),
            Is.EqualTo(0));
        Assert.That(
            WaveManager.SelectLeastPopulatedLane(
                new[] { 0, 0 },
                new[] { 0, 1 },
                0.999f),
            Is.EqualTo(1));
    }

    [Test]
    public void SpawnReservationCellKeyRoundTripsTileAndLane()
    {
        int upperCellKey = WaveManager.EncodeBoardCellKey(2, 1, 5, 2);

        Assert.That(upperCellKey, Is.EqualTo(7));
        Assert.That(
            WaveManager.TryDecodeBoardCellKey(
                upperCellKey,
                5,
                2,
                out int tileIndex,
                out int laneIndex),
            Is.True);
        Assert.That(tileIndex, Is.EqualTo(2));
        Assert.That(laneIndex, Is.EqualTo(1));

        Assert.That(
            WaveManager.TryDecodeBoardCellKey(
                2,
                5,
                2,
                out int legacyTileIndex,
                out int legacyLaneIndex),
            Is.True);
        Assert.That(legacyTileIndex, Is.EqualTo(2));
        Assert.That(legacyLaneIndex, Is.Zero);
    }

    [Test]
    public void PlayerEffectsStayInLaneUnlessExplicitlyBoardWide()
    {
        Assert.That(
            PlayerShoot.CanPlayerEffectTargetLane(1, 1, false),
            Is.True);
        Assert.That(
            PlayerShoot.CanPlayerEffectTargetLane(1, 0, false),
            Is.False);
        Assert.That(
            PlayerShoot.CanPlayerEffectTargetLane(1, 0, true),
            Is.True);
    }

    [Test]
    public void OccupiedLaneMoveIsRejectedWithoutCompletingTurn()
    {
        GameObject boardObject = new GameObject("Board Manager");
        GameObject waveObject = new GameObject("Wave Manager");
        GameObject playerObject = new GameObject("Player");
        GameObject enemyObject = new GameObject("Enemy");
        GameObject tileParentObject = new GameObject("Tile Parent");
        GameObject tileTemplateObject = new GameObject("Tile Template");

        try
        {
            BoardManager board = boardObject.AddComponent<BoardManager>();
            BoardTile tileTemplate =
                tileTemplateObject.AddComponent<BoardTile>();
            SerializedObject serializedBoard = new SerializedObject(board);
            serializedBoard.FindProperty("tileParent").objectReferenceValue =
                tileParentObject.transform;
            serializedBoard.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(board.ConfigureBoard(3, 2, tileTemplate), Is.True);

            ActorMotion actorMotion = playerObject.AddComponent<ActorMotion>();
            PlayerMove playerMove = playerObject.AddComponent<PlayerMove>();
            SerializedObject serializedPlayer = new SerializedObject(playerMove);
            serializedPlayer.FindProperty("boardManager").objectReferenceValue =
                board;
            serializedPlayer.FindProperty("actorMotion").objectReferenceValue =
                actorMotion;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
            playerMove.SetLaneIndex(0);
            Assert.That(
                board.TryGetTilePosition(1, 0, out Vector3 playerPosition),
                Is.True);
            playerObject.transform.position = playerPosition;

            EnemyController enemy = enemyObject.AddComponent<EnemyController>();
            ConfigureEnemyLane(enemy, 1);
            Assert.That(
                board.TryGetTilePosition(0, 1, out Vector3 enemyPosition),
                Is.True);
            enemyObject.transform.position = enemyPosition;

            WaveManager waveManager = waveObject.AddComponent<WaveManager>();
            SerializedObject serializedWave = new SerializedObject(waveManager);
            serializedWave.FindProperty("boardManager").objectReferenceValue =
                board;
            SerializedProperty activeEnemies =
                serializedWave.FindProperty("activeEnemies");
            activeEnemies.arraySize = 1;
            activeEnemies.GetArrayElementAtIndex(0).objectReferenceValue = enemy;
            serializedWave.ApplyModifiedPropertiesWithoutUndo();
            playerMove.SetWaveManager(waveManager);
            int completedTurnCount = 0;
            playerMove.TurnCompleted += () => completedTurnCount++;

            playerMove.MoveUp();

            Assert.That(playerMove.CurrentLaneIndex, Is.Zero);
            Assert.That(playerMove.TurnCount, Is.Zero);
            Assert.That(completedTurnCount, Is.Zero);
            Assert.That(playerMove.IsActing, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(waveObject);
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(tileParentObject);
            Object.DestroyImmediate(tileTemplateObject);
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
            MeshRenderer gridWarning = tileParentObject.transform
                .GetChild(3).Find("Grid Warning").GetComponent<MeshRenderer>();
            Assert.That(gridWarning, Is.Not.Null);
            Assert.That(gridWarning.enabled, Is.False);

            Assert.That(
                manager.SetTileWarningActive(1, 1, firstOwner, true),
                Is.True);
            Assert.That(
                manager.SetTileWarningActive(1, 1, secondOwner, true),
                Is.True);
            manager.ReleaseTileWarnings(firstOwner);
            Assert.That(spawnedWarning.activeSelf, Is.True);
            Assert.That(gridWarning.enabled, Is.True);

            Assert.That(manager.SetTileWarningActive(1, 1, true), Is.True);
            manager.ReleaseTileWarnings(secondOwner);
            Assert.That(spawnedWarning.activeSelf, Is.True);

            Assert.That(manager.SetTileWarningActive(1, 1, false), Is.True);
            Assert.That(spawnedWarning.activeSelf, Is.False);
            Assert.That(gridWarning.enabled, Is.False);
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

    [TestCase(1, 1)]
    [TestCase(1, 3)]
    [TestCase(4, 2)]
    [TestCase(7, 3)]
    public void StaggeredCellsRoundTripWithActorHeightAndTransformedParent(
        int columns, int lanes)
    {
        GameObject root = new GameObject("Staggered Board Test");
        GameObject templateObject = new GameObject("Template");
        try
        {
            BoardManager board = root.AddComponent<BoardManager>();
            BoardTile template = templateObject.AddComponent<BoardTile>();
            GameObject parent = new GameObject("Tiles");
            parent.transform.SetParent(root.transform, false);
            parent.transform.localPosition = new Vector3(5f, -3f, 0f);
            parent.transform.localRotation = Quaternion.Euler(0f, 0f, 13f);
            parent.transform.localScale = new Vector3(1.5f, 0.8f, 1f);
            SerializedObject so = new SerializedObject(board);
            so.FindProperty("tileParent").objectReferenceValue = parent.transform;
            so.FindProperty("boardDistance").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(board.ConfigureBoard(columns, lanes, template), Is.True);

            for (int lane = 0; lane < lanes; lane++)
            {
                for (int tile = 0; tile < columns; tile++)
                {
                    Assert.That(board.TryGetTilePosition(tile, lane, out Vector3 position), Is.True);
                    Vector3 actorPosition = position + parent.transform.TransformVector(Vector3.up * 1.3f);
                    Assert.That(board.TryGetTileIndex(actorPosition, lane, out int restoredTile), Is.True);
                    Assert.That(restoredTile, Is.EqualTo(tile));
                    Assert.That(parent.transform.GetChild(lane * columns + tile).position,
                        Is.EqualTo(position).Using(UnityEngine.TestTools.Utils.Vector3ComparerWithEqualsOperator.Instance));

                    if (lane + 1 < lanes)
                    {
                        bool hasNeighbour = columns == 1 || tile > 0;
                        bool canMove = board.TryGetAdjacentLanePosition(tile, lane, 1,
                            out int destinationLane, out Vector3 destination);
                        Assert.That(canMove, Is.EqualTo(hasNeighbour));
                        if (canMove)
                        {
                            Assert.That(board.TryGetTileIndex(destination, destinationLane,
                                out int destinationTile), Is.True);
                            Assert.That(board.GetColumnIndex(tile, lane),
                                Is.EqualTo(board.GetColumnIndex(destinationTile, destinationLane)));
                        }
                    }
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(templateObject);
        }
    }

    [Test]
    public void StaggeredUpperLaneKeepsHorizontalBoundsAndMissingCorners()
    {
        GameObject root = new GameObject("Board Bounds Test");
        GameObject templateObject = new GameObject("Template");
        try
        {
            BoardManager board = root.AddComponent<BoardManager>();
            BoardTile template = templateObject.AddComponent<BoardTile>();
            SerializedObject so = new SerializedObject(board);
            so.FindProperty("tileParent").objectReferenceValue = root.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(board.ConfigureBoard(4, 2, template), Is.True);
            Assert.That(board.TryGetTilePosition(3, 1, out Vector3 upperRight), Is.True);
            Assert.That(board.TryGetAdjacentTilePosition(upperRight, 1, 1, out _), Is.False);
            Assert.That(board.TryGetRangedTilePosition(upperRight, 1, -1, 99,
                out Vector3 upperLeft), Is.True);
            Assert.That(board.TryGetTileIndex(upperLeft, 1, out int leftIndex), Is.True);
            Assert.That(leftIndex, Is.Zero);
            Assert.That(board.TryGetAdjacentLanePosition(0, 0, 1, out _, out _), Is.False);
            Assert.That(board.TryGetAdjacentLanePosition(3, 1, -1, out _, out _), Is.False);
            Assert.That(board.TryGetTileIndex(upperRight, 0, out _), Is.False);

            Assert.That(board.TryGetTilePosition(1, 0, out Vector3 lowerAligned), Is.True);
            Assert.That(board.TryGetTileDistance(upperLeft, lowerAligned, out int distance), Is.True);
            Assert.That(distance, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(templateObject);
        }
    }

    [Test]
    public void VerticalMoveAcrossStaggeredRowsPublishesOneTileDistance()
    {
        GameObject root = new GameObject("Movement Distance Test");
        try
        {
            BoardManager board = root.AddComponent<BoardManager>();
            PlayerMove player = root.AddComponent<PlayerMove>();
            SerializedObject boardSo = new SerializedObject(board);
            boardSo.FindProperty("boardCount").intValue = 4;
            boardSo.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject playerSo = new SerializedObject(player);
            playerSo.FindProperty("boardManager").objectReferenceValue = board;
            playerSo.ApplyModifiedPropertiesWithoutUndo();
            PlayerMovementContext? observed = null;
            player.PlayerMoved += context => observed = context;
            var method = typeof(PlayerMove).GetMethod("NotifyPlayerMoved",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(PlayerMovementSource) }, null);
            method.Invoke(player, new object[] { 2, 0, 1, 1, PlayerMovementSource.NormalMove });
            Assert.That(observed.HasValue, Is.True);
            Assert.That(observed.Value.Distance, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(root); }
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

public class EnemyTileTelegraphTests
{
    private readonly List<Object> created = new List<Object>();
    private BoardManager board;
    private Transform tiles;
    private const System.Reflection.BindingFlags PrivateInstance =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        board = Create<BoardManager>("Telegraph Board");
        tiles = new GameObject("Tiles").transform;
        created.Add(tiles.gameObject);
        BoardTile template = Create<BoardTile>("Template");
        SetField(board, "tileParent", tiles);
        Assert.That(board.ConfigureBoard(5, 2, template), Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
            {
                Object.DestroyImmediate(created[i]);
            }
        }
        created.Clear();
    }

    [TestCase(1, 3, 4)]
    [TestCase(-1, 0, 1)]
    public void GunnerColorsOnlyFacingTilesInItsLaneWithoutLineMaterial(
        int direction, int first, int last)
    {
        EnemyController enemy = CreateEnemy(EnemyBehaviorType.Gunner);
        SetField(enemy.Data, "firingRange", 2);
        Place(enemy, 2, 1);
        enemy.transform.localScale = new Vector3(direction, 1f, 1f);
        Invoke(enemy, "RefreshAttackTelegraph");
        AssertCells(1, first, last);
        Assert.That(enemy.GetComponentsInChildren<LineRenderer>(), Is.Empty);
        Invoke(enemy, "HideAttackTelegraph");
        AssertCells(-1);
    }

    [TestCase(1, 1, 3)]
    [TestCase(-1, 1, 1)]
    [TestCase(1, 2, 3, 4)]
    [TestCase(1, 0)]
    public void MeleeUsesQueuedAttackRangeAndOnlyShowsPreparedFacingCells(
        int direction, int range, params int[] expected)
    {
        EnemyController enemy = CreateEnemy(EnemyBehaviorType.Melee);
        EnemyAttackData attack = ScriptableObject.CreateInstance<EnemyAttackData>();
        EnemyActionData action = ScriptableObject.CreateInstance<EnemyActionData>();
        created.Add(attack);
        created.Add(action);
        SetField(attack, "range", range);
        SetField(action, "attackData", attack);
        SetField(enemy, "queuedAttackActions", new List<EnemyActionData> { action });
        Place(enemy, 2, 1);
        enemy.transform.localScale = new Vector3(direction, 1f, 1f);
        SetField(enemy, "isAttackPrepared", false);
        Invoke(enemy, "RefreshAttackTelegraph");
        AssertCells(-1);
        SetField(enemy, "isAttackPrepared", true);
        Invoke(enemy, "LateUpdate");
        AssertCells(1, expected);
        Assert.That(enemy.GetComponentsInChildren<LineRenderer>(), Is.Empty);
        Place(enemy, direction > 0 ? 4 : 0, 0);
        Invoke(enemy, "LateUpdate");
        AssertCells(-1);
        Invoke(enemy, "OnDisable");
        AssertCells(-1);
    }

    [Test]
    public void ThrowerRemovalPreservesOtherAttackAndSpawnWarnings()
    {
        EnemyController first = CreateEnemy(EnemyBehaviorType.Thrower);
        EnemyController second = CreateEnemy(EnemyBehaviorType.Thrower);
        foreach (EnemyController enemy in new[] { first, second })
        {
            SetField(enemy, "preparedTargetTileIndex", 3);
            SetField(enemy, "preparedTargetLaneIndex", 1);
            Invoke(enemy, "RefreshAttackTelegraph");
        }
        board.SetTileWarningActive(3, 1, true);
        Invoke(first, "OnDisable");
        AssertCells(1, 3);
        Invoke(second, "OnDisable");
        AssertCells(1, 3);
        board.SetTileWarningActive(3, 1, false);
        AssertCells(-1);
    }

    [Test]
    public void ShotgunRefreshMovesWarningsToNewLaneAndClipsMissingCorner()
    {
        EnemyController enemy = CreateEnemy(EnemyBehaviorType.BigBarrel);
        var step = typeof(EnemyController).GetField("bigBarrelStep", PrivateInstance);
        step.SetValue(enemy, System.Enum.Parse(step.FieldType, "ExecuteShotgun"));
        Place(enemy, 2, 1);
        Invoke(enemy, "RefreshPreparedShotgunAfterPositionChange");
        AssertCells(1, 1, 3);
        Place(enemy, 0, 0);
        Invoke(enemy, "RefreshPreparedShotgunAfterPositionChange");
        AssertCells(0, 1);
        Assert.That(enemy.GetComponentsInChildren<LineRenderer>(), Is.Empty);
        Invoke(enemy, "HideAttackTelegraph");
        AssertCells(-1);
    }

    [Test]
    public void BombFuseRestoreAndDisableReleaseOnlyItsClippedRange()
    {
        BossBombManager manager = Create<BossBombManager>("Bomb Manager");
        SetField(manager, "boardManager", board);
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        created.Add(data);
        BossBomb bomb = Create<BossBomb>("Bomb");
        Assert.That(bomb.Initialize(manager, data, 0, 1, 2, 0), Is.True);
        AssertCells(-1);
        bomb.ProcessEnemyTurnCycleEnd(1);
        int radius = Mathf.Min(4, data.BigBarrel.BombExplosionRadius);
        var expected = new List<int>();
        for (int i = 0; i <= radius; i++) expected.Add(i);
        AssertCells(1, expected.ToArray());
        Assert.That(bomb.GetComponentsInChildren<LineRenderer>(), Is.Empty);
        bomb.RestoreRunTiming(3, 0);
        AssertCells(-1);
        bomb.RestoreRunTiming(1, 0);
        AssertCells(1, expected.ToArray());
        Invoke(bomb, "OnDisable");
        AssertCells(-1);
        Invoke(bomb, "OnEnable");
        AssertCells(1, expected.ToArray());
        Assert.That(bomb.TryBeginExplosion(), Is.True);
        AssertCells(1, expected.ToArray());
        Assert.That(WarningProperty(0, 1, "_Urgency"), Is.EqualTo(1f));
        bomb.DisposeVisuals();
        AssertCells(-1);
    }

    [Test]
    public void DetachedThrowKeepsWarningAfterSourceRemovalAndReleasesOnCancel()
    {
        EnemyController enemy = CreateEnemy(EnemyBehaviorType.Thrower);
        SetField(enemy, "preparedTargetTileIndex", 2);
        SetField(enemy, "preparedTargetLaneIndex", 1);
        Invoke(enemy, "RefreshAttackTelegraph");
        GameObject projectile = new GameObject("Projectile");
        created.Add(projectile);
        var flight = new EnemyThrownProjectileFlight(Vector3.zero, Vector3.one,
            1f, 2f, 0.2f);
        var runtime = new EnemyThrownAttackRuntime(enemy, null, board,
            null, null, null, null, flight, projectile, 2, 1, Vector3.one,
            0, null, 1f);
        System.Collections.IEnumerator routine = runtime.Resolve();
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            Invoke(enemy, "OnDisable");
            AssertCells(1, 2);
        }
        finally
        {
            ((System.IDisposable)routine).Dispose();
        }
        Assert.That(runtime.IsComplete, Is.True);
        AssertCells(-1);
    }

    [TestCase(0f, 2)]
    [TestCase(1f, 4)]
    [TestCase(25f, 4)]
    [TestCase(50f, 4)]
    [TestCase(99f, 4)]
    [TestCase(100f, 4)]
    public void BulletTilePreviewPreservesColorLaneAndPenetration(float chance, int lastTile)
    {
        PlayerMove player = Create<PlayerMove>("Preview Player");
        player.SetLaneIndex(1);
        board.TryGetTilePosition(0, 1, out Vector3 position);
        player.transform.position = position;
        WaveManager wave = Create<WaveManager>("Preview Wave");
        SetField(wave, "boardManager", board);
        EnemyController first = CreateEnemy(EnemyBehaviorType.Melee);
        EnemyController second = CreateEnemy(EnemyBehaviorType.Melee);
        SetField(first, "currentHealth", 10);
        SetField(second, "currentHealth", 10);
        Place(first, 2, 1);
        Place(second, 3, 1);
        SetField(wave, "activeEnemies", new List<EnemyController> { first, second });
        BulletData data = ScriptableObject.CreateInstance<BulletData>();
        created.Add(data);
        SetField(data, "maxRange", 4);
        Color expectedColor = new Color(0.1f, 0.8f, 1f, 1f);
        SetField(data, "secondaryLineColor", expectedColor);
        var penetration = new PenetrationChanceData();
        SetField(penetration, "chance", chance);
        SetField(data, "penetrationChances", new List<PenetrationChanceData> { penetration, penetration });
        var preview = new PlayerShotRangePreview(player.transform, null, board, wave, null);
        var randomBefore = Random.state;
        try
        {
            Assert.That(preview.Show(new[] { new BulletInstance(data, 0) }, 0), Is.True);
            Assert.That(Random.state, Is.EqualTo(randomBefore));
            for (int row = 0; row < 2; row++)
            {
                for (int tile = 0; tile < 5; tile++)
                {
                    Transform view = tiles.GetChild(row * 5 + tile).Find("Grid Range Preview");
                    bool expected = row == 1 && tile > 0 && tile <= lastTile;
                    Assert.That(view != null && view.GetComponent<MeshRenderer>().enabled,
                        Is.EqualTo(expected));
                    if (!expected) continue;
                    var properties = new MaterialPropertyBlock();
                    view.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
                    Color actual = properties.GetColor("_Tint");
                    Assert.That(actual.r, Is.EqualTo(expectedColor.r).Within(0.001f));
                    Assert.That(actual.g, Is.EqualTo(expectedColor.g).Within(0.001f));
                    Assert.That(actual.b, Is.EqualTo(expectedColor.b).Within(0.001f));
                    Assert.That(actual.a, Is.EqualTo(tile <= 2 || chance >= 100f ? 1f : 0.5f).Within(0.001f));
                }
            }
            Assert.That(player.GetComponentsInChildren<LineRenderer>(), Is.Empty);
        }
        finally
        {
            preview.Dispose();
        }
        foreach (MeshRenderer renderer in tiles.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.name == "Grid Range Preview") Assert.That(renderer.enabled, Is.False);
        }
    }

    [Test]
    public void BulletPreviewRetainsDangerRimAndInvalidHoverOnlyClearsPreview()
    {
        Transform player = Create<PlayerMove>("Preview Player").transform;
        board.TryGetTilePosition(1, 0, out Vector3 position);
        player.position = position;
        BulletData data = ScriptableObject.CreateInstance<BulletData>();
        created.Add(data);
        SetField(data, "maxRange", 1);
        var preview = new PlayerShotRangePreview(player, null, board, null, null);
        try
        {
            board.SetTileWarningActive(2, 0, true);
            Assert.That(preview.Show(new[] { new BulletInstance(data, 0) }, 0), Is.True);
            var renderer = tiles.GetChild(2).Find("Grid Range Preview").GetComponent<MeshRenderer>();
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_Inset"), Is.GreaterThan(0f));
            Assert.That(properties.GetFloat("_WarningEffect"), Is.EqualTo(1f));
            Assert.That(renderer.sharedMaterial.shader,
                Is.EqualTo(tiles.GetChild(2).Find("Grid Warning").GetComponent<MeshRenderer>().sharedMaterial.shader));
            Assert.That(preview.Show(null, 0), Is.False);
            Assert.That(renderer.enabled, Is.False);
            AssertCells(0, 2);
        }
        finally { preview.Dispose(); }
    }

    [Test]
    public void BoardWideBulletPreviewCoversBothStaggeredLanes()
    {
        PlayerMove player = Create<PlayerMove>("Preview Player");
        player.SetLaneIndex(1);
        board.TryGetTilePosition(2, 1, out Vector3 position);
        player.transform.position = position;
        BulletData data = ScriptableObject.CreateInstance<BulletData>();
        created.Add(data);
        SetField(data, "bulletType", BulletType.Storm);
        var preview = new PlayerShotRangePreview(player.transform, null, board, null, null);
        try
        {
            Assert.That(preview.Show(new[] { new BulletInstance(data, 0) }, 0), Is.True);
            for (int i = 0; i < 10; i++)
            {
                var view = tiles.GetChild(i).Find("Grid Range Preview");
                Assert.That(view != null && view.GetComponent<MeshRenderer>().enabled, Is.EqualTo(i != 7));
            }
        }
        finally { preview.Dispose(); }
    }

    [Test]
    public void EnemyHoverSwitchesOnlyOwnedWarningsAndQueueEmphasis()
    {
        EnemyController first = CreateEnemy(EnemyBehaviorType.Thrower);
        EnemyController second = CreateEnemy(EnemyBehaviorType.Gunner);
        SetField(first, "currentHealth", 10);
        SetField(second, "currentHealth", 10);
        var firstImage = AttachHoverQueue(first);
        var secondImage = AttachHoverQueue(second);
        board.SetTileWarningActive(1, 0, first, true);
        board.SetTileWarningActive(2, 0, first, true);
        board.SetTileWarningActive(2, 0, second, true);
        board.SetTileWarningActive(3, 1, second, true);
        board.SetTileWarningActive(4, 0, true);
        var hover = new EnemyAttackHoverPresenter();
        try
        {
            hover.SetHovered(board, first);
            AssertFocus(0, 1, true);
            AssertFocus(0, 2, true);
            AssertFocus(1, 3, false);
            AssertFocus(0, 4, false);
            Assert.That(firstImage.GetComponent<UnityEngine.UI.Outline>().enabled, Is.True);
            Assert.That(firstImage.GetComponent<UnityEngine.UI.Outline>().effectDistance.x,
                Is.LessThanOrEqualTo(firstImage.rectTransform.rect.height * 0.1f));
            hover.SetHovered(board, second);
            AssertFocus(0, 1, false);
            AssertFocus(0, 2, true);
            AssertFocus(1, 3, true);
            Assert.That(firstImage.GetComponent<UnityEngine.UI.Outline>().enabled, Is.False);
            Assert.That(secondImage.GetComponent<UnityEngine.UI.Outline>().enabled, Is.True);
            Invoke(second, "OnDisable");
            AssertFocus(0, 2, false);
            AssertFocus(1, 3, false);
            Assert.That(secondImage.GetComponent<UnityEngine.UI.Outline>().enabled, Is.False);
            AssertCells(0, 1, 2, 4);
            hover.Clear();
            AssertCells(0, 1, 2, 4);
        }
        finally { hover.Clear(); }
    }

    [Test]
    public void HoveredOwnerCanPrepareOrCancelWithoutClearingOtherWarnings()
    {
        EnemyController enemy = CreateEnemy(EnemyBehaviorType.Melee);
        SetField(enemy, "currentHealth", 10);
        var hover = new EnemyAttackHoverPresenter();
        try
        {
            hover.SetHovered(board, enemy);
            AssertCells(-1);
            board.SetTileWarningActive(2, 1, enemy, true);
            AssertFocus(1, 2, true);
            board.SetTileWarningActive(2, 1, true);
            board.ReleaseTileWarnings(enemy);
            AssertFocus(1, 2, false);
            AssertCells(1, 2);
        }
        finally { hover.Clear(); }
    }

    [Test]
    public void WorldHoverChoosesFrontLaneAndIgnoresDeadOrHiddenAvatars()
    {
        EnemyController lower = CreateEnemy(EnemyBehaviorType.Melee);
        EnemyController upper = CreateEnemy(EnemyBehaviorType.Thrower);
        SetField(lower, "currentHealth", 10);
        SetField(upper, "currentHealth", 10);
        SetField(upper, "currentLaneIndex", 1);
        Sprite sprite = Sprite.Create(Texture2D.whiteTexture,
            new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        created.Add(sprite);
        foreach (EnemyController enemy in new[] { lower, upper })
        {
            SpriteRenderer renderer = enemy.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            SetField(enemy, "avatarSortingRenderer", renderer);
        }
        var enemies = new[] { upper, lower };
        Ray ray = new Ray(new Vector3(0f, 0f, -10f), Vector3.forward);
        Assert.That(EnemyAttackHoverPresenter.FindWorldTarget(enemies, ray), Is.SameAs(lower));
        SetField(lower, "currentHealth", 0);
        Assert.That(EnemyAttackHoverPresenter.FindWorldTarget(enemies, ray), Is.SameAs(upper));
        upper.HoverRenderer.enabled = false;
        Assert.That(EnemyAttackHoverPresenter.FindWorldTarget(enemies, ray), Is.Null);
    }

    [Test]
    public void QueueHoverRestoresExistingOutline()
    {
        EnemyController enemy = CreateEnemy(EnemyBehaviorType.Melee);
        var image = AttachHoverQueue(enemy);
        var outline = image.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = Color.cyan;
        outline.effectDistance = new Vector2(5f, -3f);
        outline.enabled = true;
        enemy.ActionQueueView.SetHovered(true);
        enemy.ActionQueueView.SetHovered(true);
        enemy.ActionQueueView.SetHovered(false);
        Assert.That(outline.enabled, Is.True);
        Assert.That(outline.effectColor, Is.EqualTo(Color.cyan));
        Assert.That(outline.effectDistance, Is.EqualTo(new Vector2(5f, -3f)));
    }

    [Test]
    public void UIHoverOnlySelectsLiveOwnerOfTopmostGraphic()
    {
        EnemyController first = CreateEnemy(EnemyBehaviorType.Thrower);
        EnemyController second = CreateEnemy(EnemyBehaviorType.Gunner);
        SetField(first, "currentHealth", 10);
        SetField(second, "currentHealth", 10);
        var firstImage = AttachHoverQueue(first);
        var secondImage = AttachHoverQueue(second);
        var enemies = new[] { first, second };
        Assert.That(EnemyAttackHoverPresenter.FindUIOwner(enemies, firstImage.transform), Is.SameAs(first));
        Assert.That(EnemyAttackHoverPresenter.FindUIOwner(enemies, secondImage.transform), Is.SameAs(second));
        Assert.That(EnemyAttackHoverPresenter.FindUIOwner(enemies, board.transform), Is.Null);
        SetField(first, "currentHealth", 0);
        Assert.That(EnemyAttackHoverPresenter.FindUIOwner(enemies, firstImage.transform), Is.Null);
    }

    [Test]
    public void CompletedWarningFadesWhileCancellationClearsImmediately()
    {
        EnemyController owner = CreateEnemy(EnemyBehaviorType.Thrower);
        board.SetTileWarningActive(2, 1, owner, true);
        board.SetWarningUrgent(owner, true);
        Assert.That(WarningProperty(2, 1, "_Urgency"), Is.EqualTo(1f));
        board.CompleteTileWarnings(owner);
        BoardTile tile = tiles.GetChild(7).GetComponent<BoardTile>();
        Assert.That(WarningProperty(2, 1, "_Afterglow"), Is.EqualTo(1f));
        tile.AdvanceWarningAfterglow(1f, true);
        Assert.That(WarningProperty(2, 1, "_Fade"), Is.EqualTo(1f));
        tile.AdvanceWarningAfterglow(0.09f, false);
        Assert.That(WarningProperty(2, 1, "_Fade"), Is.EqualTo(0.5f).Within(0.001f));
        tile.AdvanceWarningAfterglow(0.1f, false);
        AssertCells(-1);
        board.SetTileWarningActive(2, 1, owner, true);
        Assert.That(WarningProperty(2, 1, "_Afterglow"), Is.Zero);
        Assert.That(WarningProperty(2, 1, "_Urgency"), Is.Zero);
        board.ReleaseTileWarnings(owner);
        AssertCells(-1);
    }

    [Test]
    public void FinishingOneOwnerPreservesOtherUrgencyAndNewWarningsReplaceAfterglow()
    {
        EnemyController first = CreateEnemy(EnemyBehaviorType.Thrower);
        EnemyController second = CreateEnemy(EnemyBehaviorType.Thrower);
        board.SetTileWarningActive(2, 0, first, true);
        board.SetTileWarningActive(2, 0, second, true);
        board.SetWarningUrgent(first, true);
        board.SetWarningUrgent(second, true);
        board.CompleteTileWarnings(first);
        AssertCells(0, 2);
        Assert.That(WarningProperty(2, 0, "_Urgency"), Is.EqualTo(1f));
        Assert.That(WarningProperty(2, 0, "_Afterglow"), Is.Zero);
        board.CompleteTileWarnings(second);
        Assert.That(WarningProperty(2, 0, "_Afterglow"), Is.EqualTo(1f));
        board.SetTileWarningActive(2, 0, true);
        tiles.GetChild(2).GetComponent<BoardTile>().AdvanceWarningAfterglow(1f, false);
        AssertCells(0, 2);
        Assert.That(WarningProperty(2, 0, "_Fade"), Is.EqualTo(1f));
        Assert.That(WarningProperty(2, 0, "_Afterglow"), Is.Zero);
        board.SetTileWarningActive(2, 0, false);
        AssertCells(-1);
    }

    [Test]
    public void AttackWithoutAnimatorEmphasizesAtDodgeWindowAndCompletesAtImpact()
    {
        EnemyController enemy = CreateEnemy(EnemyBehaviorType.Melee);
        board.SetTileWarningActive(1, 0, enemy, true);
        int impacts = 0;
        System.Action<bool> onImpact = dodged => { impacts++; board.CompleteTileWarnings(enemy); };
        var method = typeof(EnemyController).GetMethod("PlayAvatarAnimation", PrivateInstance,
            null, new[] { typeof(int), typeof(System.Action<bool>), typeof(System.Func<bool>) }, null);
        var animation = (System.Collections.IEnumerator)method.Invoke(enemy,
            new object[] { 0, onImpact, new System.Func<bool>(() => false) });
        Assert.That(animation.MoveNext(), Is.True);
        Assert.That(impacts, Is.Zero);
        Assert.That(WarningProperty(1, 0, "_Urgency"), Is.EqualTo(1f));
        Assert.That(animation.Current, Is.InstanceOf<System.Collections.IEnumerator>());
        // Resume the parent after its timing child, as Unity's coroutine runner does.
        Assert.That(animation.MoveNext(), Is.False);
        Assert.That(impacts, Is.EqualTo(1));
        Assert.That(WarningProperty(1, 0, "_Afterglow"), Is.EqualTo(1f));
    }

    [Test]
    public void DetachedImpactCompletesWithAfterglowEvenWithoutSource()
    {
        GameObject projectile = new GameObject("Instant Projectile");
        created.Add(projectile);
        EnemyAttackData attack = ScriptableObject.CreateInstance<EnemyAttackData>();
        created.Add(attack);
        var flight = new EnemyThrownProjectileFlight(Vector3.zero, Vector3.one, 0f, 0f, 0.2f);
        var runtime = new EnemyThrownAttackRuntime(null, attack, board,
            null, null, null, null, flight, projectile, 2, 1, Vector3.one, 0, null, 1f);
        Assert.That(runtime.Resolve().MoveNext(), Is.False);
        Assert.That(runtime.IsComplete, Is.True);
        Assert.That(WarningProperty(2, 1, "_Afterglow"), Is.EqualTo(1f));
        tiles.GetChild(7).GetComponent<BoardTile>().AdvanceWarningAfterglow(1f, false);
        AssertCells(-1);
    }

    [Test]
    public void ExecutingMeleeUsesDequeuedAttackRangeAndCompletesWithoutPreparedState()
    {
        EnemyController enemy = CreateEnemy(EnemyBehaviorType.Melee);
        Place(enemy, 1, 0);
        SetField(enemy, "isAttackPrepared", false);
        EnemyAttackData attack = ScriptableObject.CreateInstance<EnemyAttackData>();
        created.Add(attack);
        SetField(attack, "range", 2);
        object presenter = typeof(EnemyController).GetField("telegraphPresenter", PrivateInstance).GetValue(enemy);
        presenter.GetType().GetMethod("BeginAttack").Invoke(presenter, new object[] { attack });
        AssertCells(0, 2, 3);
        Invoke(enemy, "LateUpdate");
        AssertCells(0, 2, 3);
        presenter.GetType().GetMethod("MarkAttackImminent").Invoke(presenter, null);
        Assert.That(WarningProperty(2, 0, "_Urgency"), Is.EqualTo(1f));
        presenter.GetType().GetMethod("CompleteAttack").Invoke(presenter, null);
        Assert.That(WarningProperty(2, 0, "_Afterglow"), Is.EqualTo(1f));
        Invoke(enemy, "HideAttackTelegraph");
        Assert.That(WarningProperty(2, 0, "_Afterglow"), Is.EqualTo(1f));
    }

    [Test]
    public void ChainedBombShowsImminentRangeAndBossDefeatCancelsIt()
    {
        BossBombManager manager = Create<BossBombManager>("Bomb Manager");
        SetField(manager, "boardManager", board);
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        created.Add(data);
        BossBomb bomb = Create<BossBomb>("Chained Bomb");
        Assert.That(bomb.Initialize(manager, data, 2, 0, 3, 0), Is.True);
        var active = (List<BossBomb>)typeof(BossBombManager).GetField("activeBombs", PrivateInstance).GetValue(manager);
        active.Add(bomb);
        AssertCells(-1);
        Assert.That(bomb.TryBeginExplosion(), Is.True);
        Assert.That(WarningProperty(2, 0, "_Urgency"), Is.EqualTo(1f));
        Assert.That(bomb.TryBeginExplosion(), Is.False);
        manager.PauseForBossDefeat();
        AssertCells(-1);
        active.Clear();
    }

    [TestCase(EnemyBehaviorType.Gunner, 1)]
    [TestCase(EnemyBehaviorType.Gunner, -1)]
    [TestCase(EnemyBehaviorType.Melee, 1)]
    [TestCase(EnemyBehaviorType.Melee, -1)]
    public void DirectWarningStopsAtFirstActualHitAndUpdatesDuringWindup(
        EnemyBehaviorType behavior, int direction)
    {
        EnemyController attacker = CreateEnemy(behavior);
        EnemyController blocker = CreateEnemy(EnemyBehaviorType.Gunner);
        SetField(blocker, "currentHealth", 10);
        SetField(attacker.Data, "firingRange", 4);
        EnemyAttackData attack = ScriptableObject.CreateInstance<EnemyAttackData>();
        EnemyActionData action = ScriptableObject.CreateInstance<EnemyActionData>();
        created.Add(attack);
        created.Add(action);
        SetField(attack, "range", 4);
        SetField(action, "attackData", attack);
        SetField(attacker, "queuedAttackActions", new List<EnemyActionData> { action });
        WaveManager waves = Create<WaveManager>("Blocking Wave");
        SetField(waves, "boardManager", board);
        SetField(waves, "activeEnemies", new List<EnemyController> { attacker, blocker });
        PlayerMove player = Create<PlayerMove>("Blocking Player");
        SetField(attacker, "waveManager", waves);
        SetField(attacker, "playerMove", player);
        Place(attacker, direction > 0 ? 0 : 4, 0);
        attacker.transform.localScale = new Vector3(direction, 1f, 1f);
        Place(blocker, 2, 0);
        board.TryGetTilePosition(direction > 0 ? 4 : 0, 0, out Vector3 playerPosition);
        player.transform.position = playerPosition;
        Invoke(attacker, "LateUpdate");
        AssertCells(0, direction > 0 ? new[] { 1, 2 } : new[] { 2, 3 });
        var targetArgs = new object[] { attack, null, false, Vector3.zero };
        Assert.That(typeof(EnemyController).GetMethod("TryGetAttackTarget", PrivateInstance)
            .Invoke(attacker, targetArgs), Is.True);
        Assert.That(targetArgs[1], Is.SameAs(blocker));

        SetField(attacker, "isAttackPrepared", false);
        object presenter = typeof(EnemyController).GetField("telegraphPresenter", PrivateInstance).GetValue(attacker);
        presenter.GetType().GetMethod("BeginAttack").Invoke(presenter, new object[] { attack });
        presenter.GetType().GetMethod("MarkAttackImminent").Invoke(presenter, null);
        Place(blocker, direction > 0 ? 1 : 3, 0);
        Invoke(attacker, "LateUpdate");
        AssertCells(0, direction > 0 ? 1 : 3);
        Assert.That(WarningProperty(direction > 0 ? 1 : 3, 0, "_Urgency"), Is.EqualTo(1f));

        SetField(blocker, "currentHealth", 0);
        Invoke(attacker, "LateUpdate");
        AssertCells(0, direction > 0 ? new[] { 1, 2, 3, 4 } : new[] { 0, 1, 2, 3 });
        SetField(blocker, "currentHealth", 10);
        Place(blocker, 2, 1);
        board.TryGetTilePosition(2, 0, out playerPosition);
        player.transform.position = playerPosition;
        Invoke(attacker, "LateUpdate");
        AssertCells(0, direction > 0 ? new[] { 1, 2 } : new[] { 2, 3 });
        targetArgs = new object[] { attack, null, false, Vector3.zero };
        Assert.That(typeof(EnemyController).GetMethod("TryGetAttackTarget", PrivateInstance)
            .Invoke(attacker, targetArgs), Is.True);
        Assert.That(targetArgs[2], Is.True);
    }

    private float WarningProperty(int tile, int lane, string property)
    {
        var renderer = tiles.GetChild(lane * 5 + tile).Find("Grid Warning").GetComponent<MeshRenderer>();
        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        return properties.GetFloat(property);
    }

    private UnityEngine.UI.Image AttachHoverQueue(EnemyController enemy)
    {
        var go = new GameObject("Test Queue", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(enemy.transform, false);
        var image = go.GetComponent<UnityEngine.UI.Image>();
        var queue = enemy.gameObject.AddComponent<EnemyActionQueueUI>();
        SetField(queue, "queueImage", image);
        SetField(enemy, "actionQueueUI", queue);
        return image;
    }

    private void AssertFocus(int lane, int tile, bool expected)
    {
        var renderer = tiles.GetChild(lane * 5 + tile).Find("Grid Warning").GetComponent<MeshRenderer>();
        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        Assert.That(properties.GetFloat("_Focus"), Is.EqualTo(expected ? 1f : 0f));
    }

    private T Create<T>(string name) where T : Component
    {
        GameObject go = new GameObject(name);
        created.Add(go);
        return go.AddComponent<T>();
    }

    private EnemyController CreateEnemy(EnemyBehaviorType behavior)
    {
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        created.Add(data);
        SetField(data, "behaviorType", behavior);
        EnemyController enemy = Create<EnemyController>("Enemy");
        var presenterType = typeof(EnemyController).GetNestedType(
            "EnemyTelegraphPresenter", System.Reflection.BindingFlags.NonPublic);
        SetField(enemy, "telegraphPresenter", System.Activator.CreateInstance(
            presenterType, new object[] { enemy }));
        SetField(enemy, "boardManager", board);
        SetField(enemy, "enemyData", data);
        SetField(enemy, "isAttackPrepared", true);
        return enemy;
    }

    private void Place(EnemyController enemy, int tile, int lane)
    {
        SetField(enemy, "currentLaneIndex", lane);
        Assert.That(board.TryGetTilePosition(tile, lane, out Vector3 position), Is.True);
        enemy.transform.position = position + Vector3.up * 0.3f;
    }

    private void AssertCells(int lane, params int[] expected)
    {
        var indices = new HashSet<int>(expected);
        for (int row = 0; row < 2; row++)
        {
            for (int tile = 0; tile < 5; tile++)
            {
                MeshRenderer warning = tiles.GetChild(row * 5 + tile)
                    .Find("Grid Warning").GetComponent<MeshRenderer>();
                Assert.That(warning.enabled, Is.EqualTo(row == lane && indices.Contains(tile)),
                    $"Unexpected warning at ({tile}, {row})");
            }
        }
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    }

    private static void Invoke(object target, string name)
    {
        target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);
    }
}
