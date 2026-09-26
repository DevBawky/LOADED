using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleWorldCanvasDepthOffset : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0f)] private float cameraDepthOffset = 3f;

    private RectTransform rectTransform;
    private Vector3 authoredLocalPosition;
    private Vector3 authoredAnchoredPosition;
    private float authoredLocalScaleMagnitudeX;
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
        authoredLocalScaleMagnitudeX = Mathf.Abs(transform.localScale.x);
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
            RestoreAuthoredScale();
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

        ApplyCameraAspectCompensation();
    }

    private void ApplyCameraAspectCompensation()
    {
        Vector3 cameraForward = targetCamera.transform.forward;
        float projectedWidth = Vector3.ProjectOnPlane(
            transform.right,
            cameraForward).magnitude;
        float projectedHeight = Vector3.ProjectOnPlane(
            transform.up,
            cameraForward).magnitude;

        if (projectedWidth <= Mathf.Epsilon)
        {
            RestoreAuthoredScale();
            return;
        }

        float facingSign = transform.localScale.x < 0f ? -1f : 1f;
        Vector3 localScale = transform.localScale;
        localScale.x = authoredLocalScaleMagnitudeX
            * facingSign
            * projectedHeight
            / projectedWidth;
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

        float facingSign = transform.localScale.x < 0f ? -1f : 1f;
        Vector3 localScale = transform.localScale;
        localScale.x = authoredLocalScaleMagnitudeX * facingSign;
        transform.localScale = localScale;
    }
}
