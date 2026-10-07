internal enum EnemyStandardTurnIntent
{
    Wait,
    MoveTowardPlayer,
    CreateAttackQueue,
    RegisterAttack,
    PrepareAttack,
    CaptureThrowerTarget
}

internal enum EnemyBigBarrelTurnIntent
{
    Wait,
    PursuePlayerLane,
    RotateTowardPlayer,
    AdvanceOpeningStep,
    CreateBombQueue,
    RegisterBomb,
    PrepareBomb,
    ExecuteBomb,
    AdjustDistance,
    CreateShotgunQueue,
    RegisterShotgun,
    PrepareShotgun,
    ExecuteShotgun,
    Reload
}

internal static class EnemyTurnDecisionPolicy
{
    public static EnemyStandardTurnIntent GetMeleeIntent(
        bool hasAttack,
        bool isQueueCreated,
        int queuedAttackCount,
        int distanceToPlayer)
    {
        if (!hasAttack)
        {
            return EnemyStandardTurnIntent.MoveTowardPlayer;
        }

        if (!isQueueCreated)
        {
            return EnemyStandardTurnIntent.CreateAttackQueue;
        }

        if (queuedAttackCount <= 0)
        {
            return EnemyStandardTurnIntent.RegisterAttack;
        }

        return distanceToPlayer > 1
            ? EnemyStandardTurnIntent.MoveTowardPlayer
            : EnemyStandardTurnIntent.PrepareAttack;
    }

    public static EnemyStandardTurnIntent GetGunnerIntent(
        bool hasAttack,
        bool isQueueCreated,
        int queuedAttackCount,
        bool canPrepareAttack,
        int distanceToPlayer,
        int firingRange)
    {
        if (!hasAttack)
        {
            return EnemyStandardTurnIntent.MoveTowardPlayer;
        }

        if (!isQueueCreated)
        {
            return EnemyStandardTurnIntent.CreateAttackQueue;
        }

        if (queuedAttackCount <= 0)
        {
            return EnemyStandardTurnIntent.RegisterAttack;
        }

        if (canPrepareAttack)
        {
            return EnemyStandardTurnIntent.PrepareAttack;
        }

        return distanceToPlayer > firingRange
            ? EnemyStandardTurnIntent.MoveTowardPlayer
            : EnemyStandardTurnIntent.Wait;
    }

    public static EnemyStandardTurnIntent GetThrowerIntent(
        bool hasAttack,
        bool isQueueCreated,
        int queuedAttackCount)
    {
        if (!hasAttack)
        {
            return EnemyStandardTurnIntent.Wait;
        }

        if (!isQueueCreated)
        {
            return EnemyStandardTurnIntent.CreateAttackQueue;
        }

        return queuedAttackCount <= 0
            ? EnemyStandardTurnIntent.RegisterAttack
            : EnemyStandardTurnIntent.CaptureThrowerTarget;
    }

    public static EnemyBigBarrelTurnIntent GetBigBarrelIntent(
        bool hasTurnContext,
        bool hasLaneMismatch,
        bool isFacingPlayer,
        BigBarrelStep step)
    {
        if (!hasTurnContext)
        {
            return EnemyBigBarrelTurnIntent.Wait;
        }

        bool canIgnoreLaneMismatch = step == BigBarrelStep.ExecuteBomb
            || step == BigBarrelStep.ExecuteShotgun;

        if (hasLaneMismatch && !canIgnoreLaneMismatch)
        {
            return EnemyBigBarrelTurnIntent.PursuePlayerLane;
        }

        if (!isFacingPlayer)
        {
            return EnemyBigBarrelTurnIntent.RotateTowardPlayer;
        }

        return step switch
        {
            BigBarrelStep.RotateToPlayer =>
                EnemyBigBarrelTurnIntent.AdvanceOpeningStep,
            BigBarrelStep.CreateBombQueue =>
                EnemyBigBarrelTurnIntent.CreateBombQueue,
            BigBarrelStep.RegisterBomb =>
                EnemyBigBarrelTurnIntent.RegisterBomb,
            BigBarrelStep.PrepareBomb =>
                EnemyBigBarrelTurnIntent.PrepareBomb,
            BigBarrelStep.ExecuteBomb =>
                EnemyBigBarrelTurnIntent.ExecuteBomb,
            BigBarrelStep.AdjustDistance =>
                EnemyBigBarrelTurnIntent.AdjustDistance,
            BigBarrelStep.CreateShotgunQueue =>
                EnemyBigBarrelTurnIntent.CreateShotgunQueue,
            BigBarrelStep.RegisterShotgun =>
                EnemyBigBarrelTurnIntent.RegisterShotgun,
            BigBarrelStep.PrepareShotgun =>
                EnemyBigBarrelTurnIntent.PrepareShotgun,
            BigBarrelStep.ExecuteShotgun =>
                EnemyBigBarrelTurnIntent.ExecuteShotgun,
            BigBarrelStep.Reload => EnemyBigBarrelTurnIntent.Reload,
            _ => EnemyBigBarrelTurnIntent.Wait
        };
    }
}
