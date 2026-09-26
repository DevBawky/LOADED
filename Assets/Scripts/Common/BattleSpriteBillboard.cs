using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleSpriteBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        FaceCamera();
    }

    private void LateUpdate()
    {
        FaceCamera();
    }

    private void FaceCamera()
    {
        targetCamera ??= Camera.main;

        FaceTransform(transform, targetCamera);
    }

    internal static void FaceTransform(
        Transform target,
        Camera camera = null,
        float zRotation = 0f)
    {
        if (target == null)
        {
            return;
        }

        target.rotation = GetFacingRotation(camera ?? Camera.main, zRotation);
    }

    internal static Quaternion GetFacingRotation(
        Camera camera,
        float zRotation = 0f)
    {
        Quaternion roll = Quaternion.Euler(0f, 0f, zRotation);

        if (camera == null)
        {
            return roll;
        }

        return Quaternion.LookRotation(
            camera.transform.forward,
            camera.transform.up) * roll;
    }
}
