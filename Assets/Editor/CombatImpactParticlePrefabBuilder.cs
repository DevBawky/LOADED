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
        Material ringMaterial = CreateOrUpdateMaterial(
            MaterialDirectory + "/CombatImpactRing.mat",
            shader,
            2f,
            0.08f);

        BuildNormalPrefab(sparkMaterial);
        BuildCriticalPrefab(sparkMaterial, ringMaterial);
        BuildDefeatPrefab(sparkMaterial);
        WirePlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Rebuilt combat impact Particle System prefabs.");
    }

    public static void BuildFromCommandLine()
    {
        Build();
    }

    private static void BuildNormalPrefab(Material sparkMaterial)
    {
        GameObject root = CreateRoot("VFX_EnemyHit");
        CombatImpactParticleEffect effect =
            root.GetComponent<CombatImpactParticleEffect>();
        List<CombatImpactParticleEffect.Layer> layers =
            new List<CombatImpactParticleEffect.Layer>
            {
                CreateCoreFlashLayer(
                    root.transform,
                    "Hit Core Burst",
                    sparkMaterial,
                    3,
                    0.07f,
                    0.11f,
                    0.1f,
                    0.16f,
                    4),
                CreateLayer(
                    root.transform,
                    "Hit Directional Sparks",
                    sparkMaterial,
                    11,
                    CombatImpactParticleEffect.ColorSource.BrightAccent,
                    0.12f,
                    0.2f,
                    1.25f,
                    2.55f,
                    0.032f,
                    0.07f,
                    30f,
                    0.16f,
                    true,
                    false,
                    3,
                    true),
                CreateMicroGlintLayer(
                    root.transform,
                    "Hit Micro Glint",
                    sparkMaterial,
                    3,
                    0.2f,
                    0.28f,
                    0.08f,
                    0.2f,
                    0.08f,
                    0.13f,
                    0.025f,
                    5)
            };
        effect.ConfigureForEditor(layers.ToArray(), 0.12f);
        SavePrefab(root, NormalPrefabPath);
    }

    private static void BuildCriticalPrefab(
        Material sparkMaterial,
        Material ringMaterial)
    {
        GameObject root = CreateRoot("VFX_EnemyCriticalHit");
        CombatImpactParticleEffect effect =
            root.GetComponent<CombatImpactParticleEffect>();
        List<CombatImpactParticleEffect.Layer> layers =
            new List<CombatImpactParticleEffect.Layer>
            {
                CreateCoreFlashLayer(
                    root.transform,
                    "Critical Core Burst",
                    sparkMaterial,
                    4,
                    0.09f,
                    0.15f,
                    0.14f,
                    0.22f,
                    5),
                CreateLayer(
                    root.transform,
                    "Critical Shards",
                    sparkMaterial,
                    18,
                    CombatImpactParticleEffect.ColorSource.BrightAccent,
                    0.18f,
                    0.3f,
                    1.4f,
                    3.5f,
                    0.045f,
                    0.09f,
                    62f,
                    0.12f,
                    true,
                    false,
                    4,
                    true),
                CreateRingLayer(
                    root.transform,
                    ringMaterial,
                    1,
                    3),
                CreateMicroGlintLayer(
                    root.transform,
                    "Critical Star Glints",
                    sparkMaterial,
                    6,
                    0.24f,
                    0.36f,
                    0.12f,
                    0.35f,
                    0.1f,
                    0.16f,
                    0.05f,
                    6)
            };
        effect.ConfigureForEditor(layers.ToArray(), 0.18f);
        SavePrefab(root, CriticalPrefabPath);
    }

    private static void BuildDefeatPrefab(Material sparkMaterial)
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
                    "Defeat Core Burst",
                    sparkMaterial,
                    11,
                    CombatImpactParticleEffect.ColorSource.BrightAccent,
                    0.16f,
                    0.3f,
                    1.7f,
                    3.4f,
                    0.025f,
                    0.06f,
                    74f,
                    0.12f,
                    true,
                    false,
                    4,
                    true),
                CreateFireworkBloomLayer(
                    root.transform,
                    sparkMaterial,
                    14,
                    3),
                CreateStarGlintLayer(
                    root.transform,
                    sparkMaterial,
                    5,
                    5)
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

    private static CombatImpactParticleEffect.Layer CreateCoreFlashLayer(
        Transform parent,
        string name,
        Material material,
        int count,
        float minimumLifetime,
        float maximumLifetime,
        float minimumSize,
        float maximumSize,
        int relativeSortingOrder)
    {
        GameObject layerObject = new GameObject(
            name,
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            minimumLifetime,
            maximumLifetime);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(
            minimumSize,
            maximumSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        main.maxParticles = 8;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;
        ConfigureColorOverLifetime(system);
        ConfigureSizeOverLifetime(system, false, false);

        ParticleSystemRenderer renderer =
            layerObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = relativeSortingOrder;

        return new CombatImpactParticleEffect.Layer(
            system,
            count,
            CombatImpactParticleEffect.ColorSource.BrightAccent);
    }

    private static CombatImpactParticleEffect.Layer CreateMicroGlintLayer(
        Transform parent,
        string name,
        Material material,
        int count,
        float minimumLifetime,
        float maximumLifetime,
        float minimumSpeed,
        float maximumSpeed,
        float minimumSize,
        float maximumSize,
        float radius,
        int relativeSortingOrder)
    {
        GameObject layerObject = new GameObject(
            name,
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            minimumLifetime,
            maximumLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            minimumSpeed,
            maximumSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(
            minimumSize,
            maximumSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        main.gravityModifier = -0.01f;
        main.maxParticles = 16;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        shape.radiusThickness = 0.6f;
        shape.randomDirectionAmount = 0.16f;
        ConfigureMicroGlintColor(system);
        ConfigureMicroGlintSize(system);

        ParticleSystemRenderer renderer =
            layerObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = relativeSortingOrder;

        return new CombatImpactParticleEffect.Layer(
            system,
            count,
            CombatImpactParticleEffect.ColorSource.BrightAccent);
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
        int relativeSortingOrder,
        bool usesTrails = false)
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
        ConfigureTrails(system, renderer, material, usesTrails);

        return new CombatImpactParticleEffect.Layer(
            system,
            count,
            colorSource);
    }

    private static CombatImpactParticleEffect.Layer CreateFireworkBloomLayer(
        Transform parent,
        Material material,
        int count,
        int relativeSortingOrder)
    {
        GameObject layerObject = new GameObject(
            "Defeat Firework Bloom",
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.46f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.85f, 1.85f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
        main.gravityModifier = 0.1f;
        main.maxParticles = 96;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.035f;
        shape.radiusThickness = 0f;
        shape.randomDirectionAmount = 0.08f;
        ConfigureDelayedBloomColor(system);
        ConfigureSizeOverLifetime(system, false, false);

        ParticleSystemRenderer renderer =
            layerObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = relativeSortingOrder;
        ConfigureTrails(system, renderer, material, true);

        return new CombatImpactParticleEffect.Layer(
            system,
            count,
            CombatImpactParticleEffect.ColorSource.Accent);
    }

    private static CombatImpactParticleEffect.Layer CreateStarGlintLayer(
        Transform parent,
        Material material,
        int count,
        int relativeSortingOrder)
    {
        GameObject layerObject = new GameObject(
            "Defeat Star Glints",
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.36f, 0.54f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.48f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.085f, 0.15f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        main.gravityModifier = -0.015f;
        main.maxParticles = 32;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;
        shape.radiusThickness = 0.7f;
        shape.randomDirectionAmount = 0.2f;
        ConfigureStarGlintColor(system);
        ConfigureStarGlintSize(system);

        ParticleSystemRenderer renderer =
            layerObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = relativeSortingOrder;

        return new CombatImpactParticleEffect.Layer(
            system,
            count,
            CombatImpactParticleEffect.ColorSource.BrightAccent);
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
            CombatImpactParticleEffect.ColorSource.BrightAccent);
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

    private static void ConfigureDelayedBloomColor(ParticleSystem system)
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
                new GradientAlphaKey(0f, 0.1f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(0.86f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        ParticleSystem.ColorOverLifetimeModule color =
            system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void ConfigureMicroGlintColor(ParticleSystem system)
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
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(0.92f, 0.52f),
                new GradientAlphaKey(0f, 1f)
            });
        ParticleSystem.ColorOverLifetimeModule color =
            system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void ConfigureMicroGlintSize(ParticleSystem system)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0.12f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.52f, 0.55f),
            new Keyframe(1f, 0.03f));
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static void ConfigureStarGlintColor(ParticleSystem system)
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
                new GradientAlphaKey(0f, 0.18f),
                new GradientAlphaKey(1f, 0.27f),
                new GradientAlphaKey(0.18f, 0.48f),
                new GradientAlphaKey(0.92f, 0.64f),
                new GradientAlphaKey(0f, 1f)
            });
        ParticleSystem.ColorOverLifetimeModule color =
            system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void ConfigureStarGlintSize(ParticleSystem system)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0.08f),
            new Keyframe(0.24f, 0.2f),
            new Keyframe(0.34f, 1f),
            new Keyframe(0.5f, 0.32f),
            new Keyframe(0.66f, 0.82f),
            new Keyframe(1f, 0.04f));
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static void ConfigureTrails(
        ParticleSystem system,
        ParticleSystemRenderer renderer,
        Material material,
        bool enabled)
    {
        ParticleSystem.TrailModule trails = system.trails;
        trails.enabled = enabled;

        if (!enabled)
        {
            return;
        }

        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 0.58f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
        trails.inheritParticleColor = true;
        trails.dieWithParticles = true;
        renderer.trailMaterial = material;
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
