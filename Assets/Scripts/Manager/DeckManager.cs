using System;
using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [Header("Deck Settings")]
    [SerializeField] private List<BulletData> startingBullets = new List<BulletData>();
    [Min(1)]
    [SerializeField] private int maxReloadAmount = 6;

    [Header("Runtime State")]
    [SerializeField] private List<BulletData> deck = new List<BulletData>();
    [SerializeField] private List<BulletData> loadedBullets = new List<BulletData>();
    [SerializeField] private List<BulletData> graveyard = new List<BulletData>();

    public event Action StateChanged;

    public IReadOnlyList<BulletData> Deck => deck;
    public IReadOnlyList<BulletData> LoadedBullets => loadedBullets;
    public IReadOnlyList<BulletData> Graveyard => graveyard;
    public int MaxReloadAmount => maxReloadAmount;

    private void Awake()
    {
        InitializeDeck();
    }

    public bool TryReload()
    {
        if (loadedBullets.Count >= Mathf.Max(1, maxReloadAmount))
        {
            return false;
        }

        if (deck.Count == 0)
        {
            RecycleGraveyard();
        }

        if (deck.Count == 0)
        {
            return false;
        }

        int topIndex = deck.Count - 1;
        loadedBullets.Add(deck[topIndex]);
        deck.RemoveAt(topIndex);
        StateChanged?.Invoke();
        return true;
    }

    public bool TryFireLoadedBullet(out BulletData bulletData)
    {
        if (loadedBullets.Count == 0)
        {
            bulletData = null;
            return false;
        }

        int topIndex = loadedBullets.Count - 1;
        bulletData = loadedBullets[topIndex];
        loadedBullets.RemoveAt(topIndex);
        graveyard.Add(bulletData);
        StateChanged?.Invoke();
        return true;
    }

    private void InitializeDeck()
    {
        deck.Clear();
        loadedBullets.Clear();
        graveyard.Clear();

        foreach (BulletData bulletData in startingBullets)
        {
            if (bulletData != null)
            {
                deck.Add(bulletData);
            }
        }

        ShuffleDeck();
        StateChanged?.Invoke();
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

    private void ShuffleDeck()
    {
        for (int index = deck.Count - 1; index > 0; index--)
        {
            int randomIndex = UnityEngine.Random.Range(0, index + 1);
            BulletData temporary = deck[index];
            deck[index] = deck[randomIndex];
            deck[randomIndex] = temporary;
        }
    }
}
