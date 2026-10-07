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
    public IEnumerator FrontlinePreparationRespectsBothSidesAndLanesWithoutSkippingPreparation()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseFrontlinePreparation();
        yield return new ExitPlayMode();
    }

    private static IEnumerator ExerciseFrontlinePreparation()
    {
        var created = new List<Object>();
        try
        {
            var board = CreateComponent<BoardManager>("Frontline Board", created);
            var tiles = new GameObject("Frontline Tiles"); created.Add(tiles);
            SetField(board, "tileParent", tiles.transform);
            var template = CreateComponent<BoardTile>("Frontline Template", created);
            Assert.That(board.ConfigureBoard(9, 2, template), Is.True);
            var player = CreateComponent<PlayerMove>("Frontline Player", created);
            var health = player.gameObject.AddComponent<PlayerHealth>();
            var waveObject = new GameObject("Frontline Wave"); created.Add(waveObject);
            waveObject.SetActive(false);
            var wave = waveObject.AddComponent<WaveManager>();
            SetField(wave, "boardManager", board);
            SetField(wave, "isResolvingTurn", true);
            void At(Transform target, int tile, int lane)
            {
                Assert.That(board.TryGetTilePosition(tile, lane, out var position), Is.True);
                target.position = position;
            }
            foreach (var behavior in new[] { EnemyBehaviorType.Melee, EnemyBehaviorType.Gunner })
            foreach (int lane in new[] { 0, 1 })
            foreach (int side in new[] { -1, 1 })
            {
                player.SetLaneIndex(lane);
                At(player.transform, 4, lane);
                var action = CreateAttackAction(behavior == EnemyBehaviorType.Melee
                    ? EnemyActionType.MeleeAttack : EnemyActionType.RangedAttack, 8, created);
                var rear = CreateEnemy(behavior, new[] { action }, board, player, health, wave, created);
                var front = CreateEnemy(EnemyBehaviorType.Porter, null, board, player, health, wave, created);
                var throwAction = CreateAttackAction(EnemyActionType.RangedAttack, 8, created);
                var thrower = CreateEnemy(EnemyBehaviorType.Thrower, new[] { throwAction }, board, player, health, wave, created);
                SetField(rear.Data, "firingRange", 8);
                SetField(thrower.Data, "firingRange", 8);
                foreach (var enemy in new[] { rear, front, thrower }) SetField(enemy, "currentLaneIndex", lane);
                At(rear.transform, 4 + side * 3, lane);
                At(front.transform, 4 + side, lane);
                At(thrower.transform, 4 + side * 4, lane);
                rear.transform.localScale = thrower.transform.localScale = new Vector3(-side, 1, 1);
                SetActiveEnemies(wave, rear, front, thrower);
                // A different lane does not occlude direct fire.
                SetField(front, "currentLaneIndex", 1 - lane);
                At(front.transform, 4 + side, 1 - lane);
                Assert.That(rear.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
                SetField(front, "currentLaneIndex", lane);
                At(front.transform, 4 + side, lane);
                Assert.That(thrower.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
                rear.CommitScheduledTurnIntent(true);
                Assert.That(rear.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Wait));
                // Removing the front actor cannot rewrite the advertised wait.
                SetField(front, "currentHealth", 0);
                rear.TakeTurn(); yield return WaitForAction(rear);
                Assert.That(rear.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Wait));
                Assert.That(rear.IsAttackPrepared, Is.False);
                rear.CommitScheduledTurnIntent(true);
                Assert.That(rear.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
                rear.TakeTurn(); yield return WaitForAction(rear);
                Assert.That(rear.LastTurnAction, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
                Assert.That(rear.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Fire));
                foreach (var enemy in new[] { rear, front, thrower }) enemy.gameObject.SetActive(false);
            }
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.Destroy(created[i]);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator PreparationLimitKeepsPopulationAndPrioritizesWaitingEnemies()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExercisePreparationLimit();
        yield return new ExitPlayMode();
    }

    private static IEnumerator ExercisePreparationLimit()
    {
        var created = new List<Object>();
        try
        {
            var board = CreateComponent<BoardManager>("Preparation Board", created);
            var tiles = new GameObject("Preparation Tiles"); created.Add(tiles);
            SetField(board, "tileParent", tiles.transform);
            var template = CreateComponent<BoardTile>("Preparation Template", created);
            Assert.That(board.ConfigureBoard(8, 2, template), Is.True);
            var player = CreateComponent<PlayerMove>("Preparation Player", created);
            var health = player.gameObject.AddComponent<PlayerHealth>();
            Place(board, player.transform, 7);
            var waveObject = new GameObject("Preparation Wave"); created.Add(waveObject);
            waveObject.SetActive(false);
            var wave = waveObject.AddComponent<WaveManager>();
            SetField(wave, "boardManager", board);
            SetField(wave, "isResolvingTurn", true);
            var attack = CreateAttackAction(EnemyActionType.RangedAttack, 3, created);
            attack.name = "Preparation Test Attack";
            var enemies = new List<EnemyController>();
            for (int index = 0; index < 6; index++)
            {
                var enemy = CreateEnemy(EnemyBehaviorType.Thrower, new[] { attack },
                    board, player, health, wave, created);
                SetField(enemy.Data, "firingRange", 8);
                Place(board, enemy.transform, index);
                enemy.transform.localScale = Vector3.one;
                enemies.Add(enemy);
            }
            SetActiveEnemies(wave, enemies.ToArray());
            for (int cycle = 0; cycle < 6; cycle++)
            {
                if (cycle == 4)
                {
                    // Make earlier-spawned actors eligible again to exercise
                    // waiting priority independently of the cooldown advantage.
                    SetField(enemies[0], "recoveryTurnsRemaining", 0);
                    SetField(enemies[1], "recoveryTurnsRemaining", 0);
                }
                string rng = JsonUtility.ToJson(Random.state);
                // Even a later enemy asking first must not jump the queue.
                wave.TryCommitEnemyTurnIntents(enemies[5]);
                Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(rng));
                int firstPreparing = cycle / 2 * 2;
                int preparationCount = cycle % 2 == 0 ? 2 : 0;
                int attackCount = cycle % 2 == 1 ? 2 : 0;
                var plans = new List<EnemyTurnActionType>();
                for (int index = 0; index < enemies.Count; index++)
                {
                    var intent = enemies[index].GetNextTurnIntent().Action;
                    plans.Add(intent);
                    if (preparationCount > 0 && (index == firstPreparing || index == firstPreparing + 1))
                        Assert.That(intent, Is.EqualTo(EnemyTurnActionType.PrepareAttack), $"cycle {cycle}, enemy {index}");
                }
                Assert.That(plans.FindAll(a => a == EnemyTurnActionType.PrepareAttack).Count, Is.EqualTo(preparationCount));
                Assert.That(plans.FindAll(a => a == EnemyTurnActionType.Fire).Count, Is.EqualTo(attackCount));
                Assert.That(wave.ActiveEnemies.Count, Is.EqualTo(6));

                if (cycle == 0)
                {
                    // Losing a reserved preparer cannot rewrite the other advertised plans.
                    SetActiveEnemies(wave, enemies.GetRange(1, 5).ToArray());
                    wave.TryCommitEnemyTurnIntents(enemies[5]);
                    Assert.That(enemies[2].GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Wait));
                    SetActiveEnemies(wave, enemies.ToArray());
                }
                if (cycle == 1)
                {
                    var snapshot = new RunSaveData();
                    foreach (var enemy in enemies) snapshot.enemies.Add(enemy.CaptureRunState(wave.ActiveEnemies));
                    string path = System.IO.Path.Combine(Application.dataPath, "../Logs/ActionCombat/preparation-roundtrip.json");
                    System.IO.File.WriteAllText(path, JsonUtility.ToJson(snapshot));
                    snapshot = JsonUtility.FromJson<RunSaveData>(System.IO.File.ReadAllText(path));
                    RunSaveSystem.NormalizeSaveData(snapshot);
                    Assert.That(snapshot.enemies[4].preparationWaitTurns, Is.EqualTo(1));
                    Assert.That(snapshot.enemies[4].preparationDeferred, Is.True);
                    for (int index = 0; index < enemies.Count; index++)
                    {
                        enemies[index].RestoreRunState(snapshot.enemies[index], null);
                        Assert.That(enemies[index].GetNextTurnIntent().Action, Is.EqualTo(plans[index]));
                    }
                }
                for (int repeat = 0; repeat < 3; repeat++) wave.TryCommitEnemyTurnIntents(enemies[5]);
                for (int index = 0; index < enemies.Count; index++)
                    Assert.That(enemies[index].GetNextTurnIntent().Action, Is.EqualTo(plans[index]));

                foreach (var enemy in enemies)
                {
                    enemy.TakeTurn();
                    yield return WaitForAction(enemy);
                }
                Assert.That(enemies.FindAll(e => e.LastTurnAction == EnemyTurnActionType.PrepareAttack).Count, Is.EqualTo(preparationCount));
                Assert.That(enemies.FindAll(e => e.LastTurnAction == EnemyTurnActionType.Fire).Count, Is.EqualTo(attackCount));
                if (cycle == 1) Assert.That(enemies[4].PreparationWaitTurns, Is.EqualTo(2));
            }
            var legacy = JsonUtility.FromJson<RunEnemySaveData>("{}");
            Assert.That(legacy.preparationWaitTurns, Is.Zero);
            Assert.That(legacy.preparationDeferred, Is.False);
            var invalid = new RunSaveData();
            invalid.enemies.Add(new RunEnemySaveData { preparationWaitTurns = -10 });
            RunSaveSystem.NormalizeSaveData(invalid);
            Assert.That(invalid.enemies[0].preparationWaitTurns, Is.Zero);
        }
        finally
        {
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index] != null) Object.Destroy(created[index]);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator PreparationRequiresCurrentRangeAndCapturesCurrentPlayerCell()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExercisePreparationRange();
        yield return new ExitPlayMode();
    }

    private static IEnumerator ExercisePreparationRange()
    {
        var created = new List<Object>();
        try
        {
            var board = CreateComponent<BoardManager>("Preparation Range Board", created);
            var tiles = new GameObject("Range Tiles"); created.Add(tiles);
            SetField(board, "tileParent", tiles.transform);
            var template = CreateComponent<BoardTile>("Range Template", created);
            Assert.That(board.ConfigureBoard(8, 2, template), Is.True);
            var player = CreateComponent<PlayerMove>("Range Player", created);
            var health = player.gameObject.AddComponent<PlayerHealth>();
            var waveObject = new GameObject("Range Wave"); created.Add(waveObject);
            waveObject.SetActive(false);
            var wave = waveObject.AddComponent<WaveManager>();
            SetField(wave, "boardManager", board);
            SetField(wave, "isResolvingTurn", true);
            foreach (var behavior in new[] { EnemyBehaviorType.Melee, EnemyBehaviorType.Gunner, EnemyBehaviorType.Thrower, EnemyBehaviorType.BigBarrel })
            {
                var action = CreateAttackAction(behavior == EnemyBehaviorType.Melee ? EnemyActionType.MeleeAttack
                    : behavior == EnemyBehaviorType.BigBarrel ? EnemyActionType.ShotgunAttack : EnemyActionType.RangedAttack, 2, created);
                var enemy = CreateEnemy(behavior, new[] { action }, board, player, health, wave, created);
                SetActiveEnemies(wave, enemy);
                void Reset()
                {
                    typeof(EnemyController).GetMethod("ClearTurnIntent", PrivateInstance).Invoke(enemy, null);
                    SetField(enemy, "isAttackPrepared", false);
                    SetField(enemy, "recoveryTurnsRemaining", 0);
                    SetField(enemy, "isRetreating", false);
                    if (behavior == EnemyBehaviorType.BigBarrel)
                    {
                        var field = typeof(EnemyController).GetField("bigBarrelStep", PrivateInstance);
                        field.SetValue(enemy, System.Enum.Parse(field.FieldType, "PrepareShotgun"));
                    }
                    Place(board, enemy.transform, 2);
                    enemy.transform.localScale = Vector3.one;
                    player.SetLaneIndex(0);
                    Place(board, player.transform, behavior == EnemyBehaviorType.BigBarrel ? 3 : 4);
                }
                Reset();
                Place(board, player.transform, 6);
                if (behavior == EnemyBehaviorType.BigBarrel)
                {
                    enemy.transform.localScale = new Vector3(-1f, 1f, 1f);
                    Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.PrepareAttack), "Shotgun ignores distance and facing within the lane");
                }
                else
                    Assert.That(enemy.GetNextTurnIntent().Action, Is.Not.EqualTo(EnemyTurnActionType.PrepareAttack), behavior + " outside range");
                Reset();
                enemy.CommitTurnIntent();
                Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.PrepareAttack), behavior + " in range");
                Place(board, player.transform, 6);
                enemy.TakeTurn(); yield return WaitForAction(enemy);
                if (behavior == EnemyBehaviorType.BigBarrel)
                {
                    Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
                    var expected = new List<Vector2Int>();
                    for (int tile = 0; tile < 8; tile++)
                        if (tile != 2) expected.Add(new Vector2Int(tile, 0));
                    CollectionAssert.AreEquivalent(expected, GetField<HashSet<Vector2Int>>(enemy, "committedAttackCells"));
                    var left = CreateEnemy(EnemyBehaviorType.Melee, new[] { action }, board, player, health, wave, created);
                    var right = CreateEnemy(EnemyBehaviorType.Melee, new[] { action }, board, player, health, wave, created);
                    var otherLane = CreateEnemy(EnemyBehaviorType.Melee, new[] { action }, board, player, health, wave, created);
                    foreach (var target in new[] { left, right, otherLane })
                    {
                        SetField(target.Data, "maxHealth", 100);
                        SetField(target, "currentHealth", 100);
                    }
                    Place(board, left.transform, 0);
                    Place(board, right.transform, 7);
                    SetField(otherLane, "currentLaneIndex", 1);
                    board.TryGetTilePosition(7, 1, out Vector3 otherPosition);
                    otherLane.transform.position = otherPosition;
                    SetActiveEnemies(wave, enemy, left, right, otherLane);
                    int playerBefore = health.CurrentHealth;
                    int leftBefore = left.CurrentHealth;
                    int rightBefore = right.CurrentHealth;
                    int otherBefore = otherLane.CurrentHealth;
                    int bossBefore = enemy.CurrentHealth;
                    enemy.TakeTurn(); yield return WaitForAction(enemy);
                    int damage = enemy.Data.BigBarrel.ShotgunDamage;
                    Assert.That(health.CurrentHealth, Is.EqualTo(playerBefore - damage));
                    Assert.That(left.CurrentHealth, Is.EqualTo(Mathf.Max(0, leftBefore - damage)));
                    Assert.That(right.CurrentHealth, Is.EqualTo(Mathf.Max(0, rightBefore - damage)));
                    Assert.That(otherLane.CurrentHealth, Is.EqualTo(otherBefore));
                    Assert.That(enemy.CurrentHealth, Is.EqualTo(bossBefore));
                    foreach (var target in new[] { left, right, otherLane }) target.gameObject.SetActive(false);
                    SetActiveEnemies(wave, enemy);
                }
                else
                {
                    Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Wait), behavior + " left range after preview");
                    Assert.That(enemy.IsAttackPrepared, Is.False);
                }

                Reset();
                enemy.CommitTurnIntent();
                player.SetLaneIndex(1);
                board.TryGetTilePosition(3, 1, out Vector3 newPosition);
                player.transform.position = newPosition;
                enemy.TakeTurn(); yield return WaitForAction(enemy);
                if (behavior == EnemyBehaviorType.Thrower)
                {
                    Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
                    CollectionAssert.AreEquivalent(new[] { new Vector2Int(3, 1) },
                        GetField<HashSet<Vector2Int>>(enemy, "committedAttackCells"));
                }
                else
                {
                    Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Wait), behavior + " wrong lane");
                    Assert.That(enemy.IsAttackPrepared, Is.False);
                }

                if (behavior == EnemyBehaviorType.Melee || behavior == EnemyBehaviorType.Gunner)
                {
                    Reset();
                    enemy.CommitTurnIntent();
                    var blocker = CreateEnemy(EnemyBehaviorType.Melee, new[] { action }, board, player, health, wave, created);
                    Place(board, blocker.transform, 3);
                    SetActiveEnemies(wave, enemy, blocker);
                    enemy.TakeTurn(); yield return WaitForAction(enemy);
                    Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Wait), behavior + " blocked line");
                    SetActiveEnemies(wave, enemy);
                    blocker.gameObject.SetActive(false);
                }
                enemy.gameObject.SetActive(false);
            }
        }
        finally
        {
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index] != null) Object.Destroy(created[index]);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator AttackCooldownAllowsActionsAndSurvivesRestore()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseAttackCooldown();
        yield return new ExitPlayMode();
    }

    private static IEnumerator ExerciseAttackCooldown()
    {
        var created = new List<Object>();
        try
        {
            var board = CreateComponent<BoardManager>("Cooldown Board", created);
            var tiles = new GameObject("Cooldown Tiles"); created.Add(tiles);
            SetField(board, "tileParent", tiles.transform);
            var template = CreateComponent<BoardTile>("Cooldown Template", created);
            Assert.That(board.ConfigureBoard(7, 2, template), Is.True);
            var player = CreateComponent<PlayerMove>("Cooldown Player", created);
            var health = player.gameObject.AddComponent<PlayerHealth>();
            var waveObject = new GameObject("Cooldown Wave"); created.Add(waveObject);
            waveObject.SetActive(false);
            var wave = waveObject.AddComponent<WaveManager>();
            SetField(wave, "boardManager", board);
            SetField(wave, "isResolvingTurn", true);
            var approach = ScriptableObject.CreateInstance<EnemyActionData>(); created.Add(approach);
            SetField(approach, "actionType", EnemyActionType.Approach);
            SetField(approach, "movementDistance", 1);

            foreach (var behavior in new[] { EnemyBehaviorType.Melee, EnemyBehaviorType.Gunner, EnemyBehaviorType.Thrower })
            {
                var attack = CreateAttackAction(behavior == EnemyBehaviorType.Melee
                    ? EnemyActionType.MeleeAttack : EnemyActionType.RangedAttack, 3, created);
                attack.name = behavior + " Cooldown Attack";
                var enemy = CreateEnemy(behavior, new[] { approach, attack }, board, player, health, wave, created);
                SetActiveEnemies(wave, enemy);
                Place(board, enemy.transform, 2);
                player.SetLaneIndex(0); Place(board, player.transform, 3);
                enemy.transform.localScale = Vector3.one;
                Assert.That(enemy.Data.RecoveryTurns, Is.EqualTo(2));
                enemy.TakeTurn(); yield return WaitForAction(enemy);
                Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
                enemy.TakeTurn(); yield return WaitForAction(enemy);
                Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Fire));
                Assert.That(enemy.CaptureRunState(wave.ActiveEnemies).recoveryTurnsRemaining, Is.EqualTo(2));

                enemy.transform.localScale = new Vector3(-1, 1, 1);
                enemy.CommitTurnIntent();
                Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Rotate));
                enemy.TakeTurn(); yield return WaitForAction(enemy);
                Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Rotate));
                Assert.That(enemy.transform.localScale.x, Is.GreaterThan(0));
                enemy.CommitTurnIntent();
                var saved = JsonUtility.FromJson<RunEnemySaveData>(JsonUtility.ToJson(enemy.CaptureRunState(wave.ActiveEnemies)));
                Assert.That(saved.recoveryTurnsRemaining, Is.EqualTo(1));
                enemy.RestoreRunState(saved, null);
                Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Wait));
                yield return null;
                Assert.That(enemy.CaptureRunState(wave.ActiveEnemies).recoveryTurnsRemaining, Is.EqualTo(1), "Time and preview must not tick cooldown");
                enemy.TakeTurn(); yield return WaitForAction(enemy);
                Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Wait));
                Assert.That(enemy.IsAttackPrepared, Is.False, "Last cooldown action must not also prepare");
                Assert.That(enemy.CaptureRunState(wave.ActiveEnemies).recoveryTurnsRemaining, Is.Zero);
                enemy.TakeTurn(); yield return WaitForAction(enemy);
                Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
                enemy.TakeTurn(); yield return WaitForAction(enemy);
                Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Fire));
                Assert.That(enemy.CaptureRunState(wave.ActiveEnemies).recoveryTurnsRemaining, Is.EqualTo(2));

                if (behavior != EnemyBehaviorType.Thrower)
                {
                    // Cross-lane pursuit spends a cooling turn instead of freezing it.
                    player.SetLaneIndex(1);
                    board.TryGetTilePosition(2, 1, out Vector3 playerPosition);
                    player.transform.position = playerPosition;
                    enemy.CommitTurnIntent();
                    var intent = enemy.GetNextTurnIntent();
                    Assert.That(intent.Action, Is.EqualTo(EnemyTurnActionType.Move));
                    Assert.That(intent.Lane, Is.EqualTo(1));
                    enemy.TakeTurn(); yield return WaitForAction(enemy);
                    Assert.That(enemy.CurrentLaneIndex, Is.EqualTo(1));
                    Assert.That(enemy.CaptureRunState(wave.ActiveEnemies).recoveryTurnsRemaining, Is.EqualTo(1));
                    // A blocked advertised move still consumes exactly one cooling turn.
                    board.TryGetTilePosition(6, 1, out playerPosition);
                    player.transform.position = playerPosition;
                    enemy.CommitTurnIntent();
                    intent = enemy.GetNextTurnIntent();
                    Assert.That(intent.Action, Is.EqualTo(EnemyTurnActionType.Move));
                    player.transform.position = intent.Path[0];
                    enemy.TakeTurn(); yield return WaitForAction(enemy);
                    Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Wait));
                    Assert.That(enemy.CaptureRunState(wave.ActiveEnemies).recoveryTurnsRemaining, Is.Zero);
                }
                enemy.gameObject.SetActive(false);
            }

            var boss = CreateEnemy(EnemyBehaviorType.BigBarrel, null, board, player, health, wave, created);
            Place(board, boss.transform, 1);
            SetActiveEnemies(wave, boss);
            var legacy = boss.CaptureRunState(wave.ActiveEnemies);
            legacy.bigBarrelStep = (int)BigBarrelStep.Reload;
            legacy.bigBarrelReloadTurnsRemaining = 2;
            legacy.recoveryTurnsRemaining = 1;
            boss.RestoreRunState(JsonUtility.FromJson<RunEnemySaveData>(JsonUtility.ToJson(legacy)), null);
            var restored = boss.CaptureRunState(wave.ActiveEnemies);
            Assert.That(restored.recoveryTurnsRemaining, Is.EqualTo(2), "Legacy recovery must merge, not stack");
            Assert.That(restored.bigBarrelReloadTurnsRemaining, Is.Zero);
            Assert.That(restored.bigBarrelStep, Is.EqualTo((int)BigBarrelStep.CreateBombQueue));
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.Destroy(created[i]);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator CommittedIntentsKeepDirectionCellsAndSavedPlans()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseCommittedIntents();
        yield return new ExitPlayMode();
    }

    private static IEnumerator ExerciseCommittedIntents()
    {
        var created = new List<Object>();
        try
        {
            var board = CreateComponent<BoardManager>("Intent Board", created);
            var tiles = new GameObject("Intent Tiles"); created.Add(tiles);
            SetField(board, "tileParent", tiles.transform);
            var template = CreateComponent<BoardTile>("Intent Tile Template", created);
            Assert.That(board.ConfigureBoard(7, 2, template), Is.True);
            var player = CreateComponent<PlayerMove>("Intent Player", created);
            var health = player.gameObject.AddComponent<PlayerHealth>();
            var waveObject = new GameObject("Intent Wave"); created.Add(waveObject);
            waveObject.SetActive(false);
            var wave = waveObject.AddComponent<WaveManager>();
            SetField(wave, "boardManager", board);
            // Keep LateUpdate from publishing another plan during manual cycles.
            SetField(wave, "isResolvingTurn", true);
            var approach = ScriptableObject.CreateInstance<EnemyActionData>(); created.Add(approach);
            SetField(approach, "actionType", EnemyActionType.Approach);
            SetField(approach, "movementDistance", 1);
            var attack = CreateAttackAction(EnemyActionType.MeleeAttack, 1, created);
            var enemy = CreateEnemy(EnemyBehaviorType.Melee, new[] { approach, attack },
                board, player, health, wave, created);
            SetActiveEnemies(wave, enemy);

            void Reset(int enemyTile, int playerTile, int playerLane = 0)
            {
                typeof(EnemyController).GetMethod("ClearTurnIntent", PrivateInstance).Invoke(enemy, null);
                SetField(enemy, "isRetreating", false);
                SetField(enemy, "isAttackPrepared", false);
                SetField(enemy, "recoveryTurnsRemaining", 0);
                SetField(enemy, "currentLaneIndex", 0);
                Place(board, enemy.transform, enemyTile);
                enemy.transform.localScale = Vector3.one;
                player.SetLaneIndex(playerLane);
                board.TryGetTilePosition(playerTile, playerLane, out Vector3 pos);
                player.transform.position = pos;
            }
            Reset(1, 5);
            enemy.CommitTurnIntent();
            var move = enemy.GetNextTurnIntent();
            Assert.That(move.Action, Is.EqualTo(EnemyTurnActionType.Move));
            Place(board, player.transform, 0);
            Assert.That(enemy.GetNextTurnIntent().Path, Is.EqualTo(move.Path));
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            board.TryGetTileIndex(enemy.transform.position, 0, out int moved);
            Assert.That(moved, Is.EqualTo(2), "A crossing player must not reverse a published move");

            Reset(1, 5);
            enemy.CommitTurnIntent();
            Place(board, player.transform, 2);
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            board.TryGetTileIndex(enemy.transform.position, 0, out moved);
            Assert.That(moved, Is.EqualTo(1));
            Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Wait), "Blocked movement must not become an attack");

            Reset(3, 3, 1);
            enemy.CommitTurnIntent();
            var laneMove = enemy.GetNextTurnIntent();
            Assert.That(laneMove.Action, Is.EqualTo(EnemyTurnActionType.Move));
            Assert.That(laneMove.Lane, Is.EqualTo(1));
            player.SetLaneIndex(0); Place(board, player.transform, 0);
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            Assert.That(enemy.CurrentLaneIndex, Is.EqualTo(1));
            Assert.That(enemy.transform.position, Is.EqualTo(laneMove.Path[0]));

            Reset(1, 5); enemy.transform.localScale = new Vector3(-1, 1, 1);
            enemy.CommitTurnIntent();
            Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Rotate));
            Place(board, player.transform, 0);
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            Assert.That(enemy.transform.localScale.x, Is.GreaterThan(0));

            var ranged = CreateAttackAction(EnemyActionType.RangedAttack, 6, created);
            SetField(enemy.Data, "behaviorType", EnemyBehaviorType.Gunner);
            SetField(enemy.Data, "actions", new List<EnemyActionData> { ranged });
            Reset(1, 4);
            enemy.CommitTurnIntent();
            Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
            Assert.That(GetField<HashSet<Vector2Int>>(enemy, "committedAttackCells"), Is.Empty);
            var aiming = JsonUtility.FromJson<RunEnemySaveData>(JsonUtility.ToJson(enemy.CaptureRunState(wave.ActiveEnemies)));
            enemy.RestoreRunState(aiming, null);
            Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
            int attacks = 0;
            enemy.AttackExecuted += (_, _) => attacks++;
            int completions = 0;
            enemy.TurnActionCompleted += (_, _) => completions++;
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
            Assert.That(enemy.IsAttackPrepared, Is.True);
            Assert.That(attacks, Is.Zero, "Preparing must not execute damage");
            var cells = new HashSet<Vector2Int>(GetField<HashSet<Vector2Int>>(enemy, "committedAttackCells"));
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(4, 0) }, cells);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Fire));
            yield return null;
            Assert.That(attacks, Is.Zero, "Elapsed time must not release a prepared attack");
            player.SetLaneIndex(1);
            board.TryGetTilePosition(4, 1, out Vector3 changedPlayerPosition);
            player.transform.position = changedPlayerPosition;
            typeof(EnemyController).GetMethod("LateUpdate", PrivateInstance).Invoke(enemy, null);
            for (int cell = 0; cell < 14; cell++)
            {
                var renderer = tiles.transform.GetChild(cell).Find("Grid Warning").GetComponent<MeshRenderer>();
                Assert.That(renderer.enabled, Is.EqualTo(cells.Contains(new Vector2Int(cell % 7, cell / 7))), "Planning warning cell " + cell);
            }
            var saved = JsonUtility.FromJson<RunEnemySaveData>(JsonUtility.ToJson(enemy.CaptureRunState(wave.ActiveEnemies)));
            Assert.That(saved.nextIntent.committed, Is.True);
            var randomBefore = JsonUtility.ToJson(Random.state);
            enemy.RestoreRunState(saved, null);
            Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(randomBefore));
            Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Fire));
            CollectionAssert.AreEquivalent(cells, GetField<HashSet<Vector2Int>>(enemy, "committedAttackCells"));
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Fire), "Leaving the lane does not replace the shot with movement");
            Assert.That(attacks, Is.EqualTo(1));
            Assert.That(completions, Is.EqualTo(2));

            Reset(1, 4);
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            Assert.That(enemy.IsAttackPrepared, Is.True);
            Place(board, enemy.transform, 2);
            Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Wait));
            var pushed = JsonUtility.FromJson<RunEnemySaveData>(JsonUtility.ToJson(enemy.CaptureRunState(wave.ActiveEnemies)));
            enemy.RestoreRunState(pushed, null);
            Assert.That(enemy.IsAttackPrepared, Is.False, "Restoring a pushed plan must cancel readiness");
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            Assert.That(attacks, Is.EqualTo(1));
            Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.PrepareAttack));

            Reset(1, 4);
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            typeof(EnemyController).GetMethod("HandleStatusStacksChanged", PrivateInstance)
                .Invoke(enemy, new object[] { StatusEffectType.Stun, 1 });
            Assert.That(enemy.IsAttackPrepared, Is.False);
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            Assert.That(attacks, Is.EqualTo(1), "Interrupted preparation cannot fire");
            Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.PrepareAttack));

            Reset(1, 4);
            var blocker = CreateEnemy(EnemyBehaviorType.Melee, new[] { attack }, board, player, health, wave, created);
            Place(board, blocker.transform, 2);
            SetActiveEnemies(wave, enemy, blocker);
            enemy.CommitTurnIntent();
            Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Wait));
            SetActiveEnemies(wave, enemy);
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            Assert.That(enemy.LastTurnAction, Is.EqualTo(EnemyTurnActionType.Wait), "Removing a blocker cannot invent an unannounced shot");

            SetField(enemy.Data, "behaviorType", EnemyBehaviorType.Thrower);
            SetField(enemy.Data, "firingRange", 6);
            SetField(enemy, "recoveryTurnsRemaining", 0);
            Reset(1, 4, 1);
            enemy.CommitTurnIntent();
            Assert.That(GetField<HashSet<Vector2Int>>(enemy, "committedAttackCells"), Is.Empty);
            board.TryGetTilePosition(3, 1, out Vector3 latestTarget);
            player.transform.position = latestTarget;
            enemy.TakeTurn(); yield return WaitForAction(enemy);
            Assert.That(enemy.PreparedTargetTileIndex, Is.EqualTo(3));
            player.SetLaneIndex(0); Place(board, player.transform, 0);
            saved = JsonUtility.FromJson<RunEnemySaveData>(JsonUtility.ToJson(enemy.CaptureRunState(wave.ActiveEnemies)));
            enemy.RestoreRunState(saved, null);
            enemy.CommitTurnIntent();
            Assert.That(enemy.PreparedTargetTileIndex, Is.EqualTo(3));
            Assert.That(GetField<int>(enemy, "preparedTargetLaneIndex"), Is.EqualTo(1));

            saved.nextIntent.action = int.MaxValue;
            saved.isAttackPrepared = true;
            enemy.RestoreRunState(saved, null);
            Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Wait), "Malformed saved intent fails closed");
            Assert.That(tiles.transform.GetChild(11).Find("Grid Warning").GetComponent<MeshRenderer>().enabled,
                Is.False, "Legacy prepared state cannot restore a warning over a cancelled plan");
            saved.nextIntent = null;
            saved.isAttackPrepared = false;
            enemy.RestoreRunState(saved, null);
            enemy.CommitTurnIntent();
            Assert.That(enemy.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.Rotate), "Old saves plan once from the restored board");
        }
        finally
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.Destroy(created[i]);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator MixedEnemyCycleFiresReadyAttacksWithoutNewPreparation()
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
                // Throwers ignore frontline gating in this fixture.
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
            Assert.That(enemies[1].LastTurnAction, Is.EqualTo(EnemyTurnActionType.Wait));
            Assert.That(enemies[3].LastTurnAction, Is.EqualTo(EnemyTurnActionType.Wait));
            Assert.That(enemies[1].IsAttackPrepared || enemies[3].IsAttackPrepared, Is.False,
                "A firing cycle must leave the next cycle free of newly prepared attacks");
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

            Assert.That(melee.GetNextTurnIntent().Action, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
            melee.TakeTurn();
            yield return WaitForAction(melee);
            Assert.That(melee.LastTurnAction, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
            Assert.That(melee.IsAttackPrepared, Is.True);
            melee.TakeTurn();
            yield return WaitForAction(melee);

            Assert.That(
                melee.LastTurnAction,
                Is.EqualTo(EnemyTurnActionType.Fire));
            Assert.That(melee.IsAttackPrepared, Is.False);
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
            yield return WaitForAction(gunner);
            Assert.That(gunner.LastTurnAction, Is.EqualTo(EnemyTurnActionType.PrepareAttack));
            gunner.TakeTurn();
            yield return WaitForAction(gunner);

            Assert.That(
                gunner.LastTurnAction,
                Is.EqualTo(EnemyTurnActionType.Fire));
            Assert.That(gunner.IsAttackPrepared, Is.False);
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
                Is.EqualTo(BigBarrelStep.RotateToPlayer));
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

    private static IEnumerator WaitForAction(EnemyController enemy)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (enemy.IsActing && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(enemy.IsActing, Is.False, "Attack did not settle");
    }

    [UnityTest]
    public IEnumerator GunnerRecoveryKeepsImpactAndAuthoredWindowTiming()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var created = new List<Object>();
        float originalScale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            var enemy = CreateEnemy(EnemyBehaviorType.Gunner, null,
                null, null, null, null, created);
            var avatar = new GameObject("Recovery Avatar");
            created.Add(avatar);
            var animator = avatar.AddComponent<Animator>();
            var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Prefabs/Enemy/Enemy_Avatar/Gunner/Animation/Avatar_Gunner.controller");
            Assert.That(controller, Is.Not.Null);
            animator.runtimeAnimatorController = controller;
            avatar.AddComponent<EnemyAttackAnimationEvents>().Initialize(enemy);
            SetField(enemy, "avatarAnimator", animator);
            yield return null;

            // The production gunner clip is 1.5 seconds with fallback impact at 0.2.
            yield return CheckAttackTiming(enemy, 0.2f, 0.31f, 0.65f);

            var clip = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Prefabs/Enemy/Enemy_Avatar/Gunner/Animation/Attack.anim"));
            created.Add(clip);
            UnityEditor.AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent { functionName = EnemyAttackAnimationEvents.DodgeFunctionName, time = 0.3f },
                new AnimationEvent { functionName = EnemyAttackAnimationEvents.BeginFunctionName, time = 0.6f },
                new AnimationEvent { functionName = EnemyAttackAnimationEvents.EndFunctionName, time = 0.8f }
            });
            var overrides = new AnimatorOverrideController(controller);
            created.Add(overrides);
            overrides["Attack"] = clip;
            animator.runtimeAnimatorController = overrides;
            yield return CheckAttackTiming(enemy, 0.6f, 0.79f, 1.15f);

            // The same animation on another behavior must retain its full duration.
            SetField(enemy.Data, "behaviorType", EnemyBehaviorType.Melee);
            animator.runtimeAnimatorController = controller;
            yield return CheckAttackTiming(enemy, 0.2f, 1.49f, 1.85f);

            SetField(enemy.Data, "behaviorType", EnemyBehaviorType.Gunner);
            SetField(enemy, "avatarAnimator", null);
            yield return CheckAttackTiming(enemy, 0.2f, 0.19f, 0.5f);
        }
        finally
        {
            Time.timeScale = originalScale;
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.Destroy(created[i]);
        }
        yield return null;
        yield return new ExitPlayMode();
    }

    private static IEnumerator CheckAttackTiming(
        EnemyController enemy, float expectedImpact, float minimum, float maximum)
    {
        int hits = 0;
        float hitTime = -1f;
        float started = Time.time;
        SetField(enemy, "isActing", true);
        System.Action<bool> hit = _ => { hits++; hitTime = Time.time - started; };
        var method = typeof(EnemyController).GetMethod("PlayAvatarAnimation", PrivateInstance,
            null, new[] { typeof(int), typeof(System.Action<bool>), typeof(System.Func<bool>) }, null);
        var routines = new Stack<IEnumerator>();
        routines.Push((IEnumerator)method.Invoke(enemy, new object[]
        {
            Animator.StringToHash("Base Layer.Attack"), hit, new System.Func<bool>(() => false)
        }));
        float deadline = Time.realtimeSinceStartup + 5f;
        while (routines.Count > 0)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Attack did not complete");
            var current = routines.Peek();
            if (!current.MoveNext()) { routines.Pop(); continue; }
            if (current.Current is IEnumerator nested) routines.Push(nested);
            else yield return current.Current;
        }
        Assert.That(hits, Is.EqualTo(1));
        Assert.That(hitTime, Is.InRange(expectedImpact - 0.03f, expectedImpact + 0.1f));
        Assert.That(Time.time - started, Is.InRange(minimum, maximum));
        Assert.That(GetField<bool>(enemy, "isAttackDodgeWindowOpen"), Is.False);
        Assert.That(GetField<bool>(enemy, "isAttackActiveWindowOpen"), Is.False);
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
