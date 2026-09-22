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
