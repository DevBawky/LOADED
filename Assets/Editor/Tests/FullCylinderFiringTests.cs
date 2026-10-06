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

    [UnityTest]
    public IEnumerator MissingTargetsPreserveSequenceEffectsAndPreviewButVictoryStops()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseScenarios();
        yield return new ExitPlayMode();
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
                Action<BulletInstance> fired = bullet => { shots++; firedNames.Add(bullet.Data.name); };
                shoot.BulletFired += fired;
                try
                {
                    shoot.Shoot();
                    float deadline = Time.realtimeSinceStartup + 15;
                    while (!test.IsSettled && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(test.IsSettled, Is.True, "Scenario " + scenario);
                }
                finally { shoot.BulletFired -= fired; }

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
