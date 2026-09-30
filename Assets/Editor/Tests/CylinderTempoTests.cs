using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CylinderTempoTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();
    }

    [TestCase(PlayerBehaviourAction.MoveLeft, 2)]
    [TestCase(PlayerBehaviourAction.MoveRight, 2)]
    [TestCase(PlayerBehaviourAction.MoveUp, 2)]
    [TestCase(PlayerBehaviourAction.MoveDown, 2)]
    [TestCase(PlayerBehaviourAction.Rotate, 1)]
    [TestCase(PlayerBehaviourAction.Wait, 1)]
    [TestCase(PlayerBehaviourAction.Reload, 3)]
    [TestCase(PlayerBehaviourAction.Shoot, 3)]
    public void ActionCostsMatchCylinderTempoRules(
        PlayerBehaviourAction action,
        int expectedCost)
    {
        Assert.That(
            DuelClockController.GetTempoCost(action),
            Is.EqualTo(expectedCost));
    }

    [Test]
    public void OverflowRemainsHiddenUntilEnemyCycleCompletes()
    {
        CreateController(
            out DuelClockController controller,
            out PlayerMove playerMove);
        long committedBeats = 0;
        controller.BeatsCommitted += beats => committedBeats += beats;

        CommitCompletedAction(
            controller,
            playerMove,
            PlayerBehaviourAction.Rotate);
        CommitCompletedAction(
            controller,
            playerMove,
            PlayerBehaviourAction.Rotate);
        CommitCompletedAction(
            controller,
            playerMove,
            PlayerBehaviourAction.Rotate);
        CommitCompletedAction(
            controller,
            playerMove,
            PlayerBehaviourAction.Rotate);
        CommitCompletedAction(
            controller,
            playerMove,
            PlayerBehaviourAction.Rotate);

        controller.HandlePlayerActionStarted(PlayerBehaviourAction.Shoot);

        Assert.That(committedBeats, Is.EqualTo(1));
        Assert.That(controller.IsTempoCycleReserved, Is.True);
        Assert.That(controller.TempoProgress, Is.EqualTo(6d));

        controller.HandleEnemyCycleStarted();

        Assert.That(controller.TempoProgress, Is.EqualTo(6d));

        int completionStateChanges = 0;
        controller.StateChanged += () => completionStateChanges++;
        controller.HandleEnemyCycleCompleted();

        Assert.That(controller.IsTempoCycleReserved, Is.False);
        Assert.That(controller.TempoProgress, Is.EqualTo(2d)
            .Within(0.0001d));
        Assert.That(completionStateChanges, Is.EqualTo(1));
    }

    [Test]
    public void SuccessfulDodgeMovementDoesNotAddTempo()
    {
        CreateController(
            out DuelClockController controller,
            out PlayerMove playerMove);

        controller.HandlePlayerActionStarted(
            PlayerBehaviourAction.MoveLeft);
        controller.HandlePlayerDodgeSucceededDuringAction();
        playerMove.CompleteTurn();

        Assert.That(controller.TempoProgress, Is.Zero);
    }

    [Test]
    public void FreeReloadWithoutTurnCompletionDoesNotAddTempo()
    {
        CreateController(
            out DuelClockController controller,
            out PlayerMove playerMove);

        controller.HandlePlayerActionStarted(
            PlayerBehaviourAction.Reload);
        controller.HandlePlayerActionStarted(
            PlayerBehaviourAction.Wait);
        playerMove.CompleteTurn();

        Assert.That(controller.TempoProgress, Is.EqualTo(1d)
            .Within(0.0001d));
    }

    [Test]
    public void NaturalTimeAndEnemyDefeatDoNotChangeTempo()
    {
        CreateController(
            out DuelClockController controller,
            out PlayerMove playerMove);
        CommitCompletedAction(
            controller,
            playerMove,
            PlayerBehaviourAction.MoveLeft);

        bool advanced = controller.TryAdvanceNaturalTime(100d);
        bool reduced = controller.ApplyEnemyDefeat();

        Assert.That(advanced, Is.False);
        Assert.That(reduced, Is.False);
        Assert.That(controller.TempoProgress, Is.EqualTo(2d)
            .Within(0.0001d));
    }

    [TestCase(1, 1)]
    [TestCase(10, 1)]
    [TestCase(11, 2)]
    [TestCase(20, 2)]
    [TestCase(21, 3)]
    [TestCase(100, 10)]
    public void EmptyBattleMinimumUsesCeilingTenPercent(
        int tileCount,
        int expectedMinimum)
    {
        Assert.That(
            WaveManager.CalculateMinimumCylinderTempoEnemyCount(tileCount),
            Is.EqualTo(expectedMinimum));
    }

    [Test]
    public void EmptyBattleSpawnCountIsClampedByPoolCapacityAndCells()
    {
        Assert.That(
            WaveManager.CalculateImmediateCylinderTempoSpawnCount(
                21,
                2,
                0,
                8,
                7),
            Is.EqualTo(2));
        Assert.That(
            WaveManager.CalculateImmediateCylinderTempoSpawnCount(
                21,
                10,
                0,
                2,
                7),
            Is.EqualTo(2));
        Assert.That(
            WaveManager.CalculateImmediateCylinderTempoSpawnCount(
                21,
                10,
                1,
                8,
                7),
            Is.Zero);
    }

    [Test]
    public void TurnActionsAreBlockedDuringEnemyResolution()
    {
        Assert.That(
            PlayerMove.ShouldBlockActionForEnemyResolution(true, true),
            Is.True);
        Assert.That(
            PlayerMove.ShouldBlockActionForEnemyResolution(true, false),
            Is.False);
    }

    [TestCase(EnemyTurnActionType.Fire, true, true)]
    [TestCase(EnemyTurnActionType.Fire, false, false)]
    [TestCase(EnemyTurnActionType.Move, true, false)]
    [TestCase(EnemyTurnActionType.Rotate, true, false)]
    [TestCase(EnemyTurnActionType.Wait, true, false)]
    public void EnemySpacingOnlyFollowsAnAttackWithAnotherEnemy(
        EnemyTurnActionType completedAction,
        bool hasFollowingEnemy,
        bool expected)
    {
        Assert.That(
            WaveManager.ShouldWaitBetweenEnemyActions(
                completedAction,
                hasFollowingEnemy),
            Is.EqualTo(expected));
    }

    [UnityTest]
    public IEnumerator EnemyCycleWaitsForPresentationThenExecutesBufferedReloadOnce()
    {
        EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single);

        yield return new EnterPlayMode();

        GameObject deckObject = new GameObject("Tempo Test Deck");
        DeckManager deckManager = deckObject.AddComponent<DeckManager>();
        GameObject playerObject = new GameObject("Tempo Test Player");
        playerObject.SetActive(false);
        PlayerMove playerMove = playerObject.AddComponent<PlayerMove>();
        playerObject.AddComponent<PlayerHealth>();
        PlayerShoot playerShoot = playerObject.AddComponent<PlayerShoot>();
        SerializedObject serializedMove = new SerializedObject(playerMove);
        serializedMove.FindProperty("instantActionCooldown").floatValue = 0f;
        serializedMove.FindProperty("inputBufferDuration").floatValue = 1f;
        serializedMove.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject serializedShoot = new SerializedObject(playerShoot);
        serializedShoot.FindProperty("deckManager").objectReferenceValue =
            deckManager;
        serializedShoot.FindProperty("playerMove").objectReferenceValue =
            playerMove;
        serializedShoot.ApplyModifiedPropertiesWithoutUndo();

        GameObject boardObject = new GameObject("Tempo Test Board");
        boardObject.SetActive(false);
        BoardManager boardManager = boardObject.AddComponent<BoardManager>();
        GameObject enemyTemplateObject =
            new GameObject("Tempo Test Enemy Template");
        enemyTemplateObject.SetActive(false);
        EnemyController enemyTemplate =
            enemyTemplateObject.AddComponent<EnemyController>();
        GameObject waveObject = new GameObject("Tempo Test Wave Manager");
        waveObject.SetActive(false);
        WaveManager waveManager = waveObject.AddComponent<WaveManager>();
        DuelClockController tempoController =
            waveObject.AddComponent<DuelClockController>();
        SerializedObject serializedWave = new SerializedObject(waveManager);
        serializedWave.FindProperty("enemyPrefabTemplate")
            .objectReferenceValue = enemyTemplate;
        serializedWave.FindProperty("boardManager").objectReferenceValue =
            boardManager;
        serializedWave.FindProperty("playerMove").objectReferenceValue =
            playerMove;
        serializedWave.FindProperty("playerHealth").objectReferenceValue =
            playerObject.GetComponent<PlayerHealth>();
        serializedWave.FindProperty("enemyTurnDelay").floatValue = 0f;
        serializedWave.FindProperty("enemyActionInterval").floatValue = 0f;
        serializedWave.FindProperty("combatPacingMode").enumValueIndex =
            (int)CombatPacingMode.DuelClock;
        serializedWave.ApplyModifiedPropertiesWithoutUndo();

        BulletData bullet = ScriptableObject.CreateInstance<BulletData>();
        BattleData battle = ScriptableObject.CreateInstance<BattleData>();

        try
        {
            playerObject.SetActive(true);
            waveObject.SetActive(true);
            playerMove.SetWaveManager(waveManager);
            tempoController.Initialize(playerMove, waveManager);
            tempoController.BeatsCommitted +=
                waveManager.QueueDuelClockBeats;
            tempoController.ConfigureFresh(
                battle,
                CombatPacingMode.DuelClock);
            Assert.That(deckManager.TryAddBullet(bullet), Is.True);

            yield return null;

            for (int actionIndex = 0; actionIndex < 5; actionIndex++)
            {
                playerMove.Wait();
            }

            Assert.That(playerMove.TurnCount, Is.EqualTo(5));
            Assert.That(
                tempoController.TempoProgress,
                Is.EqualTo(5d).Within(0.0001d));

            bool presentationCompleted = false;

            IEnumerator CompletePresentation()
            {
                for (int frame = 0; frame < 4; frame++)
                {
                    yield return null;
                }

                presentationCompleted = true;
            }

            Assert.That(
                waveManager.TryStartDetachedEnemyAttack(
                    CompletePresentation(),
                    null),
                Is.True);
            tempoController.HandlePlayerActionStarted(
                PlayerBehaviourAction.Shoot);
            playerMove.BufferInputAction(PlayerBehaviourAction.Reload);

            Assert.That(waveManager.IsResolvingTurn, Is.True);
            Assert.That(playerMove.IsEnemyTurnResolving, Is.True);
            Assert.That(playerMove.CanStartAction, Is.False);
            Assert.That(
                InvokeCanPerformMovementAction(playerMove),
                Is.True);
            Assert.That(tempoController.IsTempoCycleReserved, Is.True);
            Assert.That(tempoController.TempoProgress, Is.EqualTo(6d));

            yield return null;

            Assert.That(presentationCompleted, Is.False);
            Assert.That(deckManager.LoadedBullets, Is.Empty);
            Assert.That(playerMove.TurnCount, Is.EqualTo(5));
            Assert.That(tempoController.IsTempoCycleReserved, Is.True);

            int remainingFrames = 120;

            while (waveManager.IsResolvingTurn && remainingFrames-- > 0)
            {
                yield return null;
            }

            Assert.That(remainingFrames, Is.GreaterThan(0));
            Assert.That(presentationCompleted, Is.True);
            Assert.That(playerMove.IsEnemyTurnResolving, Is.False);

            remainingFrames = 30;

            while (deckManager.LoadedBullets.Count == 0
                   && remainingFrames-- > 0)
            {
                yield return null;
            }

            Assert.That(remainingFrames, Is.GreaterThan(0));
            Assert.That(deckManager.LoadedBullets, Has.Count.EqualTo(1));
            Assert.That(playerMove.TurnCount, Is.EqualTo(6));
            Assert.That(tempoController.IsTempoCycleReserved, Is.False);
            Assert.That(
                tempoController.TempoProgress,
                Is.EqualTo(5d).Within(0.0001d));
            Assert.That(
                playerMove.TryPeekBufferedInput(out _),
                Is.False);

            yield return null;
            yield return null;

            Assert.That(deckManager.LoadedBullets, Has.Count.EqualTo(1));
            Assert.That(playerMove.TurnCount, Is.EqualTo(6));
        }
        finally
        {
            tempoController.BeatsCommitted -=
                waveManager.QueueDuelClockBeats;
            Object.Destroy(waveObject);
            Object.Destroy(enemyTemplateObject);
            Object.Destroy(boardObject);
            Object.Destroy(playerObject);
            Object.Destroy(deckObject);
            Object.Destroy(bullet);
            Object.Destroy(battle);
        }

        yield return null;
        yield return new ExitPlayMode();

        EditorSceneManager.OpenScene(
            "Assets/Scenes/MainMenu.unity",
            OpenSceneMode.Single);
    }

    [Test]
    public void BattleHudKeepsPhaseLabelAndSixComboCounts()
    {
        GameObject canvas = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Canvas.prefab");

        Assert.That(canvas, Is.Not.Null);

        Transform tempoPanel = canvas.transform.Find(
            "Panel | Floating/Panel | Cylinder Tempo");
        Assert.That(tempoPanel, Is.Not.Null);

        TMP_Text phaseText = tempoPanel
            .Find("Text | Phase")
            ?.GetComponent<TMP_Text>();
        Assert.That(phaseText, Is.Not.Null);
        Assert.That(phaseText.text, Is.EqualTo("PLAYER PHASE"));
        Assert.That(phaseText.raycastTarget, Is.False);

        CylinderTempoHUD tempoHud =
            tempoPanel.GetComponent<CylinderTempoHUD>();
        Assert.That(tempoHud, Is.Not.Null);

        SerializedObject serializedHud = new SerializedObject(tempoHud);
        Assert.That(
            serializedHud.FindProperty("phaseText").objectReferenceValue,
            Is.SameAs(phaseText));

        Transform comboTimer = canvas.transform.Find(
            "Panel | MainGame/Panel | Feedback/Layout | Combo/"
            + "Image | Combo Timer BG");
        Assert.That(comboTimer, Is.Not.Null);
        Assert.That(comboTimer.childCount, Is.EqualTo(6));

        for (int index = 0; index < comboTimer.childCount; index++)
        {
            Transform slot = comboTimer.GetChild(index);
            Assert.That(
                slot.name,
                Is.EqualTo($"Image | Turn {index + 1}"));
            Assert.That(
                slot.Find("Image | Turn Value"),
                Is.Not.Null);
        }

        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            EditorSceneManager.OpenScene(
                "Assets/Scenes/Battle.unity",
                OpenSceneMode.Single);
            CombatFeedbackController feedback =
                Object.FindFirstObjectByType<CombatFeedbackController>(
                    FindObjectsInactive.Include);
            Assert.That(feedback, Is.Not.Null);

            SerializedObject serializedFeedback =
                new SerializedObject(feedback);
            Assert.That(
                serializedFeedback
                    .FindProperty("comboCountLimit")
                    .intValue,
                Is.EqualTo(6));

            WaveManager waveManager =
                Object.FindFirstObjectByType<WaveManager>(
                    FindObjectsInactive.Include);
            Assert.That(waveManager, Is.Not.Null);
            Assert.That(
                waveManager.EnemyTurnDelay,
                Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(
                waveManager.EnemyActionInterval,
                Is.EqualTo(0.05f).Within(0.0001f));
        }
        finally
        {
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    private void CreateController(
        out DuelClockController controller,
        out PlayerMove playerMove)
    {
        GameObject playerObject = new GameObject("Cylinder Tempo Player");
        GameObject managerObject = new GameObject("Cylinder Tempo Manager");
        createdObjects.Add(playerObject);
        createdObjects.Add(managerObject);

        playerMove = playerObject.AddComponent<PlayerMove>();
        WaveManager waveManager = managerObject.AddComponent<WaveManager>();
        controller = managerObject.AddComponent<DuelClockController>();
        BattleData battleData = ScriptableObject.CreateInstance<BattleData>();
        createdObjects.Add(battleData);
        controller.Initialize(playerMove, waveManager);
        controller.ConfigureFresh(
            battleData,
            CombatPacingMode.DuelClock);
    }

    private static void CommitCompletedAction(
        DuelClockController controller,
        PlayerMove playerMove,
        PlayerBehaviourAction action)
    {
        controller.HandlePlayerActionStarted(action);
        playerMove.CompleteTurn();
    }

    private static bool InvokeCanPerformMovementAction(
        PlayerMove playerMove)
    {
        System.Reflection.MethodInfo method = typeof(PlayerMove).GetMethod(
            "CanPerformMovementAction",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(playerMove, null);
    }
}
