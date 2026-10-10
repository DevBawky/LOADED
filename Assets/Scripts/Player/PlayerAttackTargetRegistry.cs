using System.Collections.Generic;
using UnityEngine;

public interface IPlayerAttackTarget
{
    Transform TargetTransform { get; }
    Vector3 ImpactPoint { get; }
    int LaneIndex { get; }
    int CurrentDurability { get; }
    int MaxDurability { get; }
    int TotalStatusStackCount { get; }
    int ActiveStatusTypeCount { get; }
    bool IsTargetable { get; }

    int PredictAttackDamage(int attackDamage);
    int ApplyAttackDamage(
        int attackDamage,
        bool isCritical,
        BulletInstance sourceBullet);
    bool TryApplyEffect(BulletEffectData effect, int horizontalDirection);
}

public interface IPlayerAttackTargetPresentation
{
    SpriteRenderer ImpactRenderer { get; }

    void ShowDamagePreview(
        IReadOnlyList<EnemyHealthBarFeedback.DamagePreviewSegment> segments,
        BulletData bullet);
    void ClearDamagePreview();
}

[DisallowMultipleComponent]
public sealed class PlayerAttackTargetRegistry : MonoBehaviour
{
    private readonly List<IPlayerAttackTarget> targets =
        new List<IPlayerAttackTarget>();

    public IReadOnlyList<IPlayerAttackTarget> Targets => targets;

    public void Register(IPlayerAttackTarget target)
    {
        RemoveMissingTargets();

        if (IsAlive(target) && !targets.Contains(target))
        {
            targets.Add(target);
        }
    }

    public void Unregister(IPlayerAttackTarget target)
    {
        targets.Remove(target);
        RemoveMissingTargets();
    }

    private void RemoveMissingTargets()
    {
        for (int index = targets.Count - 1; index >= 0; index--)
        {
            if (!IsAlive(targets[index]))
            {
                targets.RemoveAt(index);
            }
        }
    }

    internal static bool IsAlive(IPlayerAttackTarget target)
    {
        if (target == null)
        {
            return false;
        }

        return target is not Object unityObject || unityObject != null;
    }
}
