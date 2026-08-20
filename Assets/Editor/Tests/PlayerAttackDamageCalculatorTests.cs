using NUnit.Framework;

public sealed class PlayerAttackDamageCalculatorTests
{
    [TestCase(10, 1.01d, 11)]
    [TestCase(10, 1.5d, 15)]
    [TestCase(1, 0.01d, 1)]
    [TestCase(0, 10d, 0)]
    [TestCase(10, 0d, 0)]
    [TestCase(10, -1d, 0)]
    public void MultiplyCeiling_ClampsAndRoundsExpectedValues(
        int damage,
        double multiplier,
        int expected)
    {
        Assert.That(
            PlayerAttackDamageCalculator.MultiplyCeiling(
                damage,
                multiplier),
            Is.EqualTo(expected));
    }

    [Test]
    public void MultiplyCeiling_SaturatesOverflow()
    {
        Assert.That(
            PlayerAttackDamageCalculator.MultiplyCeiling(
                int.MaxValue,
                double.MaxValue),
            Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void MultiplyCeiling_RejectsNotANumber()
    {
        Assert.That(
            PlayerAttackDamageCalculator.MultiplyCeiling(10, double.NaN),
            Is.Zero);
    }
}
