using System;
using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    public const int MinimumOwnedBulletCount = 7;
    public const int MaximumOwnedBulletCount = 15;

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
    [SerializeField] private int nextAcquisitionOrder;

    public event Action StateChanged;
    public event Action LoadedBulletsCleared;

    public IReadOnlyList<BulletInstance> Deck => deck;
    public IReadOnlyList<BulletInstance> LoadedBullets => loadedBullets;
    public IReadOnlyList<BulletInstance> Graveyard => graveyard;
    public int MaxReloadAmount => maxReloadAmount;
    public int TotalBulletCount => deck.Count + loadedBullets.Count
        + graveyard.Count;
    public int OwnedBulletCount => CountOwnedBullets(deck)
        + CountOwnedBullets(loadedBullets)
        + CountOwnedBullets(graveyard);
    public bool CanRemoveOwnedBullet =>
        OwnedBulletCount > MinimumOwnedBulletCount;

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

        RecycleGraveyardBeforeDeckRunsOut();

        if (deck.Count == 0)
        {
            return false;
        }

        int topIndex = deck.Count - 1;
        loadedBullet = deck[topIndex];
        loadedBullet?.BeginCylinderShotTracking();
        loadedBullets.Add(loadedBullet);
        deck.RemoveAt(topIndex);
        RecycleGraveyardBeforeDeckRunsOut();
        StateChanged?.Invoke();
        return true;
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
        StateChanged?.Invoke();
        return true;
    }

    public bool CanAddBullet(BulletData bulletData)
    {
        return bulletData != null
            && (!CountsTowardOwnedLimit(bulletData, 0)
                || OwnedBulletCount < MaximumOwnedBulletCount);
    }

    public bool TryUpgradeBullet(BulletInstance bullet)
    {
        if (!Contains(bullet) || !bullet.CanUpgrade)
        {
            return false;
        }

        bool countsNow = CountsTowardOwnedLimit(bullet);
        bool countsAfterUpgrade = CountsTowardOwnedLimit(
            bullet.Data,
            bullet.Level + 1);

        if (!countsNow && countsAfterUpgrade
            && OwnedBulletCount >= MaximumOwnedBulletCount)
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

        StateChanged?.Invoke();
        return true;
    }

    public bool CanRemoveBullet(BulletInstance bullet)
    {
        return bullet != null
            && Contains(bullet)
            && (!CountsTowardOwnedLimit(bullet)
                || CanRemoveOwnedBullet);
    }

    public bool TryDestroyBullet(BulletInstance bullet)
    {
        return TryRemoveBullet(bullet);
    }

    public bool ClearLoadedBullets()
    {
        if (loadedBullets.Count == 0)
        {
            return false;
        }

        graveyard.AddRange(loadedBullets);
        loadedBullets.Clear();
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

    public BulletInstance PeekNextBullet()
    {
        return deck.Count == 0 ? null : deck[deck.Count - 1];
    }

    public bool ReshuffleDeck()
    {
        if (deck.Count == 0)
        {
            return false;
        }

        ShuffleDeck();
        StateChanged?.Invoke();
        return true;
    }

    private void InitializeDeck()
    {
        deck.Clear();
        loadedBullets.Clear();
        graveyard.Clear();
        nextAcquisitionOrder = 0;

        foreach (BulletData bulletData in startingBullets)
        {
            if (CanAddBullet(bulletData))
            {
                deck.Add(CreateBulletInstance(bulletData));
            }
        }

        EnsureMinimumStartingBulletCount();

        ShuffleDeck();
        StateChanged?.Invoke();
    }

    private void EnsureMinimumStartingBulletCount()
    {
        if (OwnedBulletCount >= MinimumOwnedBulletCount)
        {
            return;
        }

        BulletData fallbackBullet = null;

        foreach (BulletData bulletData in startingBullets)
        {
            if (bulletData != null
                && CountsTowardOwnedLimit(bulletData, 0))
            {
                fallbackBullet = bulletData;
                break;
            }
        }

        if (fallbackBullet == null)
        {
            Debug.LogError(
                $"At least {MinimumOwnedBulletCount} valid starting bullets "
                + "are required, but no fallback BulletData is assigned.",
                this);
            return;
        }

        int missingBulletCount = MinimumOwnedBulletCount - OwnedBulletCount;

        for (int index = 0; index < missingBulletCount; index++)
        {
            deck.Add(CreateBulletInstance(fallbackBullet));
        }

        Debug.LogWarning(
            $"Starting bullet count was below {MinimumOwnedBulletCount}. "
            + $"Added {missingBulletCount} copies of "
            + $"'{fallbackBullet.name}' to satisfy the minimum.",
            this);
    }

    public static bool CountsTowardOwnedLimit(BulletInstance bullet)
    {
        return bullet != null && !HasDestructionEffect(
            bullet.Effects,
            bullet.ConditionalEvents);
    }

    public static bool CountsTowardOwnedLimit(
        BulletData bulletData,
        int level = 0)
    {
        return bulletData != null && !HasDestructionEffect(
            bulletData.GetEffects(level),
            bulletData.GetConditionalEvents(level));
    }

    private static int CountOwnedBullets(
        IReadOnlyList<BulletInstance> bullets)
    {
        int count = 0;

        foreach (BulletInstance bullet in bullets)
        {
            if (CountsTowardOwnedLimit(bullet))
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasDestructionEffect(
        IReadOnlyList<BulletEffectData> effects,
        IReadOnlyList<BulletConditionalEventData> conditionalEvents)
    {
        if (ContainsDestructionEffect(effects))
        {
            return true;
        }

        foreach (BulletConditionalEventData conditionalEvent
                 in conditionalEvents)
        {
            if (conditionalEvent != null
                && ContainsDestructionEffect(conditionalEvent.Events))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsDestructionEffect(
        IReadOnlyList<BulletEffectData> effects)
    {
        foreach (BulletEffectData effect in effects)
        {
            if (effect != null
                && (effect.EffectType == BulletEffectType.DestroyBullet
                    || effect.EffectType == BulletEffectType.PowderPouch))
            {
                return true;
            }
        }

        return false;
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

    private void RecycleGraveyardBeforeDeckRunsOut()
    {
        if (deck.Count <= 1)
        {
            RecycleGraveyard();
        }
    }

    private void ShuffleDeck()
    {
        for (int index = deck.Count - 1; index > 0; index--)
        {
            int randomIndex = UnityEngine.Random.Range(0, index + 1);
            BulletInstance temporary = deck[index];
            deck[index] = deck[randomIndex];
            deck[randomIndex] = temporary;
        }
    }
}
