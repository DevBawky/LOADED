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
    [Tooltip("이 행동의 종류입니다. AI 타입과 일치하는 공격 종류를 사용하세요.")]
    [SerializeField] private EnemyActionType actionType;
    [Min(0)]
    [Tooltip("Approach 또는 Retreat 행동 한 번에 이동할 최대 타일 수입니다.")]
    [SerializeField] private int movementDistance;
    [Tooltip("공격 행동이 사용할 피해량, 사거리 및 상태이상 데이터입니다.")]
    [SerializeField] private EnemyAttackData attackData;
    [Tooltip("적의 공격 예약 슬롯에 표시할 아이콘입니다.")]
    [SerializeField] private Sprite icon;
    [TextArea]
    [Tooltip("기획 및 UI에서 사용할 행동 설명입니다.")]
    [SerializeField] private string description;

    public EnemyActionType ActionType => actionType;
    public int MovementDistance => movementDistance;
    public EnemyAttackData AttackData => attackData;
    public Sprite Icon => icon;
    public string Description => description;
}
