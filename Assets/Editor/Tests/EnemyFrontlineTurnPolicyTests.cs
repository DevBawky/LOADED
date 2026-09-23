using NUnit.Framework;

public sealed class EnemyFrontlineTurnPolicyTests
{
    [Test]
    public void NonFrontlineBehaviorCanActWithoutBoardContext()
    {
        Assert.That(
            EnemyFrontlineTurnPolicy.CanTakeTurn(
                false,
                false,
                0,
                0,
                null),
            Is.True);
    }

    [Test]
    public void FrontlineBehaviorWaitsWhenBoardContextIsUnavailable()
    {
        Assert.That(
            EnemyFrontlineTurnPolicy.CanTakeTurn(
                true,
                false,
                0,
                0,
                null),
            Is.False);
    }

    [Test]
    public void EnemyOnPlayerTileCanAct()
    {
        Assert.That(
            EnemyFrontlineTurnPolicy.CanTakeTurn(
                true,
                true,
                4,
                4,
                new[] { 3, 5 }),
            Is.True);
    }

    [TestCase(7, 4, 5)]
    [TestCase(1, 4, 3)]
    public void CloserEnemyOnSameSideBlocksTurn(
        int selfTileIndex,
        int playerTileIndex,
        int blockingTileIndex)
    {
        Assert.That(
            EnemyFrontlineTurnPolicy.CanTakeTurn(
                true,
                true,
                selfTileIndex,
                playerTileIndex,
                new[] { blockingTileIndex }),
            Is.False);
    }

    [Test]
    public void EnemyOnOppositeSideDoesNotBlockTurn()
    {
        Assert.That(
            EnemyFrontlineTurnPolicy.CanTakeTurn(
                true,
                true,
                7,
                4,
                new[] { 3 }),
            Is.True);
    }

    [Test]
    public void FartherEnemyOnSameSideDoesNotBlockTurn()
    {
        Assert.That(
            EnemyFrontlineTurnPolicy.CanTakeTurn(
                true,
                true,
                6,
                4,
                new[] { 7 }),
            Is.True);
    }
}

public sealed class EnemyLanePursuitPolicyTests
{
    [Test]
    public void SelectsClosestEnemyOnEachSide()
    {
        EnemyLanePursuitCandidate[] candidates =
        {
            new EnemyLanePursuitCandidate(10, 1, 0),
            new EnemyLanePursuitCandidate(20, 3, 0),
            new EnemyLanePursuitCandidate(30, 7, 0),
            new EnemyLanePursuitCandidate(40, 9, 0)
        };

        Assert.That(
            EnemyLanePursuitPolicy.ShouldPursueLane(20, 5, 1, candidates),
            Is.True);
        Assert.That(
            EnemyLanePursuitPolicy.ShouldPursueLane(30, 5, 1, candidates),
            Is.True);
        Assert.That(
            EnemyLanePursuitPolicy.ShouldPursueLane(10, 5, 1, candidates),
            Is.False);
        Assert.That(
            EnemyLanePursuitPolicy.ShouldPursueLane(40, 5, 1, candidates),
            Is.False);
    }

    [Test]
    public void EqualDistancePrefersEnemyAlreadyOnPlayerLane()
    {
        EnemyLanePursuitCandidate[] candidates =
        {
            new EnemyLanePursuitCandidate(10, 3, 0),
            new EnemyLanePursuitCandidate(20, 3, 1)
        };

        Assert.That(
            EnemyLanePursuitPolicy.ShouldPursueLane(10, 5, 1, candidates),
            Is.False);
        Assert.That(
            EnemyLanePursuitPolicy.ShouldPursueLane(20, 5, 1, candidates),
            Is.True);
    }

    [Test]
    public void EqualPriorityUsesStableCandidateOrder()
    {
        EnemyLanePursuitCandidate[] candidates =
        {
            new EnemyLanePursuitCandidate(10, 3, 0),
            new EnemyLanePursuitCandidate(20, 3, 0)
        };

        Assert.That(
            EnemyLanePursuitPolicy.ShouldPursueLane(10, 5, 1, candidates),
            Is.True);
        Assert.That(
            EnemyLanePursuitPolicy.ShouldPursueLane(20, 5, 1, candidates),
            Is.False);
    }
}
