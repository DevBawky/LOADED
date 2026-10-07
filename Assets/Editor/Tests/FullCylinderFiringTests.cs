using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class FullCylinderFiringTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(0, 1, true)]
    [TestCase(0, 3, false)]
    [TestCase(1, 3, false)]
    [TestCase(2, 3, true)]
    [TestCase(3, 3, true)]
    public void ShotgunVolleyWaitsOnlyAfterItsFinalPellet(
        int shotIndex,
        int volleyShotCount,
        bool expected)
    {
        Assert.That(
            PlayerShoot.ShouldWaitForCadenceAfterVolleyShot(
                shotIndex,
                volleyShotCount),
            Is.EqualTo(expected));
    }

    [TestCase(CombatPacingMode.DuelClock, false, false, 0, true, true)]
    [TestCase(CombatPacingMode.DuelClock, false, false, 1, true, false)]
    [TestCase(CombatPacingMode.DuelClock, false, false, 0, false, false)]
    [TestCase(CombatPacingMode.DuelClock, true, false, 0, true, false)]
    [TestCase(CombatPacingMode.DuelClock, false, true, 0, true, false)]
    [TestCase(CombatPacingMode.Legacy, false, false, 0, true, false)]
    public void EnemyReplacementWaitOnlyBlocksAnEmptyActiveDuelClockBattle(
        CombatPacingMode pacingMode,
        bool isBattleCompleted,
        bool isPlayerDefeated,
        int livingEnemyCount,
        bool hasRemainingEnemiesToSpawn,
        bool expected)
    {
        Assert.That(
            PlayerShoot.ShouldWaitForEnemyReplacement(
                pacingMode,
                isBattleCompleted,
                isPlayerDefeated,
                livingEnemyCount,
                hasRemainingEnemiesToSpawn),
            Is.EqualTo(expected));
    }

    [UnityTest]
    public IEnumerator MissingTargetsPreserveSequenceEffectsAndPreviewButVictoryStops()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseScenarios();
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ShotgunPelletsShareSingleBurstBeforeNextPhysicalBullet()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseShotgunVolleyScenario();
        yield return new ExitPlayMode();
    }

    private static IEnumerator ExerciseShotgunVolleyScenario()
    {
        EditorSceneManager.LoadSceneInPlayMode(
            BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(
                UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;

        BattleTestController test =
            Object.FindFirstObjectByType<BattleTestController>();
        PlayerShoot shoot = Object.FindFirstObjectByType<PlayerShoot>();
        WaveManager wave = Object.FindFirstObjectByType<WaveManager>();
        DeckManager deck = Object.FindFirstObjectByType<DeckManager>();
        Object.FindObjectsByType<UnityEngine.UI.Button>(
                FindObjectsSortMode.None)
            .First(button => button.GetComponentInChildren<TMPro.TMP_Text>()
                ?.text == "전투로 돌아가기")
            .onClick.Invoke();

        Run(test, "clear");
        Run(test, "board 11 2");
        Run(test, "player 5 0");
        Run(test, "god on");
        Run(test, "ai off");
        Run(test, "refill off");
        Run(test, "spawn 2 6 0");
        Run(test, "enemy shield 6 0 1000");

        BulletData shotgun = Object.Instantiate(
            test.Bullets.Single(bullet => bullet.name == "Shot"));
        BulletData normal = Object.Instantiate(
            test.Bullets.Single(bullet => bullet.name == "Normal"));
        shotgun.name = "Shot Volley Test";
        normal.name = "Normal After Volley Test";
        float originalShotInterval = (float)typeof(PlayerShoot)
            .GetField("shotInterval", Private)
            .GetValue(shoot);

        try
        {
            foreach (BulletData bullet in new[] { shotgun, normal })
            {
                SerializedObject serialized = new SerializedObject(bullet);
                serialized.FindProperty("criticalChance").floatValue = 0f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            BulletData[] sequence = { shotgun, normal };
            deck.RestoreRunState(
                sequence.Select((bullet, index) => new RunBulletSaveData
                {
                    assetName = bullet.name,
                    acquisitionOrder = index,
                    location = 1,
                    locationIndex = sequence.Length - 1 - index
                }).ToArray(),
                saved => sequence.Single(bullet =>
                    bullet.name == saved.assetName),
                0,
                null);
            Set(shoot, "shotInterval", 0.5f);

            List<string> firedNames = new List<string>();
            List<float> shotTimes = new List<float>();
            List<int> shotFrames = new List<int>();
            int volleyPresentationCount = 0;
            int presentedPelletCount = 0;
            Action<BulletInstance> fired = bullet =>
            {
                firedNames.Add(bullet.Data.name);
                shotTimes.Add(Time.unscaledTime);
                shotFrames.Add(Time.frameCount);
            };
            Action<int> volleyPresented = pelletCount =>
            {
                volleyPresentationCount++;
                presentedPelletCount = pelletCount;
            };
            shoot.BulletFired += fired;
            shoot.ShotgunVolleyPresented += volleyPresented;
            try
            {
                shoot.Shoot();
                float deadline = Time.realtimeSinceStartup + 15f;
                while (!test.IsSettled
                    && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }
            finally
            {
                shoot.BulletFired -= fired;
                shoot.ShotgunVolleyPresented -= volleyPresented;
            }

            Assert.That(test.IsSettled, Is.True);
            Assert.That(
                volleyPresentationCount,
                Is.EqualTo(1),
                "A shotgun must present exactly one firing burst.");
            Assert.That(
                presentedPelletCount,
                Is.EqualTo(shotgun.GetShotCount(0)),
                "The single burst must create every pellet together.");
            Assert.That(firedNames, Is.EqualTo(new[]
            {
                shotgun.name,
                shotgun.name,
                shotgun.name,
                normal.name
            }));
            Assert.That(
                shotTimes[1] - shotTimes[0],
                Is.LessThan(0.48f),
                "Pellet resolution must not replay the firing cadence.");
            Assert.That(
                shotTimes[2] - shotTimes[1],
                Is.LessThan(0.48f),
                "Every pellet must remain inside the same firing burst.");
            Assert.That(
                shotFrames[3],
                Is.GreaterThan(shotFrames[2]),
                "The next physical bullet must not join the shotgun volley.");
            Assert.That(
                shotTimes[3] - shotTimes[2],
                Is.GreaterThanOrEqualTo(0.48f),
                "The next physical bullet must wait after the volley resolves.");
            Assert.That(wave.LivingEnemyCount, Is.EqualTo(1));
        }
        finally
        {
            Set(shoot, "shotInterval", originalShotInterval);
            Object.Destroy(shotgun);
            Object.Destroy(normal);
        }
    }

    private static IEnumerator ExerciseScenarios()
    {
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;
        var test = Object.FindFirstObjectByType<BattleTestController>();
        var shoot = Object.FindFirstObjectByType<PlayerShoot>();
        var player = Object.FindFirstObjectByType<PlayerMove>();
        var wave = Object.FindFirstObjectByType<WaveManager>();
        var deck = Object.FindFirstObjectByType<DeckManager>();
        Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
            .First(button => button.GetComponentInChildren<TMPro.TMP_Text>()?.text == "전투로 돌아가기")
            .onClick.Invoke();
        var clones = new List<BulletData>();
        try
        {
            for (int scenario = 0; scenario < 5; scenario++)
            {
                Run(test, "clear");
                Run(test, "board 11 2");
                Run(test, "player 5 0");
                Run(test, "god on");
                Run(test, "ai off");
                Run(test, "refill off");
                Run(test, "spawn 2 6 0");
                Run(test, "enemy hp 6 0 1");
                string[] names;
                if (scenario == 0)
                {
                    Run(test, "spawn 2 4 0");
                    names = new[] { "Normal", "Rotation Shot", "Charge" };
                }
                else if (scenario == 1)
                {
                    Run(test, "spawn 2 0 1");
                    names = new[] { "Normal", "Normal", "Sniping" };
                }
                else if (scenario == 2)
                {
                    Run(test, "clear");
                    Run(test, "spawn 2 7 0");
                    Run(test, "spawn 2 3 0");
                    names = new[] { "Evasion", "Normal", "Reverse Shot" };
                }
                else if (scenario == 3)
                {
                    Run(test, "spawn 2 0 1");
                    names = new[] { "Normal", "Powder Pouch", "Sniping" };
                }
                else
                {
                    names = new[] { "Normal", "Powder Pouch", "Normal" };
                    Set(wave, "isTestBattle", false);
                    Set(wave, "combatPacingMode", CombatPacingMode.Legacy);
                    Set(wave, "currentWaveIndex", 0);
                    Set(wave, "waves", new EnemyWave[1]);
                }

                var sequence = names.Select(name =>
                {
                    var clone = Object.Instantiate(test.Bullets.Single(b => b.name == name));
                    clone.name = name + " Test " + clones.Count;
                    var data = new SerializedObject(clone);
                    data.FindProperty("criticalChance").floatValue = 0;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    clones.Add(clone);
                    return clone;
                }).ToArray();
                deck.RestoreRunState(sequence.Select((bullet, index) => new RunBulletSaveData
                {
                    assetName = bullet.name, acquisitionOrder = index,
                    location = 1, locationIndex = sequence.Length - 1 - index
                }).ToArray(), saved => sequence.Single(b => b.name == saved.assetName), 0, null);
                Assert.That(deck.LoadedBullets.Reverse().Select(b => b.Data), Is.EqualTo(sequence));

                var targets = wave.ActiveEnemies.ToArray();
                var expectedHealth = new Dictionary<EnemyController, int>();
                var randomBefore = JsonUtility.ToJson(UnityEngine.Random.state);
                shoot.ShowLoadedBulletDamagePreview(0);
                var preview = typeof(PlayerShoot).GetField("damagePreview", Private).GetValue(shoot);
                var states = (IDictionary)preview.GetType().GetField("damagePreviewStates", Private).GetValue(preview);
                foreach (DictionaryEntry pair in states)
                {
                    expectedHealth[(EnemyController)pair.Key] = (int)pair.Value.GetType()
                        .GetProperty("RemainingHealth").GetValue(pair.Value);
                }
                Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(randomBefore), "Preview RNG");
                Assert.That(deck.LoadedBullets.Count, Is.EqualTo(3), "Preview must not consume bullets");

                int shots = 0;
                int turns = player.TurnCount;
                var firedNames = new List<string>();
                var shotTimes = new List<float>();
                var damageTimes = new List<float>();
                Action<BulletInstance> fired = bullet =>
                {
                    shots++;
                    firedNames.Add(bullet.Data.name);
                    shotTimes.Add(Time.unscaledTime);
                };
                Action<int> damaged = _ => damageTimes.Add(Time.unscaledTime);
                shoot.BulletFired += fired;
                shoot.DamageDealt += damaged;
                try
                {
                    shoot.Shoot();
                    float deadline = Time.realtimeSinceStartup + 15;
                    while (!test.IsSettled && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(test.IsSettled, Is.True, "Scenario " + scenario);
                }
                finally
                {
                    shoot.BulletFired -= fired;
                    shoot.DamageDealt -= damaged;
                }

                if (scenario == 0)
                {
                    float shotInterval = (float)typeof(PlayerShoot)
                        .GetField("shotInterval", Private)
                        .GetValue(shoot);
                    Assert.That(shotTimes, Has.Count.GreaterThanOrEqualTo(2));
                    Assert.That(damageTimes, Has.Count.GreaterThanOrEqualTo(1));
                    Assert.That(
                        shotTimes[1] - damageTimes[0],
                        Is.GreaterThanOrEqualTo(shotInterval - 0.03f),
                        "The next bullet must wait a full shot interval after the previous shot resolves.");
                }

                Assert.That(shots, Is.EqualTo(scenario == 4 ? 1 : scenario == 3 ? 2 : 3),
                    "Scenario " + scenario + ": " + string.Join(",", firedNames));
                Assert.That(deck.LoadedBullets.Count, Is.EqualTo(scenario == 4 ? 2 : 0));
                Assert.That(deck.TotalBulletCount, Is.EqualTo(scenario == 3 ? 2 : 3));
                if (scenario == 4)
                {
                    Assert.That(wave.IsBattleCompleted, Is.True);
                    Assert.That(player.TurnCount, Is.EqualTo(turns));
                    Assert.That(deck.LoadedBullets.Any(b => b.Data.name.StartsWith("Powder Pouch")), Is.True);
                }
                else
                {
                    Assert.That(player.TurnCount, Is.EqualTo(turns + 1));
                    // Powder grants probabilistic criticals; all other cases are deterministic.
                    if (scenario != 3)
                        foreach (var target in targets)
                            Assert.That(target == null ? 0 : target.CurrentHealth,
                                Is.EqualTo(expectedHealth[target]), "Preview parity scenario=" + scenario);
                }
            }
        }
        finally
        {
            foreach (var clone in clones) if (clone != null) Object.Destroy(clone);
        }
    }

    private static void Run(BattleTestController test, string command)
    {
        Assert.That(test.ExecuteCommand(command), Does.Not.StartWith(BattleTestCommandRouter.RejectedPrefix), command);
    }

    private static void Set(object owner, string field, object value)
    {
        owner.GetType().GetField(field, Private).SetValue(owner, value);
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
