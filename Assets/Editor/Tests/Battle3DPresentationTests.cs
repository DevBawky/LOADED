using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using VolFx;

public sealed class Battle3DPresentationTests
{
    private const string BattleScenePath = "Assets/Scenes/Battle.unity";
    private const string ProjectileProfilePath =
        "Assets/Resources/ProjectileVisuals/DefaultProjectileVisual.asset";
    private const string EnvironmentProfilePath =
        "Assets/Resources/Battle/DefaultBattleEnvironment.asset";
    private const string BattleRendererPath =
        "Assets/Settings/Battle3DRenderer.asset";
    private const string PipelinePath =
        "Assets/Settings/UniversalRP.asset";
    private const string KillImpactShaderPath =
        "Assets/Shaders/KillImpactFullscreen.shader";
    private const string SkyboxMaterialPath =
        "Assets/Materials/Battle3DSkybox.mat";
    private const string BattleVolumeProfilePath =
        "Assets/Settings/BattleEnvironmentVolume.asset";
    private const string TerrainLayerPath =
        "Assets/Terrain/BattleGround.terrainlayer";
    private const string TerrainDirtLayerPath =
        "Assets/Terrain/BattleGroundDirt.terrainlayer";
    private const string TerrainRockLayerPath =
        "Assets/Terrain/BattleGroundRock.terrainlayer";
    private const string TerrainMaterialPath =
        "Assets/Materials/Battle3DTerrain.mat";
    private const string TerrainNormalPath =
        "Assets/Terrain/BattleGroundNormal.asset";
    private const string TerrainTexturePath =
        "Assets/Terrain/BattleGroundTexture.asset";
    private const string TerrainDataPath =
        "Assets/Terrain/BattleTerrain.asset";
    private const string BattleLitSpriteMaterialPath =
        "Assets/Resources/Battle/BattleLitSprite.mat";
    private const string BattleSunFlarePath =
        "Assets/Resources/Battle/BattleSunFlare.asset";
    private const string GroundShadowShaderPath =
        "Assets/Shaders/BattleSpriteGroundShadow.shader";
    private const string WindowGlowShaderPath =
        "Assets/Shaders/BattleWindowGlow.shader";
    private const string WindowGlowMaterialPath =
        "Assets/Materials/BattleWindowGlow.mat";
    private const string DustShaderPath =
        "Assets/Shaders/BattleAtmosphericParticle.shader";
    private const string DustTexturePath =
        "Assets/Resources/Battle/BattleDustMote.asset";
    private const string DustMaterialPath =
        "Assets/Materials/BattleAtmosphericDust.mat";

    [Test]
    public void KillImpactShader_AvoidsFullFrameBrightnessPulse()
    {
        string source = System.IO.File.ReadAllText(KillImpactShaderPath);

        Assert.That(source, Does.Not.Contain("screenFlash"));
        Assert.That(source, Does.Not.Contain("exposureLift"));
    }

    [Test]
    public void KillImpactShader_DoesNotDoubleFlipViewportY()
    {
        string source = System.IO.File.ReadAllText(KillImpactShaderPath);

        Assert.That(
            source,
            Does.Contain(
                "float2 center = _KillImpactCenters[impactIndex].xy;"));
        Assert.That(source, Does.Not.Contain("center.y = 1.0 - center.y;"));
    }

