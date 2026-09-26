using System;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class Battle3DSetupBuilder
{
    private const string ScenePath = "Assets/Scenes/Battle.unity";
    private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
    private const string RendererPath = "Assets/Settings/Battle3DRenderer.asset";
    private const string TerrainDataPath = "Assets/Terrain/BattleTerrain.asset";
    private const string TerrainLayerPath =
        "Assets/Terrain/BattleGround.terrainlayer";
    private const string TerrainTexturePath =
        "Assets/Terrain/BattleGroundTexture.asset";
    private const string TerrainMaterialPath =
        "Assets/Materials/Battle3DTerrain.mat";
    private const string ProjectileMaterialPath =
        "Assets/Materials/DefaultSphereProjectile.mat";
    private const string ProjectilePrefabPath =
        "Assets/Prefabs/Bullet/DefaultSphereProjectile.prefab";
    private const string ProjectileProfilePath =
        "Assets/Resources/ProjectileVisuals/DefaultProjectileVisual.asset";
    private const string EnvironmentProfilePath =
        "Assets/Resources/Battle/DefaultBattleEnvironment.asset";

    private const string EnvironmentRootName = "##--ENVIRONMENT--##";
    private const string BoardRootName = "##--BOARDS--##";
    private const string BackgroundRootName = "##--BACKGROUNDS--##";
    private const string TerrainObjectName = "Terrain | Battle Ground";
    private const string DirectionalLightName = "Directional Light | Battle 3D";

    [MenuItem("Tools/LOADED/Apply Battle 3D Prototype")]
    public static void Apply()
    {
        EnsureFolder("Assets", "Terrain");
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Prefabs", "Bullet");
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "ProjectileVisuals");
        EnsureFolder("Assets/Resources", "Battle");

        int rendererIndex = EnsureBattleRenderer();
        Material terrainMaterial = EnsureTerrainMaterial();
        Material projectileMaterial = EnsureProjectileMaterial();
        BulletProjectileView projectilePrefab = EnsureProjectilePrefab(
            projectileMaterial);
        ProjectileVisualProfile projectileProfile =
            EnsureProjectileProfile(projectilePrefab);
        BattleEnvironmentProfile environmentProfile =
            EnsureEnvironmentProfile(rendererIndex);
        Texture2D terrainTexture = EnsureTerrainTexture();
        TerrainLayer terrainLayer = EnsureTerrainLayer(terrainTexture);
        TerrainData terrainData = EnsureTerrainData(terrainLayer);

        AssignProjectileProfileToBullets(projectileProfile);
        AssignEnvironmentProfileToBattles(environmentProfile);
        ApplyScene(
            terrainData,
            terrainMaterial,
            environmentProfile,
            rendererIndex);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Applied the Battle 3D prototype setup.");
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    private static int EnsureBattleRenderer()
    {
        UniversalRenderPipelineAsset pipeline =
            AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                PipelinePath);
        if (pipeline == null)
        {
            throw new InvalidOperationException(
                $"Universal Render Pipeline asset not found: {PipelinePath}");
        }

        UniversalRendererData renderer =
            AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                RendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "Battle3DRenderer";
            AssetDatabase.CreateAsset(renderer, RendererPath);
        }

        SerializedObject serializedPipeline = new SerializedObject(pipeline);
        SerializedProperty rendererList = serializedPipeline.FindProperty(
            "m_RendererDataList");
        if (rendererList == null)
        {
            throw new InvalidOperationException(
                "URP renderer list could not be located.");
        }

        int existingIndex = -1;
        int emptyIndex = -1;

        for (int index = 0; index < rendererList.arraySize; index++)
        {
            UnityEngine.Object entry = rendererList
                .GetArrayElementAtIndex(index)
                .objectReferenceValue;
            if (entry == renderer)
            {
                existingIndex = index;
            }
            else if (index > 0 && entry == null && emptyIndex < 0)
            {
                emptyIndex = index;
            }
        }

        if (existingIndex >= 0)
        {
            if (emptyIndex >= 0 && emptyIndex < existingIndex)
            {
                rendererList.GetArrayElementAtIndex(emptyIndex)
                    .objectReferenceValue = renderer;
                rendererList.GetArrayElementAtIndex(existingIndex)
                    .objectReferenceValue = null;

                if (existingIndex == rendererList.arraySize - 1)
                {
                    rendererList.DeleteArrayElementAtIndex(existingIndex);
                }

                serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
                return emptyIndex;
            }

            return existingIndex;
        }

        if (emptyIndex >= 0)
        {
            rendererList.GetArrayElementAtIndex(emptyIndex)
                .objectReferenceValue = renderer;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return emptyIndex;
        }

        int rendererIndex = rendererList.arraySize;
        rendererList.InsertArrayElementAtIndex(rendererIndex);
        rendererList.GetArrayElementAtIndex(rendererIndex)
            .objectReferenceValue = renderer;
        serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        return rendererIndex;
    }

    private static Material EnsureTerrainMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (shader == null)
        {
            throw new InvalidOperationException(
                "URP Terrain/Lit shader is unavailable.");
        }

        Material material = GetOrCreateMaterial(TerrainMaterialPath, shader);
        SetColorIfPresent(
            material,
            "_BaseColor",
            new Color(0.17f, 0.20f, 0.13f, 1f));
        SetColorIfPresent(
            material,
            "_Color",
            new Color(0.17f, 0.20f, 0.13f, 1f));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureProjectileMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            throw new InvalidOperationException("URP Lit shader is unavailable.");
        }

        Material material = GetOrCreateMaterial(
            ProjectileMaterialPath,
            shader);
        Color baseColor = new Color(0.88f, 0.91f, 0.95f, 1f);
        SetColorIfPresent(material, "_BaseColor", baseColor);
        SetColorIfPresent(material, "_EmissionColor", baseColor * 1.8f);
        SetFloatIfPresent(material, "_Smoothness", 0.72f);
        material.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static BulletProjectileView EnsureProjectilePrefab(
        Material projectileMaterial)
    {
        GameObject temporary = GameObject.CreatePrimitive(
            PrimitiveType.Sphere);
        temporary.name = "DefaultSphereProjectile";

        Collider collider = temporary.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        MeshRenderer meshRenderer = temporary.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = projectileMaterial;
        temporary.transform.localScale = Vector3.one;
        temporary.AddComponent<BulletProjectileView>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
            temporary,
            ProjectilePrefabPath);
        UnityEngine.Object.DestroyImmediate(temporary);

        BulletProjectileView view = prefab != null
            ? prefab.GetComponent<BulletProjectileView>()
            : null;
        if (view == null)
        {
            throw new InvalidOperationException(
                "Default Sphere Projectile prefab could not be created.");
        }

        return view;
    }

    private static ProjectileVisualProfile EnsureProjectileProfile(
        BulletProjectileView projectilePrefab)
    {
        ProjectileVisualProfile profile =
            AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                ProjectileProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<ProjectileVisualProfile>();
            profile.name = "DefaultProjectileVisual";
            AssetDatabase.CreateAsset(profile, ProjectileProfilePath);
        }

        SetObjectReference(profile, "projectilePrefab", projectilePrefab);
        return profile;
    }

    private static BattleEnvironmentProfile EnsureEnvironmentProfile(
        int rendererIndex)
    {
        BattleEnvironmentProfile profile =
            AssetDatabase.LoadAssetAtPath<BattleEnvironmentProfile>(
                EnvironmentProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<
                BattleEnvironmentProfile>();
            profile.name = "DefaultBattleEnvironment";
            AssetDatabase.CreateAsset(profile, EnvironmentProfilePath);
        }

        SerializedObject serializedProfile = new SerializedObject(profile);
        serializedProfile.FindProperty("boardLocalEulerAngles").vector3Value =
            new Vector3(90f, 0f, 0f);
        serializedProfile.FindProperty("cameraLocalPosition").vector3Value =
            new Vector3(0f, 9.3f, -12f);
        serializedProfile.FindProperty("cameraLocalEulerAngles").vector3Value =
            new Vector3(35f, 0f, 0f);
        serializedProfile.FindProperty("cinemachineFollowOffset").vector3Value =
            new Vector3(0f, 9.3f, -12f);
        serializedProfile.FindProperty("orthographicSize").floatValue = 5f;
        serializedProfile.FindProperty("rendererIndex").intValue =
            rendererIndex;
        serializedProfile.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static Texture2D EnsureTerrainTexture()
    {
        const int textureSize = 32;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            TerrainTexturePath);
        if (texture != null)
        {
            return texture;
        }

        texture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false)
        {
            name = "BattleGroundTexture"
        };

        Color baseColor = new Color(0.23f, 0.26f, 0.17f, 1f);
        Color[] pixels = new Color[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.17f, y * 0.17f);
                pixels[y * textureSize + x] = baseColor
                    * Mathf.Lerp(0.88f, 1.08f, noise);
                pixels[y * textureSize + x].a = 1f;
            }
        }

        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply(false, false);
        AssetDatabase.CreateAsset(texture, TerrainTexturePath);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static TerrainLayer EnsureTerrainLayer(Texture2D terrainTexture)
    {
        TerrainLayer terrainLayer =
            AssetDatabase.LoadAssetAtPath<TerrainLayer>(TerrainLayerPath);
        if (terrainLayer != null)
        {
            return terrainLayer;
        }

        terrainLayer = new TerrainLayer
        {
            name = "BattleGround"
        };

        terrainLayer.diffuseTexture = terrainTexture;
        terrainLayer.tileSize = new Vector2(8f, 8f);
        terrainLayer.diffuseRemapMin = Vector4.zero;
        terrainLayer.diffuseRemapMax = new Vector4(
            0.17f,
            0.20f,
            0.13f,
            1f);
        terrainLayer.smoothness = 0.08f;
        AssetDatabase.CreateAsset(terrainLayer, TerrainLayerPath);
        EditorUtility.SetDirty(terrainLayer);
        return terrainLayer;
    }

    private static TerrainData EnsureTerrainData(TerrainLayer terrainLayer)
    {
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(
            TerrainDataPath);
        if (terrainData != null)
        {
            return terrainData;
        }

        terrainData = new TerrainData
        {
            name = "BattleTerrain",
            heightmapResolution = 33
        };
        AssetDatabase.CreateAsset(terrainData, TerrainDataPath);

        terrainData.size = new Vector3(36f, 2f, 14f);
        int resolution = terrainData.heightmapResolution;
        terrainData.SetHeights(0, 0, new float[resolution, resolution]);
        terrainData.alphamapResolution = 32;
        terrainData.terrainLayers = new[] { terrainLayer };
        float[,,] blend = new float[
            terrainData.alphamapHeight,
            terrainData.alphamapWidth,
            1];
        for (int y = 0; y < terrainData.alphamapHeight; y++)
        {
            for (int x = 0; x < terrainData.alphamapWidth; x++)
            {
                blend[y, x, 0] = 1f;
            }
        }

        terrainData.SetAlphamaps(0, 0, blend);
        EditorUtility.SetDirty(terrainData);
        return terrainData;
    }

    private static void AssignProjectileProfileToBullets(
        ProjectileVisualProfile profile)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BulletData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BulletData data = AssetDatabase.LoadAssetAtPath<BulletData>(path);
            SetObjectReference(data, "projectileVisual", profile);
        }
    }

    private static void AssignEnvironmentProfileToBattles(
        BattleEnvironmentProfile profile)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BattleData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BattleData data = AssetDatabase.LoadAssetAtPath<BattleData>(path);
            SetObjectReference(data, "environmentProfile", profile);
        }
    }

    private static void ApplyScene(
        TerrainData terrainData,
        Material terrainMaterial,
        BattleEnvironmentProfile profile,
        int rendererIndex)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
        }

        GameObject environmentRoot = FindSceneObject(
            scene,
            EnvironmentRootName);
        GameObject boardRoot = FindSceneObject(scene, BoardRootName);
        GameObject backgroundRoot = FindSceneObject(
            scene,
            BackgroundRootName);
        GameObject cameraObject = FindSceneObject(scene, "Main Camera");
        GameObject playerObject = FindSceneObject(scene, "Player");
        GameObject gameplayCanvasObject = FindRootObject(scene, "Canvas");
        GameObject gameStartCanvasObject = FindRootObject(
            scene,
            "Canvas | Game Start");

        if (environmentRoot == null || boardRoot == null
            || cameraObject == null || playerObject == null)
        {
            throw new InvalidOperationException(
                "Battle scene is missing a required root object.");
        }

        bool backgroundWasActive = backgroundRoot != null
            && backgroundRoot.activeSelf;

        Transform playerVisual = playerObject.transform.Find("Avatar");
        if (playerVisual == null)
        {
            throw new InvalidOperationException(
                "Player/Avatar could not be located.");
        }

        GameObject terrainObject = FindDescendant(
            environmentRoot.transform,
            TerrainObjectName);
        bool createdTerrain = terrainObject == null;
        if (terrainObject == null)
        {
            terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = TerrainObjectName;
            terrainObject.transform.SetParent(
                environmentRoot.transform,
                false);
        }

        Terrain terrain = terrainObject.GetComponent<Terrain>();
        if (terrain == null)
        {
            throw new InvalidOperationException(
                "Battle Ground does not have a Terrain component.");
        }
        if (createdTerrain)
        {
            terrainObject.transform.localPosition =
                new Vector3(-18f, -0.12f, -7f);
            terrainObject.transform.localRotation = Quaternion.identity;
            terrainObject.transform.localScale = Vector3.one;
            terrain.terrainData = terrainData;
            terrain.materialTemplate = terrainMaterial;
            terrain.drawInstanced = true;
        }
        else
        {
            terrain.terrainData ??= terrainData;
            terrain.materialTemplate ??= terrainMaterial;
        }

        GameObject lightObject = FindDescendant(
            environmentRoot.transform,
            DirectionalLightName);
        if (lightObject == null)
        {
            lightObject = new GameObject(DirectionalLightName);
            lightObject.transform.SetParent(environmentRoot.transform, false);
        }

        Light directionalLight = lightObject.GetComponent<Light>();
        if (directionalLight == null)
        {
            directionalLight = lightObject.AddComponent<Light>();
        }
        directionalLight.type = LightType.Directional;
        directionalLight.shadows = LightShadows.Hard;

        Camera battleCamera = cameraObject.GetComponent<Camera>();
        if (battleCamera == null)
        {
            throw new InvalidOperationException(
                "Main Camera does not have a Camera component.");
        }

        ApplyCamera(
            cameraObject,
            battleCamera,
            profile,
            rendererIndex,
            playerObject.transform);
        ConfigureOverlayCanvas(gameplayCanvasObject, 0);
        ConfigureOverlayCanvas(gameStartCanvasObject, 5);

        boardRoot.transform.SetLocalPositionAndRotation(
            profile.BoardLocalPosition,
            profile.BoardLocalRotation);

        BattleSpriteBillboard playerBillboard =
            playerVisual.GetComponent<BattleSpriteBillboard>();
        if (playerBillboard == null)
        {
            playerBillboard =
                playerVisual.gameObject.AddComponent<BattleSpriteBillboard>();
        }
        SetObjectReference(playerBillboard, "targetCamera", battleCamera);

        BattleWorld3DController controller =
            environmentRoot.GetComponent<BattleWorld3DController>();
        if (controller == null)
        {
            controller =
                environmentRoot.AddComponent<BattleWorld3DController>();
        }
        SetObjectReference(controller, "defaultProfile", profile);
        SetObjectReference(controller, "boardRoot", boardRoot.transform);
        SetObjectReference(controller, "battleCamera", battleCamera);
        SetObjectReference(controller, "directionalLight", directionalLight);
        SetObjectReference(controller, "playerVisualRoot", playerVisual);

        StateManager stateManager = UnityEngine.Object.FindFirstObjectByType<
            StateManager>(FindObjectsInactive.Include);
        if (stateManager != null)
        {
            SetObjectReference(
                stateManager,
                "battleWorld3DController",
                controller);
        }

        if (backgroundRoot != null
            && backgroundRoot.activeSelf != backgroundWasActive)
        {
            backgroundRoot.SetActive(backgroundWasActive);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ApplyCamera(
        GameObject cameraObject,
        Camera battleCamera,
        BattleEnvironmentProfile profile,
        int rendererIndex,
        Transform followTarget)
    {
        cameraObject.transform.SetLocalPositionAndRotation(
            profile.CameraLocalPosition,
            profile.CameraLocalRotation);
        battleCamera.orthographic = true;
        battleCamera.orthographicSize = profile.OrthographicSize;
        battleCamera.backgroundColor = profile.CameraBackgroundColor;

        UniversalAdditionalCameraData cameraData =
            battleCamera.GetUniversalAdditionalCameraData();
        cameraData.SetRenderer(rendererIndex);
        cameraData.renderShadows = true;
        cameraData.requiresDepthTexture = true;

        CinemachineFollow follow =
            cameraObject.GetComponent<CinemachineFollow>();
        if (follow != null)
        {
            follow.FollowOffset = profile.CinemachineFollowOffset;
        }

        CinemachineCamera cinemachineCamera =
            cameraObject.GetComponent<CinemachineCamera>();
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = followTarget;
            LensSettings lens = cinemachineCamera.Lens;
            lens.OrthographicSize = profile.OrthographicSize;
            cinemachineCamera.Lens = lens;
        }

        CinemachineRotationComposer rotationComposer =
            cameraObject.GetComponent<CinemachineRotationComposer>();
        if (rotationComposer != null)
        {
            UnityEngine.Object.DestroyImmediate(rotationComposer);
        }

        OrthographicCameraRectConfiner confiner =
            cameraObject.GetComponent<OrthographicCameraRectConfiner>();
        if (confiner != null)
        {
            confiner.enabled = false;
        }
    }

    private static Material GetOrCreateMaterial(string path, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        return material;
    }

    private static void ConfigureOverlayCanvas(
        GameObject canvasObject,
        int sortingOrder)
    {
        if (canvasObject == null)
        {
            return;
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = false;
        canvas.sortingOrder = sortingOrder;
        EditorUtility.SetDirty(canvas);
    }

    private static void SetColorIfPresent(
        Material material,
        string propertyName,
        Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetFloatIfPresent(
        Material material,
        string propertyName,
        float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedTarget = new SerializedObject(target);
        SerializedProperty property = serializedTarget.FindProperty(
            propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"{target.name} has no serialized property '{propertyName}'.");
        }

        property.objectReferenceValue = value;
        serializedTarget.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }

            GameObject descendant = FindDescendant(root.transform, objectName);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }
        }

        return null;
    }

    private static GameObject FindDescendant(
        Transform root,
        string objectName)
    {
        foreach (Transform child in root)
        {
            if (child.name == objectName)
            {
                return child.gameObject;
            }

            GameObject descendant = FindDescendant(child, objectName);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
