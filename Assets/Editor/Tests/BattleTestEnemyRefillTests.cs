using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        Assert.That(test.EnemyRefillSummary, Does.Contain("적 체력 유지"));
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
    public IEnumerator RefillLimitStopsAtMaximumRestoresWithCheckpointAndZeroIsUnlimited()
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
        gui.SelectPage(2);
        yield return null;

        UnityEngine.UI.Slider limit = Field<UnityEngine.UI.Slider>(gui, "enemyRefillLimit");
        TMP_Text limitLabel = Field<TMP_Text>(gui, "enemyRefillLimitLabel");
        Assert.That(limit.minValue, Is.Zero);
        Assert.That(limit.maxValue, Is.EqualTo(BattleTestController.MaximumEnemyRefillLimit));
        Assert.That(limit.wholeNumbers, Is.True);
        Assert.That(limitLabel.text, Does.Contain("무한"));
        Assert.That(limit.GetComponent<BattleTestControlTooltip>().Explanation, Does.Contain("0은 무한"));
        Assert.That(test.ExecuteCommand("refill limit 11"), Does.StartWith(BattleTestCommandRouter.RejectedPrefix));

        limit.value = 1;
        Assert.That(test.EnemyRefillLimit, Is.EqualTo(1));
        Assert.That(test.EnemyRefillUses, Is.Zero);
        Run(test, "refill on 0");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.Zero);
        Assert.That(test.EnemyRefillUses, Is.Zero, "적을 생성하지 못하면 제한 횟수를 사용하지 않아야 합니다.");
        Run(test, "refill percent 50");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(7));
        Assert.That(test.EnemyRefillUses, Is.EqualTo(1));

        limit.value = 2;
        Assert.That(test.EnemyRefillUses, Is.Zero, "제한을 바꾸면 사용 횟수를 다시 시작해야 합니다.");
        Run(test, "clear");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(7));
        Assert.That(test.EnemyRefillUses, Is.EqualTo(1));
        Run(test, "checkpoint");
        Run(test, "clear");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(7));
        Assert.That(test.EnemyRefillUses, Is.EqualTo(2));
        Run(test, "clear");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.Zero);
        Assert.That(test.EnemyRefillsRemaining, Is.Zero);

        Run(test, "reset");
        Assert.That(test.EnemyRefillLimit, Is.EqualTo(2));
        Assert.That(test.EnemyRefillUses, Is.EqualTo(1));
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(7));
        Run(test, "clear");
        yield return null;
        yield return null;
        Assert.That(test.EnemyRefillUses, Is.EqualTo(2));
        Run(test, "clear");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.Zero);

        limit.value = 0;
        yield return null;
        yield return null;
        Assert.That(test.EnemyRefillLimit, Is.Zero);
        Assert.That(test.EnemyRefillUses, Is.Zero);
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(7));
        Run(test, "clear");
        yield return null;
        yield return null;
        Assert.That(waves.ActiveEnemies.Count, Is.EqualTo(7));
        Assert.That(test.EnemyRefillUses, Is.Zero);
        LogAssert.NoUnexpectedReceived();
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ExtinctionRefillsAfterPhysicalBulletBeforeRemainingCylinderShots()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseExtinctionRefillDuringCylinder();
        yield return new ExitPlayMode();
    }

    private static IEnumerator ExerciseExtinctionRefillDuringCylinder()
    {
        int previousSceneHandle = UnityEngine.SceneManagement.SceneManager
            .GetActiveScene().handle;
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        BattleTestController test = null;
        float sceneDeadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < sceneDeadline)
        {
            yield return null;
            UnityEngine.SceneManagement.Scene activeScene =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.handle == previousSceneHandle
                || activeScene.path != BattleTestSceneBuilder.ScenePath)
                continue;
            test = Object.FindFirstObjectByType<BattleTestController>();
            if (test != null && test.IsReady) break;
        }
        Assert.That(test != null && test.IsReady, Is.True,
            "BattleTest scene did not finish loading.");
        BattleTestConsole console =
            Object.FindFirstObjectByType<BattleTestConsole>();
        PlayerShoot shoot = Object.FindFirstObjectByType<PlayerShoot>();
        WaveManager waves = Object.FindFirstObjectByType<WaveManager>();
        DeckManager deck = Object.FindFirstObjectByType<DeckManager>();
        Assert.That(console, Is.Not.Null);
        Assert.That(shoot, Is.Not.Null);
        Assert.That(waves, Is.Not.Null);
        Assert.That(deck, Is.Not.Null);
        Run(test, "ai off"); Run(test, "god on");
        Run(test, "deck set Normal 0");
        for (int index = 1; index < 6; index++)
            Run(test, "bullet add Normal 0");
        Run(test, "fill");
        Run(test, "player 2 0");
        Run(test, "spawn 0 3 0"); Run(test, "enemy hp 3 0 1");
        Assert.That(deck.LoadedBullets.Count, Is.EqualTo(6));
        Run(test, "refill on 100");
        console.SetOpen(false);
        var enemiesAtShot = new List<int>();
        int damageEvents = 0;
        System.Action<BulletInstance> fired = _ =>
            enemiesAtShot.Add(waves.ActiveEnemies.Count);
        System.Action<int> damaged = _ => damageEvents++;
        shoot.BulletFired += fired;
        shoot.DamageDealt += damaged;
        try
        {
            shoot.Shoot();
            float deadline = Time.realtimeSinceStartup + 10;
            while (!test.IsSettled && Time.realtimeSinceStartup < deadline)
                yield return null;
        }
        finally
        {
            shoot.BulletFired -= fired;
            shoot.DamageDealt -= damaged;
        }
        Assert.That(test.IsSettled, Is.True);
        Assert.That(enemiesAtShot, Has.Count.EqualTo(6));
        Assert.That(enemiesAtShot[0], Is.EqualTo(1));
        Assert.That(enemiesAtShot.Skip(1), Is.All.GreaterThan(0),
            "전멸 직후 보충된 적은 다음 물리 탄환부터 표적이 되어야 합니다.");
        Assert.That(damageEvents, Is.GreaterThanOrEqualTo(6),
            "남은 실린더 탄환도 보충된 적에게 피해를 줘야 합니다.");
        Assert.That(waves.ActiveEnemies.Count, Is.GreaterThan(0));
        AssertDistinctFreeCells(test);
        Run(test, "refill off"); Run(test, "clear");
        yield return null;
        Assert.That(test.TestWaves.ActiveEnemies.Count, Is.Zero);
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
