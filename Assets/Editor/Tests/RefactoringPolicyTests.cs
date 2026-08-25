using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class RefactoringPolicyTests
{
    private readonly List<ScriptableObject> createdAssets =
        new List<ScriptableObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (ScriptableObject asset in createdAssets)
        {
            if (asset != null)
            {
                Object.DestroyImmediate(asset);
            }
        }

        createdAssets.Clear();
    }

    [Test]
    public void GenerateItems_RemovesDuplicatesAndRespectsCapacity()
    {
        ItemData first = CreateAsset<ItemData>();
        ItemData second = CreateAsset<ItemData>();
        ItemData third = CreateAsset<ItemData>();
        List<ItemData> pool = new List<ItemData>
        {
            first,
            null,
            second,
            first,
            third
        };
        List<ItemData> offers = new List<ItemData> { first };

        Random.InitState(1729);
        ShopOfferGenerator.GenerateItems(pool, 2, offers);

        Assert.That(offers, Has.Count.EqualTo(2));
        Assert.That(new HashSet<ItemData>(offers), Has.Count.EqualTo(2));
        Assert.That(offers, Has.All.Matches<ItemData>(pool.Contains));
    }

    [Test]
    public void GenerateItems_ClearsDestinationWhenCapacityIsZero()
    {
        ItemData item = CreateAsset<ItemData>();
        List<ItemData> offers = new List<ItemData> { item };

        ShopOfferGenerator.GenerateItems(
            new[] { item },
            0,
            offers);

        Assert.That(offers, Is.Empty);
    }

    [TestCase(-1, 0)]
    [TestCase(0, 0)]
    [TestCase(4, 4)]
    [TestCase(5, 5)]
    [TestCase(6, 5)]
    [TestCase(int.MaxValue, 5)]
    public void ClampRefreshCost_RestrictsCostToSupportedRange(
        int refreshCost,
        int expected)
    {
        Assert.That(
            ShopManager.ClampRefreshCost(refreshCost),
            Is.EqualTo(expected));
    }

    [TestCase(-1, 1)]
    [TestCase(0, 1)]
    [TestCase(4, 5)]
    [TestCase(5, 5)]
    [TestCase(6, 5)]
    [TestCase(int.MaxValue, 5)]
    public void CalculateNextRefreshCost_IncreasesByOneAndStopsAtFive(
        int currentRefreshCost,
        int expected)
    {
        Assert.That(
            ShopManager.CalculateNextRefreshCost(currentRefreshCost),
            Is.EqualTo(expected));
    }

    [Test]
    public void RefreshCostRule_StartsAtZeroAndStopsAtFive()
    {
        int refreshCost = ShopManager.InitialRefreshCost;

        Assert.That(refreshCost, Is.Zero);

        for (int expected = 1; expected <= 5; expected++)
        {
            refreshCost = ShopManager.CalculateNextRefreshCost(refreshCost);
            Assert.That(refreshCost, Is.EqualTo(expected));
        }

        Assert.That(
            ShopManager.CalculateNextRefreshCost(refreshCost),
            Is.EqualTo(5));
    }

    [Test]
    public void CylinderEffectPolicy_TracksDirectTemporaryDamageBonus()
    {
        BulletData data = CreateAsset<BulletData>();
        BulletInstance bullet = new BulletInstance(data, 0);
        List<BulletInstance> loadedBullets =
            new List<BulletInstance> { bullet };

        Assert.That(
            CylinderBulletEffectPolicy.ShouldShow(
                loadedBullets,
                0,
                null,
                null,
                null,
                null),
            Is.False);

        bullet.AddTemporaryDamageBonus(0.25f);

        Assert.That(
            CylinderBulletEffectPolicy.ShouldShow(
                loadedBullets,
                0,
                null,
                null,
                null,
                null),
            Is.True);
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void CylinderEffectPolicy_RejectsInvalidIndex(int index)
    {
        Assert.That(
            CylinderBulletEffectPolicy.ShouldShow(
                new BulletInstance[1],
                index,
                null,
                null,
                null,
                null),
            Is.False);
    }

    private T CreateAsset<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        createdAssets.Add(asset);
        return asset;
    }
}
