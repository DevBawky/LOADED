using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal interface IEnemyTurnCycleRuntime
{
    IReadOnlyList<EnemyController> ActiveEnemies { get; }
    bool IsBattleCompleted { get; }
    bool IsPlayerDefeated { get; }
    bool HasPendingDetachedEnemyAttacks { get; }
    bool IsResolvingBossBombExplosions { get; }

    void RemoveMissingEnemies();
    void ProcessBossBombs(int enemyTurnCycle);
}

internal sealed class EnemyTurnCycleRunner
{
    public IEnumerator Resolve(
        IEnemyTurnCycleRuntime runtime,
        int enemyTurnCycle,
        float minimumTurnDuration,
        float actionInterval)
    {
        runtime.RemoveMissingEnemies();

        EnemyController[] enemiesThisTurn = CopyActiveEnemies(runtime);
        List<EnemyController> concurrentActions = new List<EnemyController>();
        float turnStartedAt = Time.time;

        for (int enemyIndex = 0;
             enemyIndex < enemiesThisTurn.Length;
             enemyIndex++)
        {
            EnemyController enemy = enemiesThisTurn[enemyIndex];

            if (enemy == null || !Contains(runtime.ActiveEnemies, enemy))
            {
                continue;
            }

            bool usesDedicatedMotion = enemy.WillExecuteDedicatedTurnMotion;

            if (usesDedicatedMotion)
            {
                yield return WaitForEnemyActions(concurrentActions);
                concurrentActions.Clear();
            }

            enemy.TakeTurn();

            if (!usesDedicatedMotion)
            {
                if (enemy != null && enemy.IsActing)
                {
                    concurrentActions.Add(enemy);
                }

                continue;
            }

            yield return WaitForEnemyAction(enemy);

            if (runtime.IsPlayerDefeated)
            {
                break;
            }

            if (ShouldWaitBetweenEnemyActions(
                    enemy == null
                        ? EnemyTurnActionType.None
                        : enemy.LastTurnAction,
                    enemyIndex < enemiesThisTurn.Length - 1))
            {
                yield return WaitForTurnTime(actionInterval);
            }
        }

        yield return WaitForEnemyActions(concurrentActions);
        yield return WaitForDetachedEnemyAttacks(runtime);

        float remainingTurnDelay = Mathf.Max(
            0f,
            minimumTurnDuration - (Time.time - turnStartedAt));
        yield return WaitForTurnTime(remainingTurnDelay);

        runtime.RemoveMissingEnemies();
        runtime.ProcessBossBombs(enemyTurnCycle);
        yield return WaitForBossBombExplosions(runtime);
        runtime.RemoveMissingEnemies();
    }

    internal static bool ShouldWaitBetweenEnemyActions(
        EnemyTurnActionType completedAction,
        bool hasFollowingEnemy)
    {
        return hasFollowingEnemy
            && completedAction == EnemyTurnActionType.Fire;
    }

    private static EnemyController[] CopyActiveEnemies(
        IEnemyTurnCycleRuntime runtime)
    {
        IReadOnlyList<EnemyController> activeEnemies = runtime.ActiveEnemies;
        EnemyController[] copy = new EnemyController[activeEnemies.Count];

        for (int index = 0; index < activeEnemies.Count; index++)
        {
            copy[index] = activeEnemies[index];
        }

        return copy;
    }

    private static bool Contains(
        IReadOnlyList<EnemyController> enemies,
        EnemyController target)
    {
        for (int index = 0; index < enemies.Count; index++)
        {
            if (enemies[index] == target)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerator WaitForTurnTime(float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                elapsedTime += Time.deltaTime;
            }
        }
    }

    private static IEnumerator WaitForEnemyAction(EnemyController enemy)
    {
        while (enemy != null && enemy.IsActing)
        {
            yield return null;
        }
    }

    private static IEnumerator WaitForEnemyActions(
        IReadOnlyList<EnemyController> enemies)
    {
        if (enemies == null || enemies.Count == 0)
        {
            yield break;
        }

        bool hasRunningAction = true;

        while (hasRunningAction)
        {
            hasRunningAction = false;

            for (int index = 0; index < enemies.Count; index++)
            {
                EnemyController enemy = enemies[index];

                if (enemy != null && enemy.IsActing)
                {
                    hasRunningAction = true;
                    break;
                }
            }

            if (hasRunningAction)
            {
                yield return null;
            }
        }
    }

    private static IEnumerator WaitForDetachedEnemyAttacks(
        IEnemyTurnCycleRuntime runtime)
    {
        while (runtime.HasPendingDetachedEnemyAttacks
               && !runtime.IsBattleCompleted && !runtime.IsPlayerDefeated)
        {
            yield return null;
        }
    }

    private static IEnumerator WaitForBossBombExplosions(
        IEnemyTurnCycleRuntime runtime)
    {
        while (runtime.IsResolvingBossBombExplosions
               && !runtime.IsBattleCompleted && !runtime.IsPlayerDefeated)
        {
            yield return null;
        }
    }
}
