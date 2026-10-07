using UnityEngine;

[CreateAssetMenu(
    fileName = "SpecialBattleBombProfile",
    menuName = "LOADED/Special Battle Bomb Profile")]
public sealed class SpecialBattleBombProfile : ScriptableObject
{
    [SerializeField] private string profileId = "special.death-bomb";
    [SerializeField] private GameObject bombPrefab;
    [Min(1)] [SerializeField] private int fuseTurns = 2;
    [Min(0)] [SerializeField] private int explosionRadius = 1;
    [Min(0)] [SerializeField] private int playerDamage = 10;
    [Min(0)] [SerializeField] private int enemyDamage = 10;
    [Min(0)] [SerializeField] private int bossDamage = 5;
    [Min(0f)] [SerializeField] private float dodgeWindowDuration = 0.2f;
    [SerializeField] private GameObject explosionVfxPrefab;
    [Min(0f)] [SerializeField] private float explosionVfxScale = 1f;

    public string ProfileId => string.IsNullOrWhiteSpace(profileId)
        ? name
        : profileId;
    public GameObject BombPrefab => bombPrefab;
    public int FuseTurns => Mathf.Clamp(fuseTurns, 1, 3);
    public int ExplosionRadius => Mathf.Max(0, explosionRadius);
    public int PlayerDamage => Mathf.Max(0, playerDamage);
    public int EnemyDamage => Mathf.Max(0, enemyDamage);
    public int BossDamage => Mathf.Max(0, bossDamage);
    public float DodgeWindowDuration => Mathf.Max(0f, dodgeWindowDuration);
    public GameObject ExplosionVfxPrefab => explosionVfxPrefab;
    public float ExplosionVfxScale => Mathf.Max(0f, explosionVfxScale);
}
