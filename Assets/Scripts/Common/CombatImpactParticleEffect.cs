using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatImpactParticleEffect : MonoBehaviour
{
    internal enum ColorSource
    {
        Accent,
        HotAccent,
        Dust,
        Enemy
    }

    [Serializable]
    internal struct Layer
    {
        [SerializeField] private ParticleSystem particleSystem;
        [Min(1)]
        [SerializeField] private int baseEmissionCount;
        [SerializeField] private ColorSource colorSource;

        internal Layer(
            ParticleSystem particleSystem,
            int baseEmissionCount,
            ColorSource colorSource)
        {
            this.particleSystem = particleSystem;
            this.baseEmissionCount = Mathf.Max(1, baseEmissionCount);
            this.colorSource = colorSource;
        }

        internal ParticleSystem ParticleSystem => particleSystem;
        internal int BaseEmissionCount => baseEmissionCount;
        internal ColorSource Source => colorSource;
    }

    [SerializeField] private Layer[] layers = Array.Empty<Layer>();
    [Min(0f)]
    [SerializeField] private float cleanupPadding = 0.18f;

    private Action<GameObject> completionCallback;
    private float remainingLifetime;
    private bool isPlaying;
    private bool particlesPaused;

    public void Play(
        Color accent,
        Color enemyColor,
        Color dustColor,
        int horizontalDirection,
        float densityMultiplier,
        int sortingLayerId,
        int sortingOrder,
        Action<GameObject> onCompleted = null)
    {
        completionCallback = onCompleted;
        int direction = horizontalDirection < 0 ? -1 : 1;
        transform.rotation = Quaternion.Euler(
            0f,
            direction < 0 ? 180f : 0f,
            0f);
        remainingLifetime = Mathf.Max(0f, cleanupPadding);
        particlesPaused = false;
        isPlaying = false;

        foreach (Layer layer in layers)
        {
            ParticleSystem system = layer.ParticleSystem;

            if (system == null)
            {
                continue;
            }

            int emissionCount = ResolveEmissionCount(
                layer.BaseEmissionCount,
                densityMultiplier);

            if (emissionCount <= 0)
            {
                continue;
            }

            ParticleSystem.MainModule main = system.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startColor = ResolveStartColor(
                layer.Source,
                accent,
                enemyColor,
                dustColor);
            ParticleSystemRenderer renderer =
                system.GetComponent<ParticleSystemRenderer>();

            if (renderer != null)
            {
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder += sortingOrder;
            }

            system.Clear(true);
            system.Play(true);
            system.Emit(emissionCount);
            remainingLifetime = Mathf.Max(
                remainingLifetime,
                main.startDelay.constantMax
                    + main.startLifetime.constantMax
                    + cleanupPadding);
            isPlaying = true;
        }

        if (!isPlaying)
        {
            Complete();
        }
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        bool shouldPause = GamePauseController.IsPaused;

        if (shouldPause != particlesPaused)
        {
            SetParticlePause(shouldPause);
        }

        if (shouldPause)
        {
            return;
        }

        remainingLifetime -= Time.unscaledDeltaTime;

        if (remainingLifetime <= 0f)
        {
            Complete();
        }
    }

    private void SetParticlePause(bool paused)
    {
        particlesPaused = paused;

        foreach (Layer layer in layers)
        {
            ParticleSystem system = layer.ParticleSystem;

            if (system == null)
            {
                continue;
            }

            if (paused)
            {
                system.Pause(true);
            }
            else
            {
                system.Play(true);
            }
        }
    }

    private void Complete()
    {
        if (!isPlaying && completionCallback == null)
        {
            Destroy(gameObject);
            return;
        }

        isPlaying = false;
        Action<GameObject> callback = completionCallback;
        completionCallback = null;
        callback?.Invoke(gameObject);
        Destroy(gameObject);
    }

    internal static int ResolveEmissionCount(
        int baseEmissionCount,
        float densityMultiplier)
    {
        if (baseEmissionCount <= 0 || densityMultiplier <= 0f)
        {
            return 0;
        }

        return Mathf.Max(
            1,
            Mathf.RoundToInt(baseEmissionCount * densityMultiplier));
    }

    internal static ParticleSystem.MinMaxGradient ResolveStartColor(
        ColorSource source,
        Color accent,
        Color enemyColor,
        Color dustColor)
    {
        accent.a = 1f;
        enemyColor.a = 1f;
        dustColor.a = 1f;

        return source switch
        {
            ColorSource.HotAccent => new ParticleSystem.MinMaxGradient(
                Color.Lerp(accent, new Color(1f, 0.72f, 0.28f, 1f), 0.45f),
                Color.Lerp(accent, Color.white, 0.82f)),
            ColorSource.Dust => new ParticleSystem.MinMaxGradient(
                Color.Lerp(dustColor, new Color(0.18f, 0.1f, 0.05f, 1f), 0.34f),
                Color.Lerp(dustColor, accent, 0.18f)),
            ColorSource.Enemy => new ParticleSystem.MinMaxGradient(
                Color.Lerp(enemyColor, new Color(0.06f, 0.035f, 0.02f, 1f), 0.5f),
                Color.Lerp(enemyColor, accent, 0.32f)),
            _ => new ParticleSystem.MinMaxGradient(
                Color.Lerp(accent, Color.white, 0.18f),
                accent)
        };
    }

#if UNITY_EDITOR
    internal void ConfigureForEditor(
        Layer[] configuredLayers,
        float configuredCleanupPadding)
    {
        layers = configuredLayers ?? Array.Empty<Layer>();
        cleanupPadding = Mathf.Max(0f, configuredCleanupPadding);
    }
#endif
}
