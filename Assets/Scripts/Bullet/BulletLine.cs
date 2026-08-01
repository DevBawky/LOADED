using System.Collections;
using System.Collections.Generic;
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

    [Header("Layered Trail")]
    [SerializeField] private bool useLayeredTrail = true;
    [Range(0.1f, 1f)]
    [SerializeField] private float coreWidthMultiplier = 0.32f;
    [Range(1f, 4f)]
    [SerializeField] private float glowWidthMultiplier = 1.9f;
    [Range(0f, 1f)]
    [SerializeField] private float glowAlpha = 0.28f;

    private MaterialPropertyBlock materialPropertyBlock;
    private readonly List<LineRenderer> trailLayers = new List<LineRenderer>();

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
        BuildTrailLayers(
            bulletData,
            startPoint,
            endPoint);

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

    private void BuildTrailLayers(
        BulletInstance bulletData,
        Vector3 startPoint,
        Vector3 endPoint)
    {
        trailLayers.Clear();
        trailLayers.Add(lineRenderer);

        if (!useLayeredTrail)
        {
            return;
        }

        Color coreColor = Color.Lerp(
            Color.white,
            bulletData.PrimaryLineColor,
            0.18f);
        coreColor.a = 1f;
        CreateTrailLayer(
            "White Hot Core",
            startPoint,
            endPoint,
            coreColor,
            Color.white,
            coreWidthMultiplier,
            lineRenderer.sortingOrder + 1);

        Color glowColor = bulletData.SecondaryLineColor;
        glowColor.a = glowAlpha;
        Color glowSecondary = bulletData.PrimaryLineColor;
        glowSecondary.a = glowAlpha;
        CreateTrailLayer(
            "Colored Glow",
            startPoint,
            endPoint,
            glowColor,
            glowSecondary,
            glowWidthMultiplier,
            lineRenderer.sortingOrder - 1);
    }

    private void CreateTrailLayer(
        string layerName,
        Vector3 startPoint,
        Vector3 endPoint,
        Color primaryColor,
        Color secondaryColor,
        float widthMultiplier,
        int sortingOrder)
    {
        GameObject layerObject = new GameObject(
            layerName,
            typeof(LineRenderer));
        layerObject.transform.SetParent(transform, false);
        LineRenderer layer = layerObject.GetComponent<LineRenderer>();
        layer.sharedMaterial = lineRenderer.sharedMaterial;
        layer.useWorldSpace = true;
        layer.textureMode = lineRenderer.textureMode;
        layer.alignment = lineRenderer.alignment;
        layer.numCapVertices = lineRenderer.numCapVertices;
        layer.numCornerVertices = lineRenderer.numCornerVertices;
        layer.widthCurve = lineRenderer.widthCurve;
        layer.widthMultiplier = lineRenderer.widthMultiplier * widthMultiplier;
        layer.sortingLayerID = lineRenderer.sortingLayerID;
        layer.sortingOrder = sortingOrder;
        layer.startColor = primaryColor;
        layer.endColor = primaryColor;
        layer.positionCount = 2;
        layer.SetPosition(0, startPoint);
        layer.SetPosition(1, endPoint);

        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetColor(PrimaryColorId, primaryColor);
        propertyBlock.SetColor(SecondaryColorId, secondaryColor);
        layer.SetPropertyBlock(propertyBlock);
        trailLayers.Add(layer);
    }

    private IEnumerator FadeOut()
    {
        Color[] startColors = new Color[trailLayers.Count];
        Color[] endColors = new Color[trailLayers.Count];

        for (int layerIndex = 0;
             layerIndex < trailLayers.Count;
             layerIndex++)
        {
            LineRenderer layer = trailLayers[layerIndex];
            startColors[layerIndex] = layer.startColor;
            endColors[layerIndex] = layer.endColor;
        }

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
            for (int layerIndex = 0;
                 layerIndex < trailLayers.Count;
                 layerIndex++)
            {
                LineRenderer layer = trailLayers[layerIndex];

                if (layer == null)
                {
                    continue;
                }

                Color startColor = startColors[layerIndex];
                Color endColor = endColors[layerIndex];
                startColor.a *= 1f - smoothProgress;
                endColor.a *= 1f - smoothProgress;
                layer.startColor = startColor;
                layer.endColor = endColor;
            }
        }

        foreach (LineRenderer layer in trailLayers)
        {
            if (layer == null)
            {
                continue;
            }

            Color startColor = layer.startColor;
            Color endColor = layer.endColor;
            startColor.a = 0f;
            endColor.a = 0f;
            layer.startColor = startColor;
            layer.endColor = endColor;
        }

        Destroy(gameObject);
    }
}
