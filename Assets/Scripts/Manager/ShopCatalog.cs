using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Shop Catalog", menuName = "Loaded/Shop Catalog")]
public sealed class ShopCatalog : ScriptableObject
{
    [SerializeField] private List<BulletData> bullets = new List<BulletData>();
    [SerializeField] private List<ItemData> items = new List<ItemData>();

    public IReadOnlyList<BulletData> Bullets => bullets;
    public IReadOnlyList<ItemData> Items => items;

    public BulletData FindBullet(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return null;
        }

        return bullets.Find(candidate => candidate != null
            && string.Equals(candidate.name, assetName, StringComparison.Ordinal));
    }

    public ItemData FindItem(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return null;
        }

        return items.Find(candidate => candidate != null
            && string.Equals(candidate.name, assetName, StringComparison.Ordinal));
    }
}
