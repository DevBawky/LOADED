using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class BattleWorld3DController : MonoBehaviour
{
    [SerializeField] private BattleEnvironmentProfile defaultProfile;
    [SerializeField] private Transform boardRoot;
    [SerializeField] private Camera battleCamera;
    [SerializeField] private Volume battleVolume;
    [SerializeField] private Light directionalLight;
    [SerializeField] private Transform playerVisualRoot;
    [SerializeField] private Transform backgroundPropRoot;

    private static readonly int BaseColorId = Shader.PropertyToID(
        "_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock backgroundProperties;

    private bool hasCapturedAuthoredCameraState;
    private Vector3 authoredCameraLocalPosition;
    private Quaternion authoredCameraLocalRotation;
    private Vector3 authoredFollowOffset;
    private bool authoredCameraOrthographic;
    private float authoredCameraFieldOfView;
    private float authoredCameraOrthographicSize;
    private float authoredCameraNearClipPlane;
    private float authoredCameraFarClipPlane;
    private bool hasCapturedAuthoredCinemachineLens;
    private LensSettings authoredCinemachineLens;

    public BattleEnvironmentProfile ActiveProfile { get; private set; }

    private void Awake()
    {
        CaptureAuthoredCameraState();
        ApplyProfile(defaultProfile);
    }

    public void ApplyProfile(BattleEnvironmentProfile profile)
    {
        BattleEnvironmentProfile resolved = profile != null
            ? profile
            : defaultProfile;

        if (resolved == null)
        {
            return;
        }

        ActiveProfile = resolved;

        if (boardRoot != null)
        {
            boardRoot.SetLocalPositionAndRotation(
                resolved.BoardLocalPosition,
                resolved.BoardLocalRotation);
        }

        if (battleCamera != null)
        {
            CaptureAuthoredCameraState();
            RestoreAuthoredCameraState();
            battleCamera.backgroundColor = resolved.CameraBackgroundColor;
            battleCamera.clearFlags = CameraClearFlags.Skybox;
            battleCamera.allowHDR = true;
            battleCamera.allowMSAA = false;

            UniversalAdditionalCameraData cameraData =
                battleCamera.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(resolved.RendererIndex);
            cameraData.renderShadows = true;
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true;
            cameraData.requiresColorTexture = true;
            cameraData.antialiasing = AntialiasingMode
                .SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.dithering = true;
            cameraData.stopNaN = true;

            BattleUiOverlayCamera uiOverlay =
                battleCamera.GetComponentInChildren<BattleUiOverlayCamera>(
                    true);
            if (uiOverlay != null)
            {
                uiOverlay.Configure(battleCamera, resolved.RendererIndex);
            }
        }

        if (battleVolume != null && resolved.VolumeProfile != null)
        {
            battleVolume.sharedProfile = resolved.VolumeProfile;
            ApplyTerrainDepthOfField();
        }

        ApplyWorldRendering(resolved);

        if (directionalLight != null)
        {
            directionalLight.color = resolved.DirectionalLightColor;
            directionalLight.intensity =
                resolved.DirectionalLightIntensity;
            directionalLight.transform.rotation =
                resolved.DirectionalLightRotation;
        }

        ConfigureBillboard(playerVisualRoot);
        ApplyBackgroundDepthStyling(resolved.FogColor);
    }

    private void ApplyTerrainDepthOfField()
    {
        if (battleCamera == null || battleVolume == null)
        {
            return;
        }

        Terrain terrain = FindFirstObjectByType<Terrain>(
            FindObjectsInactive.Include);
        if (terrain == null
            || !battleVolume.profile.TryGet(out DepthOfField depthOfField))
        {
            return;
        }

        float farDepth = ResolveTerrainFarDepth(battleCamera, terrain);
        depthOfField.gaussianEnd.Override(Mathf.Max(
            depthOfField.gaussianStart.value + 1f,
            farDepth));
    }

    internal static float ResolveTerrainFarDepth(
        Camera camera,
        Terrain terrain)
    {
        if (camera == null || terrain == null || terrain.terrainData == null)
        {
            return 0f;
        }

        Bounds localBounds = terrain.terrainData.bounds;
        Vector3 center = localBounds.center;
        Vector3 extents = localBounds.extents;
        float farDepth = camera.nearClipPlane;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = center + Vector3.Scale(
                        extents,
                        new Vector3(x, y, z));
                    Vector3 worldCorner = terrain.transform.TransformPoint(
                        localCorner);
                    float depth = Vector3.Dot(
                        camera.transform.forward,
                        worldCorner - camera.transform.position);
                    farDepth = Mathf.Max(farDepth, depth);
                }
            }
        }

        return farDepth;
    }

    private void CaptureAuthoredCameraState()
    {
        if (hasCapturedAuthoredCameraState || battleCamera == null)
        {
            return;
        }

        Transform cameraTransform = battleCamera.transform;
        authoredCameraLocalPosition = cameraTransform.localPosition;
        authoredCameraLocalRotation = cameraTransform.localRotation;
        authoredCameraOrthographic = battleCamera.orthographic;
        authoredCameraFieldOfView = battleCamera.fieldOfView;
        authoredCameraOrthographicSize = battleCamera.orthographicSize;
        authoredCameraNearClipPlane = battleCamera.nearClipPlane;
        authoredCameraFarClipPlane = battleCamera.farClipPlane;

        CinemachineFollow follow =
            battleCamera.GetComponent<CinemachineFollow>();
        authoredFollowOffset = follow != null
            ? follow.FollowOffset
            : Vector3.zero;

        CinemachineCamera cinemachineCamera =
            battleCamera.GetComponent<CinemachineCamera>();
        if (cinemachineCamera != null)
        {
            authoredCinemachineLens = cinemachineCamera.Lens;
            hasCapturedAuthoredCinemachineLens = true;
        }

        if (follow != null
            && cinemachineCamera != null
            && cinemachineCamera.Follow != null
            && follow.TrackerSettings.BindingMode
                == Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace)
        {
            authoredFollowOffset = cameraTransform.position
                - cinemachineCamera.Follow.position;
        }

        hasCapturedAuthoredCameraState = true;
    }

    private void RestoreAuthoredCameraState()
    {
        if (!hasCapturedAuthoredCameraState || battleCamera == null)
        {
            return;
        }

        battleCamera.transform.SetLocalPositionAndRotation(
            authoredCameraLocalPosition,
            authoredCameraLocalRotation);
        battleCamera.orthographic = authoredCameraOrthographic;
        battleCamera.fieldOfView = authoredCameraFieldOfView;
        battleCamera.orthographicSize = authoredCameraOrthographicSize;
        battleCamera.nearClipPlane = authoredCameraNearClipPlane;
        battleCamera.farClipPlane = authoredCameraFarClipPlane;

        CinemachineFollow follow =
            battleCamera.GetComponent<CinemachineFollow>();
        if (follow != null)
        {
            follow.FollowOffset = authoredFollowOffset;
        }

        CinemachineCamera cinemachineCamera =
            battleCamera.GetComponent<CinemachineCamera>();
        if (cinemachineCamera != null && hasCapturedAuthoredCinemachineLens)
        {
            cinemachineCamera.Lens = authoredCinemachineLens;
        }
    }

    private static void ApplyWorldRendering(BattleEnvironmentProfile profile)
    {
        RenderSettings.skybox = profile.SkyboxMaterial;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = profile.AmbientIntensity;
        RenderSettings.reflectionIntensity = profile.ReflectionIntensity;
        RenderSettings.fog = profile.FogEnabled;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = profile.FogColor;
        RenderSettings.fogDensity = profile.FogDensity;
        DynamicGI.UpdateEnvironment();
    }

    public void ConfigureBillboard(Transform visualRoot)
    {
        if (visualRoot == null)
        {
            return;
        }

        BattleSpriteBillboard billboard =
            visualRoot.GetComponent<BattleSpriteBillboard>();
        if (billboard == null)
        {
            billboard = visualRoot.gameObject.AddComponent<
                BattleSpriteBillboard>();
        }
        billboard.SetTargetCamera(battleCamera);
    }

    private void ApplyBackgroundDepthStyling(Color fogColor)
    {
        if (backgroundPropRoot == null || battleCamera == null)
        {
            return;
        }

        backgroundProperties ??= new MaterialPropertyBlock();

        foreach (MeshRenderer renderer in backgroundPropRoot
                     .GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            float distance = Vector3.Distance(
                battleCamera.transform.position,
                renderer.bounds.center);
            float distanceFactor = Mathf.InverseLerp(14f, 42f, distance);
            Color depthTint = Color.Lerp(
                Color.white,
                fogColor,
                distanceFactor * 0.38f);

            backgroundProperties.Clear();
            renderer.GetPropertyBlock(backgroundProperties);
            Material material = renderer.sharedMaterial;
            if (material.HasProperty(BaseColorId))
            {
                backgroundProperties.SetColor(
                    BaseColorId,
                    material.GetColor(BaseColorId) * depthTint);
            }
            if (material.HasProperty(ColorId))
            {
                backgroundProperties.SetColor(
                    ColorId,
                    material.GetColor(ColorId) * depthTint);
            }
            renderer.SetPropertyBlock(backgroundProperties);
        }
    }
}
