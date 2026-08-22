using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class VeteranEnemyAssetTests
{
    private const string EnemyFolder = "Assets/Scripts/Enemy/Enemy SO";
    private const string BattleFolder =
        "Assets/Scripts/Manager/Battle SO/Stage 1";

    [TestCase("Veteran Melee", "Test Enemy", "stage1_veteran_melee", 100, 15)]
    [TestCase("Veteran Gunner", "Test Gunner", "stage1_veteran_gunner", 40, 22)]
    [TestCase("Veteran Thrower", "Test Thrower", "stage1_veteran_thrower", 75, 22)]
    public void VeteranEnemyUsesSharedAvatarAndStrongerAuthoredAttack(
        string veteranName,
        string baseName,
        string expectedId,
        int expectedHealth,
        int expectedDamage)
    {
        EnemyData veteran = LoadEnemy(veteranName);
        EnemyData baseEnemy = LoadEnemy(baseName);

        Assert.That(veteran, Is.Not.Null);
        Assert.That(baseEnemy, Is.Not.Null);
        Assert.That(veteran.EnemyId, Is.EqualTo(expectedId));
        Assert.That(veteran.MaxHealth, Is.EqualTo(expectedHealth));
        Assert.That(veteran.Avatar, Is.SameAs(baseEnemy.Avatar));
        Assert.That(veteran.AvatarTint, Is.Not.EqualTo(Color.white));

        EnemyActionData attack = veteran.Actions.FirstOrDefault(action =>
            action != null && action.AttackData != null);
        Assert.That(attack, Is.Not.Null);
        Assert.That(attack.AttackData.Damage, Is.EqualTo(expectedDamage));
    }

    [Test]
    public void StageOneUsesVeteransButNeverUsesPorter()
    {
        string[] battleGuids = AssetDatabase.FindAssets(
            "t:BattleData",
            new[] { BattleFolder });
        HashSet<string> veteranIds = new HashSet<string>();

        foreach (string guid in battleGuids)
        {
            BattleData battle = AssetDatabase.LoadAssetAtPath<BattleData>(
                AssetDatabase.GUIDToAssetPath(guid));

            foreach (EnemyWave wave in battle.Waves)
            {
                foreach (EnemyWaveEntry entry in wave.Enemies)
                {
                    EnemyData enemy = entry.EnemyData;
                    Assert.That(enemy, Is.Not.Null);
                    Assert.That(
                        enemy.BehaviorType,
                        Is.Not.EqualTo(EnemyBehaviorType.Porter),
                        $"Porter is assigned to Stage 1 battle '{battle.name}'.");

                    if (enemy.EnemyId.StartsWith("stage1_veteran_"))
                    {
                        veteranIds.Add(enemy.EnemyId);
                    }
                }
            }
        }

        Assert.That(
            veteranIds,
            Is.EquivalentTo(new[]
            {
                "stage1_veteran_melee",
                "stage1_veteran_gunner",
                "stage1_veteran_thrower"
            }));
    }

    [Test]
    public void FinaleLastWavesContainEveryVeteranType()
    {
        string[] finaleNames =
        {
            "Stage 1 Finale 1",
            "Stage 1 Finale 2",
            "Stage 1 Finale 3"
        };

        foreach (string finaleName in finaleNames)
        {
            BattleData battle = AssetDatabase.LoadAssetAtPath<BattleData>(
                $"{BattleFolder}/3 Finale/{finaleName}.asset");
            Assert.That(battle, Is.Not.Null);
            EnemyWave finalWave = battle.Waves[battle.Waves.Count - 1];
            string[] ids = finalWave.Enemies
                .Select(entry => entry.EnemyData.EnemyId)
                .ToArray();

            Assert.That(ids, Does.Contain("stage1_veteran_melee"));
            Assert.That(ids, Does.Contain("stage1_veteran_gunner"));
            Assert.That(ids, Does.Contain("stage1_veteran_thrower"));
        }
    }

    private static EnemyData LoadEnemy(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<EnemyData>(
            $"{EnemyFolder}/{assetName}.asset");
    }
}
