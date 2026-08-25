using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ExposedStatusEffectTests
{
    private GameObject enemyObject;
    private StatusEffectController statusEffects;
    private EnemyController enemy;

    [SetUp]
    public void SetUp()
    {
        enemyObject = new GameObject("Exposed Test Enemy");
        statusEffects = enemyObject.AddComponent<StatusEffectController>();
        enemy = enemyObject.AddComponent<EnemyController>();
        SerializedObject serializedEnemy = new SerializedObject(enemy);
        serializedEnemy.FindProperty("currentHealth").intValue = 10;
        serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(enemyObject);
    }

    [Test]
    public void ExposedCanOnlyBeAppliedThroughDedicatedDodgePath()
    {
        Assert.That(
            statusEffects.Add(StatusEffectType.Exposed, 1, true),
            Is.False);
        Assert.That(enemy.ApplyExposedFromDodge(), Is.True);
        Assert.That(enemy.ApplyExposedFromDodge(), Is.False);
        Assert.That(enemy.IsExposed, Is.True);
        Assert.That(
            enemy.GetStatusStacks(StatusEffectType.Exposed),
            Is.EqualTo(1));
        Assert.That(enemy.TotalStatusStackCount, Is.EqualTo(1));
    }

    [Test]
    public void ExposedDoesNotStackOrMultiply()
    {
        Assert.That(enemy.ApplyExposedFromDodge(), Is.True);
        Assert.That(statusEffects.MultiplyActiveStacks(3), Is.False);
        Assert.That(
            statusEffects.GetStacks(StatusEffectType.Exposed),
            Is.EqualTo(1));
    }

    [Test]
    public void DestroyedDodgeSourceDoesNotExposeAnotherEnemy()
    {
        GameObject otherObject = new GameObject("Other Thrower");
        otherObject.AddComponent<StatusEffectController>();
        EnemyController otherEnemy =
            otherObject.AddComponent<EnemyController>();
        SerializedObject serializedOther = new SerializedObject(otherEnemy);
        serializedOther.FindProperty("currentHealth").intValue = 10;
        serializedOther.ApplyModifiedPropertiesWithoutUndo();
        EnemyController destroyedSource = enemy;

        Object.DestroyImmediate(enemyObject);
        enemyObject = null;

        try
        {
            Assert.That(
                EnemyController.TryApplyDodgeExposedToSource(
                    destroyedSource),
                Is.False);
            Assert.That(otherEnemy.IsExposed, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(otherObject);
        }
    }

    [Test]
    public void ExposedPersistsInRunStateAndOlderStateDefaultsInactive()
    {
        enemy.ApplyExposedFromDodge();
        RunStatusEffectSaveData captured = statusEffects.CaptureRunState();

        statusEffects.Clear();
        Assert.That(statusEffects.IsExposed, Is.False);
        statusEffects.RestoreRunState(captured);
        Assert.That(statusEffects.IsExposed, Is.True);

        statusEffects.RestoreRunState(new RunStatusEffectSaveData());
        Assert.That(statusEffects.IsExposed, Is.False);
    }

    [Test]
    public void NonShootActionClearsExposedImmediately()
    {
        enemy.ApplyExposedFromDodge();

        enemy.HandlePlayerActionStarted(PlayerBehaviourAction.Wait);

        Assert.That(enemy.IsExposed, Is.False);
    }

    [Test]
    public void ShootKeepsExposedUntilHitOrActionCompletion()
    {
        enemy.ApplyExposedFromDodge();
        enemy.HandlePlayerActionStarted(PlayerBehaviourAction.Shoot);
        Assert.That(enemy.IsExposed, Is.True);

        Assert.That(enemy.TryConsumeExposedForAttack(), Is.True);
        enemy.HandlePlayerTurnCompleted();
        Assert.That(enemy.IsExposed, Is.False);

        enemy.ApplyExposedFromDodge();
        enemy.HandlePlayerActionStarted(PlayerBehaviourAction.Shoot);
        enemy.HandlePlayerTurnCompleted();
        Assert.That(enemy.IsExposed, Is.False);
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    public void ExposedGuaranteesTargetCriticalWithoutChangingOtherTargets(
        bool rolledCritical,
        bool targetIsExposed,
        bool expected)
    {
        Assert.That(
            PlayerAttackDamageCalculator.ResolveCriticalForTarget(
                rolledCritical,
                targetIsExposed),
            Is.EqualTo(expected));
    }

    [Test]
    public void EnemyPrefabReferencesExposedDebuffSprite()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemy/Enemy.prefab");
        StatusEffectController prefabStatus =
            prefab.GetComponent<StatusEffectController>();
        SerializedObject serializedStatus =
            new SerializedObject(prefabStatus);

        Assert.That(prefabStatus, Is.Not.Null);
        Assert.That(
            serializedStatus.FindProperty("exposedSprite")
                .objectReferenceValue,
            Is.Not.Null);
    }
}
