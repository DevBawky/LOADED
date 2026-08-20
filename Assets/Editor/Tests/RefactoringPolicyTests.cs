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
