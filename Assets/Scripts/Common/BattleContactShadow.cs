using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(14000)]
[DisallowMultipleComponent]
public sealed class BattleContactShadow : MonoBehaviour
{
    private const string ShadowObjectName = "Silhouette Shadow | Runtime";
    private const string ShadowShaderName =
        "LOADED/Battle Sprite Ground Shadow";

    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // Kept only so existing prefab serialization remains compatible.
    [SerializeField, HideInInspector] private Vector2 worldSize =
        new Vector2(1f, 0.46f);
    [Range(0f, 1f)]
    [SerializeField] private float opacity = 0.34f;
    [Min(0f)]
    [SerializeField] private float surfaceOffset = 0.012f;
    [Min(0.1f)]
    [SerializeField] private float groundProbeDistance = 6f;
    [Range(0.25f, 2f)]
    [SerializeField] private float shadowLengthMultiplier = 0.9f;
    [Min(0f)]
    [SerializeField] private float groundingTolerance = 0.001f;

    private static Material sharedShadowMaterial;

    private readonly List<ShadowPart> shadowParts = new List<ShadowPart>();
    private MaterialPropertyBlock propertyBlock;

    private ActorMotion actorMotion;
    private BoardManager boardManager;
    private BattleSpriteBillboard billboard;
    private Transform visualRoot;
    private Transform shadowRoot;
    private Terrain activeTerrain;
    private TerrainCollider activeTerrainCollider;
    private Light directionalLight;
    private Vector3 cachedLocalFootAnchor;
    private bool hasCachedLocalFootAnchor;
    private Vector3 cachedLocalVisualCenter;
    private bool hasCachedLocalVisualCenter;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        actorMotion = GetComponent<ActorMotion>();
        boardManager = FindFirstObjectByType<BoardManager>();
        propertyBlock = new MaterialPropertyBlock();
        EnsureVisualRoot();
        RefreshPresentation();
    }

    private void LateUpdate()
    {
        if (!EnsureVisualRoot())
        {
            SetShadowRootActive(false);
            return;
        }

        if (actorMotion == null || !actorMotion.IsAnimating)
        {
            AlignFeetToTerrain();
        }

        UpdateSilhouetteShadow();
    }

    private void OnDisable()
    {
        SetShadowRootActive(false);
    }

    private void OnDestroy()
    {
        DestroyShadowParts();
    }

    private bool EnsureVisualRoot()
    {
        if (billboard != null && billboard.transform != null)
        {
            return EnsureShadowParts();
        }

        billboard = GetComponentInChildren<BattleSpriteBillboard>(true);
        Transform resolvedRoot = billboard != null
            ? billboard.transform
            : null;
        if (resolvedRoot == visualRoot)
        {
            return resolvedRoot != null && EnsureShadowParts();
        }

        visualRoot = resolvedRoot;
        hasCachedLocalFootAnchor = false;
        hasCachedLocalVisualCenter = false;
        DestroyShadowParts();
        return visualRoot != null && EnsureShadowParts();
    }

    private bool EnsureShadowParts()
    {
        if (visualRoot == null)
        {
            return false;
        }

        SpriteRenderer[] renderers =
            visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        bool requiresRebuild = renderers.Length != shadowParts.Count;
        if (!requiresRebuild)
        {
            for (int index = 0; index < renderers.Length; index++)
            {
                if (shadowParts[index].Source != renderers[index])
                {
                    requiresRebuild = true;
                    break;
                }
            }
        }

        if (!requiresRebuild)
        {
            return shadowParts.Count > 0;
        }

        DestroyShadowParts();
        if (renderers.Length == 0 || !EnsureShadowMaterial())
        {
            return false;
        }

        GameObject rootObject = new GameObject(ShadowObjectName);
        rootObject.hideFlags = HideFlags.HideInHierarchy
            | HideFlags.DontSaveInEditor
            | HideFlags.DontSaveInBuild;
        shadowRoot = rootObject.transform;
        shadowRoot.SetParent(transform, false);

        for (int index = 0; index < renderers.Length; index++)
        {
            shadowParts.Add(CreateShadowPart(renderers[index], index));
        }

        return shadowParts.Count > 0;
    }

    private static bool EnsureShadowMaterial()
    {
        if (sharedShadowMaterial != null)
        {
            return true;
        }

        Shader shader = Shader.Find(ShadowShaderName);
        if (shader == null)
        {
            return false;
        }

        sharedShadowMaterial = new Material(shader)
        {
            name = "Battle Sprite Ground Shadow Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return true;
    }

    private ShadowPart CreateShadowPart(SpriteRenderer source, int index)
    {
        GameObject partObject = new GameObject($"Shadow Part {index}");
        partObject.hideFlags = HideFlags.HideInHierarchy
            | HideFlags.DontSaveInEditor
            | HideFlags.DontSaveInBuild;
        Transform partTransform = partObject.transform;
        partTransform.SetParent(shadowRoot, false);

        MeshFilter meshFilter = partObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = partObject.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh
        {
            name = $"{source.name} Ground Shadow",
            hideFlags = HideFlags.HideAndDontSave
        };
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = sharedShadowMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.sortingOrder = -32000;

        return new ShadowPart(source, partObject, meshRenderer, mesh);
    }

    private void RefreshPresentation()
    {
        if (visualRoot == null || !EnsureShadowParts())
        {
            return;
        }

        AlignFeetToTerrain();
        UpdateSilhouetteShadow();
    }

    private void AlignFeetToTerrain()
    {
        if (!TryGetStableFootAnchor(out Vector3 footAnchor))
        {
            return;
        }

        Camera battleCamera = Camera.main;
        Vector3 gridCenter = transform.position;
        TryResolveGridCenter(out gridCenter);

        Vector3 targetFootPoint;
        if (!TryResolveProjectedTerrainAnchor(
                gridCenter,
                battleCamera,
                out targetFootPoint)
            && !TrySampleTerrain(gridCenter, out targetFootPoint))
        {
            return;
        }

        Vector3 correction = targetFootPoint - footAnchor;
        if (correction.sqrMagnitude
            > groundingTolerance * groundingTolerance)
        {
            visualRoot.position += correction;
        }

        AlignVisualCenterToGrid(gridCenter, battleCamera);
    }

    private void AlignVisualCenterToGrid(
        Vector3 gridCenter,
        Camera battleCamera)
    {
        if (!TryGetStableVisualCenter(out Vector3 visualCenter))
        {
            return;
        }

        Vector3 targetCenterLine = new Vector3(
            gridCenter.x,
            visualCenter.y,
            gridCenter.z);
        visualRoot.position += BattleCameraEffectSpace
            .ResolveHorizontalViewportCorrection(
                visualCenter,
                targetCenterLine,
                battleCamera);
    }

    private bool TryGetStableFootAnchor(out Vector3 footAnchor)
    {
        if (!hasCachedLocalFootAnchor)
        {
            if (!TryCalculateFootAnchor(out footAnchor))
            {
                return false;
            }

            cachedLocalFootAnchor = visualRoot.InverseTransformPoint(
                footAnchor);
            hasCachedLocalFootAnchor = true;
        }

        footAnchor = visualRoot.TransformPoint(cachedLocalFootAnchor);
        return true;
    }

    private bool TryCalculateFootAnchor(out Vector3 footAnchor)
    {
        if (TryCalculateFootAnchor(true, out footAnchor))
        {
            return true;
        }

        return TryCalculateFootAnchor(false, out footAnchor);
    }

    private bool TryCalculateFootAnchor(
        bool feetOnly,
        out Vector3 footAnchor)
    {
        footAnchor = default;
        Vector3 rendererAnchorSum = Vector3.zero;
        float lowestHeight = float.PositiveInfinity;
        int rendererAnchorCount = 0;

        foreach (ShadowPart part in shadowParts)
        {
            SpriteRenderer source = part.Source;
            if (source == null
                || !source.enabled
                || !source.gameObject.activeInHierarchy
                || (feetOnly && !IsFootRenderer(source))
                || !part.EnsureSpriteData())
            {
                continue;
            }

            Vector3 lowestVertexSum = Vector3.zero;
            float rendererLowestHeight = float.PositiveInfinity;
            int lowestVertexCount = 0;
            for (int index = 0; index < part.SpriteVertices.Length; index++)
            {
                Vector2 vertex = ResolveFlippedVertex(
                    part.SpriteVertices[index],
                    part.CachedSprite.bounds,
                    source.flipX,
                    source.flipY);
                Vector3 worldPoint = source.transform.TransformPoint(
                    new Vector3(vertex.x, vertex.y, 0f));
                if (worldPoint.y < rendererLowestHeight - 0.0001f)
                {
                    rendererLowestHeight = worldPoint.y;
                    lowestVertexSum = worldPoint;
                    lowestVertexCount = 1;
                }
                else if (Mathf.Abs(worldPoint.y - rendererLowestHeight)
                    <= 0.0001f)
                {
                    lowestVertexSum += worldPoint;
                    lowestVertexCount++;
                }
            }

            if (lowestVertexCount == 0)
            {
                continue;
            }

            rendererAnchorSum += lowestVertexSum / lowestVertexCount;
            lowestHeight = Mathf.Min(lowestHeight, rendererLowestHeight);
            rendererAnchorCount++;
        }

        if (rendererAnchorCount == 0)
        {
            return false;
        }

        footAnchor = rendererAnchorSum / rendererAnchorCount;
        footAnchor.y = lowestHeight;
        return true;
    }

    private bool TryGetStableVisualCenter(out Vector3 visualCenter)
    {
        if (!hasCachedLocalVisualCenter)
        {
            if (!TryCalculateVisualCenter(out visualCenter))
            {
                return false;
            }

            cachedLocalVisualCenter = visualRoot.InverseTransformPoint(
                visualCenter);
            hasCachedLocalVisualCenter = true;
        }

        visualCenter = visualRoot.TransformPoint(cachedLocalVisualCenter);
        return true;
    }

    private bool TryCalculateVisualCenter(out Vector3 visualCenter)
    {
        if (TryCalculateVisualCenter(true, out visualCenter))
        {
            return true;
        }

        return TryCalculateVisualCenter(false, out visualCenter);
    }

    private bool TryCalculateVisualCenter(
        bool excludeHeldEquipment,
        out Vector3 visualCenter)
    {
        visualCenter = default;
        Vector3 minimum = new Vector3(
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity);
        Vector3 maximum = new Vector3(
            float.NegativeInfinity,
            float.NegativeInfinity,
            float.NegativeInfinity);
        bool hasVertex = false;

        foreach (ShadowPart part in shadowParts)
        {
            SpriteRenderer source = part.Source;
            if (source == null
                || !source.enabled
                || !source.gameObject.activeInHierarchy
                || (excludeHeldEquipment && IsHeldEquipment(source))
                || !part.EnsureSpriteData())
            {
                continue;
            }

            for (int index = 0; index < part.SpriteVertices.Length; index++)
            {
                Vector2 vertex = ResolveFlippedVertex(
                    part.SpriteVertices[index],
                    part.CachedSprite.bounds,
                    source.flipX,
                    source.flipY);
                Vector3 worldPoint = source.transform.TransformPoint(
                    new Vector3(vertex.x, vertex.y, 0f));
                Vector3 localPoint = visualRoot.InverseTransformPoint(
                    worldPoint);
                minimum = Vector3.Min(minimum, localPoint);
                maximum = Vector3.Max(maximum, localPoint);
                hasVertex = true;
            }
        }

        if (!hasVertex)
        {
            return false;
        }

        visualCenter = visualRoot.TransformPoint((minimum + maximum) * 0.5f);
        return true;
    }

    private static bool IsFootRenderer(SpriteRenderer renderer)
    {
        return renderer.name.IndexOf(
            "Foot",
            StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsHeldEquipment(SpriteRenderer renderer)
    {
        return renderer.name.IndexOf(
                "Weapon",
                StringComparison.OrdinalIgnoreCase) >= 0
            || renderer.name.IndexOf(
                "Revolver",
                StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal bool TryResolveEffectAnchor(
        Camera battleCamera,
        out Vector3 effectAnchor)
    {
        effectAnchor = transform.position;
        if (!EnsureVisualRoot())
        {
            return false;
        }

        if ((actorMotion == null || !actorMotion.IsAnimating)
            && Application.isPlaying)
        {
            AlignFeetToTerrain();
        }

        if (!TryGetStableVisualCenter(out effectAnchor))
        {
            return false;
        }

        if (battleCamera == null)
        {
            return true;
        }

        Vector3 viewportPoint = battleCamera.WorldToViewportPoint(
            effectAnchor);
        Vector3 gridCenter = transform.position;
        TryResolveGridCenter(out gridCenter);
        Vector3 targetCenterLine = new Vector3(
            gridCenter.x,
            effectAnchor.y,
            gridCenter.z);
        Vector3 targetViewport = battleCamera.WorldToViewportPoint(
            targetCenterLine);
        if (viewportPoint.z <= 0f || targetViewport.z <= 0f)
        {
            return true;
        }

        viewportPoint.x = targetViewport.x;
        effectAnchor = battleCamera.ViewportToWorldPoint(viewportPoint);
        return true;
    }

    private bool TryResolveGridCenter(out Vector3 gridCenter)
    {
        gridCenter = transform.position;
        boardManager ??= FindFirstObjectByType<BoardManager>();
        if (boardManager == null)
        {
            return false;
        }

        float nearestPlanarDistance = float.PositiveInfinity;
        bool found = false;
        for (int laneIndex = 0;
             laneIndex < boardManager.LaneCount;
             laneIndex++)
        {
            if (!boardManager.TryGetTileIndex(
                    transform.position,
                    laneIndex,
                    out int tileIndex)
                || !boardManager.TryGetTilePosition(
                    tileIndex,
                    laneIndex,
                    out Vector3 candidate))
            {
                continue;
            }

            Vector2 planarDelta = new Vector2(
                candidate.x - transform.position.x,
                candidate.z - transform.position.z);
            float planarDistance = planarDelta.sqrMagnitude;
            if (planarDistance >= nearestPlanarDistance)
            {
                continue;
            }

            nearestPlanarDistance = planarDistance;
            gridCenter = candidate;
            found = true;
        }

        return found;
    }

    private bool TryResolveProjectedTerrainAnchor(
        Vector3 gridCenter,
        Camera battleCamera,
        out Vector3 terrainAnchor)
    {
        terrainAnchor = gridCenter;
        if (battleCamera == null
            || !ResolveTerrain(gridCenter))
        {
            return false;
        }

        Vector3 viewportPoint = battleCamera.WorldToViewportPoint(gridCenter);
        if (viewportPoint.z <= 0f || !IsFinite(viewportPoint))
        {
            return false;
        }

        Ray viewportRay = battleCamera.ViewportPointToRay(viewportPoint);
        if (activeTerrainCollider != null
            && activeTerrainCollider.Raycast(
                viewportRay,
                out RaycastHit hit,
                Mathf.Max(
                    battleCamera.farClipPlane,
                    groundProbeDistance)))
        {
            terrainAnchor = hit.point;
            return true;
        }

        float terrainHeight = activeTerrain.SampleHeight(gridCenter)
            + activeTerrain.GetPosition().y;
        for (int iteration = 0; iteration < 4; iteration++)
        {
            if (!TryResolveProjectedGroundPoint(
                    battleCamera,
                    gridCenter,
                    terrainHeight,
                    out terrainAnchor))
            {
                return false;
            }

            terrainHeight = activeTerrain.SampleHeight(terrainAnchor)
                + activeTerrain.GetPosition().y;
        }

        terrainAnchor.y = terrainHeight;
        return true;
    }

    private void UpdateSilhouetteShadow()
    {
        if (shadowRoot == null || !ResolveTerrain(transform.position))
        {
            SetShadowRootActive(false);
            return;
        }

        SetShadowRootActive(true);
        Vector3 projectionDirection = ResolveProjectionDirection();
        Matrix4x4 worldToShadow = shadowRoot.worldToLocalMatrix;

        foreach (ShadowPart part in shadowParts)
        {
            SpriteRenderer source = part.Source;
            bool visible = source != null
                && source.enabled
                && source.gameObject.activeInHierarchy
                && source.color.a > 0.001f
                && part.EnsureSpriteData();
            part.Renderer.enabled = visible;
            if (!visible)
            {
                continue;
            }

            for (int index = 0; index < part.SpriteVertices.Length; index++)
            {
                Vector2 vertex = ResolveFlippedVertex(
                    part.SpriteVertices[index],
                    part.CachedSprite.bounds,
                    source.flipX,
                    source.flipY);
                Vector3 worldVertex = source.transform.TransformPoint(
                    new Vector3(vertex.x, vertex.y, 0f));
                if (!TryProjectToTerrain(
                        worldVertex,
                        projectionDirection,
                        out Vector3 projectedPoint))
                {
                    projectedPoint = worldVertex;
                    if (TrySampleTerrain(worldVertex, out Vector3 groundPoint))
                    {
                        projectedPoint.y = groundPoint.y + surfaceOffset;
                    }
                }

                part.ProjectedVertices[index] =
                    worldToShadow.MultiplyPoint3x4(projectedPoint);
            }

            part.Mesh.vertices = part.ProjectedVertices;
            part.Mesh.uv = part.SpriteUvs;
            part.Mesh.triangles = part.Triangles;
            part.Mesh.RecalculateBounds();

            propertyBlock.Clear();
            propertyBlock.SetTexture(
                MainTextureId,
                part.CachedSprite.texture);
            propertyBlock.SetColor(
                ColorId,
                new Color(
                    0.018f,
                    0.014f,
                    0.012f,
                    opacity * source.color.a));
            part.Renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private Vector3 ResolveProjectionDirection()
    {
        directionalLight ??= RenderSettings.sun;
        Vector3 direction = directionalLight != null
            ? directionalLight.transform.forward
            : Vector3.down;
        if (direction.y >= -0.01f)
        {
            return Vector3.down;
        }

        direction.x *= shadowLengthMultiplier;
        direction.z *= shadowLengthMultiplier;
        return direction.normalized;
    }

    private bool TryProjectToTerrain(
        Vector3 worldPoint,
        Vector3 projectionDirection,
        out Vector3 projectedPoint)
    {
        projectedPoint = default;
        if (!ResolveTerrain(worldPoint))
        {
            return false;
        }

        if (activeTerrainCollider != null)
        {
            Ray ray = new Ray(
                worldPoint - projectionDirection * 0.1f,
                projectionDirection);
            if (activeTerrainCollider.Raycast(
                    ray,
                    out RaycastHit hit,
                    groundProbeDistance + 12f))
            {
                projectedPoint = hit.point + Vector3.up * surfaceOffset;
                return true;
            }
        }

        if (Mathf.Abs(projectionDirection.y) <= 0.0001f)
        {
            return false;
        }

        Vector3 candidate = worldPoint;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            float terrainHeight = activeTerrain.SampleHeight(candidate)
                + activeTerrain.GetPosition().y;
            float distance = (terrainHeight - worldPoint.y)
                / projectionDirection.y;
            candidate = worldPoint + projectionDirection * distance;
        }

        candidate.y = activeTerrain.SampleHeight(candidate)
            + activeTerrain.GetPosition().y
            + surfaceOffset;
        projectedPoint = candidate;
        return true;
    }

    private bool TrySampleTerrain(
        Vector3 worldPoint,
        out Vector3 groundPoint)
    {
        groundPoint = worldPoint;
        if (!ResolveTerrain(worldPoint))
        {
            return false;
        }

        groundPoint.y = activeTerrain.SampleHeight(worldPoint)
            + activeTerrain.GetPosition().y;
        return true;
    }

    private bool ResolveTerrain(Vector3 worldPoint)
    {
        if (ContainsWorldPoint(activeTerrain, worldPoint))
        {
            return true;
        }

        activeTerrain = null;
        activeTerrainCollider = null;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (!ContainsWorldPoint(terrain, worldPoint))
            {
                continue;
            }

            activeTerrain = terrain;
            activeTerrainCollider = terrain.GetComponent<TerrainCollider>();
            return true;
        }

        return false;
    }

    private static bool ContainsWorldPoint(
        Terrain terrain,
        Vector3 worldPoint)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            return false;
        }

        Vector3 position = terrain.GetPosition();
        Vector3 size = terrain.terrainData.size;
        return worldPoint.x >= position.x
            && worldPoint.x <= position.x + size.x
            && worldPoint.z >= position.z
            && worldPoint.z <= position.z + size.z;
    }

    private void SetShadowRootActive(bool active)
    {
        if (shadowRoot != null && shadowRoot.gameObject.activeSelf != active)
        {
            shadowRoot.gameObject.SetActive(active);
        }
    }

    private void DestroyShadowParts()
    {
        foreach (ShadowPart part in shadowParts)
        {
            part.Destroy();
        }
        shadowParts.Clear();

        if (shadowRoot != null)
        {
            Destroy(shadowRoot.gameObject);
            shadowRoot = null;
        }
    }

    internal static float ResolveGroundingCorrection(
        float footHeight,
        float terrainHeight)
    {
        return terrainHeight - footHeight;
    }

    internal static Vector3 ResolveGridGroundingCorrection(
        Vector3 footAnchor,
        Vector3 gridCenter,
        float terrainHeight)
    {
        return new Vector3(
            gridCenter.x - footAnchor.x,
            terrainHeight - footAnchor.y,
            gridCenter.z - footAnchor.z);
    }

    internal static bool TryResolveProjectedGroundPoint(
        Camera battleCamera,
        Vector3 projectedTarget,
        float groundHeight,
        out Vector3 groundPoint)
    {
        groundPoint = projectedTarget;
        if (battleCamera == null)
        {
            return false;
        }

        Vector3 viewportPoint = battleCamera.WorldToViewportPoint(
            projectedTarget);
        if (viewportPoint.z <= 0f || !IsFinite(viewportPoint))
        {
            return false;
        }

        Ray viewportRay = battleCamera.ViewportPointToRay(viewportPoint);
        Plane groundPlane = new Plane(
            Vector3.up,
            new Vector3(0f, groundHeight, 0f));
        if (!groundPlane.Raycast(viewportRay, out float distance))
        {
            return false;
        }

        groundPoint = viewportRay.GetPoint(distance);
        return IsFinite(groundPoint);
    }

    internal static Vector2 ResolveFlippedVertex(
        Vector2 vertex,
        Bounds spriteBounds,
        bool flipX,
        bool flipY)
    {
        if (flipX)
        {
            vertex.x = spriteBounds.center.x * 2f - vertex.x;
        }
        if (flipY)
        {
            vertex.y = spriteBounds.center.y * 2f - vertex.y;
        }
        return vertex;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsNaN(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x)
            && !float.IsInfinity(value.y)
            && !float.IsInfinity(value.z);
    }

    private sealed class ShadowPart
    {
        public ShadowPart(
            SpriteRenderer source,
            GameObject gameObject,
            MeshRenderer renderer,
            Mesh mesh)
        {
            Source = source;
            GameObject = gameObject;
            Renderer = renderer;
            Mesh = mesh;
        }

        public SpriteRenderer Source { get; }
        public GameObject GameObject { get; }
        public MeshRenderer Renderer { get; }
        public Mesh Mesh { get; }
        public Sprite CachedSprite { get; private set; }
        public Vector2[] SpriteVertices { get; private set; }
        public Vector2[] SpriteUvs { get; private set; }
        public Vector3[] ProjectedVertices { get; private set; }
        public int[] Triangles { get; private set; }

        public bool EnsureSpriteData()
        {
            Sprite sprite = Source != null ? Source.sprite : null;
            if (sprite == null)
            {
                return false;
            }
            if (sprite == CachedSprite)
            {
                return true;
            }

            CachedSprite = sprite;
            // Animation frames can use different topology. Discard the old
            // indices and UVs before assigning a smaller vertex array.
            Mesh.Clear();
            SpriteVertices = sprite.vertices;
            SpriteUvs = sprite.uv;
            ProjectedVertices = new Vector3[SpriteVertices.Length];
            ushort[] sourceTriangles = sprite.triangles;
            Triangles = new int[sourceTriangles.Length];
            for (int index = 0; index < sourceTriangles.Length; index++)
            {
                Triangles[index] = sourceTriangles[index];
            }
            return true;
        }

        public void Destroy()
        {
            if (Mesh != null)
            {
                UnityEngine.Object.Destroy(Mesh);
            }
            if (GameObject != null)
            {
                UnityEngine.Object.Destroy(GameObject);
            }
        }
    }
}
