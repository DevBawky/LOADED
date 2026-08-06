using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Confines Cinemachine's body position before its Noise stage runs, so camera
/// shake is not clipped or overwritten at the edge of the background.
/// </summary>
public sealed class OrthographicCameraRectConfiner : CinemachineExtension
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RectTransform boundsRect;
    [Min(0f)]
    [SerializeField] private float padding;
    [SerializeField] private bool confineHorizontal = true;
    [SerializeField] private bool confineVertical = true;

    private readonly Vector3[] worldCorners = new Vector3[4];

    protected override void Awake()
    {
        base.Awake();
        targetCamera ??= GetComponent<Camera>();
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body
            || boundsRect == null
            || !state.Lens.Orthographic)
        {
            return;
        }

        boundsRect.GetWorldCorners(worldCorners);

        float minX = worldCorners[0].x;
        float maxX = worldCorners[0].x;
        float minY = worldCorners[0].y;
        float maxY = worldCorners[0].y;

        for (int index = 1; index < worldCorners.Length; index++)
        {
            Vector3 corner = worldCorners[index];
            minX = Mathf.Min(minX, corner.x);
            maxX = Mathf.Max(maxX, corner.x);
            minY = Mathf.Min(minY, corner.y);
            maxY = Mathf.Max(maxY, corner.y);
        }

        float halfHeight = state.Lens.OrthographicSize;
        float aspect = state.Lens.Aspect > 0f
            ? state.Lens.Aspect
            : targetCamera != null ? targetCamera.aspect : 1f;
        float halfWidth = halfHeight * aspect;
        Vector3 position = state.RawPosition;

        if (confineHorizontal)
        {
            position.x = ClampCameraAxis(
                position.x,
                minX,
                maxX,
                halfWidth,
                padding);
        }

        if (confineVertical)
        {
            position.y = ClampCameraAxis(
                position.y,
                minY,
                maxY,
                halfHeight,
                padding);
        }

        state.RawPosition = position;
    }

    private static float ClampCameraAxis(
        float position,
        float boundsMinimum,
        float boundsMaximum,
        float cameraExtent,
        float padding)
    {
        float minimum = boundsMinimum + cameraExtent + padding;
        float maximum = boundsMaximum - cameraExtent - padding;

        if (minimum > maximum)
        {
            return (boundsMinimum + boundsMaximum) * 0.5f;
        }

        return Mathf.Clamp(position, minimum, maximum);
    }
}
