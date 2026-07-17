using System.Collections.Generic;
using UnityEngine;

public enum EnemyBehaviorType
{
    Melee = 0,
    Range = 1
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
    [Min(0)]
    [SerializeField] private int defeatReward;

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
    public int DefeatReward => defeatReward;
    public EnemyBehaviorType BehaviorType => behaviorType;
    public int PreferredDistance => preferredDistance;
    public IReadOnlyList<EnemyActionData> Actions => actions;
    public bool RandomizeStartingActionIndex => randomizeStartingActionIndex;
}
