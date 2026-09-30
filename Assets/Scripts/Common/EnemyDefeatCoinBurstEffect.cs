using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class EnemyDefeatCoinBurstEffect : MonoBehaviour
{
    private struct CoinParticle
    {
        public Transform Transform;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public Vector3 BaseScale;
        public float Age;
        public float Lifetime;
    }

    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private Material coinMaterial;
    [Min(1)]
    [SerializeField] private int baseCoinCount = 10;
    [Min(0f)]
    [SerializeField] private float comboGrowthRate = 0.2f;
    [SerializeField] private bool playOnStart = true;
    [Min(1)]
    [SerializeField] private int previewComboKillCount = 1;
    [SerializeField] private bool destroyOnComplete = true;
    [Header("Playback")]
    [Min(0.01f)]
    [SerializeField] private float playbackSpeed = 1f;
    [Min(0.01f)]
    [SerializeField] private float rootScale = 1f;

    [Header("Fountain Motion")]
    [SerializeField] private Vector2 lifetimeRange = new Vector2(1.05f, 1.35f);
    [SerializeField] private Vector2 horizontalSpeedRange = new Vector2(0.7f, 1.45f);
    [SerializeField] private Vector2 upwardSpeedRange = new Vector2(2.5f, 3.5f);
    [Min(0f)]
    [SerializeField] private float gravity = 6.2f;
    [Min(0f)]
    [SerializeField] private float spawnRadius = 0.06f;
    [Min(0f)]
    [SerializeField] private float depthScatter = 0.09f;

    [Header("Coin Appearance")]
    [SerializeField] private Vector2 coinScaleRange = new Vector2(18f, 24f);
    [SerializeField] private Vector2 angularSpeedRange = new Vector2(420f, 840f);
    [Range(0.05f, 1f)]
    [SerializeField] private float shrinkDurationRatio = 0.24f;

    private readonly List<CoinParticle> particles = new List<CoinParticle>();
    private Vector3 gravityDirection = Vector3.down;
    private float activeRootScale = 1f;
    private bool playRequested;
    private bool isPlaying;

    public void Play(int comboKillCount = 1)
    {
        PlayInternal(comboKillCount, Camera.main);
    }

    public void PlayAt(Vector3 worldPosition, int comboKillCount = 1)
    {
        transform.position = worldPosition;
        Play(comboKillCount);
    }

    private void PlayInternal(int comboKillCount, Camera effectCamera)
    {
        playRequested = true;
        ClearParticles();
        activeRootScale = GetRootScale();
        transform.localScale = Vector3.one * activeRootScale;

        if (coinPrefab == null)
        {
            Debug.LogWarning(
                "Enemy defeat coin burst cannot play without a coin prefab.",
                this);
            Complete();
            return;
        }

        int coinCount = CalculateCoinCount(
            baseCoinCount,
            comboGrowthRate,
            comboKillCount);
        int seed = unchecked(Environment.TickCount * 397 ^ GetInstanceID());
        System.Random random = new System.Random(seed);
        ResolveCameraAxes(
            effectCamera,
            out Vector3 horizontalAxis,
            out Vector3 upwardAxis,
            out Vector3 depthAxis);
        gravityDirection = -upwardAxis;

        for (int index = 0; index < coinCount; index++)
        {
            SpawnCoin(
                random,
                horizontalAxis,
                upwardAxis,
                depthAxis);
        }

        isPlaying = particles.Count > 0;

        if (!isPlaying)
        {
            Complete();
        }
    }

    private void Start()
    {
        if (playOnStart && !playRequested)
        {
            Play(previewComboKillCount);
        }
    }

    private void Update()
    {
        if (!isPlaying || GamePauseController.IsPaused)
        {
            return;
        }

        AdvanceSimulation(Time.unscaledDeltaTime * GetPlaybackSpeed());
    }

    private void AdvanceSimulation(float deltaTime)
    {
        Vector3 acceleration = gravityDirection * gravity * activeRootScale;

        for (int index = particles.Count - 1; index >= 0; index--)
        {
            CoinParticle particle = particles[index];
            Transform coinTransform = particle.Transform;

            if (coinTransform == null)
            {
                particles.RemoveAt(index);
                continue;
            }

            particle.Age += deltaTime;

            if (particle.Age >= particle.Lifetime)
            {
                DestroyCoin(coinTransform.gameObject);
                particles.RemoveAt(index);
                continue;
            }

            particle.Velocity += acceleration * deltaTime;
            coinTransform.position += particle.Velocity * deltaTime;
            coinTransform.Rotate(
                particle.AngularVelocity * deltaTime,
                Space.Self);

            float remainingRatio = 1f - particle.Age / particle.Lifetime;
            float scaleRatio = Mathf.Clamp01(
                remainingRatio / shrinkDurationRatio);
            scaleRatio = scaleRatio * scaleRatio * (3f - 2f * scaleRatio);
            coinTransform.localScale = particle.BaseScale * scaleRatio;
            particles[index] = particle;
        }

        if (particles.Count == 0)
        {
            Complete();
        }
    }

    private void SpawnCoin(
        System.Random random,
        Vector3 horizontalAxis,
        Vector3 upwardAxis,
        Vector3 depthAxis)
    {
        float spawnAngle = Range(random, 0f, Mathf.PI * 2f);
        float spawnDistance = Mathf.Sqrt(Range(random, 0f, 1f))
            * spawnRadius
            * activeRootScale;
        Vector3 spawnOffset =
            horizontalAxis * (Mathf.Cos(spawnAngle) * spawnDistance)
            + upwardAxis * (Mathf.Sin(spawnAngle) * spawnDistance)
            + depthAxis * Range(random, -depthScatter, depthScatter)
                * activeRootScale;
        GameObject coin = Instantiate(
            coinPrefab,
            transform.position + spawnOffset,
            RandomRotation(random),
            transform);
        coin.name = coinPrefab.name;
        DisablePhysicsAndShadows(coin);

        float horizontalDirection = Range(random, -1f, 1f);
        float horizontalSpeed = Range(
            random,
            horizontalSpeedRange.x,
            horizontalSpeedRange.y);
        float scale = Range(
            random,
            coinScaleRange.x,
            coinScaleRange.y);
        Vector3 rotationAxis = RandomUnitVector(random);
        float angularSpeed = Range(
            random,
            angularSpeedRange.x,
            angularSpeedRange.y);

        coin.transform.localScale = Vector3.one * scale;
        particles.Add(new CoinParticle
        {
            Transform = coin.transform,
            Velocity = (
                horizontalAxis * (horizontalDirection * horizontalSpeed)
                + upwardAxis * Range(
                    random,
                    upwardSpeedRange.x,
                    upwardSpeedRange.y)
                + depthAxis * Range(random, -depthScatter, depthScatter))
                * activeRootScale,
            AngularVelocity = rotationAxis * angularSpeed,
            BaseScale = coin.transform.localScale,
            Lifetime = Range(random, lifetimeRange.x, lifetimeRange.y)
        });
    }

    private void Complete()
    {
        isPlaying = false;

        if (destroyOnComplete && Application.isPlaying)
        {
            Destroy(gameObject);
            return;
        }

        ClearParticles();
    }

    private void ClearParticles()
    {
        foreach (CoinParticle particle in particles)
        {
            if (particle.Transform == null)
            {
                continue;
            }

            DestroyCoin(particle.Transform.gameObject);
        }

        particles.Clear();
        isPlaying = false;
    }

    private static void DestroyCoin(GameObject coin)
    {
        if (Application.isPlaying)
        {
            Destroy(coin);
        }
        else
        {
            DestroyImmediate(coin);
        }
    }

    private void DisablePhysicsAndShadows(GameObject coin)
    {
        foreach (Collider collider in coin.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Collider2D collider in coin.GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody body in coin.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        foreach (Rigidbody2D body in coin.GetComponentsInChildren<Rigidbody2D>(true))
        {
            body.simulated = false;
        }

        foreach (Renderer renderer in coin.GetComponentsInChildren<Renderer>(true))
        {
            if (coinMaterial != null)
            {
                renderer.sharedMaterial = coinMaterial;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static void ResolveCameraAxes(
        Camera camera,
        out Vector3 horizontalAxis,
        out Vector3 upwardAxis,
        out Vector3 depthAxis)
    {
        if (camera == null)
        {
            horizontalAxis = Vector3.right;
            upwardAxis = Vector3.up;
            depthAxis = Vector3.forward;
            return;
        }

        horizontalAxis = camera.transform.right;
        upwardAxis = camera.transform.up;
        depthAxis = camera.transform.forward;
    }

    private static Quaternion RandomRotation(System.Random random)
    {
        return Quaternion.Euler(
            Range(random, 0f, 360f),
            Range(random, 0f, 360f),
            Range(random, 0f, 360f));
    }

    private static Vector3 RandomUnitVector(System.Random random)
    {
        Vector3 value = new Vector3(
            Range(random, -1f, 1f),
            Range(random, -1f, 1f),
            Range(random, -1f, 1f));
        return value.sqrMagnitude <= Mathf.Epsilon
            ? Vector3.up
            : value.normalized;
    }

    private static float Range(
        System.Random random,
        float minimum,
        float maximum)
    {
        if (maximum < minimum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        return minimum
            + (float)random.NextDouble() * (maximum - minimum);
    }

    internal static int CalculateCoinCount(
        int configuredBaseCoinCount,
        float configuredComboGrowthRate,
        int comboKillCount)
    {
        int safeBaseCount = Mathf.Max(1, configuredBaseCoinCount);
        float safeGrowthRate = Mathf.Max(0f, configuredComboGrowthRate);
        int additionalKills = Mathf.Max(0, comboKillCount - 1);
        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                safeBaseCount
                * (1f + safeGrowthRate * additionalKills)));
    }

#if UNITY_EDITOR
    internal void ConfigureForEditor(
        GameObject configuredCoinPrefab,
        Material configuredCoinMaterial)
    {
        coinPrefab = configuredCoinPrefab;
        coinMaterial = configuredCoinMaterial;
        baseCoinCount = 10;
        comboGrowthRate = 0.2f;
        playOnStart = true;
        previewComboKillCount = 1;
        destroyOnComplete = true;
        playbackSpeed = 1f;
        rootScale = 1f;
        lifetimeRange = new Vector2(1.05f, 1.35f);
        horizontalSpeedRange = new Vector2(0.7f, 1.45f);
        upwardSpeedRange = new Vector2(2.5f, 3.5f);
        gravity = 6.2f;
        spawnRadius = 0.06f;
        depthScatter = 0.09f;
        coinScaleRange = new Vector2(18f, 24f);
        angularSpeedRange = new Vector2(420f, 840f);
        shrinkDurationRatio = 0.24f;
    }

    internal void PlayForEditorPreview(
        Vector3 worldPosition,
        int comboKillCount,
        Camera previewCamera)
    {
        transform.position = worldPosition;
        PlayInternal(comboKillCount, previewCamera);
    }

    internal bool AdvanceEditorPreview(float deltaTime)
    {
        if (isPlaying)
        {
            AdvanceSimulation(
                Mathf.Max(0f, deltaTime) * GetPlaybackSpeed());
        }

        return isPlaying;
    }
#endif

    private float GetPlaybackSpeed()
    {
        return Mathf.Max(0.01f, playbackSpeed);
    }

    private float GetRootScale()
    {
        return Mathf.Max(0.01f, rootScale);
    }
}
