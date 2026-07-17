using UnityEngine;

public enum EnemyActionType
{
    Approach,
    Retreat,
    Rotate,
    MeleeAttack,
    RangedAttack,
    Wait
}

[CreateAssetMenu(fileName = "New Enemy Action", menuName = "Loaded/Enemy/Action")]
public class EnemyActionData : ScriptableObject
{
    [SerializeField] private EnemyActionType actionType;
    [Min(0)]
    [SerializeField] private int movementDistance;
    [SerializeField] private EnemyAttackData attackData;
    [SerializeField] private Sprite icon;
    [TextArea]
    [SerializeField] private string description;

    public EnemyActionType ActionType => actionType;
    public int MovementDistance => movementDistance;
    public EnemyAttackData AttackData => attackData;
    public Sprite Icon => icon;
    public string Description => description;
}
