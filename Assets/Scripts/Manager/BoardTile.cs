using System.Collections.Generic;
using UnityEngine;

public class BoardTile : MonoBehaviour
{
    [SerializeField] private GameObject warningObject;
    private Mesh gridMesh;
    private Mesh warningMesh;
    private Material gridMaterial;
    private MeshRenderer warningRenderer;
    private MeshRenderer previewRenderer;
    private MaterialPropertyBlock previewProperties;
    private MaterialPropertyBlock warningProperties;
    private bool warningActive;
    private const float AfterglowDuration = 0.18f;
    private float afterglowRemaining;
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int WarningEffectId = Shader.PropertyToID("_WarningEffect");
    private static readonly int InsetId = Shader.PropertyToID("_Inset");

    private void Awake()
    {
        SetWarningActive(false);
    }

    public void SetWarningActive(bool isActive)
    {
        if (isActive)
        {
            afterglowRemaining = 0f;
            if (warningProperties != null)
            {
                warningProperties.SetFloat("_Afterglow", 0f);
                warningProperties.SetFloat("_Fade", 1f);
                warningRenderer.SetPropertyBlock(warningProperties);
            }
        }
        warningActive = isActive;
        if (warningObject != null)
        {
            warningObject.SetActive(isActive);
        }

        if (warningRenderer != null)
        {
            warningRenderer.enabled = isActive || afterglowRemaining > 0f;
        }
        UpdatePreviewInset();
    }

    internal void SetWarningUrgent(bool urgent)
    {
        if (warningRenderer == null || warningProperties == null)
        {
            return;
        }
        warningProperties.SetFloat("_Urgency", urgent ? 1f : 0f);
        warningRenderer.SetPropertyBlock(warningProperties);
    }

    internal void PlayWarningAfterglow()
    {
        if (warningActive || warningRenderer == null || !isActiveAndEnabled)
        {
            return;
        }
        afterglowRemaining = AfterglowDuration;
        warningProperties.SetFloat("_Afterglow", 1f);
        warningProperties.SetFloat("_Fade", 1f);
        warningRenderer.SetPropertyBlock(warningProperties);
        warningRenderer.enabled = true;
    }

    private void Update()
    {
        AdvanceWarningAfterglow(Time.deltaTime, GamePauseController.IsPaused);
    }

    internal void AdvanceWarningAfterglow(float deltaTime, bool paused)
    {
        if (afterglowRemaining <= 0f || paused)
        {
            return;
        }
        afterglowRemaining = Mathf.Max(0f, afterglowRemaining - Mathf.Max(0f, deltaTime));
        warningProperties.SetFloat("_Fade", afterglowRemaining / AfterglowDuration);
        warningRenderer.SetPropertyBlock(warningProperties);
        warningRenderer.enabled = afterglowRemaining > 0f;
    }

    private void OnDisable()
    {
        afterglowRemaining = 0f;
        SetWarningActive(false);
    }

    internal void SetWarningEmphasized(bool emphasized)
    {
        if (warningRenderer == null || warningProperties == null)
        {
            return;
        }
        warningProperties.SetFloat("_Focus", emphasized ? 1f : 0f);
        warningRenderer.SetPropertyBlock(warningProperties);
    }

    internal void SetPreviewColor(Color? color)
    {
        if (!color.HasValue)
        {
            if (previewRenderer != null)
            {
                previewRenderer.enabled = false;
            }
            return;
        }
        if (warningRenderer == null)
        {
            return;
        }
        if (previewRenderer == null)
        {
            previewRenderer = CreateRenderer("Grid Range Preview", warningMesh,
                warningRenderer.sortingLayerID, warningRenderer.sortingOrder + 1);
            previewProperties = new MaterialPropertyBlock();
            previewProperties.SetFloat(WarningEffectId, 1f);
        }
        previewProperties.SetColor(TintId, color.Value);
        previewRenderer.enabled = true;
        UpdatePreviewInset();
    }

    private void UpdatePreviewInset()
    {
        if (previewRenderer == null || previewProperties == null)
        {
            return;
        }
        // Preserve a red danger rim when a bullet preview overlaps an enemy warning.
        previewProperties.SetFloat(InsetId, warningActive ? 0.09f : 0f);
        previewRenderer.SetPropertyBlock(previewProperties);
    }

