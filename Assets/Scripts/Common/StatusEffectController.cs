using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatusEffectType
{
    Mark,
    Poison,
    Stun
}

public interface IStatusEffectTarget
{
    int CurrentHealth { get; }
    bool ApplyStatusDamage(int damage);
}

public class StatusEffectController : MonoBehaviour
{
    public const float MarkDamageMultiplier = 1.5f;
    public const float PoisonHealthRatio = 0.05f;

    [Header("UI")]
    [SerializeField] private Transform statusIconParent;
    [SerializeField] private GameObject debuffIconPrefab;
    [SerializeField] private Sprite markSprite;
    [SerializeField] private Sprite poisonSprite;
    [SerializeField] private Sprite stunSprite;

    [Header("Runtime State")]
    [Min(0)]
    [SerializeField] private int markStacks;
    [Min(0)]
    [SerializeField] private int poisonStacks;
    [Min(0)]
    [SerializeField] private int stunStacks;

    private IStatusEffectTarget target;
    private readonly Dictionary<StatusEffectType, DebuffIconUI> icons =
        new Dictionary<StatusEffectType, DebuffIconUI>();

    public event Action<StatusEffectType, int> StacksChanged;

    public int MarkStacks => markStacks;
    public int PoisonStacks => poisonStacks;
    public int StunStacks => stunStacks;
    public bool IsMarked => markStacks > 0;
    public bool IsStunned => stunStacks > 0;

    private void Awake()
    {
        target = GetComponent<IStatusEffectTarget>();
        RefreshAllIcons();
    }

    public int GetStacks(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Mark:
                return markStacks;
            case StatusEffectType.Poison:
                return poisonStacks;
            case StatusEffectType.Stun:
                return stunStacks;
            default:
                return 0;
        }
    }

    public bool Add(StatusEffectType type, int stacks)
    {
        if (stacks <= 0)
        {
            return false;
        }

        long combinedStacks = (long)GetStacks(type) + stacks;
        SetStacks(type, (int)Math.Min(int.MaxValue, combinedStacks));
        return true;
    }

    public int ModifyIncomingAttackDamage(int damage)
    {
        if (damage <= 0 || !IsMarked)
        {
            return damage;
        }

        return Mathf.CeilToInt(damage * MarkDamageMultiplier);
    }

    public bool ConsumeStunTurn()
    {
        if (!IsStunned)
        {
            return false;
        }

        SetStacks(StatusEffectType.Stun, stunStacks - 1);
        return true;
    }

    public void ProcessTurnEnd()
    {
        if (poisonStacks > 0)
        {
            ApplyPoisonDamage();
            SetStacks(StatusEffectType.Poison, poisonStacks - 1);
        }

        if (markStacks > 0)
        {
            SetStacks(StatusEffectType.Mark, markStacks - 1);
        }
    }

    public void Clear()
    {
        SetStacks(StatusEffectType.Mark, 0);
        SetStacks(StatusEffectType.Poison, 0);
        SetStacks(StatusEffectType.Stun, 0);
    }

    private void ApplyPoisonDamage()
    {
        if (target == null || target.CurrentHealth <= 0)
        {
            return;
        }

        int damage = Mathf.Max(
            1,
            Mathf.CeilToInt(target.CurrentHealth * PoisonHealthRatio));
        target.ApplyStatusDamage(damage);
    }

    private void SetStacks(StatusEffectType type, int stacks)
    {
        int clampedStacks = Mathf.Max(0, stacks);

        switch (type)
        {
            case StatusEffectType.Mark:
                markStacks = clampedStacks;
                break;
            case StatusEffectType.Poison:
                poisonStacks = clampedStacks;
                break;
            case StatusEffectType.Stun:
                stunStacks = clampedStacks;
                break;
        }

        RefreshIcon(type, clampedStacks);
        StacksChanged?.Invoke(type, clampedStacks);
    }

    private void RefreshAllIcons()
    {
        RefreshIcon(StatusEffectType.Mark, markStacks);
        RefreshIcon(StatusEffectType.Poison, poisonStacks);
        RefreshIcon(StatusEffectType.Stun, stunStacks);
    }

    private void RefreshIcon(StatusEffectType type, int stacks)
    {
        if (icons.TryGetValue(type, out DebuffIconUI icon) && icon == null)
        {
            icons.Remove(type);
            icon = null;
        }

        if (stacks <= 0)
        {
            if (icon != null)
            {
                icons.Remove(type);
                Destroy(icon.gameObject);
            }

            return;
        }

        if (icon == null)
        {
            icon = CreateIcon(type);

            if (icon == null)
            {
                return;
            }

            icons[type] = icon;
        }

        icon.SetStacks(stacks);
    }

    private DebuffIconUI CreateIcon(StatusEffectType type)
    {
        if (statusIconParent == null || debuffIconPrefab == null)
        {
            return null;
        }

        GameObject iconObject = Instantiate(
            debuffIconPrefab,
            statusIconParent,
            false);
        iconObject.name = $"Image _ Debuff | {type}";

        DebuffIconUI icon = iconObject.GetComponent<DebuffIconUI>();

        if (icon == null)
        {
            Debug.LogError(
                "Image _ Debuff prefab must contain DebuffIconUI.",
                debuffIconPrefab);
            Destroy(iconObject);
            return null;
        }

        icon.Initialize(GetSprite(type), GetStacks(type));
        return icon;
    }

    private Sprite GetSprite(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Mark:
                return markSprite;
            case StatusEffectType.Poison:
                return poisonSprite;
            case StatusEffectType.Stun:
                return stunSprite;
            default:
                return null;
        }
    }
}
