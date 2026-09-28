using System;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class Battle3DSetupBuilder
{
    private const string ScenePath = "Assets/Scenes/Battle.unity";
    private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
    private const string RendererPath = "Assets/Settings/Battle3DRenderer.asset";
    private const string LegacyRendererPath =
        "Assets/Settings/Renderer2D.asset";
    private const string CombatImpactRendererFeatureName =
        "Kill Impact Fullscreen";
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
    private const string SkyboxMaterialPath =
        "Assets/Materials/Battle3DSkybox.mat";
    private const string BattleVolumeProfilePath =
        "Assets/Settings/BattleEnvironmentVolume.asset";
    private const string LegacyVolumeProfilePath =
        "Assets/Scenes/SampleScene/Main Camera Profile.asset";

    private const string EnvironmentRootName = "##--ENVIRONMENT--##";
    private const string BoardRootName = "##--BOARDS--##";
    private const string BackgroundRootName = "##--BACKGROUNDS--##";
    private const string TerrainObjectName = "Terrain | Battle Ground";
    private const string DirectionalLightName = "Directional Light | Battle 3D";
    private const string FillLightName = "Directional Light | Cool Fill";
    private const string LightingRootName = "Lighting | Battle";
    private const string ReflectionProbeName = "Reflection Probe | Battle Arena";
    private const string LightProbeGroupName = "Light Probes | Battle Arena";
    private const string PropsRootName = "Props | Battle";

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
        Material skyboxMaterial = EnsureSkyboxMaterial();
        VolumeProfile volumeProfile = EnsureBattleVolumeProfile();
        Material projectileMaterial = EnsureProjectileMaterial();
        BulletProjectileView projectilePrefab = EnsureProjectilePrefab(
            projectileMaterial);
        ProjectileVisualProfile projectileProfile =
            EnsureProjectileProfile(projectilePrefab);
        BattleEnvironmentProfile environmentProfile =
            EnsureEnvironmentProfile(
                rendererIndex,
                skyboxMaterial,
                volumeProfile);
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

    [MenuItem("Tools/LOADED/Repair Battle 3D Impact Rendering")]
    public static void RepairBattleRendererPresentation()
    {
        UniversalRendererData renderer =
            AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                RendererPath);
        if (renderer == null)
        {
            throw new InvalidOperationException(
                $"Battle renderer not found: {RendererPath}");
        }

        EnsureBattleRendererPresentation(renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            RendererPath,
            ImportAssetOptions.ForceUpdate);
        Debug.Log("Repaired Battle 3D combat impact rendering.");
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

        EnsureBattleRendererPresentation(renderer);

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

    private static void EnsureBattleRendererPresentation(
        UniversalRendererData renderer)
    {
        ScriptableRendererData legacyRenderer =
            AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(
                LegacyRendererPath);
        if (legacyRenderer == null)
        {
            throw new InvalidOperationException(
                $"Legacy renderer not found: {LegacyRendererPath}");
        }

        ScriptableRendererFeature sourceFeature = null;
        foreach (ScriptableRendererFeature feature in
                 legacyRenderer.rendererFeatures)
        {
            if (feature != null
                && feature.name == CombatImpactRendererFeatureName)
            {
                sourceFeature = feature;
                break;
            }
        }

        if (sourceFeature == null)
        {
            throw new InvalidOperationException(
                $"Renderer feature not found: {CombatImpactRendererFeatureName}");
        }

        ScriptableRendererFeature targetFeature = null;
        foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
        {
            if (feature != null
                && feature.name == CombatImpactRendererFeatureName
                && feature.GetType() == sourceFeature.GetType())
            {
                targetFeature = feature;
                break;
            }
        }

        if (targetFeature == null)
        {
            targetFeature = UnityEngine.Object.Instantiate(sourceFeature);
            targetFeature.name = CombatImpactRendererFeatureName;
            targetFeature.hideFlags = sourceFeature.hideFlags;
            AssetDatabase.AddObjectToAsset(targetFeature, renderer);
            renderer.rendererFeatures.Add(targetFeature);
        }

        EditorUtility.CopySerialized(sourceFeature, targetFeature);
        targetFeature.name = CombatImpactRendererFeatureName;
        targetFeature.hideFlags = sourceFeature.hideFlags;
        EditorUtility.SetDirty(targetFeature);
        EnsureBattleAmbientOcclusion(renderer);

        SerializedObject serializedLegacy = new SerializedObject(
            legacyRenderer);
        SerializedProperty sourcePostProcessData =
            serializedLegacy.FindProperty("m_PostProcessData");
        if (sourcePostProcessData == null
            || sourcePostProcessData.objectReferenceValue == null)
        {
            throw new InvalidOperationException(
                "Legacy renderer PostProcessData could not be located.");
        }

        SerializedObject serializedRenderer = new SerializedObject(renderer);
        SerializedProperty targetPostProcessData =
            serializedRenderer.FindProperty("postProcessData")
            ?? serializedRenderer.FindProperty("m_PostProcessData");
        SerializedProperty serializedFeatures =
            serializedRenderer.FindProperty("m_RendererFeatures");
        SerializedProperty serializedFeatureMap =
            serializedRenderer.FindProperty("m_RendererFeatureMap");
        if (targetPostProcessData == null
            || serializedFeatures == null
            || serializedFeatureMap == null)
        {
            throw new InvalidOperationException(
                "Battle renderer presentation properties could not be located.");
        }

        targetPostProcessData.objectReferenceValue =
            sourcePostProcessData.objectReferenceValue;
        serializedFeatures.arraySize = renderer.rendererFeatures.Count;
        serializedFeatureMap.arraySize = renderer.rendererFeatures.Count;
        for (int index = 0;
             index < renderer.rendererFeatures.Count;
             index++)
        {
            ScriptableRendererFeature feature =
                renderer.rendererFeatures[index];
            serializedFeatures.GetArrayElementAtIndex(index)
                .objectReferenceValue = feature;

            if (feature == null)
            {
                serializedFeatureMap.GetArrayElementAtIndex(index)
                    .longValue = 0L;
                continue;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    feature,
                    out _,
                    out long localId))
            {
                throw new InvalidOperationException(
                    $"Renderer feature local ID could not be resolved: {feature.name}");
            }

            serializedFeatureMap.GetArrayElementAtIndex(index)
                .longValue = localId;
        }

        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        renderer.SetDirty();
        EditorUtility.SetDirty(renderer);
    }

    private static void EnsureBattleAmbientOcclusion(
        UniversalRendererData renderer)
    {
        ScreenSpaceAmbientOcclusion ambientOcclusion = null;
        foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
        {
            if (feature is ScreenSpaceAmbientOcclusion existing)
            {
                ambientOcclusion = existing;
                break;
            }
        }

        if (ambientOcclusion == null)
        {
            ambientOcclusion = ScriptableObject.CreateInstance<
                ScreenSpaceAmbientOcclusion>();
            ambientOcclusion.name = "Screen Space Ambient Occlusion";
            AssetDatabase.AddObjectToAsset(ambientOcclusion, renderer);
            renderer.rendererFeatures.Add(ambientOcclusion);
        }

        SerializedObject serializedFeature = new SerializedObject(
            ambientOcclusion);
        SerializedProperty settings = serializedFeature.FindProperty(
            "m_Settings");
        if (settings == null)
        {
            throw new InvalidOperationException(
                "URP ambient occlusion settings could not be located.");
        }

        settings.FindPropertyRelative("AOMethod").intValue = 0;
        settings.FindPropertyRelative("Downsample").boolValue = false;
        settings.FindPropertyRelative("AfterOpaque").boolValue = false;
        settings.FindPropertyRelative("Source").intValue = 1;
        settings.FindPropertyRelative("NormalSamples").intValue = 2;
        settings.FindPropertyRelative("Intensity").floatValue = 1.35f;
        settings.FindPropertyRelative("DirectLightingStrength").floatValue =
            0.22f;
        settings.FindPropertyRelative("Radius").floatValue = 0.08f;
        settings.FindPropertyRelative("Samples").intValue = 0;
        settings.FindPropertyRelative("BlurQuality").intValue = 0;
        settings.FindPropertyRelative("Falloff").floatValue = 80f;
        serializedFeature.ApplyModifiedPropertiesWithoutUndo();
        ambientOcclusion.Create();
        EditorUtility.SetDirty(ambientOcclusion);
    }

    private static Material EnsureTerrainMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (shader == null)
        {
            throw new InvalidOperationException(
                "URP Terrain/Lit shader is unavailable.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            TerrainMaterialPath);
        bool created = material == null;
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, TerrainMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        Color legacyTint = new Color(0.17f, 0.20f, 0.13f, 1f);
        bool usesLegacyTint = material.HasProperty("_BaseColor")
            && Approximately(material.GetColor("_BaseColor"), legacyTint);
        if (created || usesLegacyTint)
        {
            SetColorIfPresent(material, "_BaseColor", Color.white);
            SetColorIfPresent(material, "_Color", Color.white);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureSkyboxMaterial()
    {
        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader == null)
        {
            throw new InvalidOperationException(
                "Procedural Skybox shader is unavailable.");
        }

        Material material = GetOrCreateMaterial(SkyboxMaterialPath, shader);
        SetFloatIfPresent(material, "_SunDisk", 2f);
        SetFloatIfPresent(material, "_SunSize", 0.035f);
        SetFloatIfPresent(material, "_SunSizeConvergence", 5f);
        SetFloatIfPresent(material, "_AtmosphereThickness", 1.05f);
        SetColorIfPresent(
            material,
            "_SkyTint",
            new Color(0.32f, 0.42f, 0.58f, 1f));
        SetColorIfPresent(
            material,
            "_GroundColor",
            new Color(0.12f, 0.09f, 0.07f, 1f));
        SetFloatIfPresent(material, "_Exposure", 1.12f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static VolumeProfile EnsureBattleVolumeProfile()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
            BattleVolumeProfilePath);
        if (profile == null)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                    LegacyVolumeProfilePath) != null)
            {
                AssetDatabase.CopyAsset(
                    LegacyVolumeProfilePath,
                    BattleVolumeProfilePath);
                profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                    BattleVolumeProfilePath);
            }
            else
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, BattleVolumeProfilePath);
            }
        }

        profile.name = "BattleEnvironmentVolume";

        Bloom bloom = GetOrAddVolumeOverride<Bloom>(profile);
        bloom.threshold.Override(1.05f);
        bloom.intensity.Override(0.28f);
        bloom.scatter.Override(0.62f);
        bloom.clamp.Override(16f);
        bloom.highQualityFiltering.Override(true);

        Tonemapping tonemapping = GetOrAddVolumeOverride<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);

        ColorAdjustments color =
            GetOrAddVolumeOverride<ColorAdjustments>(profile);
        color.postExposure.Override(0.08f);
        color.contrast.Override(9f);
        color.colorFilter.Override(new Color(1f, 0.97f, 0.92f, 1f));
        color.saturation.Override(-4f);

        WhiteBalance whiteBalance =
            GetOrAddVolumeOverride<WhiteBalance>(profile);
        whiteBalance.temperature.Override(3f);
        whiteBalance.tint.Override(1f);

        Vignette vignette = GetOrAddVolumeOverride<Vignette>(profile);
        vignette.color.Override(new Color(0.025f, 0.03f, 0.045f, 1f));
        vignette.center.Override(new Vector2(0.5f, 0.5f));
        vignette.intensity.Override(0.14f);
        vignette.smoothness.Override(0.55f);
        vignette.rounded.Override(false);

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static T GetOrAddVolumeOverride<T>(VolumeProfile profile)
        where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>(true);

            if (AssetDatabase.Contains(profile)
                && !AssetDatabase.Contains(component))
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }
        }

        component.active = true;
        EditorUtility.SetDirty(component);
        return component;
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
        int rendererIndex,
        Material skyboxMaterial,
        VolumeProfile volumeProfile)
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
        serializedProfile.FindProperty("usePerspective").boolValue = true;
        serializedProfile.FindProperty("perspectiveFieldOfView").floatValue =
            40f;
        serializedProfile.FindProperty("orthographicSize").floatValue = 5f;
        serializedProfile.FindProperty("rendererIndex").intValue =
            rendererIndex;
        serializedProfile.FindProperty("skyboxMaterial")
            .objectReferenceValue = skyboxMaterial;
        serializedProfile.FindProperty("volumeProfile")
            .objectReferenceValue = volumeProfile;
        serializedProfile.FindProperty("ambientIntensity").floatValue = 0.82f;
        serializedProfile.FindProperty("reflectionIntensity").floatValue =
            0.78f;
        serializedProfile.FindProperty("fogEnabled").boolValue = true;
        serializedProfile.FindProperty("fogColor").colorValue =
            new Color(0.12f, 0.16f, 0.22f, 1f);
        serializedProfile.FindProperty("fogDensity").floatValue = 0.012f;
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
            if (texture.width == textureSize
                && texture.height == textureSize
                && CalculateAverageLuminance(texture) < 0.3f)
            {
                PaintTerrainTexture(texture);
                EditorUtility.SetDirty(texture);
            }
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

        PaintTerrainTexture(texture);
        AssetDatabase.CreateAsset(texture, TerrainTexturePath);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static void PaintTerrainTexture(Texture2D texture)
    {
        Color baseColor = new Color(0.42f, 0.39f, 0.26f, 1f);
        int textureSize = texture.width;
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
    }

    private static float CalculateAverageLuminance(Texture2D texture)
    {
        Color[] pixels = texture.GetPixels();
        if (pixels.Length == 0)
        {
            return 1f;
        }

        float total = 0f;
        foreach (Color pixel in pixels)
        {
            total += pixel.grayscale;
        }
        return total / pixels.Length;
    }

    private static TerrainLayer EnsureTerrainLayer(Texture2D terrainTexture)
    {
        TerrainLayer terrainLayer =
            AssetDatabase.LoadAssetAtPath<TerrainLayer>(TerrainLayerPath);
        if (terrainLayer != null)
        {
            Vector4 legacyRemap = new Vector4(
                0.17f,
                0.20f,
                0.13f,
                1f);
            if (Approximately(terrainLayer.diffuseRemapMax, legacyRemap))
            {
                terrainLayer.diffuseRemapMax = new Vector4(
                    0.82f,
                    0.88f,
                    0.68f,
                    1f);
                terrainLayer.smoothness = 0.1f;
                EditorUtility.SetDirty(terrainLayer);
            }
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
            0.82f,
            0.88f,
            0.68f,
            1f);
        terrainLayer.smoothness = 0.1f;
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
        directionalLight.color = profile.DirectionalLightColor;
        directionalLight.intensity = profile.DirectionalLightIntensity;
        directionalLight.transform.rotation = profile.DirectionalLightRotation;
        directionalLight.shadows = LightShadows.Soft;
        directionalLight.shadowStrength = 0.9f;
        directionalLight.shadowBias = 0.04f;
        directionalLight.shadowNormalBias = 0.35f;
        directionalLight.GetUniversalAdditionalLightData();
        RenderSettings.sun = directionalLight;

        EnsureEnvironmentAuthoringHierarchy(environmentRoot.transform);
        ApplyRenderSettings(profile);

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
            playerObject.transform,
            profile.VolumeProfile);
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
        SetObjectReference(
            controller,
            "battleVolume",
            battleCamera.GetComponent<Volume>());
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
        Transform followTarget,
        VolumeProfile volumeProfile)
    {
        cameraObject.transform.SetLocalPositionAndRotation(
            profile.CameraLocalPosition,
            profile.CameraLocalRotation);
        battleCamera.orthographic = !profile.UsesPerspective;
        battleCamera.fieldOfView = profile.PerspectiveFieldOfView;
        battleCamera.orthographicSize = profile.OrthographicSize;
        battleCamera.backgroundColor = profile.CameraBackgroundColor;
        battleCamera.clearFlags = CameraClearFlags.Skybox;
        battleCamera.allowHDR = true;
        battleCamera.allowMSAA = false;

        UniversalAdditionalCameraData cameraData =
            battleCamera.GetUniversalAdditionalCameraData();
        cameraData.SetRenderer(rendererIndex);
        cameraData.renderShadows = true;
        cameraData.renderPostProcessing = true;
        cameraData.requiresDepthTexture = true;
        cameraData.requiresColorTexture = true;
        cameraData.antialiasing = AntialiasingMode
            .SubpixelMorphologicalAntiAliasing;
        cameraData.antialiasingQuality = AntialiasingQuality.High;
        cameraData.dithering = true;
        cameraData.stopNaN = true;

        Volume volume = cameraObject.GetComponent<Volume>();
        if (volume == null)
        {
            volume = cameraObject.AddComponent<Volume>();
        }
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.weight = 1f;
        volume.sharedProfile = volumeProfile;

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
            lens.ModeOverride = profile.UsesPerspective
                ? LensSettings.OverrideModes.Perspective
                : LensSettings.OverrideModes.Orthographic;
            lens.FieldOfView = profile.PerspectiveFieldOfView;
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

    private static void EnsureEnvironmentAuthoringHierarchy(Transform parent)
    {
        Transform lightingRoot = EnsureChild(parent, LightingRootName);
        EnsureFillLight(lightingRoot);
        EnsureReflectionProbe(lightingRoot);
        EnsureLightProbes(lightingRoot);

        Transform propsRoot = EnsureChild(parent, PropsRootName);
        EnsureChild(propsRoot, "Architecture");
        EnsureChild(propsRoot, "Ground Detail");
        EnsureChild(propsRoot, "Background");
        EnsureChild(propsRoot, "Foreground");
        EnsureChild(propsRoot, "Environment FX");
    }

    private static void EnsureFillLight(Transform parent)
    {
        GameObject lightObject = FindDescendant(parent, FillLightName);
        bool created = lightObject == null;
        if (lightObject == null)
        {
            lightObject = new GameObject(FillLightName);
            lightObject.transform.SetParent(parent, false);
        }

        Light fillLight = lightObject.GetComponent<Light>();
        if (fillLight == null)
        {
            fillLight = lightObject.AddComponent<Light>();
        }

        if (!created)
        {
            return;
        }

        fillLight.type = LightType.Directional;
        fillLight.color = new Color(0.38f, 0.52f, 0.78f, 1f);
        fillLight.intensity = 0.24f;
        fillLight.shadows = LightShadows.None;
        lightObject.transform.localRotation = Quaternion.Euler(38f, 145f, 0f);
        fillLight.GetUniversalAdditionalLightData();
    }

    private static void EnsureReflectionProbe(Transform parent)
    {
        GameObject probeObject = FindDescendant(parent, ReflectionProbeName);
        bool created = probeObject == null;
        if (probeObject == null)
        {
            probeObject = new GameObject(ReflectionProbeName);
            probeObject.transform.SetParent(parent, false);
        }

        ReflectionProbe probe = probeObject.GetComponent<ReflectionProbe>();
        if (probe == null)
        {
            probe = probeObject.AddComponent<ReflectionProbe>();
        }

        if (!created)
        {
            return;
        }

        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.resolution = 256;
        probe.hdr = true;
        probe.boxProjection = true;
        probe.intensity = 0.8f;
        probe.blendDistance = 3f;
        probe.size = new Vector3(34f, 10f, 16f);
        probe.center = new Vector3(0f, 4f, 0f);
    }

    private static void EnsureLightProbes(Transform parent)
    {
        GameObject probeObject = FindDescendant(parent, LightProbeGroupName);
        bool created = probeObject == null;
        if (probeObject == null)
        {
            probeObject = new GameObject(LightProbeGroupName);
            probeObject.transform.SetParent(parent, false);
        }

        LightProbeGroup group = probeObject.GetComponent<LightProbeGroup>();
        if (group == null)
        {
            group = probeObject.AddComponent<LightProbeGroup>();
        }

        if (!created)
        {
            return;
        }

        Vector3[] positions = new Vector3[18];
        int index = 0;
        foreach (float height in new[] { 0.8f, 3.5f })
        {
            foreach (float z in new[] { -4f, 0f, 4f })
            {
                foreach (float x in new[] { -10f, 0f, 10f })
                {
                    positions[index++] = new Vector3(x, height, z);
                }
            }
        }
        group.probePositions = positions;
    }

    private static Transform EnsureChild(Transform parent, string objectName)
    {
        GameObject existing = FindDescendant(parent, objectName);
        if (existing != null)
        {
            return existing.transform;
        }

        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static void ApplyRenderSettings(BattleEnvironmentProfile profile)
    {
        RenderSettings.skybox = profile.SkyboxMaterial;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = profile.AmbientIntensity;
        RenderSettings.reflectionIntensity = profile.ReflectionIntensity;
        RenderSettings.fog = profile.FogEnabled;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = profile.FogColor;
        RenderSettings.fogDensity = profile.FogDensity;
        DynamicGI.UpdateEnvironment();
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

    private static bool Approximately(Color left, Color right)
    {
        return Mathf.Abs(left.r - right.r) < 0.0001f
            && Mathf.Abs(left.g - right.g) < 0.0001f
            && Mathf.Abs(left.b - right.b) < 0.0001f
            && Mathf.Abs(left.a - right.a) < 0.0001f;
    }

    private static bool Approximately(Vector4 left, Vector4 right)
    {
        return (left - right).sqrMagnitude < 0.0000001f;
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
