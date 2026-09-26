using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class BattleWorld3DController : MonoBehaviour
{
    [SerializeField] private BattleEnvironmentProfile defaultProfile;
    [SerializeField] private Transform boardRoot;
    [SerializeField] private Camera battleCamera;
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
            battleCamera.orthographic = true;
            battleCamera.orthographicSize = resolved.OrthographicSize;
            battleCamera.backgroundColor = resolved.CameraBackgroundColor;

            UniversalAdditionalCameraData cameraData =
                battleCamera.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(resolved.RendererIndex);
            cameraData.renderShadows = true;
            cameraData.requiresDepthTexture = true;
        }

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
