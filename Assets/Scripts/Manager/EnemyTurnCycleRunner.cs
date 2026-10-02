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
        List<EnemyController> attacks = new List<EnemyController>();
        float turnStartedAt = Time.time;

        // Classify before any turn runs: preparing an attack this cycle must
        // not also execute it when the attack pass begins.
        foreach (EnemyController enemy in enemiesThisTurn)
        {
            if (enemy == null)
            {
                continue;
            }
            bool attacksPlayer = enemy.WillExecuteDedicatedTurnMotion
                && (enemy.Data == null
                    || enemy.Data.BehaviorType != EnemyBehaviorType.Porter);
            if (attacksPlayer)
            {
                attacks.Add(enemy);
            }
            else
            {
                concurrentActions.Add(enemy);
            }
        }

        // Queue reveals, preparation, support and movement all start in the
        // same frame, retaining spawn order for reservations and decisions.
        foreach (EnemyController enemy in concurrentActions)
        {
            if (runtime.IsBattleCompleted || runtime.IsPlayerDefeated)
            {
                yield break;
            }
            if (CanAct(runtime, enemy))
            {
                enemy.TakeTurn();
            }
        }
        // Settle movement once so attacks never target intermediate positions.
        yield return WaitForEnemyActions(runtime, concurrentActions);
        if (runtime.IsBattleCompleted || runtime.IsPlayerDefeated)
        {
            yield break;
        }

        for (int enemyIndex = 0; enemyIndex < attacks.Count; enemyIndex++)
        {
            if (runtime.IsBattleCompleted || runtime.IsPlayerDefeated)
            {
                yield break;
            }
            EnemyController enemy = attacks[enemyIndex];
            if (!CanAct(runtime, enemy))
            {
                continue;
            }
            enemy.TakeTurn();
            yield return WaitForEnemyAction(runtime, enemy);
            if (runtime.IsBattleCompleted || runtime.IsPlayerDefeated)
            {
                yield break;
            }
            if (ShouldWaitBetweenEnemyActions(
                    enemy == null
                        ? EnemyTurnActionType.None
                        : enemy.LastTurnAction,
                    enemyIndex < attacks.Count - 1))
            {
                yield return WaitForTurnTime(actionInterval);
            }
        }

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

    private static bool CanAct(IEnemyTurnCycleRuntime runtime, EnemyController enemy)
    {
        return enemy != null && enemy.isActiveAndEnabled
            && Contains(runtime.ActiveEnemies, enemy);
    }

    private static IEnumerator WaitForEnemyAction(
        IEnemyTurnCycleRuntime runtime, EnemyController enemy)
    {
        while (!runtime.IsBattleCompleted && !runtime.IsPlayerDefeated
               && CanAct(runtime, enemy) && enemy.IsActing)
        {
            yield return null;
        }
    }

    private static IEnumerator WaitForEnemyActions(
        IEnemyTurnCycleRuntime runtime,
        IReadOnlyList<EnemyController> enemies)
    {
        if (enemies == null || enemies.Count == 0)
        {
            yield break;
        }

        bool hasRunningAction = true;

        while (hasRunningAction && !runtime.IsBattleCompleted
               && !runtime.IsPlayerDefeated)
        {
            hasRunningAction = false;

            for (int index = 0; index < enemies.Count; index++)
            {
                EnemyController enemy = enemies[index];

                if (CanAct(runtime, enemy) && enemy.IsActing)
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
