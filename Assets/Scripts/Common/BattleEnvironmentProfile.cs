using UnityEngine;

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
    [Min(0.1f)]
    [SerializeField] private float orthographicSize = 5f;
    [SerializeField] private Color cameraBackgroundColor =
        new Color(0.055f, 0.065f, 0.075f, 1f);
    [SerializeField] private Vector3 cinemachineFollowOffset =
        new Vector3(0f, 9.3f, -12f);
    [Min(0)]
    [SerializeField] private int rendererIndex = 1;

    [Header("Lighting")]
    [SerializeField] private Color directionalLightColor =
        new Color(1f, 0.86f, 0.68f, 1f);
    [Min(0f)]
    [SerializeField] private float directionalLightIntensity = 1.25f;
    [SerializeField] private Vector3 directionalLightEulerAngles =
        new Vector3(52f, -32f, 0f);

    public Vector3 BoardLocalPosition => boardLocalPosition;
    public Quaternion BoardLocalRotation =>
        Quaternion.Euler(boardLocalEulerAngles);
    public Vector3 CameraLocalPosition => cameraLocalPosition;
    public Quaternion CameraLocalRotation =>
        Quaternion.Euler(cameraLocalEulerAngles);
    public float OrthographicSize => Mathf.Max(0.1f, orthographicSize);
    public Color CameraBackgroundColor => cameraBackgroundColor;
    public Vector3 CinemachineFollowOffset => cinemachineFollowOffset;
    public int RendererIndex => Mathf.Max(0, rendererIndex);
    public Color DirectionalLightColor => directionalLightColor;
    public float DirectionalLightIntensity =>
        Mathf.Max(0f, directionalLightIntensity);
    public Quaternion DirectionalLightRotation =>
        Quaternion.Euler(directionalLightEulerAngles);
}
