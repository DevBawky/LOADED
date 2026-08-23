using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal sealed class CombatImpactSignaturePresenter
{
    internal const int MinimumVisibleFrameCount = 3;
    private const float MaximumProtectedProgress = 0.72f;

    internal readonly struct Settings
    {
        public Settings(
            float intensity,
            float normalSnapDuration,
            float criticalPrecisionDuration,
            float devastatingCompressionDuration,
            float devastatingSecondaryWaveDelay,
            float devastatingSecondaryWaveDuration,
            float defeatAfterimageDuration,
            float defeatKnockbackDistance,
            float defeatLiftHeight,
            float defeatSilhouetteHoldDuration,
            float defeatSilhouetteDuration,
            float finalDefeatDurationMultiplier,
            Color defeatDustColor)
        {
            Intensity = Mathf.Max(0f, intensity);
            NormalSnapDuration = Mathf.Max(0.05f, normalSnapDuration);
            CriticalPrecisionDuration = Mathf.Max(
                0.05f,
                criticalPrecisionDuration);
            DevastatingCompressionDuration = Mathf.Max(
                0.05f,
                devastatingCompressionDuration);
            DevastatingSecondaryWaveDelay = Mathf.Max(
                0f,
                devastatingSecondaryWaveDelay);
            DevastatingSecondaryWaveDuration = Mathf.Max(
                0.05f,
                devastatingSecondaryWaveDuration);
            DefeatAfterimageDuration = Mathf.Max(
                0.05f,
                defeatAfterimageDuration);
            DefeatKnockbackDistance = Mathf.Max(0f, defeatKnockbackDistance);
            DefeatLiftHeight = Mathf.Max(0f, defeatLiftHeight);
            DefeatSilhouetteHoldDuration = Mathf.Max(
                0f,
                defeatSilhouetteHoldDuration);
            DefeatSilhouetteDuration = Mathf.Max(
                0.05f,
                defeatSilhouetteDuration);
            FinalDefeatDurationMultiplier = Mathf.Max(
                1f,
                finalDefeatDurationMultiplier);
            DefeatDustColor = defeatDustColor;
        }

        public float Intensity { get; }
        public float NormalSnapDuration { get; }
        public float CriticalPrecisionDuration { get; }
        public float DevastatingCompressionDuration { get; }
        public float DevastatingSecondaryWaveDelay { get; }
        public float DevastatingSecondaryWaveDuration { get; }
        public float DefeatAfterimageDuration { get; }
        public float DefeatKnockbackDistance { get; }
        public float DefeatLiftHeight { get; }
        public float DefeatSilhouetteHoldDuration { get; }
        public float DefeatSilhouetteDuration { get; }
        public float FinalDefeatDurationMultiplier { get; }
        public Color DefeatDustColor { get; }
    }

    private readonly MonoBehaviour coroutineHost;
    private readonly Sprite whiteSprite;
    private readonly List<GameObject> spawnedEffects = new List<GameObject>();

    public CombatImpactSignaturePresenter(
        MonoBehaviour coroutineHost,
        Sprite whiteSprite)
    {
        this.coroutineHost = coroutineHost;
        this.whiteSprite = whiteSprite;
    }

    public void Play(
        CombatPresentation.ImpactSignature signature,
        CombatPresentation.EnemySnapshot snapshot,
        int horizontalDirection,
        Color accent,
        Color waveAccent,
        float feedbackMultiplier,
        bool wasFinalEnemy,
        Settings settings)
    {
        if (coroutineHost == null
            || whiteSprite == null
            || !snapshot.IsValid
            || settings.Intensity <= 0f)
        {
            return;
        }

        if (signature.UsesSnapAccent)
        {
            SpawnSnapAccent(
                snapshot,
                horizontalDirection,
                accent,
                settings);
        }

        if (signature.UsesPrecisionLock)
        {
            SpawnPrecisionLock(
                snapshot,
                horizontalDirection,
                accent,
                settings);
        }

        if (signature.UsesCompressionBurst)
        {
            SpawnCompressionBurst(
                snapshot,
                horizontalDirection,
                accent,
                waveAccent,
                feedbackMultiplier,
                settings);
        }

        if (signature.UsesDefeatSilhouette)
        {
            SpawnDefeatSilhouette(
                snapshot,
                horizontalDirection,
                accent,
                feedbackMultiplier,
                wasFinalEnemy,
                settings);
            SpawnDefeatAfterimages(
                snapshot,
                horizontalDirection,
                accent,
                feedbackMultiplier,
                settings.DefeatSilhouetteHoldDuration
                    * (wasFinalEnemy
                        ? settings.FinalDefeatDurationMultiplier
                        : 1f),
                settings);
        }

        if (signature.UsesFinalExecutionSeal)
        {
            SpawnFinalExecutionSeal(
                snapshot,
                horizontalDirection,
                accent,
                feedbackMultiplier,
                settings);
        }
    }

    public void Clear()
    {
        foreach (GameObject effect in spawnedEffects)
        {
            if (effect != null)
            {
                Object.Destroy(effect);
            }
        }

        spawnedEffects.Clear();
    }

    private void SpawnSnapAccent(
        CombatPresentation.EnemySnapshot snapshot,
        int horizontalDirection,
        Color accent,
        Settings settings)
    {
        float strength = Mathf.Clamp01(
            CombatAccessibilitySettings.FlashMultiplier * settings.Intensity);

        if (strength <= 0f)
        {
            return;
        }

        int direction = NormalizeDirection(horizontalDirection);
        GameObject root = CreateRoot("Normal Impact Snap", snapshot.Position);
        List<SpriteRenderer> renderers = new List<SpriteRenderer>(3);

        for (int markIndex = 0; markIndex < 2; markIndex++)
        {
            float verticalDirection = markIndex == 0 ? 1f : -1f;
            Color markColor = Color.Lerp(accent, Color.white, 0.56f);
            markColor.a = 0.74f * strength;
            SpriteRenderer mark = CreateSprite(
                "Forward Snap Mark",
                root.transform,
                markColor,
                snapshot.SortingLayerId,
                snapshot.SortingOrder + 4);
            mark.transform.localPosition = new Vector3(
                -direction * 0.07f * settings.Intensity,
                verticalDirection * 0.045f * settings.Intensity,
                0f);
            mark.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                verticalDirection * direction * 24f);
            mark.transform.localScale = new Vector3(
                0.13f * settings.Intensity,
                0.012f * settings.Intensity,
                1f);
            renderers.Add(mark);
        }

        Color coreColor = Color.Lerp(accent, Color.white, 0.82f);
        coreColor.a = 0.88f * strength;
        SpriteRenderer core = CreateSprite(
            "Snap Core",
            root.transform,
            coreColor,
            snapshot.SortingLayerId,
            snapshot.SortingOrder + 5);
        core.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        core.transform.localScale = Vector3.one
            * 0.055f * settings.Intensity;
        renderers.Add(core);

        coroutineHost.StartCoroutine(AnimateSnapAccent(
            root,
            renderers,
            settings.NormalSnapDuration,
            direction));
    }

    private IEnumerator AnimateSnapAccent(
        GameObject root,
        List<SpriteRenderer> renderers,
        float duration,
        int horizontalDirection)
    {
        float elapsed = 0f;
        int visibleFrameCount = 0;
        Vector3 startPosition = root.transform.position;
        List<Color> startColors = CaptureColors(renderers);

        while (ShouldContinueAnimation(
                   elapsed,
                   duration,
                   visibleFrameCount)
               && root != null)
        {
            yield return null;

            if (root == null)
            {
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            visibleFrameCount++;
            elapsed = AdvanceAnimationTime(
                elapsed,
                duration,
                Time.unscaledDeltaTime,
                visibleFrameCount);
            float progress = Mathf.Clamp01(elapsed / duration);
            float snap = 1f - Mathf.Pow(1f - progress, 3f);
            float release = 1f - Mathf.SmoothStep(0.18f, 1f, progress);
            root.transform.position = startPosition
                + Vector3.right * horizontalDirection * 0.085f * snap;
            root.transform.localScale = Vector3.one
                * Mathf.Lerp(0.62f, 1.28f, snap);
            ApplyAlpha(renderers, startColors, release);
        }

        DestroyEffect(root);
    }

    private void SpawnPrecisionLock(
        CombatPresentation.EnemySnapshot snapshot,
        int horizontalDirection,
        Color accent,
        Settings settings)
    {
        float flashStrength = Mathf.Clamp01(
            CombatAccessibilitySettings.FlashMultiplier * settings.Intensity);

        if (flashStrength <= 0f)
        {
            return;
        }

        GameObject root = CreateRoot("Critical Precision Lock", snapshot.Position);
        GameObject chamberRoot = new GameObject("Chamber Lock");
        chamberRoot.transform.SetParent(root.transform, false);
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        Color throughColor = Color.Lerp(accent, Color.white, 0.78f);
        throughColor.a = 0.96f * flashStrength;
        SpriteRenderer throughLine = CreateSprite(
            "Precision Through Line",
            root.transform,
            throughColor,
            snapshot.SortingLayerId,
            snapshot.SortingOrder + 5);
        throughLine.transform.localScale = new Vector3(
            0.82f * settings.Intensity,
            0.016f * settings.Intensity,
            1f);
        renderers.Add(throughLine);

        Color crossColor = accent;
        crossColor.a = 0.78f * flashStrength;
        SpriteRenderer crossLine = CreateSprite(
            "Precision Cross Line",
            root.transform,
            crossColor,
            snapshot.SortingLayerId,
            snapshot.SortingOrder + 4);
        crossLine.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        crossLine.transform.localScale = new Vector3(
            0.34f * settings.Intensity,
            0.012f * settings.Intensity,
            1f);
        renderers.Add(crossLine);

        Color coreColor = Color.Lerp(accent, Color.white, 0.9f);
        coreColor.a = flashStrength;
        SpriteRenderer core = CreateSprite(
            "Precision Core",
            root.transform,
            coreColor,
            snapshot.SortingLayerId,
            snapshot.SortingOrder + 6);
        core.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        core.transform.localScale = Vector3.one
            * 0.095f * settings.Intensity;
        renderers.Add(core);

        int chamberCount = CombatAccessibilitySettings
            .ParticleDensityMultiplier < 0.35f ? 3 : 6;
        for (int chamberIndex = 0;
             chamberIndex < chamberCount;
             chamberIndex++)
        {
            float angle = 360f * chamberIndex / chamberCount;
            Vector3 radial = GetRadial(angle);
            Color chamberColor = Color.Lerp(accent, Color.white, 0.3f);
            chamberColor.a = 0.62f * flashStrength;
            SpriteRenderer chamber = CreateSprite(
                "Chamber Tick",
                chamberRoot.transform,
                chamberColor,
                snapshot.SortingLayerId,
                snapshot.SortingOrder + 3);
            chamber.transform.localPosition = radial
                * 0.26f * settings.Intensity;
            chamber.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                angle + 90f);
            chamber.transform.localScale = new Vector3(
                0.064f * settings.Intensity,
                0.012f * settings.Intensity,
                1f);
            renderers.Add(chamber);
        }

        coroutineHost.StartCoroutine(AnimatePrecisionLock(
            root,
            chamberRoot.transform,
            renderers,
            settings.CriticalPrecisionDuration,
            NormalizeDirection(horizontalDirection)));
    }

    private IEnumerator AnimatePrecisionLock(
        GameObject root,
        Transform chamberRoot,
        List<SpriteRenderer> renderers,
        float duration,
        int horizontalDirection)
    {
        float elapsed = 0f;
        int visibleFrameCount = 0;
        Vector3 startPosition = root.transform.position;
        List<Color> startColors = CaptureColors(renderers);

        while (ShouldContinueAnimation(
                   elapsed,
                   duration,
                   visibleFrameCount)
               && root != null)
        {
            yield return null;

            if (root == null)
            {
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            visibleFrameCount++;
            elapsed = AdvanceAnimationTime(
                elapsed,
                duration,
                Time.unscaledDeltaTime,
                visibleFrameCount);
            float progress = Mathf.Clamp01(elapsed / duration);
            float lockProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(progress / 0.2f));
            float release = 1f - Mathf.SmoothStep(0.2f, 1f, progress);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            root.transform.position = startPosition
                + Vector3.right * horizontalDirection * 0.035f * pulse;
            root.transform.localScale = Vector3.one
                * Mathf.Lerp(0.68f, 1.08f, lockProgress)
                * Mathf.Lerp(1f, 1.18f, progress);
            root.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                horizontalDirection * pulse * 2.5f);
            chamberRoot.localScale = Vector3.one
                * Mathf.Lerp(1.48f, 0.86f, lockProgress)
                * Mathf.Lerp(1f, 1.28f, progress);
            chamberRoot.localRotation = Quaternion.Euler(
                0f,
                0f,
                -horizontalDirection * Mathf.Lerp(9f, 0f, lockProgress));
            ApplyAlpha(renderers, startColors, lockProgress * release);
        }

        DestroyEffect(root);
    }

    private void SpawnCompressionBurst(
        CombatPresentation.EnemySnapshot snapshot,
        int horizontalDirection,
        Color accent,
        Color waveAccent,
        float feedbackMultiplier,
        Settings settings)
    {
        float effectMultiplier = Mathf.Max(0f, feedbackMultiplier);
        int shardCount = Mathf.Clamp(
            Mathf.RoundToInt(
                8f
                * CombatAccessibilitySettings.ParticleDensityMultiplier
                * effectMultiplier),
            4,
            12);
        GameObject root = CreateRoot(
            "Devastating Compression",
            snapshot.Position);
        List<Transform> shards = new List<Transform>(shardCount);
        List<Vector3> radialPositions = new List<Vector3>(shardCount);
        List<SpriteRenderer> renderers = new List<SpriteRenderer>(shardCount + 1);
        float radius = 0.42f * settings.Intensity * effectMultiplier;

        for (int shardIndex = 0; shardIndex < shardCount; shardIndex++)
        {
            float angle = 360f * shardIndex / shardCount
                + (shardIndex % 2 == 0 ? -7f : 7f);
            Vector3 radial = GetRadial(angle);
            Color compressionColor = Color.Lerp(accent, waveAccent, 0.58f);
            Color shardColor = Color.Lerp(
                compressionColor,
                shardIndex % 3 == 0 ? Color.white : settings.DefeatDustColor,
                shardIndex % 3 == 0 ? 0.58f : 0.2f);
            shardColor.a = shardIndex % 3 == 0 ? 0.82f : 0.58f;
            SpriteRenderer shard = CreateSprite(
                "Compression Fragment",
                root.transform,
                shardColor,
                snapshot.SortingLayerId,
                snapshot.SortingOrder + 3);
            shard.transform.localPosition = radial * radius;
            shard.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            shard.transform.localScale = new Vector3(
                0.13f * settings.Intensity * effectMultiplier,
                0.014f * settings.Intensity * effectMultiplier,
                1f);
            shards.Add(shard.transform);
            radialPositions.Add(radial * radius);
            renderers.Add(shard);
        }

        SpriteRenderer core = CreateSprite(
            "Compression Core",
            root.transform,
            Color.Lerp(accent, Color.white, 0.86f),
            snapshot.SortingLayerId,
            snapshot.SortingOrder + 4);
        core.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        core.transform.localScale = Vector3.one
            * 0.11f * settings.Intensity * effectMultiplier;
        renderers.Add(core);
        coroutineHost.StartCoroutine(AnimateCompressionBurst(
            root,
            shards,
            radialPositions,
            renderers,
            core.transform,
            snapshot,
            horizontalDirection,
            waveAccent,
            effectMultiplier,
            settings));
    }

    private IEnumerator AnimateCompressionBurst(
        GameObject root,
        List<Transform> shards,
        List<Vector3> radialPositions,
        List<SpriteRenderer> renderers,
        Transform core,
        CombatPresentation.EnemySnapshot snapshot,
        int horizontalDirection,
        Color waveAccent,
        float feedbackMultiplier,
        Settings settings)
    {
        float duration = settings.DevastatingCompressionDuration;
        float secondaryDelay = Mathf.Clamp(
            settings.DevastatingSecondaryWaveDelay,
            0f,
            duration * 0.65f);
        float collapsePortion = Mathf.Clamp(
            secondaryDelay / duration,
            0.18f,
            0.42f);
        float elapsed = 0f;
        int visibleFrameCount = 0;
        bool secondaryWaveStarted = false;
        Vector3 startPosition = root.transform.position;
        Vector3 coreStartScale = core.localScale;
        List<Vector3> startScales = CaptureScales(shards);
        List<Color> startColors = CaptureColors(renderers);
        int direction = NormalizeDirection(horizontalDirection);

        while (ShouldContinueAnimation(
                   elapsed,
                   duration,
                   visibleFrameCount)
               && root != null)
        {
            yield return null;

            if (root == null)
            {
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            visibleFrameCount++;
            elapsed = AdvanceAnimationTime(
                elapsed,
                duration,
                Time.unscaledDeltaTime,
                visibleFrameCount);

            if (!secondaryWaveStarted && elapsed >= secondaryDelay)
            {
                secondaryWaveStarted = true;
                SpawnSecondaryPressureWave(
                    snapshot,
                    horizontalDirection,
                    waveAccent,
                    feedbackMultiplier,
                    settings);
            }

            float progress = Mathf.Clamp01(elapsed / duration);
            bool isCompressing = progress < collapsePortion;
            float phase = isCompressing
                ? Mathf.SmoothStep(0f, 1f, progress / collapsePortion)
                : 1f - Mathf.Pow(
                    1f - Mathf.InverseLerp(collapsePortion, 1f, progress),
                    3f);
            float radiusScale = isCompressing
                ? Mathf.Lerp(1f, 0.08f, phase)
                : Mathf.Lerp(0.08f, 2.1f, phase);
            float envelope = isCompressing
                ? phase
                : 1f - Mathf.SmoothStep(0.12f, 1f, phase);

            for (int shardIndex = 0;
                 shardIndex < shards.Count;
                 shardIndex++)
            {
                Transform shard = shards[shardIndex];
                shard.localPosition = radialPositions[shardIndex] * radiusScale;
                Vector3 startScale = startScales[shardIndex];
                shard.localScale = new Vector3(
                    startScale.x * Mathf.Lerp(1f, 1.72f, progress),
                    startScale.y * Mathf.Lerp(1.35f, 0.72f, progress),
                    startScale.z);
            }

            core.localScale = coreStartScale * (isCompressing
                ? Mathf.Lerp(1.6f, 0.42f, phase)
                : Mathf.Lerp(0.42f, 2.25f, phase));
            root.transform.position = startPosition
                + Vector3.right
                * direction
                * 0.055f
                * Mathf.Sin(progress * Mathf.PI);
            root.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                direction * progress * 13f);
            ApplyAlpha(renderers, startColors, envelope);
        }

        if (!secondaryWaveStarted)
        {
            SpawnSecondaryPressureWave(
                snapshot,
                horizontalDirection,
                waveAccent,
                feedbackMultiplier,
                settings);
        }

        DestroyEffect(root);
    }

    private void SpawnSecondaryPressureWave(
        CombatPresentation.EnemySnapshot snapshot,
        int horizontalDirection,
        Color waveAccent,
        float feedbackMultiplier,
        Settings settings)
    {
        int fragmentCount = Mathf.Clamp(
            Mathf.RoundToInt(
                10f
                * CombatAccessibilitySettings.ParticleDensityMultiplier
                * Mathf.Max(0f, feedbackMultiplier)),
            5,
            12);
        GameObject root = CreateRoot(
            "Devastating Secondary Pressure Wave",
            snapshot.Position);
        List<SpriteRenderer> renderers =
            new List<SpriteRenderer>(fragmentCount);

        for (int fragmentIndex = 0;
             fragmentIndex < fragmentCount;
             fragmentIndex++)
        {
            float angle = 360f * fragmentIndex / fragmentCount
                + (fragmentIndex % 2 == 0 ? 5f : -5f);
            Vector3 radial = GetRadial(angle);
            Color color = Color.Lerp(
                waveAccent,
                Color.white,
                fragmentIndex % 3 == 0 ? 0.34f : 0.08f);
            color.a = fragmentIndex % 3 == 0 ? 0.82f : 0.58f;
            SpriteRenderer fragment = CreateSprite(
                "Broken Pressure Arc",
                root.transform,
                color,
                snapshot.SortingLayerId,
                snapshot.SortingOrder + 2);
            fragment.transform.localPosition = radial
                * 0.18f * settings.Intensity;
            fragment.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                angle + 90f);
            fragment.transform.localScale = new Vector3(
                0.11f * settings.Intensity
                    * Mathf.Max(1f, feedbackMultiplier),
                0.013f * settings.Intensity,
                1f);
            renderers.Add(fragment);
        }

        coroutineHost.StartCoroutine(AnimateExpandingWave(
            root,
            renderers,
            settings.DevastatingSecondaryWaveDuration,
            NormalizeDirection(horizontalDirection)));
    }

    private IEnumerator AnimateExpandingWave(
        GameObject root,
        List<SpriteRenderer> renderers,
        float duration,
        int horizontalDirection)
    {
        float elapsed = 0f;
        int visibleFrameCount = 0;
        Vector3 startPosition = root.transform.position;
        List<Color> startColors = CaptureColors(renderers);

        while (ShouldContinueAnimation(
                   elapsed,
                   duration,
                   visibleFrameCount)
               && root != null)
        {
            yield return null;

            if (root == null)
            {
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            visibleFrameCount++;
            elapsed = AdvanceAnimationTime(
                elapsed,
                duration,
                Time.unscaledDeltaTime,
                visibleFrameCount);
            float progress = Mathf.Clamp01(elapsed / duration);
            float expansion = 1f - Mathf.Pow(1f - progress, 3f);
            float attack = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(progress / 0.1f));
            float release = 1f - Mathf.SmoothStep(0.08f, 1f, progress);
            root.transform.localScale = Vector3.one
                * Mathf.Lerp(0.42f, 2.65f, expansion);
            root.transform.position = startPosition
                + Vector3.right * horizontalDirection * 0.07f * expansion;
            root.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                horizontalDirection * progress * 18f);
            ApplyAlpha(renderers, startColors, attack * release);
        }

        DestroyEffect(root);
    }

    private void SpawnDefeatSilhouette(
        CombatPresentation.EnemySnapshot snapshot,
        int horizontalDirection,
        Color accent,
        float feedbackMultiplier,
        bool wasFinalEnemy,
        Settings settings)
    {
        GameObject silhouette = CreateSnapshot(
            "Defeat Held Silhouette",
            snapshot,
            snapshot.SortingOrder + 4);
        SpriteRenderer renderer = silhouette.GetComponent<SpriteRenderer>();
        float durationMultiplier = wasFinalEnemy
            ? settings.FinalDefeatDurationMultiplier
            : 1f;
        coroutineHost.StartCoroutine(AnimateDefeatSilhouette(
            silhouette,
            renderer,
            horizontalDirection,
            accent,
            settings.DefeatSilhouetteHoldDuration * durationMultiplier,
            settings.DefeatSilhouetteDuration * durationMultiplier,
            feedbackMultiplier,
            settings));
    }

    private IEnumerator AnimateDefeatSilhouette(
        GameObject silhouette,
        SpriteRenderer renderer,
        int horizontalDirection,
        Color accent,
        float holdDuration,
        float duration,
        float feedbackMultiplier,
        Settings settings)
    {
        Vector3 startPosition = silhouette.transform.position;
        Vector3 startScale = silhouette.transform.localScale;
        Quaternion startRotation = silhouette.transform.rotation;
        int direction = NormalizeDirection(horizontalDirection);
        float elapsed = 0f;
        int visibleFrameCount = 0;
        float safeHold = Mathf.Clamp(holdDuration, 0f, duration * 0.58f);
        Color hotColor = Color.Lerp(accent, Color.white, 0.88f);
        Color darkColor = Color.Lerp(
            new Color(0.025f, 0.015f, 0.012f, 1f),
            accent,
            0.14f);
        float silhouetteAlpha = Mathf.Lerp(
            0.62f,
            0.96f,
            CombatAccessibilitySettings.FlashMultiplier);

        while (ShouldContinueAnimation(
                   elapsed,
                   duration,
                   visibleFrameCount)
               && silhouette != null)
        {
            yield return null;

            if (silhouette == null)
            {
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            visibleFrameCount++;
            elapsed = AdvanceAnimationTime(
                elapsed,
                duration,
                Time.unscaledDeltaTime,
                visibleFrameCount);

            if (elapsed <= safeHold && safeHold > 0f)
            {
                float settle = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / safeHold));
                Color color = Color.Lerp(hotColor, darkColor, settle);
                color.a = silhouetteAlpha;
                renderer.color = color;
                silhouette.transform.localScale = startScale
                    * Mathf.Lerp(1.055f, 0.985f, settle);
                silhouette.transform.position = startPosition
                    + Vector3.right
                    * direction
                    * 0.025f
                    * Mathf.Sin(settle * Mathf.PI);
                continue;
            }

            float release = Mathf.InverseLerp(safeHold, duration, elapsed);
            float releaseEase = 1f - Mathf.Pow(1f - release, 3f);
            Vector3 position = startPosition;
            position.x += direction * settings.DefeatKnockbackDistance
                * 0.42f * settings.Intensity * feedbackMultiplier * releaseEase;
            position.y += Mathf.Sin(release * Mathf.PI)
                * settings.DefeatLiftHeight * 0.55f * settings.Intensity;
            silhouette.transform.position = position;
            silhouette.transform.rotation = startRotation
                * Quaternion.Euler(0f, 0f, -direction * 9f * releaseEase);
            silhouette.transform.localScale = new Vector3(
                startScale.x * Mathf.Lerp(0.985f, 1.24f, releaseEase),
                startScale.y * Mathf.Lerp(0.985f, 0.58f, releaseEase),
                startScale.z);
            Color releaseColor = darkColor;
            releaseColor.a = silhouetteAlpha
                * (1f - Mathf.SmoothStep(0.12f, 1f, release));
            renderer.color = releaseColor;
        }

        DestroyEffect(silhouette);
    }

    private void SpawnDefeatAfterimages(
        CombatPresentation.EnemySnapshot snapshot,
        int horizontalDirection,
        Color accent,
        float feedbackMultiplier,
        float baseDelay,
        Settings settings)
    {
        int echoCount = Mathf.Clamp(
            Mathf.RoundToInt(
                3f * Mathf.Max(1f, feedbackMultiplier)
                * Mathf.Lerp(
                    0.72f,
                    1f,
                    CombatAccessibilitySettings.ParticleDensityMultiplier)),
            2,
            6);

        for (int echoIndex = 0; echoIndex < echoCount; echoIndex++)
        {
            GameObject afterimage = CreateSnapshot(
                $"Defeat Afterimage {echoIndex + 1}",
                snapshot,
                snapshot.SortingOrder - echoIndex);
            SpriteRenderer renderer = afterimage.GetComponent<SpriteRenderer>();
            renderer.color = Color.Lerp(
                snapshot.Color,
                Color.Lerp(settings.DefeatDustColor, accent, 0.35f),
                0.38f + echoIndex * 0.13f);
            renderer.enabled = false;
            coroutineHost.StartCoroutine(AnimateDefeatAfterimage(
                afterimage,
                renderer,
                horizontalDirection,
                Mathf.Max(0f, baseDelay) + echoIndex * 0.035f,
                1f - echoIndex * 0.16f,
                feedbackMultiplier,
                settings));
        }
    }

    private IEnumerator AnimateDefeatAfterimage(
        GameObject afterimage,
        SpriteRenderer renderer,
        int horizontalDirection,
        float delay,
        float distanceScale,
        float feedbackMultiplier,
        Settings settings)
    {
        Vector3 startPosition = afterimage.transform.position;
        Vector3 startScale = afterimage.transform.localScale;
        Quaternion startRotation = afterimage.transform.rotation;
        int direction = NormalizeDirection(horizontalDirection);
        float elapsed = 0f;
        int visibleFrameCount = 0;

        while (delay > 0f && afterimage != null)
        {
            yield return null;

            if (afterimage == null)
            {
                yield break;
            }

            if (!GamePauseController.IsPaused)
            {
                delay -= Time.unscaledDeltaTime;
            }
        }

        if (afterimage == null || renderer == null)
        {
            yield break;
        }

        renderer.enabled = true;

        while (ShouldContinueAnimation(
                   elapsed,
                   settings.DefeatAfterimageDuration,
                   visibleFrameCount)
               && afterimage != null)
        {
            yield return null;

            if (afterimage == null)
            {
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            visibleFrameCount++;
            elapsed = AdvanceAnimationTime(
                elapsed,
                settings.DefeatAfterimageDuration,
                Time.unscaledDeltaTime,
                visibleFrameCount);
            float progress = Mathf.Clamp01(
                elapsed / settings.DefeatAfterimageDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            Vector3 position = startPosition;
            position.x += direction * settings.DefeatKnockbackDistance
                * settings.Intensity * distanceScale * feedbackMultiplier
                * eased;
            position.y += Mathf.Sin(progress * Mathf.PI)
                * settings.DefeatLiftHeight
                * settings.Intensity
                * feedbackMultiplier;
            afterimage.transform.position = position;
            afterimage.transform.rotation = startRotation
                * Quaternion.Euler(0f, 0f, -direction * 14f * eased);
            afterimage.transform.localScale = new Vector3(
                startScale.x * Mathf.Lerp(
                    1f,
                    1f + 0.12f * feedbackMultiplier,
                    eased),
                startScale.y * Mathf.Lerp(
                    1f,
                    1f - 0.32f * feedbackMultiplier,
                    eased),
                startScale.z);
            Color color = renderer.color;
            color.a = 1f - Mathf.SmoothStep(0.18f, 1f, progress);
            renderer.color = color;
        }

        DestroyEffect(afterimage);
    }

    private void SpawnFinalExecutionSeal(
        CombatPresentation.EnemySnapshot snapshot,
        int horizontalDirection,
        Color accent,
        float feedbackMultiplier,
        Settings settings)
    {
        float flashStrength = Mathf.Clamp01(
            CombatAccessibilitySettings.FlashMultiplier * settings.Intensity);

        if (flashStrength <= 0f)
        {
            return;
        }

        GameObject root = CreateRoot(
            "Final Enemy Chamber Seal",
            snapshot.Position);
        List<Transform> chambers = new List<Transform>(6);
        List<Vector3> radialPositions = new List<Vector3>(6);
        List<SpriteRenderer> renderers = new List<SpriteRenderer>(7);
        float radius = 0.5f * settings.Intensity
            * Mathf.Lerp(1f, Mathf.Max(1f, feedbackMultiplier), 0.35f);

        for (int chamberIndex = 0; chamberIndex < 6; chamberIndex++)
        {
            float angle = 360f * chamberIndex / 6f;
            Vector3 radial = GetRadial(angle);
            Color color = Color.Lerp(accent, Color.white, 0.54f);
            color.a = 0.76f * flashStrength;
            SpriteRenderer chamber = CreateSprite(
                "Final Chamber",
                root.transform,
                color,
                snapshot.SortingLayerId,
                snapshot.SortingOrder + 6);
            chamber.transform.localPosition = radial * radius;
            chamber.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                angle + 90f);
            chamber.transform.localScale = new Vector3(
                0.12f * settings.Intensity,
                0.022f * settings.Intensity,
                1f);
            chambers.Add(chamber.transform);
            radialPositions.Add(radial * radius);
            renderers.Add(chamber);
        }

        Color centerColor = Color.Lerp(accent, Color.white, 0.9f);
        centerColor.a = flashStrength;
        SpriteRenderer center = CreateSprite(
            "Final Seal Core",
            root.transform,
            centerColor,
            snapshot.SortingLayerId,
            snapshot.SortingOrder + 7);
        center.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        center.transform.localScale = Vector3.one
            * 0.13f * settings.Intensity;
        renderers.Add(center);
        coroutineHost.StartCoroutine(AnimateFinalExecutionSeal(
            root,
            chambers,
            radialPositions,
            renderers,
            center.transform,
            NormalizeDirection(horizontalDirection),
            settings.DefeatSilhouetteDuration
                * settings.FinalDefeatDurationMultiplier));
    }

    private IEnumerator AnimateFinalExecutionSeal(
        GameObject root,
        List<Transform> chambers,
        List<Vector3> radialPositions,
        List<SpriteRenderer> renderers,
        Transform center,
        int horizontalDirection,
        float duration)
    {
        const float lockPortion = 0.42f;
        float elapsed = 0f;
        int visibleFrameCount = 0;
        List<Vector3> startScales = CaptureScales(chambers);
        List<Color> startColors = CaptureColors(renderers);
        Vector3 centerStartScale = center.localScale;

        while (ShouldContinueAnimation(
                   elapsed,
                   duration,
                   visibleFrameCount)
               && root != null)
        {
            yield return null;

            if (root == null)
            {
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            visibleFrameCount++;
            elapsed = AdvanceAnimationTime(
                elapsed,
                duration,
                Time.unscaledDeltaTime,
                visibleFrameCount);
            float progress = Mathf.Clamp01(elapsed / duration);
            bool isLocking = progress < lockPortion;
            float phase = isLocking
                ? Mathf.SmoothStep(0f, 1f, progress / lockPortion)
                : 1f - Mathf.Pow(
                    1f - Mathf.InverseLerp(lockPortion, 1f, progress),
                    3f);
            float radiusScale = isLocking
                ? Mathf.Lerp(1.38f, 0.64f, phase)
                : Mathf.Lerp(0.64f, 1.92f, phase);
            float envelope = isLocking
                ? phase
                : 1f - Mathf.SmoothStep(0.08f, 1f, phase);

            for (int chamberIndex = 0;
                 chamberIndex < chambers.Count;
                 chamberIndex++)
            {
                Transform chamber = chambers[chamberIndex];
                chamber.localPosition = radialPositions[chamberIndex]
                    * radiusScale;
                Vector3 startScale = startScales[chamberIndex];
                chamber.localScale = new Vector3(
                    startScale.x * Mathf.Lerp(0.72f, 1.35f, progress),
                    startScale.y * Mathf.Lerp(1.22f, 0.62f, progress),
                    startScale.z);
            }

            center.localScale = centerStartScale * (isLocking
                ? Mathf.Lerp(0.5f, 1.45f, phase)
                : Mathf.Lerp(1.45f, 0.18f, phase));
            root.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                horizontalDirection * Mathf.Lerp(-12f, 24f, progress));
            ApplyAlpha(renderers, startColors, envelope);
        }

        DestroyEffect(root);
    }

    private GameObject CreateRoot(string effectName, Vector3 position)
    {
        GameObject root = new GameObject(effectName);
        root.transform.position = position;
        spawnedEffects.Add(root);
        return root;
    }

    private SpriteRenderer CreateSprite(
        string objectName,
        Transform parent,
        Color color,
        int sortingLayerId,
        int sortingOrder)
    {
        GameObject spriteObject = new GameObject(
            objectName,
            typeof(SpriteRenderer));
        spriteObject.transform.SetParent(parent, false);
        SpriteRenderer renderer = spriteObject.GetComponent<SpriteRenderer>();
        renderer.sprite = whiteSprite;
        renderer.color = color;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private GameObject CreateSnapshot(
        string objectName,
        CombatPresentation.EnemySnapshot snapshot,
        int sortingOrder)
    {
        GameObject snapshotObject = new GameObject(
            objectName,
            typeof(SpriteRenderer));
        snapshotObject.transform.SetPositionAndRotation(
            snapshot.Position,
            snapshot.Rotation);
        SpriteRenderer renderer = snapshotObject.GetComponent<SpriteRenderer>();

        if (snapshot.HasSprite)
        {
            snapshotObject.transform.localScale = snapshot.Scale;
            renderer.sprite = snapshot.Sprite;

            if (snapshot.Material != null)
            {
                renderer.sharedMaterial = snapshot.Material;
            }
        }
        else
        {
            snapshotObject.transform.rotation = snapshot.Rotation
                * Quaternion.Euler(0f, 0f, 45f);
            snapshotObject.transform.localScale = new Vector3(0.3f, 0.42f, 1f);
            renderer.sprite = whiteSprite;
        }

        renderer.color = snapshot.Color;
        renderer.sortingLayerID = snapshot.SortingLayerId;
        renderer.sortingOrder = sortingOrder;
        spawnedEffects.Add(snapshotObject);
        return snapshotObject;
    }

    private void DestroyEffect(GameObject effect)
    {
        if (effect == null)
        {
            return;
        }

        spawnedEffects.Remove(effect);
        Object.Destroy(effect);
    }

    private static int NormalizeDirection(int horizontalDirection)
    {
        return horizontalDirection == 0 ? 1 : horizontalDirection;
    }

    internal static bool ShouldContinueAnimation(
        float elapsed,
        float duration,
        int visibleFrameCount)
    {
        return elapsed < Mathf.Max(0f, duration)
            || visibleFrameCount < MinimumVisibleFrameCount;
    }

    internal static float AdvanceAnimationTime(
        float elapsed,
        float duration,
        float deltaTime,
        int visibleFrameCount)
    {
        float safeDuration = Mathf.Max(0.0001f, duration);
        float nextElapsed = elapsed + Mathf.Max(0f, deltaTime);

        if (visibleFrameCount < MinimumVisibleFrameCount)
        {
            nextElapsed = Mathf.Min(
                nextElapsed,
                safeDuration * MaximumProtectedProgress);
        }

        return nextElapsed;
    }

    private static Vector3 GetRadial(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
    }

    private static List<Color> CaptureColors(
        IReadOnlyList<SpriteRenderer> renderers)
    {
        List<Color> colors = new List<Color>(renderers.Count);

        foreach (SpriteRenderer renderer in renderers)
        {
            colors.Add(renderer == null ? Color.clear : renderer.color);
        }

        return colors;
    }

    private static List<Vector3> CaptureScales(
        IReadOnlyList<Transform> transforms)
    {
        List<Vector3> scales = new List<Vector3>(transforms.Count);

        foreach (Transform target in transforms)
        {
            scales.Add(target == null ? Vector3.one : target.localScale);
        }

        return scales;
    }

    private static void ApplyAlpha(
        IReadOnlyList<SpriteRenderer> renderers,
        IReadOnlyList<Color> startColors,
        float multiplier)
    {
        for (int rendererIndex = 0;
             rendererIndex < renderers.Count;
             rendererIndex++)
        {
            SpriteRenderer renderer = renderers[rendererIndex];

            if (renderer == null)
            {
                continue;
            }

            Color color = startColors[rendererIndex];
            color.a *= multiplier;
            renderer.color = color;
        }
    }
}
