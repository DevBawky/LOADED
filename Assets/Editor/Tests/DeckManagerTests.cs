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
    public void EjectNextLoadedBulletMovesOnlyFirstShotToGraveyard()
    {
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryReload(out BulletInstance first), Is.True);
        Assert.That(deckManager.TryReload(out BulletInstance next), Is.True);

        Assert.That(
            deckManager.TryEjectNextLoadedBullet(out BulletInstance ejected),
            Is.True);
        Assert.That(ejected, Is.SameAs(next));
        Assert.That(deckManager.LoadedBullets, Has.Count.EqualTo(1));
        Assert.That(deckManager.LoadedBullets[0], Is.SameAs(first));
        Assert.That(deckManager.Graveyard, Has.Count.EqualTo(1));
        Assert.That(deckManager.Graveyard[0], Is.SameAs(next));
        Assert.That(deckManager.TotalBulletCount, Is.EqualTo(3));
    }

    [Test]
    public void EjectFromFullyLoadedDeckPreservesAdvertisedReloadOrder()
    {
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryAddBullet(bulletData), Is.True);
        Assert.That(deckManager.TryReload(out _), Is.True);
        Assert.That(deckManager.TryReload(out BulletInstance next), Is.True);

        Assert.That(deckManager.TryEjectNextLoadedBullet(out _), Is.True);
        Assert.That(deckManager.PeekNextBullet(), Is.SameAs(next));
        Assert.That(deckManager.TryReload(out BulletInstance reloaded), Is.True);
        Assert.That(reloaded, Is.SameAs(next));
        Assert.That(deckManager.TotalBulletCount, Is.EqualTo(2));
    }

    [Test]
    public void EjectNextLoadedBulletFailsWithoutChangingEmptyCylinder()
    {
        Assert.That(
            deckManager.TryEjectNextLoadedBullet(out BulletInstance ejected),
            Is.False);
        Assert.That(ejected, Is.Null);
        Assert.That(deckManager.TotalBulletCount, Is.Zero);
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

public sealed class EnemyDamageNumberDisplayTests
{
    private readonly System.Collections.Generic.List<GameObject>
        createdObjects = new System.Collections.Generic.List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void FindAvailableOffset_SeparatesActiveDamageNumbers()
    {
        DamageNumberSpawnLayout layout = new DamageNumberSpawnLayout();
        Vector3 requestedOffset = new Vector3(0f, 0.75f, -1f);
        const float minimumSeparation = 0.65f;

        Vector3 firstOffset = layout.FindAvailableOffset(
            requestedOffset,
            minimumSeparation);
        layout.Track(firstOffset, CreateDamageNumber());

        Vector3 secondOffset = layout.FindAvailableOffset(
            requestedOffset,
            minimumSeparation);

        Assert.That(
            Vector2.Distance(firstOffset, secondOffset),
            Is.GreaterThanOrEqualTo(minimumSeparation));
    }

    [Test]
    public void FindAvailableOffset_ReusesSlotAfterDamageNumberIsDestroyed()
    {
        DamageNumberSpawnLayout layout = new DamageNumberSpawnLayout();
        Vector3 requestedOffset = new Vector3(0f, 0.75f, -1f);
        DamageNumbersPro.DamageNumber firstNumber = CreateDamageNumber();
        Vector3 firstOffset = layout.FindAvailableOffset(
            requestedOffset,
            0.65f);
        layout.Track(firstOffset, firstNumber);

        Object.DestroyImmediate(firstNumber.gameObject);
        Vector3 reusedOffset = layout.FindAvailableOffset(
            requestedOffset,
            0.65f);

        Assert.That(reusedOffset, Is.EqualTo(requestedOffset));
    }

    [Test]
    public void FindAvailableOffset_ZeroSeparationAddsNoOffset()
    {
        DamageNumberSpawnLayout layout = new DamageNumberSpawnLayout();
        Vector3 requestedOffset = new Vector3(0f, 0.75f, -1f);
        layout.Track(requestedOffset, CreateDamageNumber());

        Vector3 nextOffset = layout.FindAvailableOffset(
            requestedOffset,
            0f);

        Assert.That(nextOffset, Is.EqualTo(requestedOffset));
    }

    private DamageNumbersPro.DamageNumber CreateDamageNumber()
    {
        GameObject gameObject = new GameObject("Damage Number Test");
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<DamageNumbersPro.DamageNumberMesh>();
    }
}

public sealed class ComboFeedbackProgressionTests
{
    [Test]
    public void FeedbackMultiplier_IncreasesForEveryComboKill()
    {
        float first = CombatFeedbackController
            .CalculateComboFeedbackMultiplier(1, 0.2f);
        float second = CombatFeedbackController
            .CalculateComboFeedbackMultiplier(2, 0.2f);
        float third = CombatFeedbackController
            .CalculateComboFeedbackMultiplier(3, 0.2f);

        Assert.That(first, Is.EqualTo(1f));
        Assert.That(second, Is.GreaterThan(first));
        Assert.That(third, Is.GreaterThan(second));
    }

    [Test]
    public void ComboPitch_IncreasesForEveryComboKill()
    {
        float first = SoundManager.CalculateComboPitch(1);
        float second = SoundManager.CalculateComboPitch(2);
        float third = SoundManager.CalculateComboPitch(3);

        Assert.That(first, Is.EqualTo(1f));
        Assert.That(second, Is.GreaterThan(first));
        Assert.That(third, Is.GreaterThan(second));
    }
}
