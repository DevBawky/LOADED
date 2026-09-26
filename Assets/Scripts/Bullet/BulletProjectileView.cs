using UnityEngine;

[DisallowMultipleComponent]
public sealed class BulletProjectileView : MonoBehaviour
{
    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer[] renderers;

    private Vector3 travelStartPoint;
    private Vector3 travelEndPoint;
    private float travelArcHeight;
    private bool isInitialized;

    public static bool TrySpawn(
        ProjectileVisualProfile profile,
        BulletInstance bullet,
        Vector3 startPoint,
        Vector3 endPoint,
        out BulletProjectileView projectile)
    {
        projectile = null;
        ProjectileVisualProfile resolved = profile != null
            ? profile
            : ProjectileVisualProfile.Default;

        if (resolved == null || resolved.ProjectilePrefab == null
            || bullet == null)
        {
            return false;
        }

        projectile = Instantiate(
            resolved.ProjectilePrefab,
            startPoint,
            Quaternion.identity);
        bool initialized = projectile.Initialize(
            resolved,
            bullet,
            startPoint,
            endPoint);

        if (!initialized)
        {
            Destroy(projectile.gameObject);
            projectile = null;
        }

        return initialized;
    }

    public bool Initialize(
        ProjectileVisualProfile profile,
        BulletInstance bullet,
        Vector3 startPoint,
        Vector3 endPoint)
    {
        if (profile == null || bullet == null)
        {
            return false;
        }

        transform.position = startPoint;
        transform.localScale = Vector3.one * profile.Scale;
        ApplyBulletColors(bullet);
        travelStartPoint = startPoint;
        travelEndPoint = endPoint;
        travelArcHeight = profile.ArcHeight;
        isInitialized = true;

        Vector3 direction = endPoint - startPoint;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        return true;
    }

    public void SetTravelProgress(float progress)
    {
        if (!isInitialized)
        {
            return;
        }

        transform.position = EvaluateTravelPosition(
            travelStartPoint,
            travelEndPoint,
            travelArcHeight,
            progress);
    }

    public void CompleteTravel()
    {
        if (isInitialized)
        {
            transform.position = travelEndPoint;
        }

        isInitialized = false;
        Destroy(gameObject);
    }

    public void CancelTravel()
    {
        isInitialized = false;
        Destroy(gameObject);
    }

    internal static Vector3 EvaluateTravelPosition(
        Vector3 startPoint,
        Vector3 endPoint,
        float arcHeight,
        float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        Vector3 position = Vector3.Lerp(
            startPoint,
            endPoint,
            clampedProgress);
        position += Vector3.up
            * (Mathf.Sin(clampedProgress * Mathf.PI)
                * Mathf.Max(0f, arcHeight));
        return position;
    }

    private void ApplyBulletColors(BulletInstance bullet)
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        Color primary = bullet.PrimaryLineColor;
        Color emission = Color.Lerp(
            bullet.PrimaryLineColor,
            bullet.SecondaryLineColor,
            0.45f) * 1.6f;

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColorId, primary);
            properties.SetColor(EmissionColorId, emission);
            targetRenderer.SetPropertyBlock(properties);
            properties.Clear();
        }
    }

    private void OnDisable()
    {
        isInitialized = false;
    }
}
