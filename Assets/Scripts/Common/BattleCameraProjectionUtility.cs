using UnityEngine;

internal static class BattleCameraProjectionUtility
{
    public static float ResolveWorldWidth(
        Camera camera,
        Vector3 worldPosition)
    {
        if (camera == null)
        {
            return 0f;
        }

        float depth = camera.WorldToViewportPoint(worldPosition).z;
        return ResolveWorldWidth(camera, depth);
    }

    public static float ResolveWorldWidth(Camera camera, float cameraDepth)
    {
        if (camera == null || cameraDepth <= 0f)
        {
            return 0f;
        }

        if (camera.orthographic)
        {
            return camera.orthographicSize * 2f * camera.aspect;
        }

        float halfHeight = cameraDepth
            * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return halfHeight * 2f * camera.aspect;
    }

    public static float ResolveViewportX(
        Camera camera,
        Vector3 worldPosition,
        Vector3 cameraPosition)
    {
        if (camera == null)
        {
            return 0.5f;
        }

        Vector3 cameraSpacePosition = Quaternion.Inverse(
            camera.transform.rotation) * (worldPosition - cameraPosition);
        float viewWidth = ResolveWorldWidth(
            camera,
            cameraSpacePosition.z);
        return viewWidth <= Mathf.Epsilon
            ? 0.5f
            : 0.5f + cameraSpacePosition.x / viewWidth;
    }

    public static float ResolveCameraDepth(
        Camera camera,
        Vector3 worldPosition,
        Vector3 cameraPosition)
    {
        if (camera == null)
        {
            return 0f;
        }

        return Vector3.Dot(
            worldPosition - cameraPosition,
            camera.transform.forward);
    }
}
