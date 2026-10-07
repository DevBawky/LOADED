using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyTurnSchedulingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> created = new List<Object>();
    private Runtime runtime;
    private readonly List<string> started = new List<string>();

    [SetUp]
    public void SetUp()
    {
        runtime = new Runtime();
        started.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    [Test]
    public void InterleavedQueuesStartTogetherAndAttacksKeepOrder()
    {
        var firstAttack = Enemy("attack A", true);
        var firstQueue = Enemy("queue A", false);
        var secondAttack = Enemy("attack B", true);
        var secondQueue = Enemy("queue B", false);
        var stepper = new Stepper(new EnemyTurnCycleRunner().Resolve(runtime, 1, 0f, 0f));

        Assert.That(stepper.NextFrame(), Is.True);
        Assert.That(started, Is.EqualTo(new[] { "queue A", "queue B" }));
        // A newly prepared action belongs to the next cycle, not this attack pass.
        Set(secondQueue, "isAttackPrepared", true);
        Set(firstQueue, "isActing", false);
        Assert.That(stepper.NextFrame(), Is.True);
        Assert.That(started.Count, Is.EqualTo(2));
        Set(secondQueue, "isActing", false);
        Assert.That(stepper.NextFrame(), Is.True);
        Assert.That(started, Is.EqualTo(new[] { "queue A", "queue B", "attack A" }));
        Set(firstAttack, "isActing", false);
        Assert.That(stepper.NextFrame(), Is.True);
        Assert.That(started, Is.EqualTo(new[] { "queue A", "queue B", "attack A", "attack B" }));
        Set(secondAttack, "isActing", false);
        Assert.That(stepper.NextFrame(), Is.False);
        Assert.That(runtime.BombPasses, Is.EqualTo(1));
    }

    [Test]
    public void PreparedPorterSharesTheConcurrentPass()
    {
        Enemy("attack", true);
        var porter = Enemy("support", true);
        var data = ScriptableObject.CreateInstance<EnemyData>();
        created.Add(data);
        Set(data, "behaviorType", EnemyBehaviorType.Porter);
        Set(porter, "enemyData", data);
        Enemy("queue", false);
        var stepper = new Stepper(new EnemyTurnCycleRunner().Resolve(runtime, 1, 0f, 0f));
        Assert.That(stepper.NextFrame(), Is.True);
        Assert.That(started, Is.EqualTo(new[] { "support", "queue" }));
    }

    [Test]
    public void RemovedEnemyCannotHoldTheCycleOpen()
    {
        var queue = Enemy("removed", false);
        var stepper = new Stepper(new EnemyTurnCycleRunner().Resolve(runtime, 1, 0f, 0f));
        Assert.That(stepper.NextFrame(), Is.True);
        runtime.Enemies.Remove(queue);
        Assert.That(stepper.NextFrame(), Is.False);
        Assert.That(runtime.BombPasses, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BattleEndStopsPendingActions(bool defeated)
    {
        Enemy("queue", false);
        Enemy("attack", true);
        var stepper = new Stepper(new EnemyTurnCycleRunner().Resolve(runtime, 1, 0f, 0f));
        Assert.That(stepper.NextFrame(), Is.True);
        runtime.IsPlayerDefeated = defeated;
        runtime.IsBattleCompleted = !defeated;
        Assert.That(stepper.NextFrame(), Is.False);
        Assert.That(started, Is.EqualTo(new[] { "queue" }));
        Assert.That(runtime.BombPasses, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BattleEndCancelsMinimumDelayWithoutProcessingBombs(bool defeated)
    {
        var stepper = new Stepper(new EnemyTurnCycleRunner().Resolve(runtime, 1, 100f, 0f));
        Assert.That(stepper.NextFrame(), Is.True);
        runtime.IsPlayerDefeated = defeated;
        runtime.IsBattleCompleted = !defeated;
        Assert.That(stepper.NextFrame(), Is.False);
        Assert.That(runtime.BombPasses, Is.Zero);
    }

    [Test]
    public void RemovedFollowingAttackDoesNotAddAnInterval()
    {
        var first = Enemy("first", true);
        var removed = Enemy("removed", true);
        var stepper = new Stepper(new EnemyTurnCycleRunner().Resolve(runtime, 1, 0f, 100f));
        Assert.That(stepper.NextFrame(), Is.True);
        Set(first, "lastTurnAction", EnemyTurnActionType.Fire);
        Set(first, "isActing", false);
        runtime.Enemies.Remove(removed);
        Assert.That(stepper.NextFrame(), Is.False);
        Assert.That(started, Is.EqualTo(new[] { "first" }));
        Assert.That(runtime.BombPasses, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BattleEndCancelsAttackInterval(bool defeated)
    {
        var first = Enemy("first", true);
        Enemy("next", true);
        var stepper = new Stepper(new EnemyTurnCycleRunner().Resolve(runtime, 1, 0f, 100f));
        Assert.That(stepper.NextFrame(), Is.True);
        Set(first, "lastTurnAction", EnemyTurnActionType.Fire);
        Set(first, "isActing", false);
        Assert.That(stepper.NextFrame(), Is.True);
        runtime.IsPlayerDefeated = defeated;
        runtime.IsBattleCompleted = !defeated;
        Assert.That(stepper.NextFrame(), Is.False);
        Assert.That(started, Is.EqualTo(new[] { "first" }));
        Assert.That(runtime.BombPasses, Is.Zero);
    }

    [Test]
    public void DeadThrowersProjectileSettlesBeforeNextAttack()
    {
        var thrower = Enemy("thrower", true);
        var next = Enemy("next", true);
        var stepper = new Stepper(new EnemyTurnCycleRunner().Resolve(runtime, 1, 0f, 0f));
        Assert.That(stepper.NextFrame(), Is.True);
        runtime.HasPendingDetachedEnemyAttacks = true;
        runtime.Enemies.Remove(thrower);
        Assert.That(stepper.NextFrame(), Is.True);
        Assert.That(started, Is.EqualTo(new[] { "thrower" }));
        runtime.HasPendingDetachedEnemyAttacks = false;
        Assert.That(stepper.NextFrame(), Is.True);
        Assert.That(started, Is.EqualTo(new[] { "thrower", "next" }));
        Set(next, "isActing", false);
        Assert.That(stepper.NextFrame(), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BattleEndDuringDetachedAttackDoesNotProcessBombs(bool defeated)
    {
        runtime.HasPendingDetachedEnemyAttacks = true;
        var stepper = new Stepper(new EnemyTurnCycleRunner().Resolve(runtime, 1, 0f, 0f));
        Assert.That(stepper.NextFrame(), Is.True);
        runtime.IsPlayerDefeated = defeated;
        runtime.IsBattleCompleted = !defeated;
        Assert.That(stepper.NextFrame(), Is.False);
        Assert.That(runtime.BombPasses, Is.Zero);
    }

    [Test]
    public void ActionsAreClassifiedBeforeConcurrentActionsChangeTheBoard()
    {
        var mover = Enemy("moving", false);
        var newlyAvailable = Enemy("new attack", false);
        var existing = Enemy("existing attack", true);
        // A synchronous status death or reservation change can open a firing
        // lane while earlier enemies are starting their concurrent actions.
        mover.TurnActionCompleted += (_, _) => Set(newlyAvailable, "isAttackPrepared", true);
        var stepper = new Stepper(new EnemyTurnCycleRunner().Resolve(runtime, 1, 0f, 0f));

        Assert.That(stepper.NextFrame(), Is.True);
        Assert.That(started, Is.EqualTo(new[] { "moving", "new attack" }));
        Set(mover, "isActing", false);
        Assert.That(stepper.NextFrame(), Is.True);
        Assert.That(started, Is.EqualTo(new[] { "moving", "new attack" }));
        Set(newlyAvailable, "isActing", false);
        Assert.That(stepper.NextFrame(), Is.True);
        Assert.That(started, Is.EqualTo(new[] { "moving", "new attack", "existing attack" }));
        Set(existing, "isActing", false);
        Assert.That(stepper.NextFrame(), Is.False);
        Assert.That(runtime.BombPasses, Is.EqualTo(1));
    }

    private EnemyController Enemy(string name, bool prepared)
    {
        var go = new GameObject(name);
        created.Add(go);
        var enemy = go.AddComponent<EnemyController>();
        var presenterType = typeof(EnemyController).GetNestedType("EnemyTelegraphPresenter", BindingFlags.NonPublic);
        Set(enemy, "telegraphPresenter", System.Activator.CreateInstance(presenterType, new object[] { enemy }));
        Set(enemy, "isAttackPrepared", prepared);
        // An unconfigured controller completes a Wait synchronously. Hold its
        // presentation flag to model a long action without relying on frame time.
        enemy.TurnActionCompleted += (actor, action) =>
        {
            started.Add(name);
            Set(actor, "isActing", true);
        };
        runtime.Enemies.Add(enemy);
        return enemy;
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    }

    private sealed class Stepper
    {
        private readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        public Stepper(IEnumerator root) { routines.Push(root); }
        public bool NextFrame()
        {
            for (int steps = 0; routines.Count > 0; steps++)
            {
                Assert.That(steps, Is.LessThan(100), "Coroutine failed to yield or settle");
                var routine = routines.Peek();
                if (!routine.MoveNext()) { routines.Pop(); continue; }
                if (routine.Current is IEnumerator nested) routines.Push(nested);
                else return true;
            }
            return false;
        }
    }

    private sealed class Runtime : IEnemyTurnCycleRuntime
    {
        public readonly List<EnemyController> Enemies = new List<EnemyController>();
        public IReadOnlyList<EnemyController> ActiveEnemies => Enemies;
        public bool IsBattleCompleted { get; set; }
        public bool IsPlayerDefeated { get; set; }
        public bool HasPendingDetachedEnemyAttacks { get; set; }
        public bool IsResolvingBossBombExplosions => false;
        public int BombPasses { get; private set; }
        public void RemoveMissingEnemies() { }
        public void ProcessBossBombs(int cycle) { BombPasses++; }
    }
}
