using System;
using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    public const int MinimumOwnedBulletCount = 1;
    public const int MaximumOwnedBulletCount = 20;

    [Header("Deck Settings")]
    [SerializeField] private List<BulletData> startingBullets =
        new List<BulletData>();
    [Min(1)]
    [SerializeField] private int maxReloadAmount = 6;

    [Header("Runtime State")]
    [SerializeField] private List<BulletInstance> deck =
        new List<BulletInstance>();
    [SerializeField] private List<BulletInstance> loadedBullets =
        new List<BulletInstance>();
    [SerializeField] private List<BulletInstance> graveyard =
        new List<BulletInstance>();
    private readonly List<BulletInstance> nextCycleOrder =
        new List<BulletInstance>();
    private readonly List<BulletInstance> priorityReloadBullets =
        new List<BulletInstance>();
    [SerializeField] private int nextAcquisitionOrder;
    [Min(0)]
    [SerializeField] private int paidBulletRemovalCount;

    public event Action StateChanged;
    public event Action LoadedBulletsCleared;
    public event Action BulletsDepleted;

    public IReadOnlyList<BulletInstance> Deck => deck;
    public IReadOnlyList<BulletInstance> LoadedBullets => loadedBullets;
    public IReadOnlyList<BulletInstance> Graveyard => graveyard;
    public IReadOnlyList<BulletInstance> NextCycleOrder => nextCycleOrder;
    public int MaxReloadAmount => maxReloadAmount;
    public int TotalBulletCount => deck.Count + loadedBullets.Count
        + graveyard.Count;
    public int OwnedBulletCount => TotalBulletCount;
    public int ReloadableBulletCount => deck.Count + graveyard.Count;
    public int PaidBulletRemovalCount => Mathf.Max(
        0,
        paidBulletRemovalCount);
    public bool CanRemoveOwnedBullet =>
        TotalBulletCount > MinimumOwnedBulletCount;
    public int CurrentBulletRemovalCost =>
        CalculateBulletRemovalCost(paidBulletRemovalCount);

    private void Awake()
    {
        InitializeDeck();
    }

    public bool TryReload()
    {
        return TryReload(out _);
    }

    public bool TryReload(out BulletInstance loadedBullet)
    {
        loadedBullet = null;

        if (loadedBullets.Count >= Mathf.Max(1, maxReloadAmount))
        {
            return false;
        }

        BulletInstance priorityBullet = TakeNextPriorityReloadBullet();

        if (priorityBullet != null)
        {
            deck.Remove(priorityBullet);
            graveyard.Remove(priorityBullet);
            nextCycleOrder.Remove(priorityBullet);
            priorityBullet.BeginCylinderShotTracking();
            loadedBullets.Add(priorityBullet);
            RecycleGraveyardIfDeckEmpty();
            CreateNextCycleOrderIfNeeded();
            loadedBullet = priorityBullet;
            StateChanged?.Invoke();
            return true;
        }

        RecycleGraveyardIfDeckEmpty();

        if (deck.Count == 0)
        {
            return false;
        }

        int topIndex = deck.Count - 1;
        loadedBullet = deck[topIndex];
        loadedBullet?.BeginCylinderShotTracking();
        loadedBullets.Add(loadedBullet);
        deck.RemoveAt(topIndex);
        RecycleGraveyardIfDeckEmpty();
        CreateNextCycleOrderIfNeeded();
        StateChanged?.Invoke();
        return true;
    }

    public bool TryReloadOldestUsed(out BulletInstance loadedBullet)
    {
        loadedBullet = null;

        if (loadedBullets.Count >= Mathf.Max(1, maxReloadAmount)
            || graveyard.Count == 0)
        {
            return false;
        }

        loadedBullet = graveyard[0];
        graveyard.RemoveAt(0);
        priorityReloadBullets.Remove(loadedBullet);
        loadedBullet?.BeginCylinderShotTracking();
        loadedBullets.Add(loadedBullet);
        nextCycleOrder.Remove(loadedBullet);
        StateChanged?.Invoke();
        return true;
    }

    public bool QueueBulletForNextReload(int acquisitionOrder)
    {
        BulletInstance bullet = FindByAcquisitionOrder(acquisitionOrder);

        if (bullet == null)
        {
            return false;
        }

        deck.Remove(bullet);
        loadedBullets.Remove(bullet);
        graveyard.Remove(bullet);
        nextCycleOrder.Remove(bullet);
        deck.Add(bullet);

        if (!priorityReloadBullets.Contains(bullet))
        {
            priorityReloadBullets.Add(bullet);
        }

        StateChanged?.Invoke();
        return true;
    }

    public BulletInstance FindByAcquisitionOrder(int acquisitionOrder)
    {
        foreach (BulletInstance bullet in deck)
        {
            if (bullet != null
                && bullet.AcquisitionOrder == acquisitionOrder)
            {
                return bullet;
            }
        }

        foreach (BulletInstance bullet in loadedBullets)
        {
            if (bullet != null
                && bullet.AcquisitionOrder == acquisitionOrder)
            {
                return bullet;
            }
        }

        foreach (BulletInstance bullet in graveyard)
        {
            if (bullet != null
                && bullet.AcquisitionOrder == acquisitionOrder)
            {
                return bullet;
            }
        }

        return null;
    }

    public bool TryFireLoadedBullet(out BulletInstance bullet)
    {
        if (loadedBullets.Count == 0)
        {
            bullet = null;
            return false;
        }

        int topIndex = loadedBullets.Count - 1;
        bullet = loadedBullets[topIndex];
        loadedBullets.RemoveAt(topIndex);
        graveyard.Add(bullet);
        StateChanged?.Invoke();
        return true;
    }

    public bool TryEjectNextLoadedBullet(out BulletInstance bullet)
    {
        if (loadedBullets.Count == 0)
        {
            bullet = null;
            return false;
        }

        int nextShotIndex = loadedBullets.Count - 1;
        bullet = loadedBullets[nextShotIndex];
        loadedBullets.RemoveAt(nextShotIndex);
        graveyard.Add(bullet);
        FinalizeNextCycle();
        StateChanged?.Invoke();
        return true;
    }

    public bool TrySwapLoadedBullets(int firstIndex, int secondIndex)
    {
        if (firstIndex < 0 || firstIndex >= loadedBullets.Count
            || secondIndex < 0 || secondIndex >= loadedBullets.Count
            || firstIndex == secondIndex)
        {
            return false;
        }

        BulletInstance temporary = loadedBullets[firstIndex];
        loadedBullets[firstIndex] = loadedBullets[secondIndex];
        loadedBullets[secondIndex] = temporary;
        StateChanged?.Invoke();
        return true;
    }

    public bool TryAddBullet(BulletData bulletData)
    {
        if (!CanAddBullet(bulletData))
        {
            return false;
        }

        deck.Add(CreateBulletInstance(bulletData));
        nextCycleOrder.Clear();
        StateChanged?.Invoke();
        return true;
    }

    public bool CanAddBullet(BulletData bulletData)
    {
        return bulletData != null
            && TotalBulletCount < MaximumOwnedBulletCount;
    }

    public bool TryUpgradeBullet(BulletInstance bullet)
    {
        if (!Contains(bullet) || !bullet.CanUpgrade)
        {
            return false;
        }

        if (!bullet.TryUpgrade())
        {
            return false;
        }

        StateChanged?.Invoke();
        return true;
    }

    public bool TryRemoveBullet(BulletInstance bullet)
    {
        if (!CanRemoveBullet(bullet))
        {
            return false;
        }

        bool removed = deck.Remove(bullet);
        removed = loadedBullets.Remove(bullet) || removed;
        removed = graveyard.Remove(bullet) || removed;

        if (!removed)
        {
            return false;
        }

        nextCycleOrder.Clear();
        priorityReloadBullets.Remove(bullet);
        StateChanged?.Invoke();
        return true;
    }

    public void RegisterPaidBulletRemoval()
    {
        if (paidBulletRemovalCount < int.MaxValue)
        {
            paidBulletRemovalCount++;
        }

        StateChanged?.Invoke();
    }

    public bool CanRemoveBullet(BulletInstance bullet)
    {
        return bullet != null
            && Contains(bullet)
            && CanRemoveOwnedBullet;
    }

    public bool TryDestroyBullet(BulletInstance bullet)
    {
        if (bullet == null || !Contains(bullet))
        {
            return false;
        }

        bool removed = deck.Remove(bullet);
        removed = loadedBullets.Remove(bullet) || removed;
        removed = graveyard.Remove(bullet) || removed;

        if (!removed)
        {
            return false;
        }

        nextCycleOrder.Remove(bullet);
        priorityReloadBullets.Remove(bullet);
        StateChanged?.Invoke();
        return true;
    }

    public bool ClearLoadedBullets()
    {
        if (loadedBullets.Count == 0)
        {
            return false;
        }

        graveyard.AddRange(loadedBullets);
        loadedBullets.Clear();
        FinalizeNextCycle();
        StateChanged?.Invoke();
        LoadedBulletsCleared?.Invoke();
        return true;
    }

    public void PrepareForNewStage()
    {
        deck.AddRange(loadedBullets);
        deck.AddRange(graveyard);
        loadedBullets.Clear();
        graveyard.Clear();
        nextCycleOrder.Clear();

        foreach (BulletInstance bullet in deck)
        {
            bullet?.ResetStageState();
        }

        ShuffleDeck();
        StateChanged?.Invoke();
    }

    public bool Contains(BulletInstance bullet)
    {
        return bullet != null && (deck.Contains(bullet)
            || loadedBullets.Contains(bullet)
            || graveyard.Contains(bullet));
    }

    public void GetOwnedBullets(List<BulletInstance> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        results.AddRange(deck);
        results.AddRange(loadedBullets);
        results.AddRange(graveyard);
        results.Sort((left, right) => left.AcquisitionOrder.CompareTo(
            right.AcquisitionOrder));
    }

    public void CaptureRunState(
        List<RunBulletSaveData> savedBullets,
        List<int> savedNextCycleAcquisitionOrders)
    {
        if (savedBullets == null || savedNextCycleAcquisitionOrders == null)
        {
            return;
        }

        savedBullets.Clear();
        savedNextCycleAcquisitionOrders.Clear();
        CaptureBulletList(deck, 0, savedBullets);
        CaptureBulletList(loadedBullets, 1, savedBullets);
        CaptureBulletList(graveyard, 2, savedBullets);

        foreach (BulletInstance bullet in nextCycleOrder)
        {
            if (bullet != null)
            {
                savedNextCycleAcquisitionOrders.Add(
                    bullet.AcquisitionOrder);
            }
        }
    }

    public bool RestoreRunState(
        IReadOnlyList<RunBulletSaveData> savedBullets,
        Func<RunBulletSaveData, BulletData> resolveBulletData,
        int savedPaidBulletRemovalCount,
        IReadOnlyList<int> savedNextCycleAcquisitionOrders)
    {
        if (savedBullets == null || resolveBulletData == null)
        {
            return false;
        }

        List<(RunBulletSaveData Save, BulletInstance Bullet)> restoredBullets =
            new List<(RunBulletSaveData, BulletInstance)>();
        int highestAcquisitionOrder = -1;

        foreach (RunBulletSaveData savedBullet in savedBullets)
        {
            if (savedBullet == null
                || restoredBullets.Count >= MaximumOwnedBulletCount)
            {
                continue;
            }

            BulletData bulletData = resolveBulletData(savedBullet);

            if (bulletData == null)
            {
                Debug.LogWarning(
                    $"Saved bullet '{savedBullet.assetName}' could not be resolved.",
                    this);
                continue;
            }

            int acquisitionOrder = Mathf.Max(
                0,
                savedBullet.acquisitionOrder);
            BulletInstance bullet = new BulletInstance(
                bulletData,
                acquisitionOrder);
            int targetLevel = Mathf.Clamp(
                savedBullet.level,
                0,
                BulletData.MaximumUpgradeLevel);

            while (bullet.Level < targetLevel && bullet.TryUpgrade())
            {
            }

            bullet.ApplyRuntimeState(new BulletRuntimeStateSnapshot(
                savedBullet.abilityStacks,
                savedBullet.permanentStacks,
                savedBullet.storedDamageBonus,
                savedBullet.temporaryCriticalChanceBonus,
                savedBullet.temporaryDamageBonus,
                savedBullet.shotsObservedWhileLoaded));
            restoredBullets.Add((savedBullet, bullet));
            highestAcquisitionOrder = Mathf.Max(
                highestAcquisitionOrder,
                acquisitionOrder);
        }

        if (restoredBullets.Count == 0)
        {
            return false;
        }

        deck.Clear();
        loadedBullets.Clear();
        graveyard.Clear();
        nextCycleOrder.Clear();
        priorityReloadBullets.Clear();
        restoredBullets.Sort((left, right) =>
        {
            int locationComparison = left.Save.location.CompareTo(
                right.Save.location);
            return locationComparison != 0
                ? locationComparison
                : left.Save.locationIndex.CompareTo(
                    right.Save.locationIndex);
        });

        Dictionary<int, BulletInstance> bulletsByAcquisitionOrder =
            new Dictionary<int, BulletInstance>();

        foreach ((RunBulletSaveData saved, BulletInstance bullet)
                 in restoredBullets)
        {
            switch (saved.location)
            {
                case 1:
                    loadedBullets.Add(bullet);
                    break;
                case 2:
                    graveyard.Add(bullet);
                    break;
                default:
                    deck.Add(bullet);
                    break;
            }

            bulletsByAcquisitionOrder[bullet.AcquisitionOrder] = bullet;
        }

        if (savedNextCycleAcquisitionOrders != null)
        {
            foreach (int acquisitionOrder in savedNextCycleAcquisitionOrders)
            {
                if (bulletsByAcquisitionOrder.TryGetValue(
                        acquisitionOrder,
                        out BulletInstance bullet)
                    && !nextCycleOrder.Contains(bullet))
                {
                    nextCycleOrder.Add(bullet);
                }
            }
        }
        nextAcquisitionOrder = highestAcquisitionOrder == int.MaxValue
            ? int.MaxValue
            : highestAcquisitionOrder + 1;
        paidBulletRemovalCount = Mathf.Max(
            0,
            savedPaidBulletRemovalCount);
        StateChanged?.Invoke();
        return true;
    }

    private static void CaptureBulletList(
        IReadOnlyList<BulletInstance> source,
        int location,
        List<RunBulletSaveData> destination)
    {
        for (int index = 0; index < source.Count; index++)
        {
            BulletInstance bullet = source[index];

            if (bullet == null || bullet.Data == null)
            {
                continue;
            }

            BulletRuntimeStateSnapshot runtimeState =
                bullet.CaptureRuntimeState();
            destination.Add(new RunBulletSaveData
            {
                assetName = bullet.Data.name,
                bulletId = bullet.Data.BulletId,
                level = bullet.Level,
                acquisitionOrder = bullet.AcquisitionOrder,
                abilityStacks = runtimeState.AbilityStacks,
                permanentStacks = runtimeState.PermanentStacks,
                storedDamageBonus = runtimeState.StoredDamageBonus,
                temporaryCriticalChanceBonus =
                    runtimeState.TemporaryCriticalChanceBonus,
                temporaryDamageBonus = runtimeState.TemporaryDamageBonus,
                shotsObservedWhileLoaded =
                    runtimeState.ShotsObservedWhileLoaded,
                location = location,
                locationIndex = index
            });
        }
    }

    public BulletInstance PeekNextBullet()
    {
        BulletInstance priorityBullet = PeekPriorityReloadBullet();

        if (priorityBullet != null)
        {
            return priorityBullet;
        }

        if (deck.Count > 0)
        {
            return deck[deck.Count - 1];
        }

        // A short deck can be entirely loaded before this UI is enabled (for
        // example after removing bullets in the shop or restoring a battle).
        // Rebuild a missing reservation here as a final invariant guard so the
        // next-bullet preview never depends on the last reload notification.
        CreateNextCycleOrderIfNeeded();

        foreach (BulletInstance bullet in nextCycleOrder)
        {
            if (Contains(bullet))
            {
                return bullet;
            }
        }

        return null;
    }

    public void CompleteFiringSequence()
    {
        FinalizeNextCycle();
        StateChanged?.Invoke();

        if (TotalBulletCount == 0)
        {
            BulletsDepleted?.Invoke();
        }
    }

    public bool ReshuffleDeck()
    {
        if (deck.Count > 0)
        {
            ShuffleDeck();
            StateChanged?.Invoke();
            return true;
        }

        if (nextCycleOrder.Count > 1)
        {
            Shuffle(nextCycleOrder);
            StateChanged?.Invoke();
            return true;
        }

        return false;
    }

    private void InitializeDeck()
    {
        deck.Clear();
        loadedBullets.Clear();
        graveyard.Clear();
        nextCycleOrder.Clear();
        priorityReloadBullets.Clear();
        nextAcquisitionOrder = 0;
        paidBulletRemovalCount = 0;

        foreach (BulletData bulletData in startingBullets)
        {
            if (CanAddBullet(bulletData))
            {
                deck.Add(CreateBulletInstance(bulletData));
            }
        }

        ShuffleDeck();
        StateChanged?.Invoke();
    }

    private static int CalculateBulletRemovalCost(int removalCount)
    {
        int normalizedCount = Mathf.Max(0, removalCount);

        if (normalizedCount <= 1)
        {
            return 1;
        }

        int previous = 1;
        int current = 1;

        for (int index = 2; index <= normalizedCount; index++)
        {
            long next = (long)previous + current;

            if (next >= int.MaxValue)
            {
                return int.MaxValue;
            }

            previous = current;
            current = (int)next;
        }

        return current;
    }

    private BulletInstance CreateBulletInstance(BulletData bulletData)
    {
        BulletInstance bullet = new BulletInstance(
            bulletData,
            nextAcquisitionOrder);
        nextAcquisitionOrder++;
        return bullet;
    }

    private void RecycleGraveyard()
    {
        if (graveyard.Count == 0)
        {
            return;
        }

        deck.AddRange(graveyard);
        graveyard.Clear();
        ShuffleDeck();
    }

    private void RecycleGraveyardIfDeckEmpty()
    {
        // Keep the current top card stable until it is loaded. UI elements
        // expose that exact instance through PeekNextBullet(), so recycling
        // while one card remains would shuffle the advertised card away.
        if (deck.Count == 0)
        {
            RecycleGraveyard();
        }
    }

    private BulletInstance TakeNextPriorityReloadBullet()
    {
        while (priorityReloadBullets.Count > 0)
        {
            BulletInstance bullet = priorityReloadBullets[0];
            priorityReloadBullets.RemoveAt(0);

            if (bullet != null
                && (deck.Contains(bullet) || graveyard.Contains(bullet)))
            {
                return bullet;
            }
        }

        return null;
    }

    private BulletInstance PeekPriorityReloadBullet()
    {
        foreach (BulletInstance bullet in priorityReloadBullets)
        {
            if (bullet != null
                && (deck.Contains(bullet) || graveyard.Contains(bullet)))
            {
                return bullet;
            }
        }

        return null;
    }

    private void CreateNextCycleOrderIfNeeded()
    {
        if (deck.Count > 0 || graveyard.Count > 0
            || loadedBullets.Count == 0 || nextCycleOrder.Count > 0)
        {
            return;
        }

        nextCycleOrder.AddRange(loadedBullets);
        Shuffle(nextCycleOrder);
    }

    private void FinalizeNextCycle()
    {
        if (deck.Count > 0 || graveyard.Count == 0)
        {
            if (TotalBulletCount == 0)
            {
                nextCycleOrder.Clear();
            }

            return;
        }

        List<BulletInstance> orderedBullets = new List<BulletInstance>();

        foreach (BulletInstance bullet in nextCycleOrder)
        {
            if (bullet != null && graveyard.Contains(bullet)
                && !orderedBullets.Contains(bullet))
            {
                orderedBullets.Add(bullet);
            }
        }

        List<BulletInstance> remainingBullets = new List<BulletInstance>();

        foreach (BulletInstance bullet in graveyard)
        {
            if (bullet != null && !orderedBullets.Contains(bullet))
            {
                remainingBullets.Add(bullet);
            }
        }

        Shuffle(remainingBullets);
        orderedBullets.AddRange(remainingBullets);
        graveyard.Clear();

        for (int index = orderedBullets.Count - 1; index >= 0; index--)
        {
            deck.Add(orderedBullets[index]);
        }

        nextCycleOrder.Clear();
    }

    private void ShuffleDeck()
    {
        Shuffle(deck);
    }

    private static void Shuffle(List<BulletInstance> bullets)
    {
        for (int index = bullets.Count - 1; index > 0; index--)
        {
            int randomIndex = UnityEngine.Random.Range(0, index + 1);
            BulletInstance temporary = bullets[index];
            bullets[index] = bullets[randomIndex];
            bullets[randomIndex] = temporary;
        }
    }
}
