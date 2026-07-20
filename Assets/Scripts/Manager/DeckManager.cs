using System;
using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
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

    public IReadOnlyList<BulletInstance> Deck => deck;
    public IReadOnlyList<BulletInstance> LoadedBullets => loadedBullets;
    public IReadOnlyList<BulletInstance> Graveyard => graveyard;
    public int MaxReloadAmount => maxReloadAmount;
    public int OwnedBulletCount => deck.Count + loadedBullets.Count
        + graveyard.Count;

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

    public bool TryAddBullet(BulletData bulletData)
    {
        if (bulletData == null)
        {
            return false;
        }

        deck.Add(CreateBulletInstance(bulletData));
        StateChanged?.Invoke();
        return true;
    }

    public bool TryUpgradeBullet(BulletInstance bullet)
    {
        if (!Contains(bullet) || !bullet.TryUpgrade())
        {
            return false;
        }

        StateChanged?.Invoke();
        return true;
    }

    public bool TryRemoveBullet(BulletInstance bullet)
    {
        if (bullet == null)
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

    public bool TryDestroyBullet(BulletInstance bullet)
    {
        return TryRemoveBullet(bullet);
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
            if (bulletData != null)
            {
                deck.Add(CreateBulletInstance(bulletData));
            }
        }

        ShuffleDeck();
        StateChanged?.Invoke();
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
