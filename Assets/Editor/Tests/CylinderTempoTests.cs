using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CylinderTempoTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TestCase(0, 3)]
    [TestCase(-4, 3)]
    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(99, 3)]
    public void ReinforcementCountdownRestoresLegacyAndCurrentValues(int saved, int expected)
    {
        Assert.That(WaveManager.NormalizeReinforcementCountdown(saved), Is.EqualTo(expected));
    }

    [TestCase(true, 3, 0)]
    [TestCase(true, 2, 1)]
    [TestCase(true, 1, 2)]
    [TestCase(false, 1, 0)]
    public void ReinforcementTurnFillsReflectCompletedActions(
        bool hasRemainingEnemies,
        int actionsUntilReinforcement,
        int expectedFilledCount)
    {
        Assert.That(
            CylinderTempoHUD.CalculateReinforcementFilledTurnCount(
                hasRemainingEnemies,
                actionsUntilReinforcement),
            Is.EqualTo(expectedFilledCount));
    }

    [TestCase(1, 3, 5, 4, true)]
    [TestCase(2, 3, 5, 4, false)]
    [TestCase(1, 3, 5, 5, false)]
    [TestCase(1, 2, 5, 4, false)]
    public void RegularReinforcementSpawnRequiresCompletedThirdAction(
        int previousActions,
        int currentActions,
        int previousRemainingEnemyCount,
        int currentRemainingEnemyCount,
        bool expected)
    {
        Assert.That(
            CylinderTempoHUD.IsRegularReinforcementSpawn(
                previousActions,
                currentActions,
                previousRemainingEnemyCount,
                currentRemainingEnemyCount),
            Is.EqualTo(expected));
    }

    [Test]
    public void PaidActionPublishesPendingStateImmediately()
    {
        CreateController(
            out DuelClockController controller,
            out _);
        int stateChangeCount = 0;
        controller.StateChanged += () => stateChangeCount++;

        controller.HandlePlayerActionStarted(PlayerBehaviourAction.Wait);

        Assert.That(controller.HasPendingPaidAction, Is.True);
        Assert.That(stateChangeCount, Is.EqualTo(1));

        controller.HandlePlayerDodgeSucceededDuringAction();

        Assert.That(controller.HasPendingPaidAction, Is.False);
        Assert.That(stateChangeCount, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator ReinforcementsUseThreeActionsAndResumeSavedCountdown()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseReinforcementCountdown();
        yield return new ExitPlayMode();
    }

    private static IEnumerator ExerciseReinforcementCountdown()
    {
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        yield return null;
        var test = Object.FindFirstObjectByType<BattleTestController>();
        var player = Object.FindFirstObjectByType<PlayerMove>();
        var wave = Object.FindFirstObjectByType<WaveManager>();
        var tempoHud = Object.FindFirstObjectByType<CylinderTempoHUD>();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        foreach (var button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            if (button.GetComponentInChildren<TMP_Text>()?.text == "전투로 돌아가기") button.onClick.Invoke();
        test.ExecuteCommand("board 5 2");
        test.ExecuteCommand("player 2 0");
        test.ExecuteCommand("god on");
        test.ExecuteCommand("refill off");
        var battle = AssetDatabase.LoadAssetAtPath<BattleData>("Assets/Scripts/Manager/Battle SO/Stage 1/1 Entry/Stage 1 Entry.asset");
        Assert.That(wave.BeginBattle(battle), Is.True);
        Assert.That(tempoHud, Is.Not.Null);
        var reinforcementTurnFills = (Image[])typeof(CylinderTempoHUD)
            .GetField("reinforcementTurnFills", flags)
            .GetValue(tempoHud);
        var spawnLeftText = (TMP_Text)typeof(CylinderTempoHUD)
            .GetField("spawnLeftText", flags)
            .GetValue(tempoHud);
        Assert.That(reinforcementTurnFills, Has.Length.EqualTo(3));
        Assert.That(spawnLeftText, Is.Not.Null);
        Color spawnLeftRestColor = spawnLeftText.color;
        Vector3 spawnLeftRestScale = spawnLeftText.rectTransform.localScale;
        bool observedThirdFillDuringAction = false;
        player.SetInputLocked(true);
        IEnumerator Act(int expectedFillIndex = -1)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!test.IsSettled || Time.unscaledTime < (float)typeof(PlayerMove).GetField("nextInstantActionAllowedAt", flags).GetValue(player))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
            int cycle = wave.CurrentEnemyTurnCycle;
            player.SetInputLocked(false); player.Wait(); player.SetInputLocked(true);
            yield return null;

            if (expectedFillIndex >= 0)
            {
                Assert.That(
                    reinforcementTurnFills[expectedFillIndex].fillAmount,
                    Is.GreaterThan(0f),
                    "The next reinforcement slot must begin filling when the action starts.");
            }

            while (!test.IsSettled || wave.CurrentEnemyTurnCycle == cycle)
            {
                if (expectedFillIndex == 2)
                {
                    observedThirdFillDuringAction |=
                        reinforcementTurnFills[2].fillAmount >= 0.999f;
                }

                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
            Assert.That(wave.CurrentEnemyTurnCycle, Is.EqualTo(cycle + 1));
        }
        IEnumerator WaitForFill(Image fill)
        {
            float deadline = Time.realtimeSinceStartup + 1f;
            while (fill.fillAmount < 0.999f)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
        }
        bool SpawnColorIsAtRest()
        {
            return Mathf.Abs(spawnLeftText.color.r - spawnLeftRestColor.r)
                    <= 0.001f
                && Mathf.Abs(spawnLeftText.color.g - spawnLeftRestColor.g)
                    <= 0.001f
                && Mathf.Abs(spawnLeftText.color.b - spawnLeftRestColor.b)
                    <= 0.001f
                && Mathf.Abs(spawnLeftText.color.a - spawnLeftRestColor.a)
                    <= 0.001f;
        }
        bool SpawnScaleIsAtRest()
        {
            return (spawnLeftText.rectTransform.localScale
                    - spawnLeftRestScale).sqrMagnitude <= 0.000001f;
        }
        try
        {
            Assert.That(wave.LivingEnemyCount, Is.EqualTo(1));
            Assert.That(wave.ActionsUntilReinforcement, Is.EqualTo(3));
            yield return Act(0);
            yield return WaitForFill(reinforcementTurnFills[0]);
            Assert.That(reinforcementTurnFills[1].fillAmount, Is.Zero);
            Assert.That(reinforcementTurnFills[2].fillAmount, Is.Zero);
            Assert.That(wave.LivingEnemyCount, Is.EqualTo(1));
            Assert.That(wave.ActionsUntilReinforcement, Is.EqualTo(2));
            var saved = new RunSaveData(); wave.CaptureRunState(saved);
            saved = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(saved));
            Assert.That(wave.RestoreBattle(battle, saved), Is.True);
            Assert.That(wave.ActionsUntilReinforcement, Is.EqualTo(2));
            yield return Act(1);
            yield return WaitForFill(reinforcementTurnFills[1]);
            Assert.That(reinforcementTurnFills[2].fillAmount, Is.Zero);
            Assert.That(wave.LivingEnemyCount, Is.EqualTo(1));
            Assert.That(wave.ActionsUntilReinforcement, Is.EqualTo(1));
            yield return Act(2);

            float feedbackDeadline = Time.realtimeSinceStartup + 1f;
            bool observedSpawnPulse = false;

            while (Time.realtimeSinceStartup < feedbackDeadline
                   && !observedSpawnPulse)
            {
                observedSpawnPulse |= !SpawnColorIsAtRest()
                    || !SpawnScaleIsAtRest();
                yield return null;
            }

            Assert.That(observedThirdFillDuringAction, Is.True);
            Assert.That(observedSpawnPulse, Is.True);

            float resetDeadline = Time.realtimeSinceStartup + 1f;
            while ((reinforcementTurnFills[0].fillAmount > 0f
                    || reinforcementTurnFills[1].fillAmount > 0f
                    || reinforcementTurnFills[2].fillAmount > 0f
                    || !SpawnColorIsAtRest()
                    || !SpawnScaleIsAtRest())
                   && Time.realtimeSinceStartup < resetDeadline)
            {
                yield return null;
            }

            Assert.That(reinforcementTurnFills[0].fillAmount, Is.Zero);
            Assert.That(reinforcementTurnFills[1].fillAmount, Is.Zero);
            Assert.That(reinforcementTurnFills[2].fillAmount, Is.Zero);
            Assert.That(SpawnColorIsAtRest(), Is.True);
            Assert.That(SpawnScaleIsAtRest(), Is.True);
            Assert.That(wave.LivingEnemyCount, Is.EqualTo(2));
            Assert.That(wave.ActionsUntilReinforcement, Is.EqualTo(3));
            // Capacity pauses rather than banking later reinforcements.
            typeof(WaveManager).GetField("maximumActiveEnemyCount", flags).SetValue(wave, 2);
            yield return Act(); yield return Act();
            Assert.That(wave.LivingEnemyCount, Is.EqualTo(2));
            Assert.That(wave.ActionsUntilReinforcement, Is.EqualTo(3));
            typeof(WaveManager).GetField("maximumActiveEnemyCount", flags).SetValue(wave, 4);
            // Clearing every current enemy retains immediate replacement.
            var old = new List<EnemyController>(wave.ActiveEnemies);
            int poolBefore = wave.RemainingUnspawnedEnemyCount;
            foreach (var enemy in old) enemy.ApplyEnvironmentalDamage(100000);
            Assert.That(wave.LivingEnemyCount, Is.EqualTo(1));
            Assert.That(wave.RemainingUnspawnedEnemyCount, Is.EqualTo(poolBefore - 1));
            Assert.That(wave.ActionsUntilReinforcement, Is.EqualTo(3));
            yield return Act();
            Assert.That(wave.LivingEnemyCount, Is.EqualTo(1), "Immediate replacement must not cause an extra regular spawn");
            Assert.That(wave.ActionsUntilReinforcement, Is.EqualTo(2));
        }
        finally
        {
            player.SetInputLocked(false);
            wave.StopBattle();
        }
    }

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

    [TestCase(PlayerBehaviourAction.MoveLeft, 6)]
    [TestCase(PlayerBehaviourAction.MoveRight, 6)]
    [TestCase(PlayerBehaviourAction.MoveUp, 6)]
    [TestCase(PlayerBehaviourAction.MoveDown, 6)]
    [TestCase(PlayerBehaviourAction.Rotate, 6)]
    [TestCase(PlayerBehaviourAction.Wait, 6)]
    [TestCase(PlayerBehaviourAction.Reload, 6)]
    [TestCase(PlayerBehaviourAction.Shoot, 6)]
    public void ActionCostsMatchCylinderTempoRules(
        PlayerBehaviourAction action,
        int expectedCost)
    {
        Assert.That(
            DuelClockController.GetTempoCost(action),
            Is.EqualTo(expectedCost));
    }

    [Test]
    public void ShotCommitsOnlyOnCompletionAndHasNoCarry()
    {
        CreateController(out DuelClockController controller, out PlayerMove playerMove);
        long beats = 0;
        controller.BeatsCommitted += count => beats += count;
        controller.HandlePlayerActionStarted(PlayerBehaviourAction.Shoot);
        Assert.That(beats, Is.Zero);
        playerMove.CompleteTurn();
        Assert.That(beats, Is.EqualTo(1));
        Assert.That(controller.IsTempoCycleReserved, Is.True);
        controller.HandlePlayerActionStarted(PlayerBehaviourAction.MoveLeft);
        playerMove.CompleteTurn();
        controller.HandleEnemyCycleCompleted();
        Assert.That(beats, Is.EqualTo(1));
        Assert.That(controller.TempoProgress, Is.Zero);
    }

    [Test]
    public void RestoreDiscardsOldTempoButKeepsCompletedCycles()
    {
        CreateController(out DuelClockController controller, out _);
        BattleData battle = ScriptableObject.CreateInstance<BattleData>();
        createdObjects.Add(battle);
        controller.ConfigureRestored(battle, CombatPacingMode.DuelClock,
            new RunSaveData { duelClockProgress = 83, duelClockCumulativeBeats = 7 });
        Assert.That(controller.Progress, Is.Zero);
        Assert.That(controller.CumulativeBeats, Is.EqualTo(7));
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

        Assert.That(controller.TempoProgress, Is.EqualTo(6d)
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
        Assert.That(controller.TempoProgress, Is.EqualTo(6d)
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
            playerMove.CompleteTurn();
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
            Assert.That(playerMove.TurnCount, Is.EqualTo(1));
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
            Assert.That(playerMove.TurnCount, Is.EqualTo(2));
            remainingFrames = 120;
            while (waveManager.IsResolvingTurn && remainingFrames-- > 0) yield return null;
            Assert.That(remainingFrames, Is.GreaterThan(0));
            Assert.That(tempoController.IsTempoCycleReserved, Is.False);
            Assert.That(tempoController.TempoProgress, Is.Zero);
            Assert.That(
                playerMove.TryPeekBufferedInput(out _),
                Is.False);

            yield return null;
            yield return null;

            Assert.That(deckManager.LoadedBullets, Has.Count.EqualTo(1));
            Assert.That(playerMove.TurnCount, Is.EqualTo(2));
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
    public void BattleHudKeepsPhaseLabelAndEightComboCounts()
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

        Transform leftTurnLayout = tempoPanel.Find("Layout | Left Turn");
        Assert.That(leftTurnLayout, Is.Not.Null);
        Assert.That(leftTurnLayout.gameObject.activeSelf, Is.True);
        Assert.That(leftTurnLayout.childCount, Is.EqualTo(3));
        Assert.That(
            tempoPanel.Find("Layout | Tempo")?.gameObject.activeSelf,
            Is.False);

        SerializedProperty turnFills = serializedHud.FindProperty(
            "reinforcementTurnFills");
        Assert.That(turnFills.arraySize, Is.EqualTo(3));

        for (int index = 0; index < leftTurnLayout.childCount; index++)
        {
            Transform turn = leftTurnLayout.GetChild(index);
            Assert.That(turn.name, Is.EqualTo("Image | Turn"));
            Image fill = turn.Find("Image | Turn Fill")
                ?.GetComponent<Image>();
            Assert.That(fill, Is.Not.Null);
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(fill.raycastTarget, Is.False);
            Assert.That(
                turnFills.GetArrayElementAtIndex(index)
                    .objectReferenceValue,
                Is.SameAs(fill));
        }

        Assert.That(
            serializedHud.FindProperty("reinforcementFillDuration")
                .floatValue,
            Is.EqualTo(0.1f).Within(0.0001f));
        Assert.That(
            serializedHud.FindProperty("spawnLeftPulseDuration")
                .floatValue,
            Is.EqualTo(0.7f).Within(0.0001f));

        Transform comboTimer = canvas.transform.Find(
            "Panel | MainGame/Panel | Feedback/Layout | Combo/"
            + "Image | Combo Timer BG");
        Assert.That(comboTimer, Is.Not.Null);
        Assert.That(comboTimer.childCount, Is.EqualTo(8));

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
                Is.EqualTo(8));

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

public sealed class EnemyTurnCycleRunnerTests
{
    [Test]
    public void EmptyCycleSettlesEnemiesAroundBossBombProcessing()
    {
        RecordingEnemyTurnCycleRuntime runtime =
            new RecordingEnemyTurnCycleRuntime();
        EnemyTurnCycleRunner runner = new EnemyTurnCycleRunner();

        Drain(runner.Resolve(runtime, 7, 0f, 0f));

        Assert.That(
            runtime.Operations,
            Is.EqualTo(new[]
            {
                "remove-missing",
                "remove-missing",
                "process-bombs:7",
                "remove-missing"
            }));
    }

    private static void Drain(IEnumerator root)
    {
        Stack<IEnumerator> routines = new Stack<IEnumerator>();
        routines.Push(root);

        while (routines.Count > 0)
        {
            IEnumerator current = routines.Peek();

            if (!current.MoveNext())
            {
                routines.Pop();
                continue;
            }

            if (current.Current is IEnumerator nested)
            {
                routines.Push(nested);
            }
        }
    }

    private sealed class RecordingEnemyTurnCycleRuntime
        : IEnemyTurnCycleRuntime
    {
        private readonly List<EnemyController> activeEnemies =
            new List<EnemyController>();

        public List<string> Operations { get; } = new List<string>();
        public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies;
        public bool IsBattleCompleted => false;
        public bool IsPlayerDefeated => false;
        public bool HasPendingDetachedEnemyAttacks => false;
        public bool IsResolvingBossBombExplosions => false;

        public void RemoveMissingEnemies()
        {
            Operations.Add("remove-missing");
        }

        public void ProcessBossBombs(int enemyTurnCycle)
        {
            Operations.Add($"process-bombs:{enemyTurnCycle}");
        }
    }
}
