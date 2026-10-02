using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EnemyControllerTurnDecisionPlayModeTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator MixedAttackAndQueueCycleStartsBothRevealsBeforeAttacks()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var created = new List<Object>();
        try
        {
            var board = CreateComponent<BoardManager>("Scheduling Board", created);
            var tiles = new GameObject("Scheduling Tiles"); created.Add(tiles);
            SetField(board, "tileParent", tiles.transform);
            var template = CreateComponent<BoardTile>("Scheduling Tile Template", created);
            Assert.That(board.ConfigureBoard(7, 1, template), Is.True);
            var player = CreateComponent<PlayerMove>("Scheduling Player", created);
            var health = player.gameObject.AddComponent<PlayerHealth>();
            Place(board, player.transform, 6);
            var waveObject = new GameObject("Scheduling Wave"); created.Add(waveObject);
            waveObject.SetActive(false);
            var wave = waveObject.AddComponent<WaveManager>();
            SetField(wave, "boardManager", board);
            var action = CreateAttackAction(EnemyActionType.RangedAttack, 1, created);
            var enemies = new List<EnemyController>();
            for (int i = 0; i < 4; i++)
            {
                var enemy = CreateEnemy(EnemyBehaviorType.Gunner, new[] { action },
                    board, player, health, wave, created);
                Place(board, enemy.transform, i);
                enemy.transform.localScale = Vector3.one;
                // Queue-only enemies ignore frontline gating in this fixture.
                if (i % 2 == 1) SetField(enemy.Data, "behaviorType", EnemyBehaviorType.Thrower);
                var queue = enemy.GetComponent<EnemyActionQueueUI>();
                var queueObject = new GameObject("Queue", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                queueObject.transform.SetParent(enemy.transform, false);
                var icon = new GameObject("Icon Template", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                created.Add(icon);
                SetField(queue, "queueImage", queueObject.GetComponent<UnityEngine.UI.Image>());
                SetField(queue, "iconParent", queueObject.GetComponent<RectTransform>());
                SetField(queue, "attackIconPrefab", icon.GetComponent<UnityEngine.UI.Image>());
                SetField(enemy.Data, "queueElementRevealDuration", 0.1f);
                if (i % 2 == 0)
                {
                    SetQueue(enemy, action);
                    SetField(enemy, "isAttackPrepared", true);
                }
                enemies.Add(enemy);
            }
            SetActiveEnemies(wave, enemies.ToArray());
            var runtime = new SchedulingRuntime(enemies);
            var routine = new EnemyTurnCycleRunner().Resolve(runtime, 1, 0f, 0f);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(enemies[1].GetComponent<EnemyActionQueueUI>().IconCount, Is.EqualTo(1));
            Assert.That(enemies[3].GetComponent<EnemyActionQueueUI>().IconCount, Is.EqualTo(1));
            Assert.That(enemies[1].IsActing && enemies[3].IsActing, Is.True);
            Assert.That(enemies[0].IsAttackPrepared && enemies[2].IsAttackPrepared, Is.True);

            // Drive nested waits frame by frame so a regression reports a failure
            // instead of hanging the test runner indefinitely.
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            stack.Push((IEnumerator)routine.Current);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (stack.Count > 0)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Enemy cycle did not settle");
                var current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                if (current.Current is IEnumerator nested) stack.Push(nested);
                else yield return current.Current;
            }
            Assert.That(runtime.BombPasses, Is.EqualTo(1));
            Assert.That(enemies.TrueForAll(e => !e.IsActing), Is.True);
            Assert.That(enemies[0].LastTurnAction, Is.EqualTo(EnemyTurnActionType.Fire));
            Assert.That(enemies[2].LastTurnAction, Is.EqualTo(EnemyTurnActionType.Fire));
            Assert.That(enemies[1].LastTurnAction, Is.EqualTo(EnemyTurnActionType.RegisterAttack));
            Assert.That(enemies[3].LastTurnAction, Is.EqualTo(EnemyTurnActionType.RegisterAttack));
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.Destroy(created[i]);
        }
        yield return null;
        yield return new ExitPlayMode();
    }

    private sealed class SchedulingRuntime : IEnemyTurnCycleRuntime
    {
        public SchedulingRuntime(IReadOnlyList<EnemyController> enemies) { ActiveEnemies = enemies; }
        public IReadOnlyList<EnemyController> ActiveEnemies { get; }
        public bool IsBattleCompleted => false;
        public bool IsPlayerDefeated => false;
        public bool HasPendingDetachedEnemyAttacks => false;
        public bool IsResolvingBossBombExplosions => false;
        public int BombPasses { get; private set; }
        public void RemoveMissingEnemies() { }
        public void ProcessBossBombs(int cycle) { BombPasses++; }
    }

    [UnityTest]
    public IEnumerator QueuedBehaviorsKeepTheirTurnOutcomes()
    {
        EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single);

        yield return new EnterPlayMode();

        List<Object> created = new List<Object>();

        try
        {
            BoardManager board = CreateComponent<BoardManager>(
                "Turn Decision Board",
                created);
            Transform tileParent = new GameObject("Turn Decision Tiles")
                .transform;
            created.Add(tileParent.gameObject);
            SetField(board, "tileParent", tileParent);
            BoardTile tileTemplate = CreateComponent<BoardTile>(
                "Turn Decision Tile Template",
                created);
            Assert.That(board.ConfigureBoard(7, 1, tileTemplate), Is.True);

            GameObject playerObject = new GameObject("Turn Decision Player");
            created.Add(playerObject);
            PlayerMove player = playerObject.AddComponent<PlayerMove>();
            PlayerHealth playerHealth =
                playerObject.AddComponent<PlayerHealth>();
            player.SetLaneIndex(0);
            Place(board, player.transform, 4);

            GameObject waveObject = new GameObject("Turn Decision Wave");
            created.Add(waveObject);
            waveObject.SetActive(false);
            WaveManager wave = waveObject.AddComponent<WaveManager>();
            SetField(wave, "boardManager", board);

            EnemyActionData meleeAction = CreateAttackAction(
                EnemyActionType.MeleeAttack,
                1,
                created);
            EnemyController melee = CreateEnemy(
                EnemyBehaviorType.Melee,
                new[] { meleeAction },
                board,
                player,
                playerHealth,
                wave,
                created);
            Place(board, melee.transform, 3);
            melee.transform.localScale = Vector3.one;
            SetQueue(melee, meleeAction);
            SetActiveEnemies(wave, melee);

            melee.TakeTurn();

            Assert.That(
                melee.LastTurnAction,
                Is.EqualTo(EnemyTurnActionType.PrepareAttack));
            Assert.That(melee.IsAttackPrepared, Is.True);
            Assert.That(melee.IsActing, Is.False);

            EnemyActionData gunnerAction = CreateAttackAction(
                EnemyActionType.RangedAttack,
                3,
                created);
            EnemyController gunner = CreateEnemy(
                EnemyBehaviorType.Gunner,
                new[] { gunnerAction },
                board,
                player,
                playerHealth,
                wave,
                created);
            Place(board, gunner.transform, 2);
            gunner.transform.localScale = Vector3.one;
            SetQueue(gunner, gunnerAction);
            SetActiveEnemies(wave, gunner);

            gunner.TakeTurn();

            Assert.That(
                gunner.LastTurnAction,
                Is.EqualTo(EnemyTurnActionType.PrepareAttack));
            Assert.That(gunner.IsAttackPrepared, Is.True);
            Assert.That(gunner.IsActing, Is.False);

            EnemyController thrower = CreateEnemy(
                EnemyBehaviorType.Thrower,
                null,
                board,
                player,
                playerHealth,
                wave,
                created);
            Place(board, thrower.transform, 1);
            thrower.transform.localScale = Vector3.one;
            SetActiveEnemies(wave, thrower);

            thrower.TakeTurn();

            Assert.That(
                thrower.LastTurnAction,
                Is.EqualTo(EnemyTurnActionType.Wait));
            Assert.That(thrower.IsActing, Is.False);

            EnemyController bigBarrel = CreateEnemy(
                EnemyBehaviorType.BigBarrel,
                null,
                board,
                player,
                playerHealth,
                wave,
                created);
            Place(board, bigBarrel.transform, 0);
            bigBarrel.transform.localScale = Vector3.one;
            SetActiveEnemies(wave, bigBarrel);

            bigBarrel.TakeTurn();

            Assert.That(
                bigBarrel.LastTurnAction,
                Is.EqualTo(EnemyTurnActionType.Wait));
            Assert.That(
                GetField<BigBarrelStep>(bigBarrel, "bigBarrelStep"),
                Is.EqualTo(BigBarrelStep.CreateBombQueue));
            Assert.That(bigBarrel.IsActing, Is.False);
        }
        finally
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    Object.Destroy(created[index]);
                }
            }
        }

        yield return null;
        yield return new ExitPlayMode();
    }

    private static EnemyController CreateEnemy(
        EnemyBehaviorType behavior,
        IReadOnlyList<EnemyActionData> actions,
        BoardManager board,
        PlayerMove player,
        PlayerHealth playerHealth,
        WaveManager wave,
        ICollection<Object> created)
    {
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        created.Add(data);
        SetField(data, "behaviorType", behavior);
        SetField(
            data,
            "actions",
            actions == null
                ? new List<EnemyActionData>()
                : new List<EnemyActionData>(actions));

        GameObject enemyObject = new GameObject($"{behavior} Decision Enemy");
        created.Add(enemyObject);
        enemyObject.SetActive(false);
        ActorMotion actorMotion = enemyObject.AddComponent<ActorMotion>();
        SetField(actorMotion, "actorTransform", enemyObject.transform);
        EnemyActionQueueUI queue =
            enemyObject.AddComponent<EnemyActionQueueUI>();
        EnemyController enemy = enemyObject.AddComponent<EnemyController>();
        SetField(enemy, "actorMotion", actorMotion);
        SetField(enemy, "actionQueueUI", queue);
        enemyObject.SetActive(true);

        SetField(enemy, "enemyData", data);
        SetField(enemy, "boardManager", board);
        SetField(enemy, "playerMove", player);
        SetField(enemy, "playerHealth", playerHealth);
        SetField(enemy, "waveManager", wave);
        SetField(enemy, "currentHealth", data.MaxHealth);
        SetField(enemy, "currentLaneIndex", 0);
        SetField(enemy, "isInitialized", true);
        return enemy;
    }

    private static EnemyActionData CreateAttackAction(
        EnemyActionType actionType,
        int range,
        ICollection<Object> created)
    {
        EnemyAttackData attack =
            ScriptableObject.CreateInstance<EnemyAttackData>();
        EnemyActionData action =
            ScriptableObject.CreateInstance<EnemyActionData>();
        created.Add(attack);
        created.Add(action);
        SetField(attack, "range", range);
        SetField(action, "actionType", actionType);
        SetField(action, "attackData", attack);
        return action;
    }

    private static T CreateComponent<T>(
        string name,
        ICollection<Object> created)
        where T : Component
    {
        GameObject gameObject = new GameObject(name);
        created.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    private static void Place(
        BoardManager board,
        Transform target,
        int tileIndex)
    {
        Assert.That(
            board.TryGetTilePosition(tileIndex, 0, out Vector3 position),
            Is.True);
        target.position = position;
    }

    private static void SetQueue(
        EnemyController enemy,
        EnemyActionData action)
    {
        SetField(enemy, "isQueueCreated", true);
        SetField(
            enemy,
            "queuedAttackActions",
            new List<EnemyActionData> { action });
    }

    private static void SetActiveEnemies(
        WaveManager wave,
        params EnemyController[] enemies)
    {
        SetField(wave, "activeEnemies", new List<EnemyController>(enemies));
    }

    private static T GetField<T>(object target, string fieldName)
    {
        return (T)target.GetType()
            .GetField(fieldName, PrivateInstance)
            .GetValue(target);
    }

    private static void SetField(
        object target,
        string fieldName,
        object value)
    {
        target.GetType()
            .GetField(fieldName, PrivateInstance)
            .SetValue(target, value);
    }
}
