using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
}
