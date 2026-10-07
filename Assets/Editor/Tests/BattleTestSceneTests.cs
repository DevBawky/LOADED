using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BattleTestSceneTests
{
    [Test]
    public void CommandTokenizerPreservesQuotedAssetNamesAndRejectsIncompleteQuotes()
    {
        Assert.That(BattleTestCommandRouter.Tokenize("  bullet add \"탄환 이름\" 3  "),
            Is.EqualTo(new[] { "bullet", "add", "탄환 이름", "3" }));
        Assert.That(BattleTestCommandRouter.Tokenize(null), Is.Empty);
        Assert.Throws<ArgumentException>(() => BattleTestCommandRouter.Tokenize("spawn \"unfinished"));
    }

    [Test]
    public void AuthoredSceneHasCompleteCatalogsAndLeavesNormalBuildScenesUnchanged()
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(BattleTestSceneBuilder.ScenePath);
            BattleTestController test = Object.FindFirstObjectByType<BattleTestController>();
            Assert.That(test, Is.Not.Null);
            var data = new SerializedObject(test);
            foreach (string field in new[] { "board", "waves", "deck", "relics", "player", "shooting",
                         "health", "inventory", "currency", "rewards", "feedback", "state", "world", "mainGamePanel", "environment" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(Object.FindFirstObjectByType<StateManager>().enabled, Is.False);
            Assert.That(Object.FindFirstObjectByType<FirstRunGuideController>(FindObjectsInactive.Include).enabled, Is.False);
            AssertCatalog(test.Bullets.ToArray());
            AssertCatalog(test.Enemies.ToArray());
            AssertCatalog(test.Relics.ToArray());
            AssertCatalog(test.Items.ToArray());
            Assert.That(EditorBuildSettings.scenes.Any(value => value.path == BattleTestSceneBuilder.ScenePath), Is.False);
            Assert.That(EditorBuildSettings.scenes.Any(value => value.path == "Assets/Scenes/Battle.unity" && value.enabled), Is.True);
        }
        finally
        {
            if (previous.Any(value => value.isLoaded && value.isActive && !string.IsNullOrEmpty(value.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    private static void AssertCatalog<T>(T[] catalog) where T : Object
    {
        T[] authored = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        Assert.That(catalog, Is.EquivalentTo(authored));
        Assert.That(catalog.Distinct().Count(), Is.EqualTo(catalog.Length));
        Assert.That(catalog, Has.None.Null);
    }

    [UnityTest]
    public IEnumerator SandboxExercisesRealCombatOwnersWithoutEndingOrSavingTheRun()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;

        Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            Is.EqualTo(BattleTestSceneBuilder.ScenePath));

        BattleTestController test = Object.FindFirstObjectByType<BattleTestController>();
        BattleTestConsole console = Object.FindFirstObjectByType<BattleTestConsole>();
        DeckManager deck = Object.FindFirstObjectByType<DeckManager>();
        WaveManager waves = Object.FindFirstObjectByType<WaveManager>();
        PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>();
        PlayerMove player = Object.FindFirstObjectByType<PlayerMove>();
        PlayerShoot shooting = Object.FindFirstObjectByType<PlayerShoot>();
        StateManager state = Object.FindFirstObjectByType<StateManager>();
        Assert.That(test, Is.Not.Null, "Test controller missing");
        Assert.That(console, Is.Not.Null, "Test console missing");
        Assert.That(deck, Is.Not.Null, "Deck missing");
        Assert.That(waves, Is.Not.Null, "Waves missing");
        Assert.That(health, Is.Not.Null, "Health missing");
        Assert.That(player, Is.Not.Null, "Player missing");
        Assert.That(shooting, Is.Not.Null, "Shooting missing");
        Assert.That(state, Is.Not.Null, "State missing");
        string statistics = JsonUtility.ToJson(GameStatistics.Data);

        Assert.That(test.IsReady, Is.True);
        Assert.That(waves.ActiveEnemies, Is.Empty);
        Assert.That(GamePauseController.IsPaused, Is.True);
        int turns = player.TurnCount;
        player.Wait();
        shooting.Reload();
        Assert.That(player.TurnCount, Is.EqualTo(turns), "Console must block normal combat actions.");

        // Exercise the real TMP submit event as well as command dispatch.
        var consoleData = new SerializedObject(console);
        var input = (TMP_InputField)consoleData.FindProperty("input").objectReferenceValue;
        input.onSubmit.Invoke("gold 321");
        Assert.That(Object.FindFirstObjectByType<CurrencyManager>().CurrentMoney, Is.EqualTo(321));
        Run(test, "board 9 3");
        Assert.That(Object.FindFirstObjectByType<BoardManager>().TotalTileCount, Is.EqualTo(27));
        Assert.That(test.ExecuteCommand("board 0 3"), Does.StartWith(BattleTestCommandRouter.RejectedPrefix));
        Assert.That(Object.FindFirstObjectByType<BoardManager>().TotalTileCount, Is.EqualTo(27));
        Run(test, "deck set 0 2");
        Run(test, "bullet add 1 3");
        Run(test, "bullet level 0 1");
        Run(test, "load 0");
        Run(test, "fill");
        Assert.That(deck.TotalBulletCount, Is.EqualTo(2));
        Assert.That(deck.LoadedBullets.Count, Is.EqualTo(2));
        Assert.That(deck.FindByAcquisitionOrder(0).Level, Is.EqualTo(1));
        int relicCount = Object.FindFirstObjectByType<RelicManager>().Count;
        Run(test, "relic add 0");
        Assert.That(Object.FindFirstObjectByType<RelicManager>().Count, Is.EqualTo(relicCount + 1));
        Run(test, "relic remove 0");
        Run(test, "item random");
        Run(test, "item remove 0");
        Run(test, "item set 0 0");
        Assert.That(Object.FindFirstObjectByType<PlayerInventory>().GetItem(0), Is.SameAs(test.Items[0]));
        Run(test, "gold random 42 42");
        Run(test, "hp 25 150");
        Run(test, "god on");
        health.ApplyDamage(1000);
        Assert.That(health.CurrentHealth, Is.EqualTo(25));
        Run(test, "god off");
        health.ApplyDamage(1000);
        Assert.That(health.IsDefeated, Is.True);
        Assert.That(GameOverController.IsGameOver, Is.False);
        Run(test, "hp 150 150");
        Assert.That(health.IsDefeated, Is.False);

        Run(test, "spawn 0 0 0");
        Assert.That(test.ExecuteCommand("spawn 0 0 0"), Does.StartWith(BattleTestCommandRouter.RejectedPrefix));
        Run(test, "status 0 0 Poison 7");
        Run(test, "status 0 0 Mark 3");
        Run(test, "enemy shield 0 0 50");
        Assert.That(waves.ActiveEnemies[0].GetStatusStacks(StatusEffectType.Poison), Is.EqualTo(7));
        Run(test, "ai off");
        Run(test, "checkpoint");
        Run(test, "random 3");
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(4));
        var occupied = new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (EnemyController enemy in waves.ActiveEnemies)
        {
            RunEnemySaveData saved = enemy.CaptureRunState(waves.ActiveEnemies);
            occupied.Add(new Vector2Int(saved.tileIndex, saved.laneIndex));
        }
        Assert.That(occupied.Count, Is.EqualTo(4));
        Run(test, "clear");
        Run(test, "hp 1");
        Run(test, "reset");
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(1));
        Assert.That(waves.ActiveEnemies[0].GetStatusStacks(StatusEffectType.Poison), Is.EqualTo(7));
        Assert.That(waves.ActiveEnemies[0].CaptureRunState(waves.ActiveEnemies).currentShield, Is.EqualTo(50));
        Assert.That(health.CurrentHealth, Is.EqualTo(150));
        Assert.That(deck.LoadedBullets.Count, Is.EqualTo(2));
        Assert.That(test.AutomaticTurns, Is.False);
        Assert.That(Object.FindFirstObjectByType<CurrencyManager>().CurrentMoney, Is.EqualTo(42));
        int beforePoison = waves.ActiveEnemies[0].CurrentHealth + waves.ActiveEnemies[0].CurrentShield;
        console.SetOpen(false);
        Run(test, "step");
        float statusDeadline = Time.realtimeSinceStartup + 10;
        while (!test.IsSettled && Time.realtimeSinceStartup < statusDeadline) yield return null;
        Assert.That(test.IsSettled, Is.True);
        Assert.That(waves.ActiveEnemies[0].CurrentHealth + waves.ActiveEnemies[0].CurrentShield,
            Is.LessThan(beforePoison));
        Assert.That(waves.ActiveEnemies[0].GetStatusStacks(StatusEffectType.Poison), Is.EqualTo(6));
        console.SetOpen(true);
        Run(test, "reset");
        Run(test, "enemy remove 0 0");
        Assert.That(waves.ActiveEnemies, Is.Empty);

        console.SetOpen(false);
        Assert.That(GamePauseController.IsPaused, Is.False);
        int manualTargetTurns = player.TurnCount + 7;
        float manualDeadline = Time.realtimeSinceStartup + 10;
        while (player.TurnCount < manualTargetTurns && Time.realtimeSinceStartup < manualDeadline)
        {
            player.Wait();
            yield return null;
        }
        Assert.That(player.TurnCount, Is.EqualTo(manualTargetTurns));
        Assert.That(waves.CurrentEnemyTurnCycle, Is.Zero, "Manual AI must not advance through player actions.");
        Run(test, "step");
        float deadline = Time.realtimeSinceStartup + 10;
        while (!test.IsSettled && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(test.IsSettled, Is.True);
        Assert.That(waves.CurrentEnemyTurnCycle, Is.EqualTo(1));
        Run(test, "spawn 0 0 0");
        Run(test, "enemy shield 0 0 9999");
        Run(test, "enemy kill 0 0");
        yield return new WaitForSeconds(1f);
        Assert.That(waves.ActiveEnemies, Is.Empty);
        Run(test, "deck set Normal 0");
        Run(test, "fill");
        Run(test, "player 2 0");
        Run(test, "spawn 0 3 0");
        Run(test, "enemy hp 3 0 1");
        shooting.Shoot();
        float shotDeadline = Time.realtimeSinceStartup + 10;
        while (!test.IsSettled && Time.realtimeSinceStartup < shotDeadline) yield return null;
        Assert.That(test.IsSettled, Is.True);
        Assert.That(waves.ActiveEnemies, Is.Empty, "A real loaded bullet must defeat the placed target.");
        Assert.That(deck.TotalBulletCount, Is.EqualTo(1));
        Run(test, "ai on");
        int previousCycle = waves.CurrentEnemyTurnCycle;
        int automaticTargetTurns = player.TurnCount + 6;
        float automaticDeadline = Time.realtimeSinceStartup + 10;
        while (player.TurnCount < automaticTargetTurns && Time.realtimeSinceStartup < automaticDeadline)
        {
            player.Wait();
            yield return null;
        }
        Assert.That(player.TurnCount, Is.EqualTo(automaticTargetTurns));
        float cycleDeadline = Time.realtimeSinceStartup + 10;
        while (!test.IsSettled && Time.realtimeSinceStartup < cycleDeadline) yield return null;
        Assert.That(waves.CurrentEnemyTurnCycle, Is.GreaterThan(previousCycle));
        Assert.That(state.CurrentState, Is.EqualTo(GameFlowState.Battle));
        Assert.That(state.SaveCurrentRun(), Is.False);
        Assert.That(JsonUtility.ToJson(GameStatistics.Data), Is.EqualTo(statistics));
        console.SetOpen(true);
        yield return new ExitPlayMode();
    }

    private static void Run(BattleTestController test, string command)
    {
        string result = test.ExecuteCommand(command);
        Assert.That(result, Does.Not.StartWith(BattleTestCommandRouter.RejectedPrefix), command + ": " + result);
        Assert.That(result, Does.Not.StartWith("알 수 없는"), command + ": " + result);
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}
