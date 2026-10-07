using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BattleTestEnemyRefillTests
{
    [TestCase(14, 50, 13, 7)]
    [TestCase(15, 50, 14, 8)]
    [TestCase(14, 100, 13, 13)]
    [TestCase(180, 100, 179, 179)]
    [TestCase(14, 0, 13, 0)]
    [TestCase(14, 1, 13, 1)]
    [TestCase(14, 75, 3, 3)]
    [TestCase(14, 100, 0, 0)]
    public void OccupancyUsesAllTilesRoundsUpAndCapsAtFreeCells(int total, int percent, int free, int expected)
    {
        Assert.That(BattleTestController.CalculateEnemyRefillCount(total, percent, free), Is.EqualTo(expected));
    }

    [UnityTest]
    public IEnumerator SpawnPoolDefaultsExcludeBossesAndGuiSettingsControlBothRandomPaths()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;
        BattleTestController test = Object.FindFirstObjectByType<BattleTestController>();
        BattleTestGui gui = Object.FindFirstObjectByType<BattleTestGui>();
        int bossIndex = -1, normalIndex = -1;
        for (int i = 0; i < test.Enemies.Count; i++)
        {
            bool isBoss = test.Enemies[i].BehaviorType == EnemyBehaviorType.BigBarrel;
            Assert.That(test.IsEnemySpawnAllowed(test.Enemies[i]), Is.EqualTo(!isBoss));
            if (isBoss) bossIndex = i;
            else normalIndex = i;
        }
        Assert.That(bossIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(normalIndex, Is.GreaterThanOrEqualTo(0));
        Run(test, "random 13");
        Assert.That(test.TestWaves.ActiveEnemies.Count, Is.EqualTo(13));
        foreach (EnemyController enemy in test.TestWaves.ActiveEnemies)
            Assert.That(enemy.Data.BehaviorType, Is.Not.EqualTo(EnemyBehaviorType.BigBarrel));
        Run(test, "clear");
        Run(test, $"spawn {bossIndex} 0 0");
        Assert.That(test.TestWaves.ActiveEnemies[0].Data, Is.SameAs(test.Enemies[bossIndex]), "직접 배치는 허용해야 합니다.");
        Run(test, "clear");
        for (int i = 0; i < test.Enemies.Count; i++) Run(test, $"spawnpool {i} off");
        Run(test, "refill on 50");
        yield return null;
        yield return null;
        Assert.That(test.TestWaves.ActiveEnemies.Count, Is.Zero);
        Assert.That(test.EnemyRefillSummary, Does.Contain("대상 없음"));
        string rng = JsonUtility.ToJson(Random.state);
        Assert.That(test.ExecuteCommand("random 1"), Does.StartWith(BattleTestCommandRouter.RejectedPrefix));
        Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(rng));

        gui.SelectPage(2);
        yield return null;
        BattleTestListRow bossRow = null;
        foreach (BattleTestListRow row in Field<RectTransform>(gui, "catalogContent").GetComponentsInChildren<BattleTestListRow>())
            if (row.Id == bossIndex) bossRow = row;
        Assert.That(bossRow, Is.Not.Null);
        Assert.That(bossRow.Subtitle, Does.Contain("자동 스폰 꺼짐"));
        Assert.That(bossRow.Actions[1].GetComponent<BattleTestControlTooltip>().Explanation, Does.Contain("무작위 생성"));
        bossRow.Actions[1].onClick.Invoke();
        yield return null;
        yield return null;
        Assert.That(test.TestWaves.ActiveEnemies.Count, Is.EqualTo(7));
        foreach (EnemyController enemy in test.TestWaves.ActiveEnemies)
            Assert.That(enemy.Data, Is.SameAs(test.Enemies[bossIndex]));
        Run(test, "checkpoint");
        Run(test, $"spawnpool {bossIndex} off");
        Run(test, $"spawnpool {normalIndex} on");
        Assert.That(test.TestWaves.ActiveEnemies.Count, Is.EqualTo(7), "대상 설정 변경은 기존 적을 제거하지 않습니다.");
        Run(test, "clear");
        yield return null;
        yield return null;
        foreach (EnemyController enemy in test.TestWaves.ActiveEnemies)
            Assert.That(enemy.Data, Is.SameAs(test.Enemies[normalIndex]));
        Run(test, "reset");
        Assert.That(test.IsEnemySpawnAllowed(test.Enemies[bossIndex]), Is.True);
        Assert.That(test.IsEnemySpawnAllowed(test.Enemies[normalIndex]), Is.False);
        Run(test, "refill off");
        Run(test, "clear");
        Run(test, "random 3");
        Assert.That(test.TestWaves.ActiveEnemies.Count, Is.EqualTo(3));
        foreach (EnemyController enemy in test.TestWaves.ActiveEnemies)
            Assert.That(enemy.Data, Is.SameAs(test.Enemies[bossIndex]));
        gui.SelectPage(0);
        gui.SelectPage(2);
        yield return null;
        foreach (BattleTestListRow row in Field<RectTransform>(gui, "catalogContent").GetComponentsInChildren<BattleTestListRow>())
            Assert.That(row.Subtitle, Does.Contain(row.Id == bossIndex ? "자동 스폰 켜짐" : "자동 스폰 꺼짐"));
        Assert.That(test.ExecuteCommand("spawnpool"), Does.Contain(test.Enemies[bossIndex].DisplayName + " · 자동 스폰: 켜짐"));
        LogAssert.NoUnexpectedReceived();
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RefillGuiRespectsExtinctionCapacityCheckpointAndPlayerRecovery()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;
        BattleTestController test = Object.FindFirstObjectByType<BattleTestController>();
        BattleTestGui gui = Object.FindFirstObjectByType<BattleTestGui>();
        WaveManager waves = test.TestWaves;
        Assert.That(test.EnemyAutoRefill, Is.False);
        Assert.That(waves.ActiveEnemies.Count, Is.Zero);
        Assert.That(test.ExecuteCommand("refill on 101"), Does.StartWith(BattleTestCommandRouter.RejectedPrefix));
        Assert.That(test.EnemyAutoRefill, Is.False);
        Assert.That(test.EnemyRefillPercent, Is.EqualTo(50));
        gui.SelectPage(2);
        yield return null;
        var toggle = GameObject.Find("전멸 시 자동 보충").GetComponent<UnityEngine.UI.Button>();
        Assert.That(toggle.GetComponent<BattleTestControlTooltip>(), Is.Not.Null);
        toggle.onClick.Invoke();
        Assert.That(waves.ActiveEnemies.Count, Is.Zero, "A UI callback must not spawn inside a combat callback.");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(7));
        AssertDistinctFreeCells(test);
        Assert.That(Field<RectTransform>(gui, "ownedContent").GetComponentsInChildren<BattleTestListRow>().Length, Is.EqualTo(7));
        Assert.That(Field<TMP_Text>(gui, "enemyRefillLabel").text, Does.Contain("켜짐"));
        Run(test, "checkpoint");
        Run(test, "refill off 10");
        Run(test, "clear");
        Run(test, "reset");
        Assert.That(test.EnemyAutoRefill, Is.True);
        Assert.That(test.EnemyRefillPercent, Is.EqualTo(50));
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(7));
        RunEnemy(test, waves.ActiveEnemies[0], "remove");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(6), "적이 남아 있으면 계속 보충하지 않아야 합니다.");
        TMP_InputField percent = Field<TMP_InputField>(gui, "enemyRefillPercent");
        percent.text = "100";
        GameObject.Find("점유율 적용").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Assert.That(test.EnemyRefillPercent, Is.EqualTo(100));
        Run(test, "clear");
        Assert.That(waves.ActiveEnemies.Count, Is.Zero);
        // A surviving bomb also occupies a tile when refilling the otherwise empty board.
        EnemyData bomber = null;
        foreach (EnemyData enemy in test.Enemies) if (enemy.BigBarrel.BossBombPrefab != null) { bomber = enemy; break; }
        Assert.That(bomber, Is.Not.Null);
        waves.BombManager.ResumeForBattle();
        Assert.That(waves.BombManager.TrySpawnBomb(bomber, 0, 0, 10, out _), Is.True);
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(12));
        AssertDistinctFreeCells(test);
        Run(test, "refill percent 50");
        Run(test, "board 5 3");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(8));
        Run(test, "hp 0");
        Run(test, "clear");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.Zero, "사망한 플레이어를 기다려야 합니다.");
        Run(test, "hp 100");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(8));
        Run(test, "refill percent 0");
        Run(test, "clear");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.Zero);
        Run(test, "refill off 100");
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.Zero);
        LogAssert.NoUnexpectedReceived();
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator LastEnemyShotFinishesBeforeReplacementEnemiesSpawn()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;
        BattleTestController test = Object.FindFirstObjectByType<BattleTestController>();
        var console = Object.FindFirstObjectByType<BattleTestConsole>();
        var shoot = Object.FindFirstObjectByType<PlayerShoot>();
        Run(test, "ai off"); Run(test, "god on");
        Run(test, "deck set Normal 0"); Run(test, "fill");
        Run(test, "player 2 0"); Run(test, "spawn 0 3 0"); Run(test, "enemy hp 3 0 1");
        Run(test, "refill on 50");
        console.SetOpen(false);
        shoot.Shoot();
        float deadline = Time.realtimeSinceStartup + 10;
        while (!test.IsSettled && Time.realtimeSinceStartup < deadline)
        {
            Assert.That(test.TestWaves.ActiveEnemies.Count, Is.LessThanOrEqualTo(1), "사격 중에는 새 적을 넣으면 안 됩니다.");
            yield return null;
        }
        Assert.That(test.IsSettled, Is.True);
        yield return null;
        yield return null;
        Assert.That(test.TestWaves.ActiveEnemies.Count, Is.EqualTo(7));
        AssertDistinctFreeCells(test);
        Run(test, "refill off"); Run(test, "clear");
        yield return null;
        Assert.That(test.TestWaves.ActiveEnemies.Count, Is.Zero);
        yield return new ExitPlayMode();
    }

    private static void AssertDistinctFreeCells(BattleTestController test)
    {
        var occupied = new HashSet<Vector2Int>();
        test.TestBoard.TryGetTileIndex(test.TestPlayer.transform.position, test.TestPlayer.CurrentLaneIndex, out int playerTile);
        foreach (EnemyController enemy in test.TestWaves.ActiveEnemies)
        {
            RunEnemySaveData saved = enemy.CaptureRunState(test.TestWaves.ActiveEnemies);
            var cell = new Vector2Int(saved.tileIndex, saved.laneIndex);
            Assert.That(occupied.Add(cell), Is.True);
            Assert.That(cell, Is.Not.EqualTo(new Vector2Int(playerTile, test.TestPlayer.CurrentLaneIndex)));
            Assert.That(test.TestWaves.BombManager.HasBombAtTile(cell.x, cell.y), Is.False);
        }
    }
    private static void RunEnemy(BattleTestController test, EnemyController enemy, string operation)
    {
        RunEnemySaveData saved = enemy.CaptureRunState(test.TestWaves.ActiveEnemies);
        Run(test, $"enemy {operation} {saved.tileIndex} {saved.laneIndex}");
    }
    private static void Run(BattleTestController test, string command) =>
        Assert.That(test.ExecuteCommand(command), Does.Not.StartWith(BattleTestCommandRouter.RejectedPrefix), command);
    private static T Field<T>(Object target, string name) where T : Object =>
        (T)new SerializedObject(target).FindProperty(name).objectReferenceValue;
    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}
