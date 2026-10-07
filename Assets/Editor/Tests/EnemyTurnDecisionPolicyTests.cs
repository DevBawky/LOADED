using NUnit.Framework;

internal sealed class EnemyTurnDecisionPolicyTests
{
    [TestCase(false, false, 0, 4,
        EnemyStandardTurnIntent.MoveTowardPlayer)]
    [TestCase(true, false, 0, 4,
        EnemyStandardTurnIntent.CreateAttackQueue)]
    [TestCase(true, true, 0, 4,
        EnemyStandardTurnIntent.RegisterAttack)]
    [TestCase(true, true, 1, 2,
        EnemyStandardTurnIntent.MoveTowardPlayer)]
    [TestCase(true, true, 1, 1,
        EnemyStandardTurnIntent.PrepareAttack)]
    public void MeleeIntentPreservesQueueAndDistanceOrder(
        bool hasAttack,
        bool isQueueCreated,
        int queuedAttackCount,
        int distanceToPlayer,
        EnemyStandardTurnIntent expected)
    {
        Assert.That(
            EnemyTurnDecisionPolicy.GetMeleeIntent(
                hasAttack,
                isQueueCreated,
                queuedAttackCount,
                distanceToPlayer),
            Is.EqualTo(expected));
    }

    [TestCase(false, false, 0, false, 2, 3,
        EnemyStandardTurnIntent.MoveTowardPlayer)]
    [TestCase(true, false, 0, false, 2, 3,
        EnemyStandardTurnIntent.CreateAttackQueue)]
    [TestCase(true, true, 0, false, 2, 3,
        EnemyStandardTurnIntent.RegisterAttack)]
    [TestCase(true, true, 1, true, 2, 3,
        EnemyStandardTurnIntent.PrepareAttack)]
    [TestCase(true, true, 1, false, 4, 3,
        EnemyStandardTurnIntent.MoveTowardPlayer)]
    [TestCase(true, true, 1, false, 2, 3,
        EnemyStandardTurnIntent.Wait)]
    public void GunnerIntentPreservesPreparationAndRangeOrder(
        bool hasAttack,
        bool isQueueCreated,
        int queuedAttackCount,
        bool canPrepareAttack,
        int distanceToPlayer,
        int firingRange,
        EnemyStandardTurnIntent expected)
    {
        Assert.That(
            EnemyTurnDecisionPolicy.GetGunnerIntent(
                hasAttack,
                isQueueCreated,
                queuedAttackCount,
                canPrepareAttack,
                distanceToPlayer,
                firingRange),
            Is.EqualTo(expected));
    }

    [TestCase(false, false, 0, EnemyStandardTurnIntent.Wait)]
    [TestCase(true, false, 0,
        EnemyStandardTurnIntent.CreateAttackQueue)]
    [TestCase(true, true, 0,
        EnemyStandardTurnIntent.RegisterAttack)]
    [TestCase(true, true, 1,
        EnemyStandardTurnIntent.CaptureThrowerTarget)]
    public void ThrowerIntentPreservesTargetCaptureOrder(
        bool hasAttack,
        bool isQueueCreated,
        int queuedAttackCount,
        EnemyStandardTurnIntent expected)
    {
        Assert.That(
            EnemyTurnDecisionPolicy.GetThrowerIntent(
                hasAttack,
                isQueueCreated,
                queuedAttackCount),
            Is.EqualTo(expected));
    }

    [Test]
    public void BigBarrelRequiresContextBeforeAnyStateMachineAction()
    {
        Assert.That(
            EnemyTurnDecisionPolicy.GetBigBarrelIntent(
                false,
                true,
                false,
                BigBarrelStep.ExecuteBomb),
            Is.EqualTo(EnemyBigBarrelTurnIntent.Wait));
    }

    [TestCase(BigBarrelStep.CreateBombQueue)]
    [TestCase(BigBarrelStep.PrepareShotgun)]
    [TestCase(BigBarrelStep.Reload)]
    public void BigBarrelPursuesLaneBeforeUncommittedActions(
        BigBarrelStep step)
    {
        Assert.That(
            EnemyTurnDecisionPolicy.GetBigBarrelIntent(
                true,
                true,
                true,
                step),
            Is.EqualTo(EnemyBigBarrelTurnIntent.PursuePlayerLane));
    }

    [TestCase(BigBarrelStep.ExecuteBomb,
        EnemyBigBarrelTurnIntent.ExecuteBomb)]
    [TestCase(BigBarrelStep.ExecuteShotgun,
        EnemyBigBarrelTurnIntent.ExecuteShotgun)]
    public void BigBarrelCommittedAttacksIgnoreLaneMismatch(
        BigBarrelStep step,
        EnemyBigBarrelTurnIntent expected)
    {
        Assert.That(
            EnemyTurnDecisionPolicy.GetBigBarrelIntent(
                true,
                true,
                true,
                step),
            Is.EqualTo(expected));
    }

    [Test]
    public void BigBarrelRotatesBeforeAdvancingItsStateMachine()
    {
        Assert.That(
            EnemyTurnDecisionPolicy.GetBigBarrelIntent(
                true,
                false,
                false,
                BigBarrelStep.RegisterBomb),
            Is.EqualTo(EnemyBigBarrelTurnIntent.RotateTowardPlayer));
    }

    [TestCase(BigBarrelStep.RotateToPlayer,
        EnemyBigBarrelTurnIntent.AdvanceOpeningStep)]
    [TestCase(BigBarrelStep.CreateBombQueue,
        EnemyBigBarrelTurnIntent.CreateBombQueue)]
    [TestCase(BigBarrelStep.RegisterBomb,
        EnemyBigBarrelTurnIntent.RegisterBomb)]
    [TestCase(BigBarrelStep.PrepareBomb,
        EnemyBigBarrelTurnIntent.PrepareBomb)]
    [TestCase(BigBarrelStep.ExecuteBomb,
        EnemyBigBarrelTurnIntent.ExecuteBomb)]
    [TestCase(BigBarrelStep.AdjustDistance,
        EnemyBigBarrelTurnIntent.AdjustDistance)]
    [TestCase(BigBarrelStep.CreateShotgunQueue,
        EnemyBigBarrelTurnIntent.CreateShotgunQueue)]
    [TestCase(BigBarrelStep.RegisterShotgun,
        EnemyBigBarrelTurnIntent.RegisterShotgun)]
    [TestCase(BigBarrelStep.PrepareShotgun,
        EnemyBigBarrelTurnIntent.PrepareShotgun)]
    [TestCase(BigBarrelStep.ExecuteShotgun,
        EnemyBigBarrelTurnIntent.ExecuteShotgun)]
    [TestCase(BigBarrelStep.Reload,
        EnemyBigBarrelTurnIntent.Reload)]
    public void BigBarrelStepMapsToOneExplicitIntent(
        BigBarrelStep step,
        EnemyBigBarrelTurnIntent expected)
    {
        Assert.That(
            EnemyTurnDecisionPolicy.GetBigBarrelIntent(
                true,
                false,
                true,
                step),
            Is.EqualTo(expected));
    }
}
