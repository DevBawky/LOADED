using NUnit.Framework;

public sealed class PlayerCombatPreviewResourcesTests
{
    [Test]
    public void ResourceTransitionsAreLocalOrderedAndSaturating()
    {
        PlayerCombatPreviewResources resources =
            new PlayerCombatPreviewResources(10, 80, 100, null);

        resources.ApplyHealthCost(30);
        Assert.That(resources.CurrentHealth, Is.EqualTo(50));
        Assert.That(resources.TryHeal(20), Is.True);
        Assert.That(resources.CurrentHealth, Is.EqualTo(70));
        Assert.That(resources.TryIncreaseMaxHealth(10), Is.True);
        Assert.That(resources.CurrentHealth, Is.EqualTo(80));
        Assert.That(resources.MaxHealth, Is.EqualTo(110));
        Assert.That(resources.TryAddGold(int.MaxValue), Is.True);
        Assert.That(resources.CurrentGold, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void DefeatedPreviewCannotHealOrIncreaseMaxHealth()
    {
        PlayerCombatPreviewResources resources =
            new PlayerCombatPreviewResources(0, 1, 10, null);

        resources.ApplyHealthCost(1);

        Assert.That(resources.CurrentHealth, Is.Zero);
        Assert.That(resources.TryHeal(10), Is.False);
        Assert.That(resources.TryIncreaseMaxHealth(10), Is.False);
        Assert.That(resources.MaxHealth, Is.EqualTo(10));
    }
}
