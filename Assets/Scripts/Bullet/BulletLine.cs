using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class BulletLine : MonoBehaviour
{
    private static readonly int PrimaryColorId =
        Shader.PropertyToID("_PrimaryColor");
    private static readonly int SecondaryColorId =
        Shader.PropertyToID("_SecondaryColor");

    [SerializeField] private LineRenderer lineRenderer;
    [Min(0.01f)]
    [FormerlySerializedAs("lineDuration")]
    [SerializeField] private float fadeDuration = 0.1f;

    private MaterialPropertyBlock materialPropertyBlock;

    public BulletInstance Data { get; private set; }

    public bool Initialize(
        BulletInstance bulletData,
        Vector3 startPoint,
        Vector3 endPoint)
    {
        if (lineRenderer == null)
        {
            Debug.LogError("Line Renderer must be assigned in the Inspector.", this);
            return false;
        }

        if (bulletData == null)
        {
            Debug.LogError("Bullet Data is required to initialize the bullet line.", this);
            return false;
        }

        Data = bulletData;

        if (bulletData.LineMaterial != null)
        {
            lineRenderer.sharedMaterial = bulletData.LineMaterial;
        }

        lineRenderer.startColor = bulletData.PrimaryLineColor;
        lineRenderer.endColor = bulletData.PrimaryLineColor;
        ApplyLineColors(
            bulletData.PrimaryLineColor,
            bulletData.SecondaryLineColor);
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        StartCoroutine(FadeOut());
        return true;
    }

    private void ApplyLineColors(Color primaryColor, Color secondaryColor)
    {
        if (materialPropertyBlock == null)
        {
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        materialPropertyBlock.Clear();
        materialPropertyBlock.SetColor(PrimaryColorId, primaryColor);
        materialPropertyBlock.SetColor(SecondaryColorId, secondaryColor);
        lineRenderer.SetPropertyBlock(materialPropertyBlock);
    }

    private IEnumerator FadeOut()
    {
        Color startColor = lineRenderer.startColor;
        Color endColor = lineRenderer.endColor;
        float startAlpha = startColor.a;
        float endAlpha = endColor.a;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / fadeDuration);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            startColor.a = Mathf.Lerp(startAlpha, 0f, smoothProgress);
            endColor.a = Mathf.Lerp(endAlpha, 0f, smoothProgress);
            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
        }

        startColor.a = 0f;
        endColor.a = 0f;
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        Destroy(gameObject);
    }
}
