using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy Attack", menuName = "Loaded/Enemy/Attack")]
public class EnemyAttackData : ScriptableObject
{
    [Header("Basic Information")]
    [SerializeField] private string skillId;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;

    [Header("Attack")]
    [Min(0)]
    [SerializeField] private int damage;
    [Min(0)]
    [SerializeField] private int range;
    [Min(0)]
    [SerializeField] private int knockbackDistance;
    [Min(0)]
    [SerializeField] private int stunDurationTurns;
    [Min(0)]
    [SerializeField] private int markDurationTurns;
    [Min(0)]
    [SerializeField] private int poisonDurationTurns;
    [Min(0f)]
    [SerializeField] private float markDamageMultiplier = 1f;
    [SerializeField] private GameObject attackEffectPrefab;

    public string SkillId => skillId;
    public string DisplayName => displayName;
    public string Description => description;
    public int Damage => damage;
    public int Range => range;
    public int KnockbackDistance => knockbackDistance;
    public int StunDurationTurns => stunDurationTurns;
    public int MarkDurationTurns => markDurationTurns;
    public int PoisonDurationTurns => poisonDurationTurns;
    public float MarkDamageMultiplier => markDamageMultiplier;
    public GameObject AttackEffectPrefab => attackEffectPrefab;
}
