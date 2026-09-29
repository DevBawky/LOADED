using System;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using VolFx;

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
    private const string TerrainNormalPath =
        "Assets/Terrain/BattleGroundNormal.asset";
    private const string TerrainDirtTexturePath =
        "Assets/Terrain/BattleGroundDirtTexture.asset";
    private const string TerrainDirtNormalPath =
        "Assets/Terrain/BattleGroundDirtNormal.asset";
    private const string TerrainDirtLayerPath =
        "Assets/Terrain/BattleGroundDirt.terrainlayer";
    private const string TerrainRockTexturePath =
        "Assets/Terrain/BattleGroundRockTexture.asset";
    private const string TerrainRockNormalPath =
        "Assets/Terrain/BattleGroundRockNormal.asset";
    private const string TerrainRockLayerPath =
        "Assets/Terrain/BattleGroundRock.terrainlayer";
    private const string TerrainMaterialPath =
        "Assets/Materials/Battle3DTerrain.mat";
    private const string BattleLitSpriteMaterialPath =
        "Assets/Resources/Battle/BattleLitSprite.mat";
    private const string BattleSunFlarePath =
        "Assets/Resources/Battle/BattleSunFlare.asset";
    private const string BattleLightingSettingsPath =
        "Assets/Settings/BattleLightingSettings.lighting";
    private const string ProjectileMaterialPath =
        "Assets/Materials/DefaultSphereProjectile.mat";
    private const string ProjectilePrefabPath =
        "Assets/Prefabs/Bullet/DefaultSphereProjectile.prefab";
    private const string ProjectileProfilePath =
        "Assets/Resources/ProjectileVisuals/DefaultProjectileVisual.asset";
    private const string PlayerPrefabPath =
        "Assets/Prefabs/Player/Player.prefab";
    private const string EnemyPrefabPath =
        "Assets/Prefabs/Enemy/Enemy.prefab";
    private const string EnvironmentProfilePath =
        "Assets/Resources/Battle/DefaultBattleEnvironment.asset";
    private const string SkyboxMaterialPath =
        "Assets/Materials/Battle3DSkybox.mat";
    private const string BattleVolumeProfilePath =
        "Assets/Settings/BattleEnvironmentVolume.asset";
    private const string LegacyVolumeProfilePath =
        "Assets/Scenes/SampleScene/Main Camera Profile.asset";
    private const string BattleWindowGlowShaderPath =
        "Assets/Shaders/BattleWindowGlow.shader";
    private const string BattleWindowGlowMaterialPath =
        "Assets/Materials/BattleWindowGlow.mat";
    private const string BattleDustShaderPath =
        "Assets/Shaders/BattleAtmosphericParticle.shader";
    private const string BattleDustTexturePath =
        "Assets/Resources/Battle/BattleDustMote.asset";
    private const string BattleDustMaterialPath =
        "Assets/Materials/BattleAtmosphericDust.mat";

    private const string EnvironmentRootName = "##--ENVIRONMENT--##";
    private const string BoardRootName = "##--BOARDS--##";
    private const string BackgroundRootName = "##--BACKGROUNDS--##";
    private const string TerrainObjectName = "Terrain | Battle Ground";
    private const string DirectionalLightName = "Directional Light | Battle 3D";
    private const string FillLightName = "Directional Light | Cool Fill";
    private const string LegacyGlobalLightName = "Global Light 2D";
    private const string LightingRootName = "Lighting | Battle";
    private const string ReflectionProbeName = "Reflection Probe | Battle Arena";
    private const string LightProbeGroupName = "Light Probes | Battle Arena";
    private const string PropsRootName = "Props | Battle";
    private const string SaloonAccentLightName =
        "Point Light | Saloon Windows";
    private const string ChurchAccentLightName =
        "Point Light | Church Windows";
    private const string AtmosphericDustName =
        "Atmosphere | Floating Dust";
    private const string UiOverlayCameraName =
        "Camera | Battle UI Overlay";
    private const int TerrainTextureSize = 128;
    private const int TerrainAlphamapResolution = 128;

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
        EnsureActorContactShadows();
        BattleEnvironmentProfile environmentProfile =
            EnsureEnvironmentProfile(
                rendererIndex,
                skyboxMaterial,
                volumeProfile);
        EnsureBattleLitSpriteMaterial();
        TerrainLayer[] terrainLayers = EnsureStylizedTerrainLayers();
        TerrainData terrainData = EnsureTerrainData(terrainLayers);

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

    [MenuItem("Tools/LOADED/Apply Battle Lighting Upgrade")]
    public static void ApplyLightingUpgrade()
    {
        EnsureFolder("Assets", "Terrain");
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "Battle");

        int rendererIndex = EnsureBattleRenderer();
        Material skyboxMaterial = EnsureSkyboxMaterial();
        VolumeProfile volumeProfile = EnsureBattleVolumeProfile();
        BattleEnvironmentProfile environmentProfile =
            EnsureEnvironmentProfile(
                rendererIndex,
                skyboxMaterial,
                volumeProfile);
        EnsureBattleLitSpriteMaterial();
        TerrainLayer[] terrainLayers = EnsureStylizedTerrainLayers();
        EnsureTerrainData(terrainLayers);
        EnsureTerrainMaterial();
        EnsureBattleLightingSettings();

        EnsureActorContactShadows();
        ApplySceneLighting(environmentProfile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "Applied the Battle stylized lighting, terrain, sprite, depth, and HD-2D environment baseline.");
    }

    [MenuItem("Tools/LOADED/Apply Battle HD-2D Direction Pass")]
    public static void ApplyHd2DDirectionPass()
    {
        ApplyLightingUpgrade();
        Debug.Log(
            "Applied the Battle HD-2D depth, local glow, atmosphere, and set-dressing pass.");
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

        EnsurePipelineLighting(pipeline);

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
                return SetDefaultRenderer(pipeline, emptyIndex);
            }

            return SetDefaultRenderer(pipeline, existingIndex);
        }

        if (emptyIndex >= 0)
        {
            rendererList.GetArrayElementAtIndex(emptyIndex)
                .objectReferenceValue = renderer;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return SetDefaultRenderer(pipeline, emptyIndex);
        }

        int rendererIndex = rendererList.arraySize;
        rendererList.InsertArrayElementAtIndex(rendererIndex);
        rendererList.GetArrayElementAtIndex(rendererIndex)
            .objectReferenceValue = renderer;
        serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        return SetDefaultRenderer(pipeline, rendererIndex);
    }

    private static int SetDefaultRenderer(
        UniversalRenderPipelineAsset pipeline,
        int rendererIndex)
    {
        SerializedObject serializedPipeline = new SerializedObject(pipeline);
        serializedPipeline.FindProperty("m_DefaultRendererIndex").intValue =
            rendererIndex;
        serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        return rendererIndex;
    }

    private static void EnsurePipelineLighting(
        UniversalRenderPipelineAsset pipeline)
    {
        SerializedObject serializedPipeline = new SerializedObject(pipeline);
        serializedPipeline.FindProperty("m_SoftShadowsSupported").boolValue =
            true;
        serializedPipeline.FindProperty("m_SoftShadowQuality").intValue = 2;
        serializedPipeline.FindProperty("m_ShadowCascadeCount").intValue = 4;
        serializedPipeline.FindProperty("m_ShadowDistance").floatValue = 50f;
        serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
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
        settings.FindPropertyRelative("Intensity").floatValue = 0.65f;
        settings.FindPropertyRelative("DirectLightingStrength").floatValue =
            0.12f;
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
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, TerrainMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        SetColorIfPresent(material, "_BaseColor", Color.white);
        SetColorIfPresent(material, "_Color", Color.white);
        SetFloatIfPresent(material, "_SpecularHighlights", 0f);
        for (int layerIndex = 0; layerIndex < 4; layerIndex++)
        {
            SetFloatIfPresent(
                material,
                $"_Metallic{layerIndex}",
                0f);
            SetFloatIfPresent(
                material,
                $"_Smoothness{layerIndex}",
                0f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureBattleLitSpriteMaterial()
    {
        Shader shader = Shader.Find("LOADED/Battle Lit Sprite");
        if (shader == null)
        {
            throw new InvalidOperationException(
                "Battle Lit Sprite shader is unavailable.");
        }

        Material material = GetOrCreateMaterial(
            BattleLitSpriteMaterialPath,
            shader);
        SetColorIfPresent(material, "_Color", Color.white);
        SetFloatIfPresent(material, "_NormalStrength", 0.28f);
        SetFloatIfPresent(material, "_DiffuseWrap", 0.55f);
        SetFloatIfPresent(material, "_AmbientStrength", 0.78f);
        SetFloatIfPresent(material, "_RimStrength", 0.14f);
        SetFloatIfPresent(material, "_SpecularStrength", 0.08f);
        // Multipart avatars use one SpriteRenderer per animated body part.
        // GPU instancing can reuse the wrong renderer transform for those
        // parts under the Universal 3D renderer, visually disassembling them.
        material.enableInstancing = false;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureSkyboxMaterial()
    {
        Shader shader = Shader.Find("LOADED/Battle Gradient Skybox");
        if (shader == null)
        {
            throw new InvalidOperationException(
                "Battle Gradient Skybox shader is unavailable.");
        }

        Material material = GetOrCreateMaterial(SkyboxMaterialPath, shader);
        SetColorIfPresent(
            material,
            "_ZenithColor",
            new Color(0.10f, 0.25f, 0.48f, 1f));
        SetColorIfPresent(
            material,
            "_HorizonColor",
            new Color(0.48f, 0.63f, 0.82f, 1f));
        SetColorIfPresent(
            material,
            "_GroundColor",
            new Color(0.28f, 0.18f, 0.11f, 1f));
        SetColorIfPresent(
            material,
            "_SunColor",
            new Color(2.4f, 1.25f, 0.55f, 1f));
        SetColorIfPresent(
            material,
            "_CloudColor",
            new Color(0.90f, 0.72f, 0.64f, 1f));
        SetColorIfPresent(
            material,
            "_CloudShadowColor",
            new Color(0.18f, 0.23f, 0.34f, 1f));
        if (material.HasProperty("_SunDirection"))
        {
            Vector3 sunDirection = Quaternion.Euler(52f, -32f, 0f)
                * Vector3.back;
            material.SetVector("_SunDirection", sunDirection);
        }
        if (material.HasProperty("_CloudSpeed"))
        {
            material.SetVector(
                "_CloudSpeed",
                new Vector4(0.008f, 0.003f, 0f, 0f));
        }
        SetFloatIfPresent(material, "_SunSize", 0.025f);
        SetFloatIfPresent(material, "_SunHalo", 0.7f);
        SetFloatIfPresent(material, "_CloudScale", 0.85f);
        SetFloatIfPresent(material, "_CloudCoverage", 0.46f);
        SetFloatIfPresent(material, "_CloudSoftness", 0.12f);
        SetFloatIfPresent(material, "_CloudOpacity", 0.60f);
        SetFloatIfPresent(material, "_GradientPower", 0.65f);
        SetFloatIfPresent(material, "_HorizonSharpness", 6f);
        SetFloatIfPresent(material, "_Exposure", 1f);
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
        color.postExposure.Override(0.42f);
        color.contrast.Override(8f);
        color.colorFilter.Override(new Color(1f, 0.985f, 0.96f, 1f));
        color.saturation.Override(3f);

        WhiteBalance whiteBalance =
            GetOrAddVolumeOverride<WhiteBalance>(profile);
        whiteBalance.temperature.Override(5f);
        whiteBalance.tint.Override(1f);

        SplitToning splitToning =
            GetOrAddVolumeOverride<SplitToning>(profile);
        splitToning.shadows.Override(
            new Color(0.52f, 0.49f, 0.44f, 1f));
        splitToning.highlights.Override(
            new Color(0.58f, 0.53f, 0.43f, 1f));
        splitToning.balance.Override(4f);

        Vignette vignette = GetOrAddVolumeOverride<Vignette>(profile);
        vignette.color.Override(new Color(0.025f, 0.03f, 0.045f, 1f));
        vignette.center.Override(new Vector2(0.5f, 0.5f));
        vignette.intensity.Override(0.08f);
        vignette.smoothness.Override(0.55f);
        vignette.rounded.Override(false);

        DepthOfField depthOfField =
            GetOrAddVolumeOverride<DepthOfField>(profile);
        depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
        depthOfField.gaussianStart.Override(20.5f);
        depthOfField.gaussianEnd.Override(100f);
        depthOfField.gaussianMaxRadius.Override(0.5f);
        depthOfField.highQualitySampling.Override(true);

        if (profile.TryGet(out OldMovieVol oldMovie))
        {
            oldMovie.m_Grain.Override(0.12f);
            oldMovie.m_NoiseAlpha.Override(0.16f);
            oldMovie.m_Jolt.Override(0f);
            EditorUtility.SetDirty(oldMovie);
        }

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static LightingSettings EnsureBattleLightingSettings()
    {
        LightingSettings settings =
            AssetDatabase.LoadAssetAtPath<LightingSettings>(
                BattleLightingSettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings();
            settings.name = "BattleLightingSettings";
            AssetDatabase.CreateAsset(settings, BattleLightingSettingsPath);
        }

        SerializedObject serializedSettings = new SerializedObject(settings);
        SetSerializedBool(
            serializedSettings,
            "m_EnableBakedLightmaps",
            true);
        SetSerializedBool(
            serializedSettings,
            "m_EnableRealtimeLightmaps",
            false);
        SetSerializedFloat(serializedSettings, "m_BakeResolution", 10f);
        SetSerializedInt(serializedSettings, "m_LightmapMaxSize", 512);
        SetSerializedBool(serializedSettings, "m_AO", true);
        SetSerializedFloat(serializedSettings, "m_AOMaxDistance", 2.5f);
        SetSerializedFloat(serializedSettings, "m_CompAOExponent", 1.15f);
        SetSerializedFloat(
            serializedSettings,
            "m_CompAOExponentDirect",
            0.35f);
        SetSerializedInt(serializedSettings, "m_MixedBakeMode", 0);
        SetSerializedInt(serializedSettings, "m_Padding", 2);
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return settings;
    }

    private static void SetSerializedBool(
        SerializedObject target,
        string propertyName,
        bool value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetSerializedFloat(
        SerializedObject target,
        string propertyName,
        float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetSerializedInt(
        SerializedObject target,
        string propertyName,
        int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static LensFlareDataSRP EnsureBattleSunFlare()
    {
        LensFlareDataSRP flare =
            AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(
                BattleSunFlarePath);
        if (flare == null)
        {
            flare = ScriptableObject.CreateInstance<LensFlareDataSRP>();
            flare.name = "BattleSunFlare";
            AssetDatabase.CreateAsset(flare, BattleSunFlarePath);
        }

        flare.elements = new[]
        {
            new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Circle,
                blendMode = SRPLensFlareBlendMode.Additive,
                tint = new Color(1f, 0.72f, 0.32f, 0.28f),
                localIntensity = 0.42f,
                sizeXY = new Vector2(0.38f, 0.38f),
                uniformScale = 1f,
                fallOff = 3.2f,
                edgeOffset = 0.18f,
                modulateByLightColor = true
            },
            new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Ring,
                blendMode = SRPLensFlareBlendMode.Screen,
                tint = new Color(1f, 0.48f, 0.16f, 0.16f),
                localIntensity = 0.20f,
                sizeXY = new Vector2(0.62f, 0.62f),
                uniformScale = 1f,
                ringThickness = 0.12f,
                fallOff = 2.2f,
                edgeOffset = 0.12f,
                modulateByLightColor = true
            },
            new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Circle,
                blendMode = SRPLensFlareBlendMode.Screen,
                tint = new Color(0.30f, 0.52f, 1f, 0.10f),
                localIntensity = 0.12f,
                position = 0.42f,
                sizeXY = new Vector2(0.12f, 0.12f),
                uniformScale = 1f,
                fallOff = 2.8f,
                edgeOffset = 0.22f
            }
        };
        EditorUtility.SetDirty(flare);
        return flare;
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

    private static void EnsureActorContactShadows()
    {
        EnsureActorContactShadow(
            PlayerPrefabPath,
            new Vector2(1.05f, 0.46f),
            0.34f);
        EnsureActorContactShadow(
            EnemyPrefabPath,
            new Vector2(1.15f, 0.5f),
            0.36f);
    }

    private static void EnsureActorContactShadow(
        string prefabPath,
        Vector2 worldSize,
        float opacity)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            BattleContactShadow shadow =
                prefabRoot.GetComponent<BattleContactShadow>();
            bool changed = false;
            if (shadow == null)
            {
                shadow = prefabRoot.AddComponent<BattleContactShadow>();
                changed = true;
            }

            SerializedObject serializedShadow = new SerializedObject(shadow);
            SerializedProperty worldSizeProperty =
                serializedShadow.FindProperty("worldSize");
            SerializedProperty opacityProperty =
                serializedShadow.FindProperty("opacity");
            if (worldSizeProperty.vector2Value != worldSize)
            {
                worldSizeProperty.vector2Value = worldSize;
                changed = true;
            }
            if (!Mathf.Approximately(opacityProperty.floatValue, opacity))
            {
                opacityProperty.floatValue = opacity;
                changed = true;
            }

            if (changed)
            {
                serializedShadow.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
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
        serializedProfile.FindProperty("ambientIntensity").floatValue = 1.25f;
        serializedProfile.FindProperty("reflectionIntensity").floatValue =
            1f;
        serializedProfile.FindProperty("fogEnabled").boolValue = true;
        serializedProfile.FindProperty("fogColor").colorValue =
            new Color(0.22f, 0.29f, 0.40f, 1f);
        serializedProfile.FindProperty("fogDensity").floatValue = 0.006f;
        serializedProfile.FindProperty("directionalLightColor").colorValue =
            new Color(1f, 0.92f, 0.80f, 1f);
        serializedProfile.FindProperty("directionalLightIntensity")
            .floatValue = 1.65f;
        serializedProfile.FindProperty("directionalLightEulerAngles")
            .vector3Value = new Vector3(52f, -32f, 0f);
        serializedProfile.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static Texture2D EnsureTerrainTexture()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            TerrainTexturePath);
        if (texture != null)
        {
            if (texture.width != TerrainTextureSize
                || texture.height != TerrainTextureSize)
            {
                texture.Reinitialize(
                    TerrainTextureSize,
                    TerrainTextureSize,
                    TextureFormat.RGBA32,
                    true);
            }
            PaintTerrainTexture(texture);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        texture = new Texture2D(
            TerrainTextureSize,
            TerrainTextureSize,
            TextureFormat.RGBA32,
            true)
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
        int textureSize = texture.width;
        Color[] pixels = new Color[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float u = x / (float)textureSize;
                float v = y / (float)textureSize;
                float surface = EvaluateTerrainSurface(u, v);
                float fine = EvaluateTerrainFineDetail(u, v);
                Color drySoil = new Color(0.38f, 0.31f, 0.28f, 1f);
                Color sunBaked = new Color(0.82f, 0.70f, 0.62f, 1f);
                Color olive = new Color(0.40f, 0.43f, 0.40f, 1f);
                Color color = Color.Lerp(drySoil, sunBaked, surface);
                color = Color.Lerp(color, olive, fine * 0.12f);
                color *= Mathf.Lerp(0.82f, 1.16f, fine);
                // Terrain Lit can read albedo alpha as smoothness. Keep the
                // generated soil fully rough even when that source is active.
                color.a = 0f;
                pixels[y * textureSize + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply(true, false);
    }

    private static Texture2D EnsureTerrainNormalTexture()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            TerrainNormalPath);
        if (texture == null)
        {
            texture = new Texture2D(
                TerrainTextureSize,
                TerrainTextureSize,
                TextureFormat.RGBA32,
                true,
                true)
            {
                name = "BattleGroundNormal"
            };
            PaintTerrainNormalTexture(texture);
            AssetDatabase.CreateAsset(texture, TerrainNormalPath);
        }
        else
        {
            if (texture.width != TerrainTextureSize
                || texture.height != TerrainTextureSize)
            {
                texture.Reinitialize(
                    TerrainTextureSize,
                    TerrainTextureSize,
                    TextureFormat.RGBA32,
                    true);
            }
            PaintTerrainNormalTexture(texture);
        }

        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static void PaintTerrainNormalTexture(Texture2D texture)
    {
        PaintTerrainNormalTexture(
            texture,
            EvaluateTerrainSurface,
            3f);
    }

    private static TerrainLayer[] EnsureStylizedTerrainLayers()
    {
        Texture2D groundTexture = EnsureTerrainTexture();
        Texture2D groundNormal = EnsureTerrainNormalTexture();
        TerrainLayer groundLayer = EnsureTerrainLayer(
            groundTexture,
            groundNormal);

        Texture2D dirtTexture = EnsureTerrainTexture(
            TerrainDirtTexturePath,
            "BattleGroundDirtTexture",
            PaintTerrainDirtTexture,
            false);
        Texture2D dirtNormal = EnsureTerrainTexture(
            TerrainDirtNormalPath,
            "BattleGroundDirtNormal",
                texture => PaintTerrainNormalTexture(
                    texture,
                    EvaluateTerrainDirtSurface,
                    3.5f),
            true);
        TerrainLayer dirtLayer = EnsureTerrainLayer(
            TerrainDirtLayerPath,
            "BattleGroundDirt",
            dirtTexture,
            dirtNormal,
            new Vector2(7f, 6f),
            0.72f,
            0f,
            Vector4.one);

        Texture2D rockTexture = EnsureTerrainTexture(
            TerrainRockTexturePath,
            "BattleGroundRockTexture",
            PaintTerrainRockTexture,
            false);
        Texture2D rockNormal = EnsureTerrainTexture(
            TerrainRockNormalPath,
            "BattleGroundRockNormal",
                texture => PaintTerrainNormalTexture(
                    texture,
                    EvaluateTerrainRockSurface,
                    4f),
            true);
        TerrainLayer rockLayer = EnsureTerrainLayer(
            TerrainRockLayerPath,
            "BattleGroundRock",
            rockTexture,
            rockNormal,
            new Vector2(11f, 9f),
            0.86f,
            0f,
            Vector4.one);

        return new[] { groundLayer, dirtLayer, rockLayer };
    }

    private static Texture2D EnsureTerrainTexture(
        string path,
        string assetName,
        Action<Texture2D> painter,
        bool linear)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            texture = new Texture2D(
                TerrainTextureSize,
                TerrainTextureSize,
                TextureFormat.RGBA32,
                true,
                linear)
            {
                name = assetName
            };
            painter(texture);
            AssetDatabase.CreateAsset(texture, path);
        }
        else
        {
            if (texture.width != TerrainTextureSize
                || texture.height != TerrainTextureSize)
            {
                texture.Reinitialize(
                    TerrainTextureSize,
                    TerrainTextureSize,
                    TextureFormat.RGBA32,
                    true);
            }
            painter(texture);
        }

        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static void PaintTerrainDirtTexture(Texture2D texture)
    {
        PaintTerrainColorTexture(
            texture,
            (u, v) =>
            {
                float broad = EvaluateTerrainDirtSurface(u, v);
                float fleck = EvaluateTerrainFineDetail(
                    u * 1.7f + 0.13f,
                    v * 1.7f - 0.21f);
                Color packedEarth = new Color(0.32f, 0.26f, 0.23f, 1f);
                Color dryTrack = new Color(0.72f, 0.60f, 0.50f, 1f);
                Color color = Color.Lerp(packedEarth, dryTrack, broad);
                color *= Mathf.Lerp(0.84f, 1.14f, fleck);
                color.a = 0f;
                return color;
            });
    }

    private static void PaintTerrainRockTexture(Texture2D texture)
    {
        PaintTerrainColorTexture(
            texture,
            (u, v) =>
            {
                float facets = EvaluateTerrainRockSurface(u, v);
                float seams = Mathf.Abs(
                    Mathf.Sin((u * 5f + v * 2f) * Mathf.PI * 2f));
                Color shade = new Color(0.28f, 0.27f, 0.26f, 1f);
                Color sunFace = new Color(0.62f, 0.57f, 0.50f, 1f);
                Color color = Color.Lerp(shade, sunFace, facets);
                color *= Mathf.Lerp(0.94f, 1.04f, seams);
                color.a = 0f;
                return color;
            });
    }

    private static void PaintTerrainColorTexture(
        Texture2D texture,
        Func<float, float, Color> evaluator)
    {
        int textureSize = texture.width;
        Color[] pixels = new Color[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                pixels[y * textureSize + x] = evaluator(
                    x / (float)textureSize,
                    y / (float)textureSize);
            }
        }

        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply(true, false);
    }

    private static void PaintTerrainNormalTexture(
        Texture2D texture,
        Func<float, float, float> evaluator,
        float normalStrength)
    {
        int textureSize = texture.width;
        Color[] pixels = new Color[textureSize * textureSize];
        float step = 1f / textureSize;
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float u = x / (float)textureSize;
                float v = y / (float)textureSize;
                float left = evaluator(u - step, v);
                float right = evaluator(u + step, v);
                float down = evaluator(u, v - step);
                float up = evaluator(u, v + step);
                Vector3 normal = new Vector3(
                    (left - right) * normalStrength,
                    (down - up) * normalStrength,
                    1f).normalized;
                float packedX = normal.x * 0.5f + 0.5f;
                pixels[y * textureSize + x] = new Color(
                    packedX,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    packedX);
            }
        }

        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply(true, false);
    }

    private static float EvaluateTerrainDirtSurface(float u, float v)
    {
        float broad = PeriodicValueNoise(u * 5f, v * 5f, 5);
        float clods = PeriodicValueNoise(
            u * 13f + broad * 1.7f,
            v * 13f - broad * 1.3f,
            13);
        float grit = PeriodicValueNoise(u * 31f, v * 31f, 31);
        return Mathf.Clamp01(
            0.08f + broad * 0.46f + clods * 0.34f + grit * 0.18f);
    }

    private static float EvaluateTerrainRockSurface(float u, float v)
    {
        float plates = PeriodicValueNoise(u * 4f, v * 4f, 4);
        float facets = PeriodicValueNoise(
            u * 11f + plates * 1.4f,
            v * 11f - plates * 1.1f,
            11);
        float chips = PeriodicValueNoise(u * 29f, v * 29f, 29);
        float edge = Mathf.Abs(facets * 2f - 1f);
        return Mathf.Clamp01(
            0.06f + plates * 0.40f + edge * 0.36f + chips * 0.22f);
    }

    private static float EvaluateTerrainSurface(float u, float v)
    {
        float broad = PeriodicValueNoise(u * 4f, v * 4f, 4);
        float warpedU = u + (broad - 0.5f) * 0.11f;
        float warpedV = v - (broad - 0.5f) * 0.09f;
        float clods = PeriodicValueNoise(warpedU * 12f, warpedV * 12f, 12);
        float grit = PeriodicValueNoise(u * 37f, v * 37f, 37);
        float brokenCrust = Mathf.Abs(clods * 2f - 1f);
        return Mathf.Clamp01(
            0.08f
            + broad * 0.42f
            + brokenCrust * 0.30f
            + grit * 0.20f);
    }

    private static float EvaluateTerrainFineDetail(float u, float v)
    {
        float grains = PeriodicValueNoise(u * 41f, v * 41f, 41);
        float pebbles = PeriodicValueNoise(
            u * 19f + grains * 0.8f,
            v * 19f - grains * 0.6f,
            19);
        return Mathf.Clamp01(grains * 0.64f + pebbles * 0.36f);
    }

    private static float PeriodicValueNoise(
        float x,
        float y,
        int period)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        float tx = x - x0;
        float ty = y - y0;
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);

        float a = TerrainNoiseHash(
            WrapNoiseCoordinate(x0, period),
            WrapNoiseCoordinate(y0, period));
        float b = TerrainNoiseHash(
            WrapNoiseCoordinate(x1, period),
            WrapNoiseCoordinate(y0, period));
        float c = TerrainNoiseHash(
            WrapNoiseCoordinate(x0, period),
            WrapNoiseCoordinate(y1, period));
        float d = TerrainNoiseHash(
            WrapNoiseCoordinate(x1, period),
            WrapNoiseCoordinate(y1, period));
        return Mathf.Lerp(
            Mathf.Lerp(a, b, tx),
            Mathf.Lerp(c, d, tx),
            ty);
    }

    private static int WrapNoiseCoordinate(int value, int period)
    {
        int wrapped = value % period;
        return wrapped < 0 ? wrapped + period : wrapped;
    }

    private static float TerrainNoiseHash(int x, int y)
    {
        unchecked
        {
            uint hash = (uint)(x * 374761393 + y * 668265263);
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            hash ^= hash >> 16;
            return (hash & 0x00ffffffu) / 16777215f;
        }
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

    private static TerrainLayer EnsureTerrainLayer(
        Texture2D terrainTexture,
        Texture2D terrainNormal)
    {
        TerrainLayer terrainLayer =
            AssetDatabase.LoadAssetAtPath<TerrainLayer>(TerrainLayerPath);
        if (terrainLayer != null)
        {
            if (terrainLayer.diffuseTexture == null)
            {
                terrainLayer.diffuseTexture = terrainTexture;
            }
            terrainLayer.normalMapTexture = terrainNormal;
            terrainLayer.normalScale = 0.62f;
            terrainLayer.tileSize = new Vector2(5.5f, 5.5f);
            terrainLayer.diffuseRemapMax = new Vector4(
                1f,
                1f,
                1f,
                1f);
            terrainLayer.specular = Color.black;
            terrainLayer.metallic = 0f;
            terrainLayer.smoothness = 0f;
            EditorUtility.SetDirty(terrainLayer);
            return terrainLayer;
        }

        terrainLayer = new TerrainLayer
        {
            name = "BattleGround"
        };

        terrainLayer.diffuseTexture = terrainTexture;
        terrainLayer.normalMapTexture = terrainNormal;
        terrainLayer.normalScale = 0.62f;
        terrainLayer.tileSize = new Vector2(5.5f, 5.5f);
        terrainLayer.diffuseRemapMin = Vector4.zero;
        terrainLayer.diffuseRemapMax = new Vector4(
            1f,
            1f,
            1f,
            1f);
        terrainLayer.specular = Color.black;
        terrainLayer.metallic = 0f;
        terrainLayer.smoothness = 0f;
        AssetDatabase.CreateAsset(terrainLayer, TerrainLayerPath);
        EditorUtility.SetDirty(terrainLayer);
        return terrainLayer;
    }

    private static TerrainLayer EnsureTerrainLayer(
        string path,
        string assetName,
        Texture2D diffuse,
        Texture2D normal,
        Vector2 tileSize,
        float normalScale,
        float smoothness,
        Vector4 diffuseRemapMax)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer
            {
                name = assetName
            };
            AssetDatabase.CreateAsset(layer, path);
        }

        layer.diffuseTexture = diffuse;
        layer.normalMapTexture = normal;
        layer.tileSize = tileSize;
        layer.normalScale = normalScale;
        layer.specular = Color.black;
        layer.metallic = 0f;
        layer.smoothness = smoothness;
        layer.diffuseRemapMin = Vector4.zero;
        layer.diffuseRemapMax = diffuseRemapMax;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static TerrainData EnsureTerrainData(TerrainLayer[] terrainLayers)
    {
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(
            TerrainDataPath);
        if (terrainData != null)
        {
            bool requiresStyleBlend = terrainData.terrainLayers == null
                || terrainData.terrainLayers.Length < terrainLayers.Length
                || RequiresTerrainStyleBlendUpgrade(terrainData);
            terrainData.terrainLayers = terrainLayers;
            if (requiresStyleBlend)
            {
                PaintTerrainLayerBlend(terrainData);
            }
            EditorUtility.SetDirty(terrainData);
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
        terrainData.terrainLayers = terrainLayers;
        PaintTerrainLayerBlend(terrainData);
        EditorUtility.SetDirty(terrainData);
        return terrainData;
    }

    private static bool RequiresTerrainStyleBlendUpgrade(
        TerrainData terrainData)
    {
        if (terrainData.alphamapLayers < 3)
        {
            return true;
        }

        float[,,] weights = terrainData.GetAlphamaps(
            0,
            0,
            terrainData.alphamapWidth,
            terrainData.alphamapHeight);
        double groundTotal = 0d;
        double rockTotal = 0d;
        int sampleCount = terrainData.alphamapWidth
            * terrainData.alphamapHeight;
        for (int y = 0; y < terrainData.alphamapHeight; y++)
        {
            for (int x = 0; x < terrainData.alphamapWidth; x++)
            {
                groundTotal += weights[y, x, 0];
                rockTotal += weights[y, x, 2];
            }
        }

        double averageGround = groundTotal / sampleCount;
        double averageRock = rockTotal / sampleCount;
        return averageGround < 0.18d || averageRock > 0.25d;
    }

    private static void PaintTerrainLayerBlend(TerrainData terrainData)
    {
        terrainData.alphamapResolution = TerrainAlphamapResolution;
        int width = terrainData.alphamapWidth;
        int height = terrainData.alphamapHeight;
        float[,,] blend = new float[height, width, 3];
        Terrain terrain = FindTerrainUsingData(terrainData);
        Vector3 terrainPosition = terrain != null
            ? terrain.transform.position
            : new Vector3(-18f, -0.12f, -7f);

        for (int y = 0; y < height; y++)
        {
            float v = y / (float)Mathf.Max(1, height - 1);
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)Mathf.Max(1, width - 1);
                float worldX = terrainPosition.x + u * terrainData.size.x;
                float worldZ = terrainPosition.z + v * terrainData.size.z;
                float slope = terrainData.GetSteepness(u, v) / 90f;
                float height01 = terrainData.GetInterpolatedHeight(u, v)
                    / Mathf.Max(0.001f, terrainData.size.y);
                float noise = EvaluateTerrainSurface(u * 3f, v * 3f);

                float duelTrack = 1f - Mathf.SmoothStep(
                    2.4f,
                    7.2f,
                    Mathf.Abs(worldZ));
                duelTrack *= 1f - Mathf.SmoothStep(
                    14f,
                    24f,
                    Mathf.Abs(worldX));
                float rock = Mathf.Clamp01(
                    Mathf.SmoothStep(0.28f, 0.68f, slope)
                    + Mathf.SmoothStep(0.72f, 0.96f, height01) * 0.30f);
                float dirt = Mathf.Clamp01(
                    duelTrack * 0.65f
                    + Mathf.SmoothStep(0.68f, 0.92f, noise) * 0.16f);
                dirt *= 1f - rock * 0.80f;
                float ground = Mathf.Max(0.25f, 1f - dirt - rock);
                float total = ground + dirt + rock;

                blend[y, x, 0] = ground / total;
                blend[y, x, 1] = dirt / total;
                blend[y, x, 2] = rock / total;
            }
        }

        terrainData.SetAlphamaps(0, 0, blend);
    }

    private static Terrain FindTerrainUsingData(TerrainData terrainData)
    {
        foreach (Terrain terrain in UnityEngine.Object.FindObjectsByType<
                     Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (terrain.terrainData == terrainData)
            {
                return terrain;
            }
        }

        return null;
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
            terrain.terrainData = terrainData;
            terrain.materialTemplate = terrainMaterial;
        }
        else
        {
            terrain.terrainData ??= terrainData;
            terrain.materialTemplate ??= terrainMaterial;
        }
        terrainObject.transform.localPosition =
            new Vector3(-18f, -0.12f, -7f);
        terrainObject.transform.localRotation = Quaternion.identity;
        terrainObject.transform.localScale = Vector3.one;
        EnsureTerrainVisibleInEditor(terrain);

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
        lightObject.SetActive(true);
        directionalLight.enabled = true;
        directionalLight.type = LightType.Directional;
        directionalLight.color = profile.DirectionalLightColor;
        directionalLight.intensity = profile.DirectionalLightIntensity;
        directionalLight.transform.rotation = profile.DirectionalLightRotation;
        directionalLight.shadows = LightShadows.Soft;
        directionalLight.shadowStrength = 0.82f;
        directionalLight.shadowBias = 0.04f;
        directionalLight.shadowNormalBias = 0.35f;
        directionalLight.lightmapBakeType = LightmapBakeType.Mixed;
        directionalLight.GetUniversalAdditionalLightData();
        ConfigureSunFlare(lightObject, directionalLight);
        RenderSettings.sun = directionalLight;

        EnsureEnvironmentAuthoringHierarchy(environmentRoot.transform);
        OrganizeEnvironmentProps(scene, environmentRoot.transform);
        EnsureHd2DDirectionPass(scene, environmentRoot.transform);
        ConfigureStaticEnvironment(environmentRoot.transform, terrain);
        Lightmapping.lightingSettings = EnsureBattleLightingSettings();
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
        ConfigureTerrainDepthOfField(
            profile.VolumeProfile,
            battleCamera,
            terrain);
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
        SetObjectReference(
            controller,
            "backgroundPropRoot",
            FindDescendant(environmentRoot.transform, "Background")
                ?.transform);

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

    private static void ApplySceneLighting(BattleEnvironmentProfile profile)
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
        if (environmentRoot == null)
        {
            throw new InvalidOperationException(
                "Battle scene is missing the environment root.");
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

        lightObject.SetActive(true);
        directionalLight.enabled = true;
        directionalLight.type = LightType.Directional;
        directionalLight.color = profile.DirectionalLightColor;
        directionalLight.intensity = profile.DirectionalLightIntensity;
        directionalLight.transform.rotation = profile.DirectionalLightRotation;
        directionalLight.shadows = LightShadows.Soft;
        directionalLight.shadowStrength = 0.82f;
        directionalLight.shadowBias = 0.04f;
        directionalLight.shadowNormalBias = 0.35f;
        directionalLight.lightmapBakeType = LightmapBakeType.Mixed;
        directionalLight.GetUniversalAdditionalLightData();
        ConfigureSunFlare(lightObject, directionalLight);
        RenderSettings.sun = directionalLight;

        EnsureEnvironmentAuthoringHierarchy(environmentRoot.transform);
        OrganizeEnvironmentProps(scene, environmentRoot.transform);
        EnsureHd2DDirectionPass(scene, environmentRoot.transform);
        Terrain terrain = EnsureTerrainVisibleInEditor(
            FindDescendant(environmentRoot.transform, TerrainObjectName)
                ?.GetComponent<Terrain>());
        Camera battleCamera = FindSceneObject(scene, "Main Camera")
            ?.GetComponent<Camera>();
        if (battleCamera != null)
        {
            UniversalAdditionalCameraData cameraData =
                battleCamera.GetUniversalAdditionalCameraData();
            EnsureBattleUiOverlayCamera(
                battleCamera.gameObject,
                battleCamera,
                cameraData,
                profile.RendererIndex);
        }
        ConfigureTerrainDepthOfField(
            profile.VolumeProfile,
            battleCamera,
            terrain);
        ConfigureStaticEnvironment(environmentRoot.transform, terrain);
        Lightmapping.lightingSettings = EnsureBattleLightingSettings();
        ApplyRenderSettings(profile);

        BattleWorld3DController controller =
            environmentRoot.GetComponent<BattleWorld3DController>();
        if (controller != null)
        {
            SetObjectReference(controller, "defaultProfile", profile);
            SetObjectReference(controller, "directionalLight", directionalLight);
            SetObjectReference(
                controller,
                "backgroundPropRoot",
                FindDescendant(environmentRoot.transform, "Background")
                    ?.transform);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        FrameTerrainInSceneView(terrain);
    }

    private static void ApplyCamera(
        GameObject cameraObject,
        Camera battleCamera,
        BattleEnvironmentProfile profile,
        int rendererIndex,
        Transform followTarget,
        VolumeProfile volumeProfile)
    {
        Vector3 authoredCameraWorldPosition =
            cameraObject.transform.position;
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

        EnsureBattleUiOverlayCamera(
            cameraObject,
            battleCamera,
            cameraData,
            rendererIndex);

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
        CinemachineCamera cinemachineCamera =
            cameraObject.GetComponent<CinemachineCamera>();
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = followTarget;
            LensSettings lens = cinemachineCamera.Lens;
            lens.ModeOverride = battleCamera.orthographic
                ? LensSettings.OverrideModes.Orthographic
                : LensSettings.OverrideModes.Perspective;
            lens.FieldOfView = battleCamera.fieldOfView;
            lens.OrthographicSize = battleCamera.orthographicSize;
            lens.NearClipPlane = battleCamera.nearClipPlane;
            lens.FarClipPlane = battleCamera.farClipPlane;
            cinemachineCamera.Lens = lens;
        }

        if (follow != null
            && followTarget != null
            && follow.TrackerSettings.BindingMode
                == Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace)
        {
            follow.FollowOffset = authoredCameraWorldPosition
                - followTarget.position;
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

    private static void EnsureBattleUiOverlayCamera(
        GameObject cameraObject,
        Camera battleCamera,
        UniversalAdditionalCameraData cameraData,
        int rendererIndex)
    {
        Transform overlayTransform = cameraObject.transform.Find(
            UiOverlayCameraName);
        GameObject overlayObject;
        if (overlayTransform == null)
        {
            overlayObject = new GameObject(UiOverlayCameraName);
            overlayObject.transform.SetParent(cameraObject.transform, false);
        }
        else
        {
            overlayObject = overlayTransform.gameObject;
        }

        Camera overlayCamera = overlayObject.GetComponent<Camera>();
        if (overlayCamera == null)
        {
            overlayCamera = overlayObject.AddComponent<Camera>();
        }

        BattleUiOverlayCamera overlayController =
            overlayObject.GetComponent<BattleUiOverlayCamera>();
        if (overlayController == null)
        {
            overlayController =
                overlayObject.AddComponent<BattleUiOverlayCamera>();
        }

        for (int index = cameraData.cameraStack.Count - 1;
             index >= 0;
             index--)
        {
            Camera stackedCamera = cameraData.cameraStack[index];
            if (stackedCamera == null
                || stackedCamera == overlayCamera
                || stackedCamera.name == UiOverlayCameraName)
            {
                cameraData.cameraStack.RemoveAt(index);
            }
        }

        overlayController.Configure(battleCamera, rendererIndex);
        EditorUtility.SetDirty(battleCamera);
        EditorUtility.SetDirty(overlayCamera);
        EditorUtility.SetDirty(overlayController);
    }

    private static void ConfigureTerrainDepthOfField(
        VolumeProfile profile,
        Camera battleCamera,
        Terrain terrain)
    {
        if (profile == null || battleCamera == null || terrain == null
            || !profile.TryGet(out DepthOfField depthOfField))
        {
            return;
        }

        float farDepth = BattleWorld3DController.ResolveTerrainFarDepth(
            battleCamera,
            terrain);
        depthOfField.gaussianEnd.Override(Mathf.Max(
            depthOfField.gaussianStart.value + 1f,
            farDepth));
        EditorUtility.SetDirty(depthOfField);
        EditorUtility.SetDirty(profile);
    }

    private static void EnsureEnvironmentAuthoringHierarchy(Transform parent)
    {
        RemoveObsoleteLighting(parent);

        Transform lightingRoot = EnsureChild(parent, LightingRootName);
        EnsureReflectionProbe(lightingRoot);
        EnsureLightProbes(lightingRoot);

        Transform propsRoot = EnsureChild(parent, PropsRootName);
        EnsureChild(propsRoot, "Architecture");
        EnsureChild(propsRoot, "Ground Detail");
        EnsureChild(propsRoot, "Background");
        EnsureChild(propsRoot, "Foreground");
        EnsureChild(propsRoot, "Environment FX");
    }

    private static void EnsureHd2DDirectionPass(
        Scene scene,
        Transform environmentRoot)
    {
        Transform lightingRoot = FindDescendant(
            environmentRoot,
            LightingRootName).transform;
        Transform propsRoot = FindDescendant(
            environmentRoot,
            PropsRootName).transform;
        Transform groundDetail = FindDescendant(
            propsRoot,
            "Ground Detail").transform;
        Transform environmentFx = FindDescendant(
            propsRoot,
            "Environment FX").transform;

        EnsureAccentLight(
            lightingRoot,
            SaloonAccentLightName,
            new Vector3(5.8f, 2.45f, 4.75f),
            new Color(1f, 0.47f, 0.16f, 1f),
            2.2f,
            5.8f);
        EnsureAccentLight(
            lightingRoot,
            ChurchAccentLightName,
            new Vector3(-8.8f, 1.95f, 4.65f),
            new Color(1f, 0.58f, 0.24f, 1f),
            1.75f,
            5.2f);

        Material windowGlow = EnsureWindowGlowMaterial();
        ApplyWindowGlow(propsRoot, windowGlow);

        Material dustMaterial = EnsureAtmosphericDustMaterial();
        EnsureAtmosphericDust(environmentFx, dustMaterial);

        EnsureDecoration(
            scene,
            groundDetail,
            "Set Dressing | Saloon Barrels",
            "Assets/Package/Synty/PolygonWestern/Prefabs/Props/SM_Prop_Barrel_01.prefab",
            new Vector3(11.25f, 0.02f, 5.15f),
            new Vector3(0f, 18f, 0f),
            0.84f);
        EnsureDecoration(
            scene,
            groundDetail,
            "Set Dressing | Saloon Crate",
            "Assets/Package/Synty/PolygonWestern/Prefabs/Props/SM_Prop_Crate_01.prefab",
            new Vector3(9.95f, 0.02f, 4.8f),
            new Vector3(0f, -12f, 0f),
            0.82f);
        EnsureDecoration(
            scene,
            groundDetail,
            "Set Dressing | Hitching Post",
            "Assets/Package/Synty/PolygonWestern/Prefabs/Props/SM_Prop_HitchingPost_01.prefab",
            new Vector3(12.4f, 0.02f, 6.65f),
            new Vector3(0f, -20f, 0f),
            0.9f);
        EnsureDecoration(
            scene,
            groundDetail,
            "Set Dressing | Church Hay",
            "Assets/Package/Synty/PolygonWestern/Prefabs/Props/SM_Prop_Hay_Bale_01.prefab",
            new Vector3(-11.75f, 0.02f, 5.45f),
            new Vector3(0f, 24f, 0f),
            0.88f);
        EnsureDecoration(
            scene,
            groundDetail,
            "Set Dressing | Left Cactus",
            "Assets/Package/Synty/PolygonWestern/Prefabs/Environments/SM_Env_Cactus_04.prefab",
            new Vector3(-13.8f, 0.02f, 7.3f),
            new Vector3(0f, 14f, 0f),
            0.72f);
        EnsureDecoration(
            scene,
            groundDetail,
            "Set Dressing | Right Cactus",
            "Assets/Package/Synty/PolygonWestern/Prefabs/Environments/SM_Env_Cactus_02.prefab",
            new Vector3(14.2f, 0.02f, 8.1f),
            new Vector3(0f, -18f, 0f),
            0.68f);
        EnsureDecoration(
            scene,
            groundDetail,
            "Set Dressing | Left Grass",
            "Assets/Package/Synty/PolygonWestern/Prefabs/Environments/SM_Env_Grass_02.prefab",
            new Vector3(-12.6f, 0.02f, 4.4f),
            new Vector3(0f, 32f, 0f),
            0.9f);
        EnsureDecoration(
            scene,
            groundDetail,
            "Set Dressing | Tumbleweed",
            "Assets/Package/Synty/PolygonWestern/Prefabs/Props/SM_Prop_Tumbleweed_01.prefab",
            new Vector3(12.9f, 0.18f, 3.85f),
            new Vector3(0f, 0f, -8f),
            0.66f);
    }

    private static void EnsureAccentLight(
        Transform parent,
        string objectName,
        Vector3 worldPosition,
        Color color,
        float intensity,
        float range)
    {
        Transform lightTransform = EnsureChild(parent, objectName);
        lightTransform.position = worldPosition;
        lightTransform.rotation = Quaternion.identity;

        Light light = lightTransform.GetComponent<Light>();
        if (light == null)
        {
            light = lightTransform.gameObject.AddComponent<Light>();
        }

        light.enabled = true;
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        light.renderMode = LightRenderMode.Auto;
        light.GetUniversalAdditionalLightData();
        EditorUtility.SetDirty(light);
    }

    private static Material EnsureWindowGlowMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
            BattleWindowGlowShaderPath);
        if (shader == null)
        {
            throw new InvalidOperationException(
                $"Battle window glow shader not found: {BattleWindowGlowShaderPath}");
        }

        Material material = GetOrCreateMaterial(
            BattleWindowGlowMaterialPath,
            shader);
        material.SetColor(
            "_GlowColor",
            new Color(3.2f, 1.25f, 0.28f, 0.34f));
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ApplyWindowGlow(
        Transform propsRoot,
        Material material)
    {
        foreach (MeshRenderer renderer in propsRoot
                     .GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.name.IndexOf(
                    "Glass",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static Material EnsureAtmosphericDustMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
            BattleDustShaderPath);
        if (shader == null)
        {
            throw new InvalidOperationException(
                $"Battle atmospheric particle shader not found: {BattleDustShaderPath}");
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            BattleDustTexturePath);
        if (texture == null)
        {
            texture = new Texture2D(
                32,
                32,
                TextureFormat.RGBA32,
                true,
                true)
            {
                name = "BattleDustMote",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            AssetDatabase.CreateAsset(texture, BattleDustTexturePath);
        }

        Color[] pixels = new Color[texture.width * texture.height];
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                Vector2 uv = new Vector2(
                    (x + 0.5f) / texture.width,
                    (y + 0.5f) / texture.height);
                float distance = Vector2.Distance(uv, Vector2.one * 0.5f);
                float alpha = Mathf.Pow(
                    Mathf.Clamp01(1f - distance * 2f),
                    2.4f);
                pixels[y * texture.width + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true, false);
        EditorUtility.SetDirty(texture);

        Material material = GetOrCreateMaterial(
            BattleDustMaterialPath,
            shader);
        material.SetTexture("_BaseMap", texture);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureAtmosphericDust(
        Transform parent,
        Material material)
    {
        Transform dustTransform = EnsureChild(parent, AtmosphericDustName);
        dustTransform.position = new Vector3(0f, 2.35f, 4.8f);
        dustTransform.rotation = Quaternion.identity;
        dustTransform.localScale = Vector3.one;

        ParticleSystem particles =
            dustTransform.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = dustTransform.gameObject.AddComponent<ParticleSystem>();
        }

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.duration = 8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5.5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.055f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main.startRotation = new ParticleSystem.MinMaxCurve(
            0f,
            Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.68f, 0.34f, 0.08f),
            new Color(1f, 0.9f, 0.62f, 0.18f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 56;
        main.playOnAwake = true;
        main.cullingMode = ParticleSystemCullingMode.Automatic;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 3.5f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(28f, 4.4f, 6.5f);

        ParticleSystem.VelocityOverLifetimeModule velocity =
            particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.005f, 0.035f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.01f, 0.02f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.045f;
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.035f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.72f, 0.4f), 0f),
                new GradientColorKey(new Color(1f, 0.9f, 0.68f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.75f, 0.18f),
                new GradientAlphaKey(0.75f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer =
            dustTransform.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingFudge = -0.2f;

        particles.useAutoRandomSeed = false;
        particles.randomSeed = 32771u;
        EditorUtility.SetDirty(particles);
        EditorUtility.SetDirty(renderer);
    }

    private static void EnsureDecoration(
        Scene scene,
        Transform parent,
        string objectName,
        string prefabPath,
        Vector3 worldPosition,
        Vector3 worldEulerAngles,
        float uniformScale)
    {
        GameObject instance = FindDescendant(parent, objectName);
        if (instance == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"Battle set-dressing prefab not found: {prefabPath}");
                return;
            }

            instance = (GameObject)PrefabUtility.InstantiatePrefab(
                prefab,
                scene);
            instance.name = objectName;
            instance.transform.SetParent(parent, true);
        }

        instance.transform.SetPositionAndRotation(
            worldPosition,
            Quaternion.Euler(worldEulerAngles));
        instance.transform.localScale = Vector3.one * uniformScale;

        foreach (Collider collider in instance
                     .GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            EditorUtility.SetDirty(collider);
        }
    }

    private static void OrganizeEnvironmentProps(
        Scene scene,
        Transform environmentRoot)
    {
        Transform propsRoot = FindDescendant(
            environmentRoot,
            PropsRootName).transform;
        Transform architecture = FindDescendant(
            propsRoot,
            "Architecture").transform;
        Transform background = FindDescendant(
            propsRoot,
            "Background").transform;

        foreach (string objectName in new[]
                 {
                     "SM_Bld_Church_01",
                     "SM_Bld_Saloon_01"
                 })
        {
            ReparentSceneRoot(scene, objectName, architecture);
        }

        foreach (string objectName in new[]
                 {
                     "SM_Gen_Env_Cliff_04",
                     "SM_Gen_Env_Cliff_Arch_01",
                     "SM_Gen_Env_Cliff_01",
                     "SM_Gen_Env_Cliff_Pillar_01"
                 })
        {
            ReparentSceneRoot(scene, objectName, background);
        }
    }

    private static void ReparentSceneRoot(
        Scene scene,
        string objectName,
        Transform parent)
    {
        GameObject root = FindRootObject(scene, objectName);
        if (root != null)
        {
            root.transform.SetParent(parent, true);
        }
    }

    private static void ConfigureStaticEnvironment(
        Transform environmentRoot,
        Terrain terrain)
    {
        Transform propsRoot = FindDescendant(
            environmentRoot,
            PropsRootName)?.transform;
        if (propsRoot != null)
        {
            foreach (MeshRenderer renderer in propsRoot
                         .GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage =
                    ReflectionProbeUsage.BlendProbesAndSkybox;
                renderer.scaleInLightmap = 0.65f;
                GameObjectUtility.SetStaticEditorFlags(
                    renderer.gameObject,
                    StaticEditorFlags.ContributeGI
                    | StaticEditorFlags.BatchingStatic
                    | StaticEditorFlags.OccludeeStatic
                    | StaticEditorFlags.OccluderStatic
                    | StaticEditorFlags.ReflectionProbeStatic);
                EditorUtility.SetDirty(renderer);
            }
        }

        if (terrain != null)
        {
            terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 80f;
            GameObjectUtility.SetStaticEditorFlags(
                terrain.gameObject,
                StaticEditorFlags.OccludeeStatic
                | StaticEditorFlags.OccluderStatic
                | StaticEditorFlags.ReflectionProbeStatic);
            EditorUtility.SetDirty(terrain);
        }
    }

    private static void ConfigureSunFlare(
        GameObject lightObject,
        Light directionalLight)
    {
        LensFlareComponentSRP flare =
            lightObject.GetComponent<LensFlareComponentSRP>();
        if (flare == null)
        {
            flare = lightObject.AddComponent<LensFlareComponentSRP>();
        }

        flare.lensFlareData = EnsureBattleSunFlare();
        flare.intensity = 0.32f;
        flare.scale = 0.58f;
        flare.useOcclusion = true;
        flare.environmentOcclusion = true;
        flare.occlusionRadius = 0.14f;
        flare.sampleCount = 16;
        flare.attenuationByLightShape = true;
        flare.lightOverride = directionalLight;
        flare.allowOffScreen = false;
        EditorUtility.SetDirty(flare);
    }

    private static void RemoveObsoleteLighting(Transform parent)
    {
        foreach (string objectName in new[]
                 {
                     FillLightName,
                     LegacyGlobalLightName
                 })
        {
            GameObject lightObject = FindDescendant(parent, objectName);
            if (lightObject != null)
            {
                UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }
    }

    private static Terrain EnsureTerrainVisibleInEditor(Terrain terrain)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            return null;
        }

        GameObject terrainObject = terrain.gameObject;
        terrainObject.SetActive(true);
        terrain.enabled = true;
        terrain.drawHeightmap = true;
        terrain.drawTreesAndFoliage = true;
        terrain.drawInstanced = true;
        terrain.editorRenderFlags = TerrainRenderFlags.All;

        TerrainCollider terrainCollider =
            terrainObject.GetComponent<TerrainCollider>();
        if (terrainCollider != null)
        {
            terrainCollider.enabled = true;
            terrainCollider.terrainData = terrain.terrainData;
        }

        SceneVisibilityManager.instance.Show(terrainObject, true);
        SceneVisibilityManager.instance.EnablePicking(terrainObject, true);
        terrain.Flush();
        EditorUtility.SetDirty(terrain);
        return terrain;
    }

    private static void FrameTerrainInSceneView(Terrain terrain)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (terrain == null || terrain.terrainData == null || sceneView == null)
        {
            SceneView.RepaintAll();
            return;
        }

        Bounds bounds = terrain.terrainData.bounds;
        bounds.center = terrain.transform.TransformPoint(bounds.center);
        bounds.size = Vector3.Scale(
            bounds.size,
            terrain.transform.lossyScale);
        sceneView.sceneLighting = true;
        sceneView.cameraMode = SceneView.GetBuiltinCameraMode(
            DrawCameraMode.Textured);
        sceneView.LookAt(
            bounds.center,
            Quaternion.Euler(35f, 0f, 0f),
            Mathf.Max(10f, bounds.size.z * 0.85f),
            false,
            true);
        SceneView.RepaintAll();
    }

    private static void EnsureReflectionProbe(Transform parent)
    {
        GameObject probeObject = FindDescendant(parent, ReflectionProbeName);
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

        probe.mode = ReflectionProbeMode.Baked;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.resolution = 128;
        probe.hdr = true;
        probe.boxProjection = true;
        probe.intensity = 0.8f;
        probe.blendDistance = 3f;
        probe.size = new Vector3(34f, 10f, 16f);
        probe.center = new Vector3(0f, 4f, 0f);
        EditorUtility.SetDirty(probe);
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
