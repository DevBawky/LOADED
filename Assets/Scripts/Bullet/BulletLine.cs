using UnityEngine;

public class BulletLine : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [Min(0.01f)]
    [SerializeField] private float lineDuration = 0.1f;

    public BulletData Data { get; private set; }

    public bool Initialize(BulletData bulletData, Vector3 startPoint, Vector3 endPoint)
    {
        if (lineRenderer == null)
        {
            Debug.LogError("Line Renderer must be assigned in the Inspector.", this);
            return false;
        }

        if (bulletData == null)
        {
            Debug.LogError("Bullet Data is required to initialize the bullet line.", this);
            return false;
        }

        Data = bulletData;

        if (bulletData.LineMaterial != null)
        {
            lineRenderer.sharedMaterial = bulletData.LineMaterial;
        }

        lineRenderer.startColor = bulletData.LineColor;
        lineRenderer.endColor = bulletData.LineColor;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        Destroy(gameObject, lineDuration);
        return true;
    }
}
