using UnityEngine;

[DefaultExecutionOrder(13000)]
[DisallowMultipleComponent]
public sealed class BattleSpriteBillboard : MonoBehaviour
{
    private const string BattleMaterialResourcePath =
        "Battle/BattleLitSprite";

    [SerializeField] private Camera targetCamera;

    private static Material battleLitSpriteMaterial;

    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        ApplyBattleLighting();
        FaceCamera();
    }

    private void ApplyBattleLighting()
    {
        battleLitSpriteMaterial ??= Resources.Load<Material>(
            BattleMaterialResourcePath);
        if (battleLitSpriteMaterial == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in GetComponentsInChildren<
                     SpriteRenderer>(true))
        {
            if (renderer != null
                && UsesDefaultSpriteShader(renderer.sharedMaterial))
            {
                renderer.sharedMaterial = battleLitSpriteMaterial;
            }
        }
    }

    internal static bool UsesDefaultSpriteShader(Material material)
    {
        string shaderName = material != null && material.shader != null
            ? material.shader.name
            : string.Empty;
        return string.IsNullOrEmpty(shaderName)
            || shaderName == "Sprites/Default"
            || shaderName == "Universal Render Pipeline/2D/Sprite-Lit-Default"
            || shaderName == "Universal Render Pipeline/2D/Sprite-Unlit-Default";
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
