using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(20000)]
public sealed class BattleWorldCanvasDepthOffset : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0f)] private float cameraDepthOffset = 3f;

    private RectTransform rectTransform;
    private Vector3 authoredLocalPosition;
    private Vector3 authoredAnchoredPosition;
    private Vector3 authoredLocalScale;
    private Quaternion authoredLocalRotation;
    private bool hasCapturedPosition;

    public float CameraDepthOffset => Mathf.Max(0f, cameraDepthOffset);

    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        ApplyCameraProjection();
    }

    private void Awake()
    {
        CaptureAuthoredPosition();
    }

    private void OnEnable()
    {
        CaptureAuthoredPosition();
        ApplyCameraProjection();
    }

    private void OnDisable()
    {
        RestoreAuthoredPosition();
        RestoreAuthoredRotation();
        RestoreAuthoredScale();
    }

    private void LateUpdate()
    {
        ApplyCameraProjection();
    }

    private void CaptureAuthoredPosition()
    {
        if (hasCapturedPosition)
        {
            return;
        }

        authoredLocalPosition = transform.localPosition;
        authoredLocalScale = transform.localScale;
        authoredLocalRotation = transform.localRotation;
        rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
            authoredAnchoredPosition = rectTransform.anchoredPosition3D;
        }
        hasCapturedPosition = true;
    }

    private void ApplyCameraProjection()
    {
        CaptureAuthoredPosition();
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null || CameraDepthOffset <= 0f)
        {
            RestoreAuthoredPosition();
            if (targetCamera == null)
            {
                RestoreAuthoredRotation();
                RestoreAuthoredScale();
            }
            else
            {
                Vector3 facingWorldPosition = transform.parent == null
                    ? authoredLocalPosition
                    : transform.parent.TransformPoint(authoredLocalPosition);
                ApplyCameraFacing(ResolveCameraFacingScale(
                    facingWorldPosition,
                    facingWorldPosition));
            }
            return;
        }

        Vector3 authoredWorldPosition = transform.parent == null
            ? authoredLocalPosition
            : transform.parent.TransformPoint(authoredLocalPosition);
        Vector3 directionToCamera = targetCamera.orthographic
            ? -targetCamera.transform.forward
            : (targetCamera.transform.position - authoredWorldPosition)
                .normalized;
        Vector3 worldOffset = directionToCamera * CameraDepthOffset;
        Vector3 offsetWorldPosition = authoredWorldPosition + worldOffset;
        Vector3 localOffset = transform.parent == null
            ? worldOffset
            : transform.parent.InverseTransformVector(worldOffset);

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition3D =
                authoredAnchoredPosition + localOffset;
        }
        else
        {
            transform.localPosition = authoredLocalPosition + localOffset;
        }

        ApplyCameraFacing(ResolveCameraFacingScale(
            authoredWorldPosition,
            offsetWorldPosition));
    }

    private float ResolveCameraFacingScale(
        Vector3 authoredWorldPosition,
        Vector3 offsetWorldPosition)
    {
        Vector3 authoredWorldUp = transform.parent == null
            ? authoredLocalRotation * Vector3.up
            : transform.parent.TransformVector(
                authoredLocalRotation * Vector3.up);
        Vector3 facingWorldUp = targetCamera.transform.up
            * authoredWorldUp.magnitude;
        Vector3 authoredCenter = targetCamera.WorldToViewportPoint(
            authoredWorldPosition);
        Vector3 authoredUp = targetCamera.WorldToViewportPoint(
            authoredWorldPosition + authoredWorldUp);
        Vector3 facingCenter = targetCamera.WorldToViewportPoint(
            offsetWorldPosition);
        Vector3 facingUp = targetCamera.WorldToViewportPoint(
            offsetWorldPosition + facingWorldUp);
        float authoredHeight = Vector2.Distance(
            authoredCenter,
            authoredUp);
        float facingHeight = Vector2.Distance(facingCenter, facingUp);
        return facingHeight <= Mathf.Epsilon
            ? 1f
            : authoredHeight / facingHeight;
    }

    private void ApplyCameraFacing(float scale)
    {
        transform.rotation = BattleSpriteBillboard.GetFacingRotation(
            targetCamera);

        float currentFacingSign = transform.localScale.x < 0f ? -1f : 1f;
        Vector3 localScale = authoredLocalScale * scale;
        localScale.x = Mathf.Abs(authoredLocalScale.x)
            * currentFacingSign
            * scale;
        transform.localScale = localScale;
    }

    private void RestoreAuthoredPosition()
    {
        if (hasCapturedPosition)
        {
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition3D = authoredAnchoredPosition;
            }
            else
            {
                transform.localPosition = authoredLocalPosition;
            }
        }
    }

    private void RestoreAuthoredScale()
    {
        if (!hasCapturedPosition)
        {
            return;
        }

        float currentFacingSign = transform.localScale.x < 0f ? -1f : 1f;
        Vector3 localScale = authoredLocalScale;
        localScale.x = Mathf.Abs(authoredLocalScale.x) * currentFacingSign;
        transform.localScale = localScale;
    }

    private void RestoreAuthoredRotation()
    {
        if (hasCapturedPosition)
        {
            transform.localRotation = authoredLocalRotation;
        }
    }
}
