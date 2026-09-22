using NUnit.Framework;
using UnityEngine;

public sealed class CurrencyManagerTests
{
    [Test]
    public void WorldGoldCommitsBeforePresentationAndFlushDoesNotDuplicate()
    {
        GameObject managerObject = new GameObject("Currency");

        try
        {
            CurrencyManager manager =
                managerObject.AddComponent<CurrencyManager>();
            manager.RestoreRunMoney(7);
            int changeCount = 0;
            int lastAmount = -1;
            manager.MoneyChanged += amount =>
            {
                changeCount++;
                lastAmount = amount;
            };

            bool added = manager.AddMoneyFromWorld(5, Vector3.zero);

            Assert.That(added, Is.True);
            Assert.That(manager.CurrentMoney, Is.EqualTo(12));
            Assert.That(changeCount, Is.EqualTo(1));
            Assert.That(lastAmount, Is.EqualTo(12));

            manager.FlushPendingMoney();

            Assert.That(manager.CurrentMoney, Is.EqualTo(12));
            Assert.That(changeCount, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
        }
    }
}
