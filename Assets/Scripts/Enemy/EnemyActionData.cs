using UnityEngine;

public enum EnemyActionType
{
    Approach,
    Retreat,
    Rotate,
    MeleeAttack,
    RangedAttack,
    Wait,
    Support,
    ExplosiveThrow,
    ShotgunAttack,
    Reload
}

[CreateAssetMenu(fileName = "New Enemy Action", menuName = "Loaded/Enemy/Action")]
public class EnemyActionData : ScriptableObject
{
    [Tooltip("행동 툴팁에 표시할 이름입니다. 비어 있으면 공격 이름 또는 에셋 이름을 사용합니다.")]
    [SerializeField] private string displayName;
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
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName)
        ? displayName
        : attackData != null
            && !string.IsNullOrWhiteSpace(attackData.DisplayName)
                ? attackData.DisplayName
                : name;
    public int MovementDistance => movementDistance;
    public EnemyAttackData AttackData => attackData;
    public Sprite Icon => icon;
    public string Description => description;
    public string TooltipDescription
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            if (attackData != null
                && !string.IsNullOrWhiteSpace(attackData.Description))
            {
                return attackData.Description;
            }

            return actionType switch
            {
                EnemyActionType.Approach => "플레이어에게 접근합니다.",
                EnemyActionType.Retreat => "플레이어에게서 물러납니다.",
                EnemyActionType.Rotate => "플레이어 방향으로 회전합니다.",
                EnemyActionType.MeleeAttack => CreateAttackSummary(),
                EnemyActionType.RangedAttack => CreateAttackSummary(),
                EnemyActionType.Wait => "이번 턴에는 행동하지 않습니다.",
                EnemyActionType.Support => "아군을 지원합니다.",
                EnemyActionType.ExplosiveThrow => "고정된 타일에 폭탄을 투척합니다.",
                EnemyActionType.ShotgunAttack => "고정된 양옆 타일을 동시에 공격합니다.",
                EnemyActionType.Reload => "다음 공격을 위해 재장전합니다.",
                _ => string.Empty
            };
        }
    }

    private string CreateAttackSummary()
    {
        return attackData == null
            ? "공격을 실행합니다."
            : $"피해 {attackData.Damage}, 사거리 {attackData.Range}";
    }
}
