using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "New Enemy Attack", menuName = "Loaded/Enemy/Attack")]
public class EnemyAttackData : ScriptableObject
{
    [Header("Basic Information")]
    [Tooltip("저장 및 참조에 사용하는 공격의 고유 ID입니다.")]
    [SerializeField] private string skillId;
    [Tooltip("UI에 표시할 공격 이름입니다.")]
    [SerializeField] private string displayName;
    [TextArea]
    [Tooltip("공격의 특징과 효과를 설명하는 문장입니다.")]
    [SerializeField] private string description;

    [Header("Attack")]
    [Min(0)]
    [Tooltip("명중한 대상에게 적용할 기본 피해량입니다.")]
    [SerializeField] private int damage;
    [Min(0)]
    [Tooltip("근접 적이 사용하는 공격별 사거리입니다. 원거리 적은 EnemyData의 Firing Range를 우선 사용합니다.")]
    [SerializeField] private int range;
    [Min(0)]
    [Tooltip("공격이 적용할 밀치기 거리입니다. 현재 적 공격 처리에서는 별도의 밀치기 연동이 필요합니다.")]
    [SerializeField] private int knockbackDistance;
    [Min(0)]
    [Tooltip("명중한 대상에게 적용할 기절 턴 수입니다.")]
    [SerializeField] private int stunDurationTurns;
    [Min(0)]
    [Tooltip("명중한 대상에게 적용할 표식 지속 턴 수입니다.")]
    [SerializeField] private int markDurationTurns;
    [Min(0)]
    [FormerlySerializedAs("poisonDurationTurns")]
    [Tooltip("명중 시 적용할 중독 스택입니다. 매 턴 현재 스택만큼 피해를 주고 1스택 감소합니다.")]
    [SerializeField] private int poisonStackCount;
    [Min(0)]
    [Tooltip("명중한 대상에게 적용할 약화 지속 턴 수입니다.")]
    [SerializeField] private int weaknessDurationTurns;
    [Tooltip("공격이 명중한 위치에 생성할 이펙트 프리팹입니다.")]
    [SerializeField] private GameObject attackEffectPrefab;

    public string SkillId => skillId;
    public string DisplayName => displayName;
    public string Description => description;
    public int Damage => damage;
    public int Range => range;
    public int KnockbackDistance => knockbackDistance;
    public int StunDurationTurns => stunDurationTurns;
    public int MarkDurationTurns => markDurationTurns;
    public int PoisonStackCount => poisonStackCount;
    public int WeaknessDurationTurns => weaknessDurationTurns;
    public GameObject AttackEffectPrefab => attackEffectPrefab;
}
