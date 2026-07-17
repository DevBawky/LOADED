using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    [SerializeField] private TrailRenderer trailRenderer;

    public BulletData Data { get; private set; }

    public void Initialize(BulletData bulletData)
    {
        if (trailRenderer == null)
        {
            Debug.LogError("Trail Renderer must be assigned in the Inspector.", this);
            return;
        }

        trailRenderer.Clear();

        if (bulletData == null)
        {
            Debug.LogError("Bullet Data is required to initialize the projectile.", this);
            return;
        }

        Data = bulletData;
        trailRenderer.sharedMaterial = bulletData.TrailMaterial;
        trailRenderer.startColor = bulletData.TrailColor;
        trailRenderer.endColor = bulletData.TrailColor;
    }
}
