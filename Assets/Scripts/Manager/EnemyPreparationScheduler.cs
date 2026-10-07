using System.Collections.Generic;

// WaveManager owns this coordinator. Decisions are frozen before presentation
// or execution; executing an advertised Fire never competes for these slots.
internal sealed class EnemyPreparationScheduler
{
    internal const int PreparationsPerCycle = 2;
    private readonly List<EnemyController> candidates = new List<EnemyController>();

    internal void Commit(IReadOnlyList<EnemyController> enemies)
    {
        candidates.Clear();
        int reserved = 0;
        bool hasAdvertisedAttack = false;
        foreach (EnemyController enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.CurrentHealth <= 0) continue;
            EnemyTurnActionType action = enemy.GetNextTurnIntent().Action;
            if (action == EnemyTurnActionType.Fire) hasAdvertisedAttack = true;
            if (enemy.HasCommittedTurnIntent)
            {
                if (action == EnemyTurnActionType.PrepareAttack) reserved++;
                continue;
            }
            if (action != EnemyTurnActionType.PrepareAttack) continue;
            // Stable insertion retains spawn order for equal waiting times.
            int index = candidates.Count;
            while (index > 0 && candidates[index - 1].PreparationWaitTurns < enemy.PreparationWaitTurns)
                index--;
            candidates.Insert(index, enemy);
        }

        for (int index = 0; index < candidates.Count; index++)
            candidates[index].CommitScheduledTurnIntent(!hasAdvertisedAttack
                && index < PreparationsPerCycle - reserved);

        foreach (EnemyController enemy in enemies)
            if (enemy != null && enemy.isActiveAndEnabled && enemy.CurrentHealth > 0)
                enemy.CommitScheduledTurnIntent(false);
    }
}
