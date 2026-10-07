using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(
    fileName = "New Battle Environment",
    menuName = "Loaded/Battle Environment")]
public sealed class BattleEnvironmentProfile : ScriptableObject
{
    [Header("Board")]
    [SerializeField] private Vector3 boardLocalPosition =
        new Vector3(0f, 0.03f, 0f);
    [SerializeField] private Vector3 boardLocalEulerAngles =
        new Vector3(90f, 0f, 0f);

    [Header("Camera")]
    [SerializeField] private Vector3 cameraLocalPosition =
        new Vector3(0f, 9.3f, -12f);
    [SerializeField] private Vector3 cameraLocalEulerAngles =
        new Vector3(35f, 0f, 0f);
    [SerializeField] private bool usePerspective = true;
    [Range(1f, 179f)]
    [SerializeField] private float perspectiveFieldOfView = 40f;
    [Min(0.1f)]
    [SerializeField] private float orthographicSize = 5f;
    [SerializeField] private Color cameraBackgroundColor =
        new Color(0.055f, 0.065f, 0.075f, 1f);
    [SerializeField] private Vector3 cinemachineFollowOffset =
        new Vector3(0f, 9.3f, -12f);
    [Min(0)]
    [SerializeField] private int rendererIndex = 1;

    [Header("World Rendering")]
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private VolumeProfile volumeProfile;
    [Min(0f)]
    [SerializeField] private float ambientIntensity = 1.12f;
    [Min(0f)]
    [SerializeField] private float reflectionIntensity = 0.95f;
    [SerializeField] private bool fogEnabled = true;
    [SerializeField] private Color fogColor =
        new Color(0.22f, 0.29f, 0.40f, 1f);
    [Min(0f)]
    [SerializeField] private float fogDensity = 0.006f;

    [Header("Lighting")]
    [SerializeField] private Color directionalLightColor =
        new Color(1f, 0.92f, 0.80f, 1f);
    [Min(0f)]
    [SerializeField] private float directionalLightIntensity = 1.65f;
    [SerializeField] private Vector3 directionalLightEulerAngles =
        new Vector3(52f, -32f, 0f);

    public Vector3 BoardLocalPosition => boardLocalPosition;
    public Quaternion BoardLocalRotation =>
        Quaternion.Euler(boardLocalEulerAngles);
    public Vector3 CameraLocalPosition => cameraLocalPosition;
    public Quaternion CameraLocalRotation =>
        Quaternion.Euler(cameraLocalEulerAngles);
    public bool UsesPerspective => usePerspective;
    public float PerspectiveFieldOfView =>
        Mathf.Clamp(perspectiveFieldOfView, 1f, 179f);
    public float OrthographicSize => Mathf.Max(0.1f, orthographicSize);
    public Color CameraBackgroundColor => cameraBackgroundColor;
    public Vector3 CinemachineFollowOffset => cinemachineFollowOffset;
    public int RendererIndex => Mathf.Max(0, rendererIndex);
    public Material SkyboxMaterial => skyboxMaterial;
    public VolumeProfile VolumeProfile => volumeProfile;
    public float AmbientIntensity => Mathf.Max(0f, ambientIntensity);
    public float ReflectionIntensity => Mathf.Max(0f, reflectionIntensity);
    public bool FogEnabled => fogEnabled;
    public Color FogColor => fogColor;
    public float FogDensity => Mathf.Max(0f, fogDensity);
    public Color DirectionalLightColor => directionalLightColor;
    public float DirectionalLightIntensity =>
        Mathf.Max(0f, directionalLightIntensity);
    public Quaternion DirectionalLightRotation =>
        Quaternion.Euler(directionalLightEulerAngles);
}
