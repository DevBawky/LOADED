using UnityEngine;

internal static class BattleCameraEffectSpace
{
    public static Vector3 ResolveActorVisualCenter(
        Transform actor,
        Camera battleCamera,
        Vector3 fallbackPosition)
    {
        if (actor == null)
        {
            return fallbackPosition;
        }

        BattleContactShadow contactShadow =
            actor.GetComponent<BattleContactShadow>();
        if (contactShadow != null
            && contactShadow.TryResolveEffectAnchor(
                battleCamera,
                out Vector3 contactAnchor))
        {
            return contactAnchor;
        }

        Transform visualRoot = ResolveVisualRoot(actor);
        SpriteRenderer[] renderers = visualRoot
            .GetComponentsInChildren<SpriteRenderer>(true);

        if (battleCamera == null)
        {
            return ResolveWorldBoundsCenter(renderers, fallbackPosition);
        }

        bool hasPoint = false;
        Vector2 viewportMinimum = new Vector2(
            float.PositiveInfinity,
            float.PositiveInfinity);
        Vector2 viewportMaximum = new Vector2(
            float.NegativeInfinity,
            float.NegativeInfinity);
        float depthSum = 0f;
        int depthCount = 0;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (!IsVisible(renderer))
            {
                continue;
            }

            Bounds bounds = renderer.localBounds;
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;

            for (int xIndex = 0; xIndex < 2; xIndex++)
            {
                for (int yIndex = 0; yIndex < 2; yIndex++)
                {
                    Vector3 localPoint = new Vector3(
                        xIndex == 0 ? minimum.x : maximum.x,
                        yIndex == 0 ? minimum.y : maximum.y,
                        bounds.center.z);
                    Vector3 worldPoint = renderer.transform.TransformPoint(
                        localPoint);
                    Vector3 viewportPoint = battleCamera.WorldToViewportPoint(
                        worldPoint);

                    if (viewportPoint.z <= 0f
                        || !IsFinite(viewportPoint))
                    {
                        continue;
                    }

                    hasPoint = true;
                    viewportMinimum = Vector2.Min(
                        viewportMinimum,
                        viewportPoint);
                    viewportMaximum = Vector2.Max(
                        viewportMaximum,
                        viewportPoint);
                    depthSum += viewportPoint.z;
                    depthCount++;
                }
            }
        }

        if (!hasPoint || depthCount == 0)
        {
            return fallbackPosition;
        }

        Vector2 viewportCenter = (viewportMinimum + viewportMaximum) * 0.5f;
        return battleCamera.ViewportToWorldPoint(new Vector3(
            viewportCenter.x,
            viewportCenter.y,
            depthSum / depthCount));
    }

    public static Vector3 ResolveActorVisualOrigin(
        Transform actor,
        Vector3 fallbackPosition)
    {
        if (actor == null)
        {
            return fallbackPosition;
        }

        return ResolveVisualRoot(actor).position;
    }

    public static Vector3 Offset(
        Vector3 origin,
        Vector3 cameraSpaceOffset,
        Camera battleCamera)
    {
        if (battleCamera == null)
        {
            return origin + cameraSpaceOffset;
        }

        Vector3 result = origin
            + battleCamera.transform.right * cameraSpaceOffset.x
            + battleCamera.transform.up * cameraSpaceOffset.y;
        Vector3 directionToCamera = battleCamera.transform.position - result;

        if (directionToCamera.sqrMagnitude <= Mathf.Epsilon)
        {
            directionToCamera = -battleCamera.transform.forward;
        }

        return result + directionToCamera.normalized * cameraSpaceOffset.z;
    }

    public static Vector3 CameraPlaneDelta(
        Vector2 cameraPlaneDelta,
        Camera battleCamera)
    {
        if (battleCamera == null)
        {
            return cameraPlaneDelta;
        }

        return battleCamera.transform.right * cameraPlaneDelta.x
            + battleCamera.transform.up * cameraPlaneDelta.y;
    }

    public static Vector2 CameraPlaneComponents(
        Vector3 worldDelta,
        Camera battleCamera)
    {
        if (battleCamera == null)
        {
            return worldDelta;
        }

        return new Vector2(
            Vector3.Dot(worldDelta, battleCamera.transform.right),
            Vector3.Dot(worldDelta, battleCamera.transform.up));
    }

    public static Vector3 ResolveHorizontalViewportCorrection(
        Vector3 visualCenter,
        Vector3 targetCenterLine,
        Camera battleCamera)
    {
        if (battleCamera == null)
        {
            return new Vector3(
                targetCenterLine.x - visualCenter.x,
                0f,
                0f);
        }

        Vector3 visualViewport = battleCamera.WorldToViewportPoint(
            visualCenter);
        Vector3 targetViewport = battleCamera.WorldToViewportPoint(
            targetCenterLine);
        if (visualViewport.z <= 0f
            || targetViewport.z <= 0f
            || !IsFinite(visualViewport)
            || !IsFinite(targetViewport))
        {
            return Vector3.zero;
        }

        float worldHeight = battleCamera.orthographic
            ? battleCamera.orthographicSize * 2f
            : 2f
                * visualViewport.z
                * Mathf.Tan(battleCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float worldWidth = worldHeight * battleCamera.aspect;
        float horizontalOffset =
            (targetViewport.x - visualViewport.x) * worldWidth;
        return battleCamera.transform.right * horizontalOffset;
    }

    private static Transform ResolveVisualRoot(Transform actor)
    {
        BattleSpriteBillboard billboard =
            actor.GetComponentInChildren<BattleSpriteBillboard>(true);
        return billboard == null ? actor : billboard.transform;
    }

    private static Vector3 ResolveWorldBoundsCenter(
        SpriteRenderer[] renderers,
        Vector3 fallbackPosition)
    {
        bool hasBounds = false;
        Bounds combinedBounds = default;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (!IsVisible(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? combinedBounds.center : fallbackPosition;
    }

    private static bool IsVisible(SpriteRenderer renderer)
    {
        return renderer != null
            && renderer.sprite != null
            && renderer.enabled
            && renderer.gameObject.activeInHierarchy
            && renderer.color.a > 0.001f;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsNaN(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x)
            && !float.IsInfinity(value.y)
            && !float.IsInfinity(value.z);
    }
}
