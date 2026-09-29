using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(30000)]
public sealed class BattleUiOverlayCamera : MonoBehaviour
{
    private const string UiLayerName = "UI";

    [SerializeField] private Camera baseCamera;

    private Camera overlayCamera;

    public Camera BaseCamera => baseCamera;

    public void Configure(Camera sourceCamera, int rendererIndex)
    {
        baseCamera = sourceCamera;
        overlayCamera = GetComponent<Camera>();

        int uiLayer = LayerMask.NameToLayer(UiLayerName);
        if (baseCamera == null || uiLayer < 0)
        {
            enabled = false;
            return;
        }

        int uiMask = 1 << uiLayer;
        baseCamera.cullingMask &= ~uiMask;
        overlayCamera.cullingMask = uiMask;
        overlayCamera.enabled = true;

        UniversalAdditionalCameraData overlayData =
            overlayCamera.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.SetRenderer(rendererIndex);
        overlayData.renderPostProcessing = false;
        overlayData.renderShadows = false;
        overlayData.requiresDepthTexture = false;
        overlayData.requiresColorTexture = false;
        overlayData.antialiasing = AntialiasingMode.None;
        overlayData.dithering = false;
        overlayData.stopNaN = true;

        UniversalAdditionalCameraData baseData =
            baseCamera.GetUniversalAdditionalCameraData();
        if (!baseData.cameraStack.Contains(overlayCamera))
        {
            baseData.cameraStack.Add(overlayCamera);
        }

        SyncCameraState();
        enabled = true;
    }

    private void Awake()
    {
        overlayCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        SyncCameraState();
    }

    private void SyncCameraState()
    {
        if (baseCamera == null)
        {
            return;
        }

        overlayCamera ??= GetComponent<Camera>();
        transform.SetPositionAndRotation(
            baseCamera.transform.position,
            baseCamera.transform.rotation);
        overlayCamera.orthographic = baseCamera.orthographic;
        overlayCamera.fieldOfView = baseCamera.fieldOfView;
        overlayCamera.orthographicSize = baseCamera.orthographicSize;
        overlayCamera.nearClipPlane = baseCamera.nearClipPlane;
        overlayCamera.farClipPlane = baseCamera.farClipPlane;
        overlayCamera.rect = baseCamera.rect;
        overlayCamera.depth = baseCamera.depth + 1f;
        overlayCamera.clearFlags = CameraClearFlags.Depth;
        overlayCamera.allowHDR = baseCamera.allowHDR;
        overlayCamera.allowMSAA = false;
        overlayCamera.useOcclusionCulling = false;
    }
}
