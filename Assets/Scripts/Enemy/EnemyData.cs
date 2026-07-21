using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyBehaviorType
{
    Melee = 0,
    Range = 1
}

public enum EnemyDropType
{
    Gold = 0,
    InventoryItem = 1,
    Bullet = 2
}

[Serializable]
public class EnemyDropItemData
{
    [SerializeField] private EnemyDropType dropType;
    [Min(1)]
    [SerializeField] private int amount = 1;
    [Min(0f)]
    [SerializeField] private float selectionWeight = 1f;
    [SerializeField] private ItemData itemData;
    [SerializeField] private BulletData bulletData;

    public EnemyDropType DropType => dropType;
    public int Amount => Mathf.Max(1, amount);
    public float SelectionWeight => Mathf.Max(0f, selectionWeight);
    public ItemData ItemData => itemData;
    public BulletData BulletData => bulletData;

    public bool IsConfigured => dropType switch
    {
        EnemyDropType.Gold => amount > 0,
        EnemyDropType.InventoryItem => itemData != null && amount > 0,
        EnemyDropType.Bullet => bulletData != null && amount > 0,
        _ => false
    };
}

[CreateAssetMenu(fileName = "New Enemy", menuName = "Loaded/Enemy/Enemy")]
public class EnemyData : ScriptableObject
{
    [Header("Basic Information")]
    [SerializeField] private string enemyId;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite sprite;
    [SerializeField] private GameObject prefab;

    [Header("Stats")]
    [Min(1)]
    [SerializeField] private int maxHealth = 1;

    [Header("Defeat Drops")]
    [Range(0f, 100f)]
    [SerializeField] private float dropChance = 100f;
    [SerializeField] private List<EnemyDropItemData> dropItems =
        new List<EnemyDropItemData>();

    [Header("Behavior")]
    [SerializeField] private EnemyBehaviorType behaviorType;
    [Min(0)]
    [SerializeField] private int preferredDistance;
    [SerializeField] private List<EnemyActionData> actions = new List<EnemyActionData>();
    [SerializeField] private bool randomizeStartingActionIndex;

    public string EnemyId => enemyId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Sprite => sprite;
    public GameObject Prefab => prefab;
    public int MaxHealth => maxHealth;
    public float DropChance => Mathf.Clamp(dropChance, 0f, 100f);
    public IReadOnlyList<EnemyDropItemData> DropItems =>
        dropItems ?? (IReadOnlyList<EnemyDropItemData>)Array.Empty<EnemyDropItemData>();
    public EnemyBehaviorType BehaviorType => behaviorType;
    public int PreferredDistance => preferredDistance;
    public IReadOnlyList<EnemyActionData> Actions => actions;
    public bool RandomizeStartingActionIndex => randomizeStartingActionIndex;

    public bool TryRollDrop(out EnemyDropItemData selectedDrop)
    {
        selectedDrop = null;

        float chance = DropChance;

        if (chance <= 0f
            || chance < 100f && UnityEngine.Random.Range(0f, 100f) >= chance)
        {
            return false;
        }

        float totalWeight = 0f;

        foreach (EnemyDropItemData dropItem in DropItems)
        {
            if (dropItem != null && dropItem.IsConfigured)
            {
                totalWeight += dropItem.SelectionWeight;
            }
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);

        foreach (EnemyDropItemData dropItem in DropItems)
        {
            if (dropItem == null || !dropItem.IsConfigured
                || dropItem.SelectionWeight <= 0f)
            {
                continue;
            }

            roll -= dropItem.SelectionWeight;

            if (roll <= 0f)
            {
                selectedDrop = dropItem;
                return true;
            }
        }

        return false;
    }
}
