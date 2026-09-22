using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CombatImpactParticlePrefabBuilder
{
    private const string PrefabDirectory = "Assets/Prefabs/VFX";
    private const string MaterialDirectory = "Assets/Materials/VFX";
    private const string PlayerPrefabPath =
        "Assets/Prefabs/Player/Player.prefab";
    private const string NormalPrefabPath =
        PrefabDirectory + "/VFX_EnemyHit.prefab";
    private const string CriticalPrefabPath =
        PrefabDirectory + "/VFX_EnemyCriticalHit.prefab";
    private const string DefeatPrefabPath =
        PrefabDirectory + "/VFX_EnemyDefeat.prefab";

    [MenuItem("Tools/LOADED/Rebuild Combat Impact Particles")]
    public static void Build()
    {
        EnsureFolder("Assets/Prefabs", "VFX");
        EnsureFolder("Assets/Materials", "VFX");

        Shader shader = Shader.Find("Loaded/Combat Impact Particle");

        if (shader == null)
        {
            throw new System.InvalidOperationException(
                "Loaded/Combat Impact Particle shader was not imported.");
        }

        Material sparkMaterial = CreateOrUpdateMaterial(
            MaterialDirectory + "/CombatImpactSpark.mat",
            shader,
            0f,
            0.08f);
        Material dustMaterial = CreateOrUpdateMaterial(
            MaterialDirectory + "/CombatImpactDust.mat",
            shader,
            1f,
            0.2f);
        Material ringMaterial = CreateOrUpdateMaterial(
            MaterialDirectory + "/CombatImpactRing.mat",
            shader,
            2f,
            0.08f);

        BuildNormalPrefab(sparkMaterial, dustMaterial);
        BuildCriticalPrefab(sparkMaterial, dustMaterial, ringMaterial);
        BuildDefeatPrefab(sparkMaterial, dustMaterial);
        WirePlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Rebuilt combat impact Particle System prefabs.");
    }

    public static void BuildFromCommandLine()
    {
        Build();
    }

    private static void BuildNormalPrefab(
        Material sparkMaterial,
        Material dustMaterial)
    {
        GameObject root = CreateRoot("VFX_EnemyHit");
        CombatImpactParticleEffect effect =
            root.GetComponent<CombatImpactParticleEffect>();
        List<CombatImpactParticleEffect.Layer> layers =
            new List<CombatImpactParticleEffect.Layer>
            {
                CreateLayer(
                    root.transform,
                    "Hit Sparks",
                    sparkMaterial,
                    6,
                    CombatImpactParticleEffect.ColorSource.HotAccent,
                    0.13f,
                    0.22f,
                    1.1f,
                    2.6f,
                    0.025f,
                    0.055f,
                    30f,
                    0.22f,
                    true,
                    false,
                    2),
                CreateLayer(
                    root.transform,
                    "Hit Dust",
                    dustMaterial,
                    3,
                    CombatImpactParticleEffect.ColorSource.Dust,
                    0.24f,
                    0.38f,
                    0.25f,
                    0.75f,
                    0.055f,
                    0.11f,
                    52f,
                    0.05f,
                    false,
                    true,
                    0)
            };
        effect.ConfigureForEditor(layers.ToArray(), 0.16f);
        SavePrefab(root, NormalPrefabPath);
    }

    private static void BuildCriticalPrefab(
        Material sparkMaterial,
        Material dustMaterial,
        Material ringMaterial)
    {
        GameObject root = CreateRoot("VFX_EnemyCriticalHit");
        CombatImpactParticleEffect effect =
            root.GetComponent<CombatImpactParticleEffect>();
        List<CombatImpactParticleEffect.Layer> layers =
            new List<CombatImpactParticleEffect.Layer>
            {
                CreateLayer(
                    root.transform,
                    "Critical Shards",
                    sparkMaterial,
                    11,
                    CombatImpactParticleEffect.ColorSource.HotAccent,
                    0.18f,
                    0.3f,
                    1.4f,
                    3.5f,
                    0.035f,
                    0.075f,
                    62f,
                    0.18f,
                    true,
                    false,
                    3),
                CreateRingLayer(
                    root.transform,
                    ringMaterial,
                    1,
                    2),
                CreateLayer(
                    root.transform,
                    "Critical Dust",
                    dustMaterial,
                    4,
                    CombatImpactParticleEffect.ColorSource.Dust,
                    0.28f,
                    0.44f,
                    0.35f,
                    0.95f,
                    0.065f,
                    0.13f,
                    68f,
                    0.03f,
                    false,
                    true,
                    0)
            };
        effect.ConfigureForEditor(layers.ToArray(), 0.18f);
        SavePrefab(root, CriticalPrefabPath);
    }

    private static void BuildDefeatPrefab(
        Material sparkMaterial,
        Material dustMaterial)
    {
        GameObject root = CreateRoot("VFX_EnemyDefeat");
        CombatImpactParticleEffect effect =
            root.GetComponent<CombatImpactParticleEffect>();
        List<CombatImpactParticleEffect.Layer> layers =
            new List<CombatImpactParticleEffect.Layer>
            {
                CreateLayer(
                    root.transform,
                    "Enemy Fragments",
                    sparkMaterial,
                    12,
                    CombatImpactParticleEffect.ColorSource.Enemy,
                    0.32f,
                    0.58f,
                    0.8f,
                    2.1f,
                    0.05f,
                    0.11f,
                    72f,
                    0.72f,
                    false,
                    false,
                    1),
                CreateLayer(
                    root.transform,
                    "Defeat Embers",
                    sparkMaterial,
                    8,
                    CombatImpactParticleEffect.ColorSource.HotAccent,
                    0.2f,
                    0.38f,
                    1.2f,
                    2.8f,
                    0.03f,
                    0.065f,
                    58f,
                    0.2f,
                    true,
                    false,
                    3),
                CreateLayer(
                    root.transform,
                    "Defeat Dust",
                    dustMaterial,
                    14,
                    CombatImpactParticleEffect.ColorSource.Dust,
                    0.45f,
                    0.72f,
                    0.35f,
                    1.2f,
                    0.08f,
                    0.18f,
                    78f,
                    -0.03f,
                    false,
                    true,
                    0)
            };
        effect.ConfigureForEditor(layers.ToArray(), 0.22f);
        SavePrefab(root, DefeatPrefabPath);
    }

    private static GameObject CreateRoot(string name)
    {
        GameObject root = new GameObject(name);
        root.AddComponent<CombatImpactParticleEffect>();
        return root;
    }

    private static CombatImpactParticleEffect.Layer CreateLayer(
        Transform parent,
        string name,
        Material material,
        int count,
        CombatImpactParticleEffect.ColorSource colorSource,
        float minimumLifetime,
        float maximumLifetime,
        float minimumSpeed,
        float maximumSpeed,
        float minimumSize,
        float maximumSize,
        float coneAngle,
        float gravity,
        bool stretched,
        bool usesNoise,
        int relativeSortingOrder)
    {
        GameObject layerObject = new GameObject(
            name,
            typeof(ParticleSystem));
        layerObject.transform.SetParent(parent, false);
        layerObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        ParticleSystem system = layerObject.GetComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.useUnscaledTime = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            minimumLifetime,
            maximumLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            minimumSpeed,
            maximumSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(
            minimumSize,
            maximumSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.55f, 0.55f);
        main.gravityModifier = gravity;
        main.maxParticles = 96;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = 0.025f;
        shape.radiusThickness = 0f;
        ConfigureColorOverLifetime(system);
        ConfigureSizeOverLifetime(system, usesNoise, false);

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = usesNoise;

        if (usesNoise)
        {
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.16f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.2f;
        }

        ParticleSystemRenderer renderer =
            layerObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingOrder = relativeSortingOrder;
        renderer.renderMode = stretched
            ? ParticleSystemRenderMode.Stretch
            : ParticleSystemRenderMode.Billboard;
        renderer.velocityScale = stretched ? 0.24f : 0f;
        renderer.lengthScale = stretched ? 1.45f : 1f;

        return new CombatImpactParticleEffect.Layer(
            system,
            count,
            colorSource);
    }

    private static CombatImpactParticleEffect.Layer CreateRingLayer(
        Transform parent,
        Material material,
        int count,
        int relativeSortingOrder)
    {
        GameObject layerObject = new GameObject(
            "Critical Ring",
            typeof(ParticleSystem));
        layerObject.transform.SetParent(parent, false);
        ParticleSystem system = layerObject.GetComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.useUnscaledTime = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.48f, 0.62f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        main.maxParticles = 4;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;
        ConfigureColorOverLifetime(system);
        ConfigureSizeOverLifetime(system, false, true);

        ParticleSystemRenderer renderer =
            layerObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = relativeSortingOrder;

        return new CombatImpactParticleEffect.Layer(
            system,
            count,
            CombatImpactParticleEffect.ColorSource.HotAccent);
    }

    private static void ConfigureColorOverLifetime(ParticleSystem system)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.78f, 0.46f),
                new GradientAlphaKey(0f, 1f)
            });
        ParticleSystem.ColorOverLifetimeModule color =
            system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void ConfigureSizeOverLifetime(
        ParticleSystem system,
        bool expands,
        bool isRing)
    {
        AnimationCurve curve;

        if (isRing)
        {
            curve = new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.18f, 0.82f),
                new Keyframe(1f, 1.55f));
        }
        else if (expands)
        {
            curve = new AnimationCurve(
                new Keyframe(0f, 0.48f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 1.22f));
        }
        else
        {
            curve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.62f, 0.82f),
                new Keyframe(1f, 0.12f));
        }

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static Material CreateOrUpdateMaterial(
        string path,
        Shader shader,
        float shapeMode,
        float softness)
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

        material.name = System.IO.Path.GetFileNameWithoutExtension(path);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_ShapeMode", shapeMode);
        material.SetFloat("_Softness", softness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void WirePlayerPrefab()
    {
        GameObject playerRoot = PrefabUtility.LoadPrefabContents(
            PlayerPrefabPath);

        try
        {
            CombatPresentation presentation =
                playerRoot.GetComponent<CombatPresentation>();

            if (presentation == null)
            {
                throw new System.InvalidOperationException(
                    "Player prefab does not contain CombatPresentation.");
            }

            SerializedObject serializedPresentation =
                new SerializedObject(presentation);
            SetPrefabReference(
                serializedPresentation,
                "normalImpactParticlePrefab",
                NormalPrefabPath);
            SetPrefabReference(
                serializedPresentation,
                "criticalImpactParticlePrefab",
                CriticalPrefabPath);
            SetPrefabReference(
                serializedPresentation,
                "defeatImpactParticlePrefab",
                DefeatPrefabPath);
            serializedPresentation.FindProperty("impactParticleDensity")
                .floatValue = 1f;
            serializedPresentation.FindProperty(
                    "normalImpactParticleSpawnScale")
                .floatValue = 1f;
            serializedPresentation.FindProperty(
                    "criticalImpactParticleSpawnScale")
                .floatValue = 1f;
            serializedPresentation.FindProperty(
                    "defeatImpactParticleSpawnScale")
                .floatValue = 1f;
            serializedPresentation.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(playerRoot);
        }
    }

    private static void SetPrefabReference(
        SerializedObject serializedPresentation,
        string propertyName,
        string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath);

        if (prefab == null)
        {
            throw new System.InvalidOperationException(
                $"Missing generated particle prefab at {prefabPath}.");
        }

        serializedPresentation.FindProperty(propertyName)
            .objectReferenceValue =
                prefab.GetComponent<CombatImpactParticleEffect>();
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