    [Test]
    public void BattleLitSprite_AvoidsMultipartAvatarGpuInstancing()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            BattleLitSpriteMaterialPath);

        Assert.That(material, Is.Not.Null);
        Assert.That(material.enableInstancing, Is.False);
    }

    [Test]
    public void Battle3DRendererSupportsLegacyCombatImpactPresentation()
    {
        UniversalRendererData renderer =
            AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                BattleRendererPath);
        Assert.That(renderer, Is.Not.Null);

        SerializedObject serializedRenderer = new SerializedObject(renderer);
        SerializedProperty postProcessData =
            serializedRenderer.FindProperty("postProcessData")
            ?? serializedRenderer.FindProperty("m_PostProcessData");
        Assert.That(postProcessData, Is.Not.Null);
        Assert.That(postProcessData.objectReferenceValue, Is.Not.Null);

        ScriptableRendererFeature impactFeature = null;
        int impactFeatureCount = 0;
        foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
        {
            if (feature != null && feature.name == "Kill Impact Fullscreen")
            {
                impactFeatureCount++;
                impactFeature = feature;
            }
        }

        Assert.That(impactFeatureCount, Is.EqualTo(1));
        Assert.That(impactFeature, Is.Not.Null);
        Assert.That(
            impactFeature.GetType().Name,
            Is.EqualTo("FullScreenPassRendererFeature"));

        SerializedObject serializedFeature = new SerializedObject(
            impactFeature);
        Assert.That(
            serializedFeature.FindProperty("injectionPoint").intValue,
            Is.EqualTo(600));
        Assert.That(
            serializedFeature.FindProperty("fetchColorBuffer").boolValue,
            Is.True);
        Assert.That(
            serializedFeature.FindProperty("passMaterial")
                .objectReferenceValue,
            Is.Not.Null);

        SerializedProperty featureMap = serializedRenderer.FindProperty(
            "m_RendererFeatureMap");
        Assert.That(featureMap, Is.Not.Null);
        Assert.That(
            featureMap.arraySize,
            Is.EqualTo(renderer.rendererFeatures.Count));
        Assert.That(featureMap.GetArrayElementAtIndex(0).longValue, Is.Not.Zero);

        ScreenSpaceAmbientOcclusion ambientOcclusion = null;
        int ambientOcclusionCount = 0;
        foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
        {
            if (feature is ScreenSpaceAmbientOcclusion resolved)
            {
                ambientOcclusionCount++;
                ambientOcclusion = resolved;
            }
        }

        Assert.That(ambientOcclusionCount, Is.EqualTo(1));
        SerializedObject serializedAmbientOcclusion =
            new SerializedObject(ambientOcclusion);
        SerializedProperty settings = serializedAmbientOcclusion
            .FindProperty("m_Settings");
        Assert.That(settings, Is.Not.Null);
        Assert.That(
            settings.FindPropertyRelative("Intensity").floatValue,
            Is.EqualTo(0.65f).Within(0.0001f));
        Assert.That(
            settings.FindPropertyRelative("Source").intValue,
            Is.EqualTo(1));
        Assert.That(
            settings.FindPropertyRelative("Downsample").boolValue,
            Is.False);
    }

    [Test]
    public void BattleEnvironmentAssets_AreConfiguredForHdrWorldRendering()
    {
        BattleEnvironmentProfile environment =
            AssetDatabase.LoadAssetAtPath<BattleEnvironmentProfile>(
                EnvironmentProfilePath);
        Material skybox = AssetDatabase.LoadAssetAtPath<Material>(
            SkyboxMaterialPath);
        VolumeProfile volume = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
            BattleVolumeProfilePath);
        UniversalRenderPipelineAsset pipeline =
            AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                PipelinePath);

        Assert.That(environment, Is.Not.Null);
        Assert.That(skybox, Is.Not.Null);
        Assert.That(skybox.shader, Is.Not.Null);
        Assert.That(
            skybox.shader.name,
            Is.EqualTo("LOADED/Battle Gradient Skybox"));
        Assert.That(skybox.GetFloat("_CloudOpacity"),
            Is.EqualTo(0.60f).Within(0.0001f));
        Assert.That(skybox.GetFloat("_SunHalo"),
            Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(environment.SkyboxMaterial, Is.SameAs(skybox));
        Assert.That(environment.VolumeProfile, Is.SameAs(volume));
        Assert.That(environment.UsesPerspective, Is.True);
        Assert.That(environment.PerspectiveFieldOfView,
            Is.EqualTo(40f).Within(0.0001f));
        Assert.That(environment.FogEnabled, Is.True);
        Assert.That(environment.FogDensity,
            Is.EqualTo(0.006f).Within(0.0001f));
        Assert.That(environment.AmbientIntensity,
            Is.EqualTo(1.25f).Within(0.0001f));
        Assert.That(environment.DirectionalLightIntensity,
            Is.EqualTo(1.65f).Within(0.0001f));

        Assert.That(pipeline, Is.Not.Null);
        SerializedObject serializedPipeline = new SerializedObject(pipeline);
        Assert.That(
            serializedPipeline.FindProperty("m_SoftShadowsSupported")
                .boolValue,
            Is.True);
        Assert.That(
            serializedPipeline.FindProperty("m_ShadowCascadeCount").intValue,
            Is.EqualTo(4));
        Assert.That(
            serializedPipeline.FindProperty("m_DefaultRendererIndex")
                .intValue,
            Is.EqualTo(environment.RendererIndex));

        Assert.That(volume, Is.Not.Null);
        Assert.That(volume.TryGet(out Tonemapping tonemapping), Is.True);
        Assert.That(tonemapping.active, Is.True);
        Assert.That(tonemapping.mode.overrideState, Is.True);
        Assert.That(tonemapping.mode.value, Is.EqualTo(TonemappingMode.ACES));
        Assert.That(volume.TryGet(out Bloom bloom), Is.True);
        Assert.That(bloom.active, Is.True);
        Assert.That(bloom.intensity.value,
            Is.EqualTo(0.28f).Within(0.0001f));
        Assert.That(volume.TryGet(out ColorAdjustments color), Is.True);
        Assert.That(color.postExposure.value,
            Is.EqualTo(0.42f).Within(0.0001f));
        Assert.That(color.contrast.value, Is.EqualTo(8f).Within(0.0001f));
        Assert.That(volume.TryGet(out SplitToning splitToning), Is.True);
        Assert.That(splitToning.active, Is.True);
        Assert.That(splitToning.balance.value,
            Is.EqualTo(4f).Within(0.0001f));
        Assert.That(volume.TryGet(out Vignette vignette), Is.True);
        Assert.That(vignette.intensity.value,
            Is.EqualTo(0.08f).Within(0.0001f));
        Assert.That(volume.TryGet(out DepthOfField depthOfField), Is.True);
        Assert.That(depthOfField.active, Is.True);
        Assert.That(
            depthOfField.mode.value,
            Is.EqualTo(DepthOfFieldMode.Gaussian));
        Assert.That(depthOfField.gaussianStart.value,
            Is.EqualTo(20.5f).Within(0.0001f));
        Assert.That(depthOfField.gaussianEnd.value,
            Is.GreaterThan(90f));
        Assert.That(depthOfField.gaussianMaxRadius.value,
            Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(depthOfField.highQualitySampling.value, Is.True);
        Assert.That(volume.TryGet(out OldMovieVol oldMovie), Is.True);
        Assert.That(oldMovie.m_Grain.value,
            Is.EqualTo(0.12f).Within(0.0001f));
        Assert.That(oldMovie.m_NoiseAlpha.value,
            Is.EqualTo(0.16f).Within(0.0001f));

        TerrainLayer terrainLayer =
            AssetDatabase.LoadAssetAtPath<TerrainLayer>(TerrainLayerPath);
        TerrainLayer dirtLayer =
            AssetDatabase.LoadAssetAtPath<TerrainLayer>(TerrainDirtLayerPath);
        TerrainLayer rockLayer =
            AssetDatabase.LoadAssetAtPath<TerrainLayer>(TerrainRockLayerPath);
        Material terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            TerrainMaterialPath);
        Texture2D terrainNormal =
            AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainNormalPath);
        Texture2D terrainTexture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTexturePath);
        TerrainData terrainData =
            AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        Assert.That(terrainLayer, Is.Not.Null);
        Assert.That(terrainNormal, Is.Not.Null);
        Assert.That(terrainNormal.width, Is.EqualTo(128));
        Assert.That(terrainNormal.height, Is.EqualTo(128));
        Assert.That(terrainNormal.mipmapCount, Is.GreaterThan(1));
        Assert.That(terrainLayer.normalMapTexture, Is.SameAs(terrainNormal));
        Assert.That(terrainTexture, Is.Not.Null);
        float maximumSoilSmoothness = 0f;
        foreach (Color pixel in terrainTexture.GetPixels())
        {
            maximumSoilSmoothness = Mathf.Max(
                maximumSoilSmoothness,
                pixel.a);
        }
        Assert.That(maximumSoilSmoothness,
            Is.EqualTo(0f).Within(0.0001f));
        Assert.That(terrainLayer.normalScale,
            Is.EqualTo(0.62f).Within(0.0001f));
        Assert.That(terrainLayer.specular, Is.EqualTo(Color.black));
        Assert.That(terrainLayer.smoothness,
            Is.EqualTo(0f).Within(0.0001f));
        Assert.That(dirtLayer, Is.Not.Null);
        Assert.That(dirtLayer.normalScale,
            Is.EqualTo(0.72f).Within(0.0001f));
        Assert.That(dirtLayer.smoothness,
            Is.EqualTo(0f).Within(0.0001f));
        Assert.That(rockLayer, Is.Not.Null);
        Assert.That(rockLayer.normalScale,
            Is.EqualTo(0.86f).Within(0.0001f));
        Assert.That(rockLayer.smoothness,
            Is.EqualTo(0f).Within(0.0001f));
        Assert.That(terrainMaterial, Is.Not.Null);
        Assert.That(terrainMaterial.GetFloat("_Smoothness0"),
            Is.EqualTo(0f).Within(0.0001f));
        Assert.That(terrainData, Is.Not.Null);
        Assert.That(terrainData.terrainLayers, Has.Length.EqualTo(3));
        Assert.That(terrainData.alphamapResolution, Is.EqualTo(128));

        Material spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            BattleLitSpriteMaterialPath);
        Assert.That(spriteMaterial, Is.Not.Null);
        Assert.That(spriteMaterial.shader, Is.Not.Null);
        Assert.That(
            spriteMaterial.shader.name,
            Is.EqualTo("LOADED/Battle Lit Sprite"));

        LensFlareDataSRP flare =
            AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(
                BattleSunFlarePath);
        Assert.That(flare, Is.Not.Null);
        Assert.That(flare.elements, Has.Length.EqualTo(3));

        Shader windowGlowShader = AssetDatabase.LoadAssetAtPath<Shader>(
            WindowGlowShaderPath);
        Material windowGlowMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            WindowGlowMaterialPath);
        Shader dustShader = AssetDatabase.LoadAssetAtPath<Shader>(
            DustShaderPath);
        Texture2D dustTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            DustTexturePath);
        Material dustMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            DustMaterialPath);
        Assert.That(windowGlowShader, Is.Not.Null);
        Assert.That(windowGlowMaterial, Is.Not.Null);
        Assert.That(windowGlowMaterial.shader, Is.SameAs(windowGlowShader));
        Assert.That(dustShader, Is.Not.Null);
        Assert.That(dustTexture, Is.Not.Null);
        Assert.That(dustTexture.width, Is.EqualTo(32));
        Assert.That(dustTexture.height, Is.EqualTo(32));
        Assert.That(dustMaterial, Is.Not.Null);
        Assert.That(dustMaterial.shader, Is.SameAs(dustShader));
        Assert.That(
            dustMaterial.GetTexture("_BaseMap"),
            Is.SameAs(dustTexture));
    }

    [Test]
    public void PlayerAndEnemyPrefabsUseSpriteSilhouetteGroundShadows()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/Player.prefab");
        GameObject enemy = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemy/Enemy.prefab");
        Shader shadowShader = AssetDatabase.LoadAssetAtPath<Shader>(
            GroundShadowShaderPath);

        Assert.That(player, Is.Not.Null);
        Assert.That(enemy, Is.Not.Null);
        Assert.That(player.GetComponent<BattleContactShadow>(), Is.Not.Null);
        Assert.That(enemy.GetComponent<BattleContactShadow>(), Is.Not.Null);
        Assert.That(shadowShader, Is.Not.Null);
        Assert.That(
            shadowShader.name,
            Is.EqualTo("LOADED/Battle Sprite Ground Shadow"));

        Assert.That(
            BattleContactShadow.ResolveGroundingCorrection(1.25f, 0.5f),
            Is.EqualTo(-0.75f).Within(0.0001f));
        Vector3 gridCorrection =
            BattleContactShadow.ResolveGridGroundingCorrection(
                new Vector3(1.2f, 0.8f, -0.3f),
                new Vector3(1f, 4f, -0.5f),
                0.1f);
        Assert.That(gridCorrection.x,
            Is.EqualTo(-0.2f).Within(0.0001f));
        Assert.That(gridCorrection.y,
            Is.EqualTo(-0.7f).Within(0.0001f));
        Assert.That(gridCorrection.z,
            Is.EqualTo(-0.2f).Within(0.0001f));

        Bounds spriteBounds = new Bounds(
            new Vector3(0.25f, -0.1f, 0f),
            new Vector3(2f, 3f, 0f));
        Vector2 flipped = BattleContactShadow.ResolveFlippedVertex(
            new Vector2(-0.5f, 0.7f),
            spriteBounds,
            true,
            true);
        Assert.That(flipped.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(flipped.y, Is.EqualTo(-0.9f).Within(0.0001f));
    }

    [Test]
    public void EveryBulletUsesTheCommonSphereProjectileProfile()
    {
        ProjectileVisualProfile expected =
            AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                ProjectileProfilePath);

        Assert.That(expected, Is.Not.Null);
        Assert.That(expected.ProjectilePrefab, Is.Not.Null);
        Assert.That(expected.ArcHeight, Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(expected.Scale, Is.EqualTo(0.17f).Within(0.0001f));
        Assert.That(
            expected.ProjectilePrefab.GetComponent<MeshFilter>(),
            Is.Not.Null);
        Assert.That(
            expected.ProjectilePrefab.GetComponent<SphereCollider>(),
            Is.Null);

        string[] bulletGuids = AssetDatabase.FindAssets("t:BulletData");
        Assert.That(bulletGuids, Is.Not.Empty);

        foreach (string guid in bulletGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BulletData bullet = AssetDatabase.LoadAssetAtPath<BulletData>(path);
            Assert.That(
                bullet.ProjectileVisual,
                Is.SameAs(expected),
                $"{path} does not use the common projectile profile.");
        }
    }

    [TestCase(0, 0.06f)]
    [TestCase(1, 0.06f)]
    [TestCase(2, 0.08f)]
    [TestCase(3, 0.10f)]
    [TestCase(4, 0.12f)]
    [TestCase(5, 0.14f)]
    [TestCase(6, 0.14f)]
    public void PlayerProjectileTravelScalesWithTileDistance(
        int tileDistance,
        float expectedDuration)
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/Player.prefab");
        Assert.That(playerPrefab, Is.Not.Null);
        PlayerShoot playerShoot = playerPrefab.GetComponent<PlayerShoot>();
        Assert.That(playerShoot, Is.Not.Null);
        SerializedObject serializedPlayerShoot =
            new SerializedObject(playerShoot);
        float authoredInterval = serializedPlayerShoot
            .FindProperty("shotInterval")
            .floatValue;

        Assert.That(authoredInterval, Is.EqualTo(0.15f).Within(0.0001f));
        Assert.That(
            PlayerShoot.ResolveProjectileTravelDuration(tileDistance),
            Is.EqualTo(expectedDuration).Within(0.0001f));

        Vector3 start = new Vector3(-2f, 1f, 0f);
        Vector3 nearTarget = new Vector3(-1f, 2f, 0.5f);
        Vector3 farTarget = new Vector3(12f, 4f, 7f);

        Assert.That(
            BulletProjectileView.EvaluateTravelPosition(
                start,
                nearTarget,
                0.2f,
                1f),
            Is.EqualTo(nearTarget));
        Assert.That(
            BulletProjectileView.EvaluateTravelPosition(
                start,
                farTarget,
                0.2f,
                1f),
            Is.EqualTo(farTarget));
    }

    [Test]
    public void PlayerProjectileTravelUsesMaximumDelayWhenDistanceIsUnknown()
    {
        Assert.That(
            PlayerShoot.ResolveProjectileTravelDuration(-1),
            Is.EqualTo(0.14f).Within(0.0001f));
    }

    [Test]
    public void ShotTimerUsesUnscaledTimeButStopsForExplicitPause()
    {
        Assert.That(
            PlayerShoot.AdvanceShotTimer(0.04f, 0.11f, false),
            Is.EqualTo(0.15f).Within(0.0001f));
        Assert.That(
            PlayerShoot.AdvanceShotTimer(0.04f, 0.11f, true),
            Is.EqualTo(0.04f).Within(0.0001f));
    }

    [Test]
    public void EveryBattleUsesTheCommon3DEnvironmentProfile()
    {
        BattleEnvironmentProfile expected =
            AssetDatabase.LoadAssetAtPath<BattleEnvironmentProfile>(
                EnvironmentProfilePath);

        Assert.That(expected, Is.Not.Null);

        string[] battleGuids = AssetDatabase.FindAssets("t:BattleData");
        Assert.That(battleGuids, Is.Not.Empty);

        foreach (string guid in battleGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BattleData battle = AssetDatabase.LoadAssetAtPath<BattleData>(path);
            Assert.That(
                battle.EnvironmentProfile,
                Is.SameAs(expected),
                $"{path} does not use the common environment profile.");
        }
    }

    [Test]
    public void RotatedBoardKeepsEveryCellOnTheTerrainPlane()
    {
        GameObject managerObject = new GameObject("Board Manager");
        GameObject tileParentObject = new GameObject("Tile Parent");
        GameObject tileTemplateObject = new GameObject("Tile Template");

        try
        {
            tileParentObject.transform.SetPositionAndRotation(
                new Vector3(0f, 0.03f, 0f),
                Quaternion.Euler(90f, 0f, 0f));

            BoardManager manager = managerObject.AddComponent<BoardManager>();
            BoardTile tileTemplate =
                tileTemplateObject.AddComponent<BoardTile>();
            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("tileParent").objectReferenceValue =
                tileParentObject.transform;
            serializedManager.FindProperty("boardDistance").floatValue = 2f;
            serializedManager.FindProperty("laneDistance").floatValue = 0.92f;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(manager.ConfigureBoard(3, 2, tileTemplate), Is.True);

            HashSet<Vector3> cells = new HashSet<Vector3>();
            for (int laneIndex = 0; laneIndex < 2; laneIndex++)
            {
                for (int tileIndex = 0; tileIndex < 3; tileIndex++)
                {
                    Assert.That(
                        manager.TryGetTilePosition(
                            tileIndex,
                            laneIndex,
                            out Vector3 position),
                        Is.True);
                    Assert.That(position.y, Is.EqualTo(0.03f).Within(0.0001f));
                    Assert.That(cells.Add(position), Is.True);
                    Assert.That(
                        manager.TryGetTileIndex(
                            position,
                            laneIndex,
                            out int resolvedIndex),
                        Is.True);
                    Assert.That(resolvedIndex, Is.EqualTo(tileIndex));
                }
            }

            Assert.That(cells, Has.Count.EqualTo(6));
            Assert.That(
                manager.TryGetTilePosition(1, 0, out Vector3 lowerLane),
                Is.True);
            Assert.That(
                manager.TryGetTilePosition(1, 1, out Vector3 upperLane),
                Is.True);
            Assert.That(
                upperLane.z,
                Is.GreaterThan(lowerLane.z),
                "The up input lane must appear above the lower lane.");
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(tileParentObject);
            Object.DestroyImmediate(tileTemplateObject);
        }
    }

    [Test]
    public void CombatCameraShakePreservesTheAuthoredBattleCameraRotation()
    {
        GameObject cameraObject = new GameObject("Battle Camera");

        try
        {
            Quaternion expected = Quaternion.Euler(35f, 0f, 0f);
            cameraObject.transform.localRotation = expected;
            CombatCameraShake shake =
                cameraObject.AddComponent<CombatCameraShake>();
            MethodInfo awake = typeof(CombatCameraShake).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo lateUpdate = typeof(CombatCameraShake).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(awake, Is.Not.Null);
            Assert.That(lateUpdate, Is.Not.Null);
            awake.Invoke(shake, null);
            lateUpdate.Invoke(shake, null);
            Assert.That(
                Quaternion.Angle(
                    cameraObject.transform.localRotation,
                    expected),
                Is.LessThan(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void BillboardPreservesTheSpritesHorizontalFacingDirection()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject spriteObject = new GameObject("Enemy Avatar");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            BattleSpriteBillboard billboard =
                spriteObject.AddComponent<BattleSpriteBillboard>();

            billboard.SetTargetCamera(camera);

            Assert.That(
                Vector3.Dot(
                    spriteObject.transform.right,
                    cameraObject.transform.right),
                Is.GreaterThan(0.999f));
            Assert.That(
                Vector3.Dot(
                    spriteObject.transform.up,
                    cameraObject.transform.up),
                Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(spriteObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void GuaranteedDefeatShockwave_RemainsFacingThePitchedCamera()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject shockwaveObject = new GameObject("Defeat Shockwave");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

            CombatImpactSignaturePresenter.FaceGuaranteedDefeatShockwave(
                shockwaveObject.transform,
                camera,
                24f);

            Assert.That(
                Vector3.Dot(
                    shockwaveObject.transform.forward,
                    cameraObject.transform.forward),
                Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(shockwaveObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void FullscreenImpactCenter_TracksTheCurrentBattleCamera()
    {
        GameObject cameraObject = new GameObject("Battle Camera");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Vector3 impactPosition = new Vector3(2f, 0f, 0f);

            Vector2 beforeMove = CombatFeedbackController
                .ResolveFullscreenImpactCenter(camera, impactPosition);
            cameraObject.transform.position = new Vector3(1f, 0f, -10f);
            Vector2 afterMove = CombatFeedbackController
                .ResolveFullscreenImpactCenter(camera, impactPosition);

            Assert.That(afterMove.x, Is.LessThan(beforeMove.x));
            Vector2 expected = camera.WorldToViewportPoint(impactPosition);
            Assert.That(afterMove.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(afterMove.y, Is.EqualTo(expected.y).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void DamageNumberFacesThePitchedBattleCamera()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject numberObject = new GameObject("Damage Number");

        try
        {
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            DamageNumbersPro.DamageNumber number =
                numberObject.AddComponent<DamageNumbersPro.DamageNumberMesh>();

            EnemyDamageNumberDisplay.ConfigureForBattleCamera(number, camera);

            Assert.That(number.enable3DGame, Is.True);
            Assert.That(number.faceCameraView, Is.True);
            Assert.That(number.lookAtCamera, Is.False);
            Assert.That(number.cameraOverride, Is.SameAs(camera.transform));
            Assert.That(
                Vector3.Dot(
                    numberObject.transform.right,
                    cameraObject.transform.right),
                Is.GreaterThan(0.999f));
            Assert.That(
                Vector3.Dot(
                    numberObject.transform.up,
                    cameraObject.transform.up),
                Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(numberObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void SceneAuthoredCameraKeepsThePlayerInView()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        GameObject cameraObject = null;

        try
        {
            Scene scene = EditorSceneManager.OpenScene(
                BattleScenePath,
                OpenSceneMode.Single);
            GameObject player = FindSceneObject(scene, "Player");
            Camera authoredCamera = FindSceneObject(scene, "Main Camera")
                .GetComponent<Camera>();
            Assert.That(player, Is.Not.Null);
            Assert.That(authoredCamera, Is.Not.Null);
            Transform avatar = player.transform.Find("Avatar");
            Assert.That(avatar, Is.Not.Null);
            cameraObject = new GameObject("Battle Camera Test");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = authoredCamera.orthographic;
            camera.fieldOfView = authoredCamera.fieldOfView;
            camera.orthographicSize = authoredCamera.orthographicSize;
            camera.nearClipPlane = authoredCamera.nearClipPlane;
            camera.farClipPlane = authoredCamera.farClipPlane;
            cameraObject.transform.SetPositionAndRotation(
                authoredCamera.transform.position,
                authoredCamera.transform.rotation);

            Vector3 viewportPoint = camera.WorldToViewportPoint(avatar.position);
            Assert.That(viewportPoint.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(viewportPoint.y, Is.InRange(0f, 1f));
            Assert.That(viewportPoint.z, Is.GreaterThan(0f));
        }
        finally
        {
            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    [Test]
    public void ApplyingEnvironmentProfilePreservesSceneAuthoredCameraState()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(
                BattleScenePath,
                OpenSceneMode.Single);
            BattleWorld3DController controller = FindSceneObject(
                    scene,
                    "##--ENVIRONMENT--##")
                .GetComponent<BattleWorld3DController>();
            Camera camera = FindSceneObject(scene, "Main Camera")
                .GetComponent<Camera>();
            CinemachineFollow follow =
                camera.GetComponent<CinemachineFollow>();
            BattleEnvironmentProfile profile =
                AssetDatabase.LoadAssetAtPath<BattleEnvironmentProfile>(
                    EnvironmentProfilePath);

            Assert.That(controller, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(follow, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);

            Vector3 expectedPosition = camera.transform.localPosition;
            Quaternion expectedRotation = camera.transform.localRotation;
            Vector3 expectedFollowOffset = follow.FollowOffset;
            camera.orthographic = false;
            camera.fieldOfView = 57f;
            camera.orthographicSize = 6.25f;
            camera.nearClipPlane = 0.23f;
            camera.farClipPlane = 1234f;
            CinemachineCamera cinemachineCamera =
                camera.GetComponent<CinemachineCamera>();
            Assert.That(cinemachineCamera, Is.Not.Null);
            LensSettings expectedLens = cinemachineCamera.Lens;
            expectedLens.ModeOverride =
                LensSettings.OverrideModes.Perspective;
            expectedLens.FieldOfView = 57f;
            expectedLens.OrthographicSize = 6.25f;
            expectedLens.NearClipPlane = 0.23f;
            expectedLens.FarClipPlane = 1234f;
            cinemachineCamera.Lens = expectedLens;

            controller.ApplyProfile(profile);

            Assert.That(camera.transform.localPosition,
                Is.EqualTo(expectedPosition));
            Assert.That(
                Quaternion.Angle(
                    camera.transform.localRotation,
                    expectedRotation),
                Is.LessThan(0.01f));
            Assert.That(follow.FollowOffset,
                Is.EqualTo(expectedFollowOffset));
            Assert.That(camera.orthographic, Is.False);
            Assert.That(camera.fieldOfView,
                Is.EqualTo(57f).Within(0.0001f));
            Assert.That(camera.orthographicSize,
                Is.EqualTo(6.25f).Within(0.0001f));
            Assert.That(camera.nearClipPlane,
                Is.EqualTo(0.23f).Within(0.0001f));
            Assert.That(camera.farClipPlane,
                Is.EqualTo(1234f).Within(0.0001f));
            Assert.That(cinemachineCamera.Lens.ModeOverride,
                Is.EqualTo(expectedLens.ModeOverride));
            Assert.That(cinemachineCamera.Lens.FieldOfView,
                Is.EqualTo(expectedLens.FieldOfView).Within(0.0001f));
            Assert.That(cinemachineCamera.Lens.OrthographicSize,
                Is.EqualTo(expectedLens.OrthographicSize).Within(0.0001f));
            Assert.That(cinemachineCamera.Lens.NearClipPlane,
                Is.EqualTo(expectedLens.NearClipPlane).Within(0.0001f));
            Assert.That(cinemachineCamera.Lens.FarClipPlane,
                Is.EqualTo(expectedLens.FarClipPlane).Within(0.0001f));
        }
        finally
        {
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    [Test]
    public void BattleSceneCameraAndCinemachineLensUseTheSameAuthoredValues()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(
                BattleScenePath,
                OpenSceneMode.Single);
            Camera camera = FindSceneObject(scene, "Main Camera")
                .GetComponent<Camera>();
            CinemachineCamera cinemachineCamera =
                camera.GetComponent<CinemachineCamera>();

            Assert.That(camera, Is.Not.Null);
            Assert.That(cinemachineCamera, Is.Not.Null);
            Assert.That(
                cinemachineCamera.Lens.ModeOverride,
                Is.EqualTo(camera.orthographic
                    ? LensSettings.OverrideModes.Orthographic
                    : LensSettings.OverrideModes.Perspective));
            Assert.That(cinemachineCamera.Lens.FieldOfView,
                Is.EqualTo(camera.fieldOfView).Within(0.0001f));
            Assert.That(cinemachineCamera.Lens.OrthographicSize,
                Is.EqualTo(camera.orthographicSize).Within(0.0001f));
            Assert.That(cinemachineCamera.Lens.NearClipPlane,
                Is.EqualTo(camera.nearClipPlane).Within(0.0001f));
            Assert.That(cinemachineCamera.Lens.FarClipPlane,
                Is.EqualTo(camera.farClipPlane).Within(0.0001f));
        }
        finally
        {
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    [Test]
    public void WorldCanvasDepthOffsetPreservesItsScreenPosition()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject ownerObject = new GameObject("Enemy");
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform));

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 9.3f, -12f),
                Quaternion.Euler(35f, 0f, 0f));
            canvasObject.transform.SetParent(ownerObject.transform, false);
            RectTransform canvasRect =
                canvasObject.GetComponent<RectTransform>();
            canvasRect.anchoredPosition3D = new Vector3(1f, 0.5f, 2f);
            Vector3 originalViewportPosition = camera.WorldToViewportPoint(
                canvasObject.transform.position);

            BattleWorldCanvasDepthOffset depthOffset =
                canvasObject.AddComponent<BattleWorldCanvasDepthOffset>();
            depthOffset.SetTargetCamera(camera);

            Vector3 offsetViewportPosition = camera.WorldToViewportPoint(
                canvasObject.transform.position);
            Assert.That(offsetViewportPosition.x,
                Is.EqualTo(originalViewportPosition.x).Within(0.0001f));
            Assert.That(offsetViewportPosition.y,
                Is.EqualTo(originalViewportPosition.y).Within(0.0001f));
            Assert.That(offsetViewportPosition.z,
                Is.EqualTo(originalViewportPosition.z
                    - depthOffset.CameraDepthOffset).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void WorldCanvasProjectionRunsAfterCombatCameraShake()
    {
        DefaultExecutionOrder cameraShakeOrder =
            typeof(CombatCameraShake).GetCustomAttribute<
                DefaultExecutionOrder>();
        DefaultExecutionOrder canvasProjectionOrder =
            typeof(BattleWorldCanvasDepthOffset).GetCustomAttribute<
                DefaultExecutionOrder>();

        Assert.That(cameraShakeOrder, Is.Not.Null);
        Assert.That(canvasProjectionOrder, Is.Not.Null);
        Assert.That(
            canvasProjectionOrder.order,
            Is.GreaterThan(cameraShakeOrder.order));
    }

    [Test]
    public void ActorProjectionRunsAfterCameraShakeInStableOrder()
    {
        DefaultExecutionOrder cameraShakeOrder =
            typeof(CombatCameraShake).GetCustomAttribute<
                DefaultExecutionOrder>();
        DefaultExecutionOrder billboardOrder =
            typeof(BattleSpriteBillboard).GetCustomAttribute<
                DefaultExecutionOrder>();
        DefaultExecutionOrder groundingOrder =
            typeof(BattleContactShadow).GetCustomAttribute<
                DefaultExecutionOrder>();
        DefaultExecutionOrder canvasOrder =
            typeof(BattleWorldCanvasDepthOffset).GetCustomAttribute<
                DefaultExecutionOrder>();

        Assert.That(cameraShakeOrder, Is.Not.Null);
        Assert.That(billboardOrder, Is.Not.Null);
        Assert.That(groundingOrder, Is.Not.Null);
        Assert.That(canvasOrder, Is.Not.Null);
        Assert.That(billboardOrder.order,
            Is.GreaterThan(cameraShakeOrder.order));
        Assert.That(groundingOrder.order,
            Is.GreaterThan(billboardOrder.order));
        Assert.That(canvasOrder.order,
            Is.GreaterThan(groundingOrder.order));
    }

    [Test]
    public void EnemyHealthBarImpactShakeKeepsItsHorizontalAnchor()
    {
        Vector2 offset = EnemyHealthBarFeedback.ResolveImpactShakeOffset(
            new Vector2(1f, 0.75f),
            new Vector2(0.12f, 0.045f),
            1.4f,
            0.25f);

        Assert.That(offset.x, Is.Zero.Within(0.0001f));
        Assert.That(offset.y, Is.Not.Zero);
    }

    [Test]
    public void WorldCanvasFacesCameraWithoutChangingItsScreenSize()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject ownerObject = new GameObject("Enemy");
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform));

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 1f;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 9.3f, -12f),
                Quaternion.Euler(35f, 0f, 0f));
            canvasObject.transform.SetParent(ownerObject.transform, false);
            RectTransform canvasRect =
                canvasObject.GetComponent<RectTransform>();
            canvasRect.localScale = Vector3.one;

            BattleWorldCanvasDepthOffset depthOffset =
                canvasObject.AddComponent<BattleWorldCanvasDepthOffset>();
            depthOffset.SetTargetCamera(camera);

            Vector3 center = camera.WorldToViewportPoint(
                canvasRect.position);
            Vector3 right = camera.WorldToViewportPoint(
                canvasRect.TransformPoint(Vector3.right));
            Vector3 up = camera.WorldToViewportPoint(
                canvasRect.TransformPoint(Vector3.up));
            float projectedWidth = Vector2.Distance(center, right);
            float projectedHeight = Vector2.Distance(center, up);

            Assert.That(
                projectedWidth,
                Is.EqualTo(projectedHeight).Within(0.0001f));
            Assert.That(
                Vector3.Dot(
                    canvasRect.right,
                    cameraObject.transform.right),
                Is.GreaterThan(0.999f));
            Assert.That(
                Vector3.Dot(
                    canvasRect.up,
                    cameraObject.transform.up),
                Is.GreaterThan(0.999f));
            Assert.That(
                canvasRect.localScale.x,
                Is.EqualTo(Mathf.Cos(35f * Mathf.Deg2Rad))
                    .Within(0.0001f));
            Assert.That(
                canvasRect.localScale.y,
                Is.EqualTo(Mathf.Cos(35f * Mathf.Deg2Rad))
                    .Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void PerspectiveWorldCanvasDepthOffsetPreservesScreenSize()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject ownerObject = new GameObject("Enemy");
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform));

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            camera.aspect = 16f / 9f;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 9.3f, -12f),
                Quaternion.Euler(35f, 0f, 0f));
            canvasObject.transform.SetParent(ownerObject.transform, false);
            RectTransform canvasRect =
                canvasObject.GetComponent<RectTransform>();
            canvasRect.anchoredPosition3D = new Vector3(1f, 0.5f, 2f);

            Vector3 originalCenter = camera.WorldToViewportPoint(
                canvasRect.position);
            Vector3 originalUp = camera.WorldToViewportPoint(
                canvasRect.TransformPoint(Vector3.up));
            float originalHeight = Vector2.Distance(
                originalCenter,
                originalUp);

            BattleWorldCanvasDepthOffset depthOffset =
                canvasObject.AddComponent<BattleWorldCanvasDepthOffset>();
            depthOffset.SetTargetCamera(camera);

            Vector3 offsetCenter = camera.WorldToViewportPoint(
                canvasRect.position);
            Vector3 offsetUp = camera.WorldToViewportPoint(
                canvasRect.TransformPoint(Vector3.up));
            float offsetHeight = Vector2.Distance(offsetCenter, offsetUp);

            Assert.That(offsetCenter.x,
                Is.EqualTo(originalCenter.x).Within(0.0001f));
            Assert.That(offsetCenter.y,
                Is.EqualTo(originalCenter.y).Within(0.0001f));
            Assert.That(offsetHeight,
                Is.EqualTo(originalHeight).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void WorldCanvasDepthOffsetPreservesRuntimeCounterFlip()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject ownerObject = new GameObject("Enemy");
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform));

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 9.3f, -12f),
                Quaternion.Euler(35f, 0f, 0f));
            canvasObject.transform.SetParent(ownerObject.transform, false);

            BattleWorldCanvasDepthOffset depthOffset =
                canvasObject.AddComponent<BattleWorldCanvasDepthOffset>();
            depthOffset.SetTargetCamera(camera);

            ownerObject.transform.localScale = new Vector3(-1f, 1f, 1f);
            Vector3 counterFlippedScale = canvasObject.transform.localScale;
            counterFlippedScale.x = -Mathf.Abs(counterFlippedScale.x);
            canvasObject.transform.localScale = counterFlippedScale;
            depthOffset.SetTargetCamera(camera);

            Assert.That(canvasObject.transform.localScale.x, Is.LessThan(0f));
            Assert.That(canvasObject.transform.lossyScale.x, Is.GreaterThan(0f));
            Assert.That(
                Vector3.Dot(
                    canvasObject.transform.right,
                    cameraObject.transform.right),
                Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void PerspectiveProjectionUtilityMatchesUnityViewportProjection()
    {
        GameObject cameraObject = new GameObject("Battle Camera");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            camera.aspect = 16f / 9f;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 9.3f, -12f),
                Quaternion.Euler(35f, 0f, 0f));
            Vector3 worldPosition = new Vector3(7f, 0.03f, 0.46f);

            float resolved = BattleCameraProjectionUtility.ResolveViewportX(
                camera,
                worldPosition,
                camera.transform.position);
            float expected = camera.WorldToViewportPoint(worldPosition).x;

            Assert.That(resolved, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(
                BattleCameraProjectionUtility.ResolveWorldWidth(
                    camera,
                    worldPosition),
                Is.GreaterThan(0f));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void PerspectiveEdgeFocusPlacesTileAtAuthoredViewportInset()
    {
        GameObject cameraObject = new GameObject("Battle Camera");

        try
        {
            const float desiredInset = 0.08f;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            camera.aspect = 16f / 9f;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 9.3f, -12f),
                Quaternion.Euler(35f, 0f, 0f));
            Vector3 leftTile = new Vector3(-7f, 0.03f, -0.46f);
            float viewWidth = BattleCameraProjectionUtility.ResolveWorldWidth(
                camera,
                leftTile);
            float focusedCameraX = leftTile.x
                - (desiredInset - 0.5f) * viewWidth;
            Vector3 focusedCameraPosition = camera.transform.position;
            focusedCameraPosition.x = focusedCameraX;

            float viewportX = BattleCameraProjectionUtility.ResolveViewportX(
                camera,
                leftTile,
                focusedCameraPosition);

            Assert.That(viewportX,
                Is.EqualTo(desiredInset).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void TerrainDepthOfFieldEndsAtTheTerrainBoundary()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject terrainObject = Terrain.CreateTerrainGameObject(
            new TerrainData
            {
                size = new Vector3(100f, 17.6f, 100f)
            });

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 3.5f, -12f),
                Quaternion.Euler(9f, 0f, 0f));
            terrainObject.transform.position =
                new Vector3(-52.8f, -0.12f, -7f);

            float farDepth = BattleWorld3DController
                .ResolveTerrainFarDepth(
                    camera,
                    terrainObject.GetComponent<Terrain>());

            Assert.That(farDepth, Is.GreaterThan(100f));
            Assert.That(farDepth, Is.LessThan(106f));
        }
        finally
        {
            Object.DestroyImmediate(
                terrainObject.GetComponent<Terrain>().terrainData);
            Object.DestroyImmediate(terrainObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void BattleTrainRouteUsesSceneAuthoredPointsAndTimings()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(
                BattleScenePath,
                OpenSceneMode.Single);
            GameObject environment = FindSceneObject(
                scene,
                "##--ENVIRONMENT--##");
            GameObject train = FindSceneObject(scene, "Train");
            GameObject startPoint = FindSceneObject(
                scene,
                "Train_StartPoint");
            GameObject firstDestination = FindSceneObject(
                scene,
                "Train_FirstDestination");
            GameObject secondPoint = FindSceneObject(
                scene,
                "Train_SecondPoint");
            GameObject secondDestination = FindSceneObject(
                scene,
                "Train_SecondDestination");

            Assert.That(environment, Is.Not.Null);
            Assert.That(train, Is.Not.Null);
            Assert.That(startPoint, Is.Not.Null);
            Assert.That(firstDestination, Is.Not.Null);
            Assert.That(secondPoint, Is.Not.Null);
            Assert.That(secondDestination, Is.Not.Null);
            Assert.That(train.transform.parent, Is.EqualTo(environment.transform));
            Assert.That(
                startPoint.transform.parent,
                Is.EqualTo(environment.transform));
            Assert.That(
                firstDestination.transform.parent,
                Is.EqualTo(environment.transform));
            Assert.That(
                secondPoint.transform.parent,
                Is.EqualTo(environment.transform));
            Assert.That(
                secondDestination.transform.parent,
                Is.EqualTo(environment.transform));

            BattleTrainRouteController route =
                train.GetComponent<BattleTrainRouteController>();
            Assert.That(route, Is.Not.Null);

            SerializedObject serializedRoute = new SerializedObject(route);
            Assert.That(
                serializedRoute.FindProperty("startPoint")
                    .objectReferenceValue,
                Is.SameAs(startPoint.transform));
            Assert.That(
                serializedRoute.FindProperty("firstDestination")
                    .objectReferenceValue,
                Is.SameAs(firstDestination.transform));
            Assert.That(
                serializedRoute.FindProperty("secondPoint")
                    .objectReferenceValue,
                Is.SameAs(secondPoint.transform));
            Assert.That(
                serializedRoute.FindProperty("secondDestination")
                    .objectReferenceValue,
                Is.SameAs(secondDestination.transform));
            Assert.That(
                serializedRoute.FindProperty("travelDuration").floatValue,
                Is.EqualTo(35f).Within(0.0001f));
            Assert.That(
                serializedRoute.FindProperty("destinationWaitDuration")
                    .floatValue,
                Is.EqualTo(10f).Within(0.0001f));
            Assert.That(
                train.transform.position,
                Is.EqualTo(startPoint.transform.position));
            Assert.That(train.transform.localScale.x, Is.GreaterThan(0f));
        }
        finally
        {
            if (System.Array.Exists(
                    originalSetup,
                    setup => setup.isLoaded))
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
            else
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
            }
        }
    }

    [Test]
    public void BattleSceneContainsConfigured3DWorldAndPreservesBackgroundState()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(
                BattleScenePath,
                OpenSceneMode.Single);
            GameObject background = FindSceneObject(
                scene,
                "##--BACKGROUNDS--##");
            GameObject environment = FindSceneObject(
                scene,
                "##--ENVIRONMENT--##");
            GameObject board = FindSceneObject(scene, "##--BOARDS--##");
            GameObject player = FindSceneObject(scene, "Player");
            Camera battleCamera = FindSceneObject(scene, "Main Camera")
                .GetComponent<Camera>();

            Assert.That(background, Is.Not.Null);
            Assert.That(background.activeSelf, Is.False);
            Assert.That(environment, Is.Not.Null);
            Assert.That(
                environment.GetComponent<BattleWorld3DController>(),
                Is.Not.Null);

            Terrain terrain = Object.FindFirstObjectByType<Terrain>(
                FindObjectsInactive.Include);
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain.gameObject.scene.path,
                Is.EqualTo(BattleScenePath));
            Assert.That(AssetDatabase.Contains(terrain.terrainData), Is.True);
            Assert.That(terrain.terrainData.size.x, Is.GreaterThanOrEqualTo(36f));
            Assert.That(terrain.terrainData.size.y, Is.GreaterThan(0f));
            Assert.That(terrain.terrainData.size.z, Is.GreaterThanOrEqualTo(14f));
            Assert.That(terrain.gameObject.activeSelf, Is.True);
            Assert.That(terrain.enabled, Is.True);
            Assert.That(terrain.drawHeightmap, Is.True);
            Assert.That(
                terrain.editorRenderFlags,
                Is.EqualTo(TerrainRenderFlags.All));
            Assert.That(
                Object.FindFirstObjectByType<Light>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Light directionalLight = FindSceneObject(
                    scene,
                    "Directional Light | Battle 3D")
                .GetComponent<Light>();
            Assert.That(directionalLight.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(RenderSettings.sun, Is.SameAs(directionalLight));
            Assert.That(directionalLight.intensity,
                Is.EqualTo(1.65f).Within(0.0001f));
            Assert.That(
                FindSceneObject(scene, "Directional Light | Cool Fill"),
                Is.Null);
            Assert.That(
                FindSceneObject(scene, "Global Light 2D"),
                Is.Null);
            Light saloonAccent = FindSceneObject(
                    scene,
                    "Point Light | Saloon Windows")
                .GetComponent<Light>();
            Light churchAccent = FindSceneObject(
                    scene,
                    "Point Light | Church Windows")
                .GetComponent<Light>();
            Assert.That(saloonAccent.type, Is.EqualTo(LightType.Point));
            Assert.That(saloonAccent.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(churchAccent.type, Is.EqualTo(LightType.Point));
            Assert.That(churchAccent.shadows, Is.EqualTo(LightShadows.None));

            Assert.That(FindSceneObject(scene, "Props | Battle"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "Architecture"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "Ground Detail"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "Background"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "Foreground"), Is.Not.Null);
            Assert.That(FindSceneObject(scene, "Environment FX"), Is.Not.Null);
            GameObject dust = FindSceneObject(
                scene,
                "Atmosphere | Floating Dust");
            Assert.That(dust, Is.Not.Null);
            Assert.That(dust.GetComponent<ParticleSystem>(), Is.Not.Null);
            Assert.That(
                dust.GetComponent<ParticleSystemRenderer>().sharedMaterial,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>(
                    DustMaterialPath)));
            Assert.That(
                FindSceneObject(scene, "Set Dressing | Saloon Barrels"),
                Is.Not.Null);
            Assert.That(
                FindSceneObject(scene, "Set Dressing | Left Cactus"),
                Is.Not.Null);
            MeshRenderer churchGlass = FindSceneObject(
                    scene,
                    "SM_Bld_Church_Glass_01")
                .GetComponent<MeshRenderer>();
            Assert.That(
                churchGlass.sharedMaterial,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>(
                    WindowGlowMaterialPath)));
            Assert.That(
                Object.FindFirstObjectByType<ReflectionProbe>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            LightProbeGroup lightProbes =
                Object.FindFirstObjectByType<LightProbeGroup>(
                    FindObjectsInactive.Include);
            Assert.That(lightProbes, Is.Not.Null);
            Assert.That(lightProbes.probePositions, Has.Length.EqualTo(18));

            Assert.That(board, Is.Not.Null);
            Assert.That(
                Quaternion.Angle(
                    board.transform.localRotation,
                    Quaternion.Euler(90f, 0f, 0f)),
                Is.LessThan(0.01f));

            Assert.That(battleCamera, Is.Not.Null);
            Assert.That(battleCamera.orthographic, Is.False);
            Assert.That(battleCamera.fieldOfView, Is.InRange(1f, 179f));
            Assert.That(battleCamera.orthographicSize, Is.GreaterThan(0f));
            Assert.That(battleCamera.clearFlags,
                Is.EqualTo(CameraClearFlags.Skybox));
            Assert.That(battleCamera.allowHDR, Is.True);
            CinemachineFollow authoredFollow =
                battleCamera.GetComponent<CinemachineFollow>();
            CinemachineCamera authoredCinemachineCamera =
                battleCamera.GetComponent<CinemachineCamera>();
            Assert.That(authoredFollow, Is.Not.Null);
            Assert.That(authoredCinemachineCamera, Is.Not.Null);
            Assert.That(authoredCinemachineCamera.Follow, Is.Not.Null);
            Assert.That(
                authoredCinemachineCamera.Lens.ModeOverride,
                Is.EqualTo(LensSettings.OverrideModes.Perspective));
            Assert.That(
                authoredCinemachineCamera.Lens.FieldOfView,
                Is.EqualTo(battleCamera.fieldOfView).Within(0.0001f));
            Assert.That(
                authoredCinemachineCamera.Lens.OrthographicSize,
                Is.EqualTo(battleCamera.orthographicSize).Within(0.0001f));
            Assert.That(
                authoredCinemachineCamera.Lens.NearClipPlane,
                Is.EqualTo(battleCamera.nearClipPlane).Within(0.0001f));
            Assert.That(
                authoredCinemachineCamera.Lens.FarClipPlane,
                Is.EqualTo(battleCamera.farClipPlane).Within(0.0001f));
            Assert.That(
                authoredFollow.FollowOffset,
                Is.EqualTo(
                    battleCamera.transform.position
                    - authoredCinemachineCamera.Follow.position));
            UniversalAdditionalCameraData cameraData =
                battleCamera.GetUniversalAdditionalCameraData();
            SerializedObject serializedCameraData =
                new SerializedObject(cameraData);
            Assert.That(
                serializedCameraData.FindProperty("m_RendererIndex").intValue,
                Is.EqualTo(1));
            Assert.That(cameraData.renderPostProcessing, Is.True);
            Assert.That(cameraData.requiresDepthTexture, Is.True);
            Assert.That(cameraData.requiresColorTexture, Is.True);
            Assert.That(
                cameraData.antialiasing,
                Is.EqualTo(AntialiasingMode
                    .SubpixelMorphologicalAntiAliasing));
            int uiLayer = LayerMask.NameToLayer("UI");
            Assert.That(uiLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                (battleCamera.cullingMask & (1 << uiLayer)),
                Is.Zero);
            BattleUiOverlayCamera uiOverlay = battleCamera
                .GetComponentInChildren<BattleUiOverlayCamera>(true);
            Assert.That(uiOverlay, Is.Not.Null);
            Camera overlayCamera = uiOverlay.GetComponent<Camera>();
            UniversalAdditionalCameraData overlayData =
                overlayCamera.GetUniversalAdditionalCameraData();
            Assert.That(overlayCamera.cullingMask, Is.EqualTo(1 << uiLayer));
            Assert.That(
                overlayData.renderType,
                Is.EqualTo(CameraRenderType.Overlay));
            Assert.That(overlayData.renderPostProcessing, Is.False);
            Assert.That(cameraData.cameraStack, Contains.Item(overlayCamera));
            UnityEngine.Rendering.Volume battleVolume =
                Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>(
                    FindObjectsInactive.Include);
            Assert.That(battleVolume, Is.Not.Null);
            Assert.That(
                battleVolume.sharedProfile,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                    BattleVolumeProfilePath)));
            Assert.That(
                (cameraData.volumeLayerMask.value
                    & (1 << battleVolume.gameObject.layer)) != 0,
                Is.True);
            Assert.That(
                RenderSettings.skybox,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>(
                    SkyboxMaterialPath)));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Skybox));
            Assert.That(RenderSettings.fog, Is.True);

            GameObject gameplayCanvas = FindRootObject(scene, "Canvas");
            GameObject gameStartCanvas = FindRootObject(
                scene,
                "Canvas | Game Start");
            Assert.That(gameplayCanvas, Is.Not.Null);
            Assert.That(gameStartCanvas, Is.Not.Null);
            Assert.That(
                gameplayCanvas.GetComponent<Canvas>().renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(
                gameStartCanvas.GetComponent<Canvas>().renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));

            Transform avatar = player.transform.Find("Avatar");
            Assert.That(avatar, Is.Not.Null);
            CinemachineCamera cinemachineCamera =
                battleCamera.GetComponent<CinemachineCamera>();
            Assert.That(cinemachineCamera, Is.Not.Null);
            Assert.That(cinemachineCamera.Follow, Is.SameAs(player.transform));
            Assert.That(
                cinemachineCamera.Lens.ModeOverride,
                Is.EqualTo(LensSettings.OverrideModes.Perspective));
            Assert.That(cinemachineCamera.Lens.FieldOfView,
                Is.EqualTo(battleCamera.fieldOfView).Within(0.0001f));
            Assert.That(
                avatar.GetComponent<BattleSpriteBillboard>(),
                Is.Not.Null);
        }
        finally
        {
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject result = FindDescendant(root.transform, objectName);
            if (result != null)
            {
                return result;
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
        Transform current,
        string objectName)
    {
        if (current.name == objectName)
        {
            return current.gameObject;
        }

        foreach (Transform child in current)
        {
            GameObject result = FindDescendant(child, objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