    internal void ConfigureGrid(float width, float height,
        bool outerLeft, bool outerRight, bool outerBottom, bool outerTop)
    {
        Shader shader = Resources.Load<Shader>("Shaders/BattleGrid");
        if (shader == null)
        {
            return;
        }

        int sortingLayer = 0;
        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sortingLayer = sprite.sortingLayerID;
            sprite.enabled = false;
        }

        gridMaterial = new Material(shader) { name = "Battle Grid (Runtime)" };
        float left = -width * 0.5f;
        float right = width * 0.5f;
        float bottom = -height * 0.5f;
        float top = height * 0.5f;
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        AddQuad(vertices, colors, triangles, left, bottom, right, top,
            new Color(0.78f, 0.83f, 0.88f, 0.035f));

        Color inner = new Color(0.72f, 0.79f, 0.84f, 0.28f);
        Color outer = new Color(0.84f, 0.89f, 0.93f, 0.65f);
        float innerWidth = Mathf.Min(width, height) * 0.018f;
        float outerWidth = innerWidth * 1.8f;

        // Shared edges are drawn once: right/top, plus exposed left/bottom.
        if (outerLeft)
        {
            AddQuad(vertices, colors, triangles, left, bottom,
                left + outerWidth, top, outer);
        }
        AddQuad(vertices, colors, triangles,
            right - (outerRight ? outerWidth : innerWidth), bottom, right, top,
            outerRight ? outer : inner);
        if (outerBottom)
        {
            AddQuad(vertices, colors, triangles, left, bottom,
                right, bottom + outerWidth, outer);
        }
        AddQuad(vertices, colors, triangles, left,
            top - (outerTop ? outerWidth : innerWidth), right, top,
            outerTop ? outer : inner);
        gridMesh = CreateMesh(vertices, colors, triangles, "Battle Grid");
        CreateRenderer("Grid", gridMesh, sortingLayer, -2);

        vertices.Clear();
        colors.Clear();
        triangles.Clear();
        // Keep the grid visible while the unlit fill stays red over bright backgrounds.
        float warningInset = innerWidth * 1.3f;
        AddQuad(vertices, colors, triangles,
            left + warningInset, bottom + warningInset,
            right - warningInset, top - warningInset,
            new Color(1f, 1f, 1f, 0.88f));
        warningMesh = CreateMesh(vertices, colors, triangles, "Battle Grid Warning");
        warningMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        warningRenderer = CreateRenderer("Grid Warning", warningMesh, sortingLayer, -1);
        warningProperties = new MaterialPropertyBlock();
        warningProperties.SetFloat(WarningEffectId, 1f);
        warningProperties.SetFloat("_Fade", 1f);
        warningProperties.SetColor(TintId, new Color(1f, 0.005f, 0.008f, 1f));
        warningRenderer.SetPropertyBlock(warningProperties);
        warningRenderer.enabled = warningActive;
    }

    private MeshRenderer CreateRenderer(string objectName, Mesh mesh,
        int sortingLayer, int sortingOrder)
    {
        GameObject view = new GameObject(objectName);
        view.transform.SetParent(transform, false);
        view.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = view.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = gridMaterial;
        renderer.sortingLayerID = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static void AddQuad(List<Vector3> vertices, List<Color> colors,
        List<int> triangles, float left, float bottom, float right, float top,
        Color color)
    {
        int start = vertices.Count;
        vertices.Add(new Vector3(left, bottom, 0f));
        vertices.Add(new Vector3(left, top, 0f));
        vertices.Add(new Vector3(right, top, 0f));
        vertices.Add(new Vector3(right, bottom, 0f));
        for (int i = 0; i < 4; i++)
        {
            colors.Add(color);
        }
        triangles.AddRange(new[] { start, start + 1, start + 2,
            start, start + 2, start + 3 });
    }

    private static Mesh CreateMesh(List<Vector3> vertices, List<Color> colors,
        List<int> triangles, string meshName)
    {
        Mesh mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        Release(gridMesh);
        Release(warningMesh);
        Release(gridMaterial);
    }

    private static void Release(Object resource)
    {
        if (resource == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(resource);
        }
        else
        {
            DestroyImmediate(resource);
        }
    }
}
