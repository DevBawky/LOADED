using UnityEngine;

[CreateAssetMenu(
    fileName = "New Projectile Visual",
    menuName = "Loaded/Projectile Visual")]
public sealed class ProjectileVisualProfile : ScriptableObject
{
    private const string DefaultResourcePath =
        "ProjectileVisuals/DefaultProjectileVisual";

    [SerializeField] private BulletProjectileView projectilePrefab;
    [Min(0f)]
    [SerializeField] private float arcHeight = 0.12f;
    [Min(0.01f)]
    [SerializeField] private float scale = 0.24f;

    private static ProjectileVisualProfile defaultProfile;

    public BulletProjectileView ProjectilePrefab => projectilePrefab;
    public float ArcHeight => Mathf.Max(0f, arcHeight);
    public float Scale => Mathf.Max(0.01f, scale);

    public static ProjectileVisualProfile Default
    {
        get
        {
            defaultProfile ??= Resources.Load<ProjectileVisualProfile>(
                DefaultResourcePath);
            return defaultProfile;
        }
    }
}
