#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public class DeckManagerTests
{
    private GameObject gameObject;
    private DeckManager deckManager;
    private BulletData bulletData;

    [SetUp]
    public void SetUp()
    {
        gameObject = new GameObject("Deck Manager Test");
        deckManager = gameObject.AddComponent<DeckManager>();
        bulletData = ScriptableObject.CreateInstance<BulletData>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gameObject);
        Object.DestroyImmediate(bulletData);
    }

    [Test]
    public void AllBulletsCountTowardTwentyBulletLimit()
    {
        for (int index = 0;
             index < DeckManager.MaximumOwnedBulletCount;
             index++)
        {
            Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        }

        Assert.That(deckManager.TotalBulletCount, Is.EqualTo(20));
        Assert.That(deckManager.CanAddBullet(bulletData), Is.False);
    }

    [Test]
    public void ManualRemovalPreservesLastOwnedBullet()
    {
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        BulletInstance onlyBullet = deckManager.PeekNextBullet();

        Assert.That(deckManager.CanRemoveBullet(onlyBullet), Is.False);
        Assert.That(deckManager.TryRemoveBullet(onlyBullet), Is.False);
        Assert.That(deckManager.TotalBulletCount, Is.EqualTo(1));
    }

    [Test]
    public void SingleBulletIsPreviewedBeforeItCanBeReloadedAgain()
    {
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        BulletInstance onlyBullet = deckManager.PeekNextBullet();

        Assert.That(deckManager.TryReload(out BulletInstance loaded), Is.True);
        Assert.That(loaded, Is.SameAs(onlyBullet));
        Assert.That(deckManager.ReloadableBulletCount, Is.Zero);
        Assert.That(deckManager.PeekNextBullet(), Is.SameAs(onlyBullet));

        Assert.That(deckManager.TryFireLoadedBullet(out BulletInstance fired),
            Is.True);
        Assert.That(fired, Is.SameAs(onlyBullet));
        deckManager.CompleteFiringSequence();

        Assert.That(deckManager.ReloadableBulletCount, Is.EqualTo(1));
        Assert.That(deckManager.PeekNextBullet(), Is.SameAs(onlyBullet));
    }

    [Test]
    public void DestroyedLastBulletRaisesDepletedAfterSequenceCompletes()
    {
        bool depleted = false;
        deckManager.BulletsDepleted += () => depleted = true;
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryReload(out _), Is.True);
        Assert.That(deckManager.TryFireLoadedBullet(out BulletInstance fired),
            Is.True);

        Assert.That(deckManager.TryDestroyBullet(fired), Is.True);
        Assert.That(depleted, Is.False);

        deckManager.CompleteFiringSequence();

        Assert.That(depleted, Is.True);
        Assert.That(deckManager.TotalBulletCount, Is.Zero);
        Assert.That(deckManager.PeekNextBullet(), Is.Null);
    }
}
#endif
