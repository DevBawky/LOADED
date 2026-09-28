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

    public BattleEnvironmentProfile ActiveProfile { get; private set; }

    private void Awake()
    {
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
            battleCamera.transform.SetLocalPositionAndRotation(
                resolved.CameraLocalPosition,
                resolved.CameraLocalRotation);
            ApplyCameraProjection(battleCamera, resolved);
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
        }

        if (battleVolume != null && resolved.VolumeProfile != null)
        {
            battleVolume.sharedProfile = resolved.VolumeProfile;
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
    }

    private static void ApplyCameraProjection(
        Camera camera,
        BattleEnvironmentProfile profile)
    {
        camera.orthographic = !profile.UsesPerspective;
        camera.fieldOfView = profile.PerspectiveFieldOfView;
        camera.orthographicSize = profile.OrthographicSize;

        CinemachineCamera cinemachineCamera =
            camera.GetComponent<CinemachineCamera>();
        if (cinemachineCamera == null)
        {
            return;
        }

        LensSettings lens = cinemachineCamera.Lens;
        lens.ModeOverride = profile.UsesPerspective
            ? LensSettings.OverrideModes.Perspective
            : LensSettings.OverrideModes.Orthographic;
        lens.FieldOfView = profile.PerspectiveFieldOfView;
        lens.OrthographicSize = profile.OrthographicSize;
        cinemachineCamera.Lens = lens;
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
}
