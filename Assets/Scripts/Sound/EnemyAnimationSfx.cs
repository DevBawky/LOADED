using System.Collections.Generic;
using UnityEngine;

/// <summary>Enemy Animation Event에서 문자열 ID로 SFX를 재생합니다.</summary>
public sealed class EnemyAnimationSfx : MonoBehaviour
{
    [System.Serializable]
    private sealed class AvatarEffect
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localRotation;
        [SerializeField] private Vector3 localScale = Vector3.one;

        public GameObject Prefab => prefab;
        public Transform SpawnPoint => spawnPoint;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalRotation => localRotation;
        public Vector3 LocalScale => localScale;
    }

    [Header("Avatar Effects")]
    [SerializeField] private List<AvatarEffect> effects =
        new List<AvatarEffect>();

    private readonly List<GameObject> activeEffects = new List<GameObject>();
    private CombatFeedbackController combatFeedback;

    public void PlaySfx(string sfxId) => SoundManager.PlaySfx(sfxId);

    public void PlayCameraShake()
    {
        combatFeedback ??=
            FindFirstObjectByType<CombatFeedbackController>();
        combatFeedback?.RecordShotCameraShake();
    }

    private void Awake()
    {
        StopEffects();
    }

    private void OnDisable()
    {
        StopEffects();
    }

    public void SpawnEffect()
    {
        StopEffects();

        foreach (AvatarEffect effect in effects)
        {
            if (effect == null || effect.Prefab == null)
            {
                continue;
            }

            Transform spawnPoint = effect.SpawnPoint != null
                ? effect.SpawnPoint
                : transform;
            GameObject instance = Instantiate(effect.Prefab, spawnPoint);
            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = effect.LocalPosition;
            instanceTransform.localRotation =
                Quaternion.Euler(effect.LocalRotation);
            instanceTransform.localScale = Vector3.Scale(
                instanceTransform.localScale,
                effect.LocalScale);
            activeEffects.Add(instance);
            RestartAnimations(instance);
        }
    }

    public void StopEffects()
    {
        foreach (GameObject effect in activeEffects)
        {
            if (effect != null)
            {
                effect.SetActive(false);
                Destroy(effect);
            }
        }

        activeEffects.Clear();
    }

    private static void RestartAnimations(GameObject effect)
    {
        foreach (Animator animator in
                 effect.GetComponentsInChildren<Animator>(true))
        {
            animator.Rebind();
            animator.Update(0f);
        }

        foreach (ParticleSystem particleSystem in
                 effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        foreach (Animation animation in
                 effect.GetComponentsInChildren<Animation>(true))
        {
            animation.Stop();
            animation.Play();
        }
    }
}

public static class TransientVfx
{
    private const float DefaultLifetime = 1f;
    private const float DestructionBuffer = 0.05f;

    public static GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        float uniformScale = 1f,
        Transform parent = null)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Object.Instantiate(
            prefab,
            position,
            rotation,
            parent);
        instance.transform.localScale *= Mathf.Max(0f, uniformScale);
        Object.Destroy(instance, GetLifetime(instance) + DestructionBuffer);
        return instance;
    }

    private static float GetLifetime(GameObject instance)
    {
        float lifetime = 0f;

        foreach (Animator animator in
                 instance.GetComponentsInChildren<Animator>(true))
        {
            RuntimeAnimatorController controller =
                animator.runtimeAnimatorController;
            if (controller == null)
            {
                continue;
            }

            float speed = Mathf.Max(0.01f, Mathf.Abs(animator.speed));
            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip != null)
                {
                    lifetime = Mathf.Max(lifetime, clip.length / speed);
                }
            }
        }

        foreach (ParticleSystem particleSystem in
                 instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particleSystem.main;
            lifetime = Mathf.Max(
                lifetime,
                main.duration
                + GetMaximum(main.startDelay)
                + GetMaximum(main.startLifetime));
        }

        foreach (Animation animation in
                 instance.GetComponentsInChildren<Animation>(true))
        {
            foreach (AnimationState state in animation)
            {
                if (state != null)
                {
                    lifetime = Mathf.Max(lifetime, state.length);
                }
            }
        }

        return lifetime > 0f ? lifetime : DefaultLifetime;
    }

    private static float GetMaximum(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => curve.constant,
            ParticleSystemCurveMode.TwoConstants => curve.constantMax,
            ParticleSystemCurveMode.Curve => GetCurveMaximum(
                curve.curve,
                curve.curveMultiplier),
            ParticleSystemCurveMode.TwoCurves => Mathf.Max(
                GetCurveMaximum(curve.curveMin, curve.curveMultiplier),
                GetCurveMaximum(curve.curveMax, curve.curveMultiplier)),
            _ => 0f
        };
    }

    private static float GetCurveMaximum(
        AnimationCurve curve,
        float multiplier)
    {
        if (curve == null || curve.length == 0)
        {
            return 0f;
        }

        float maximum = 0f;
        foreach (Keyframe key in curve.keys)
        {
            maximum = Mathf.Max(maximum, key.value * multiplier);
        }

        return maximum;
    }
}
