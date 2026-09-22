using System;
using UnityEngine;

internal sealed class PlayerCombatPreviewResources
{
    private readonly RelicLethalDamagePreviewState lethalDamageState;

    public PlayerCombatPreviewResources(
        int currentGold,
        int currentHealth,
        int maxHealth,
        RelicLethalDamagePreviewState lethalDamageState)
    {
        CurrentGold = Mathf.Max(0, currentGold);
        CurrentHealth = Mathf.Max(0, currentHealth);
        MaxHealth = Mathf.Max(0, maxHealth);
        this.lethalDamageState = lethalDamageState;
    }

    public int CurrentGold { get; private set; }
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }

    public void ApplyHealthCost(int amount)
    {
        int cost = Mathf.Max(0, amount);

        if (cost <= 0 || CurrentHealth <= 0)
        {
            return;
        }

        if (lethalDamageState != null
            && lethalDamageState.TryPrevent(
                cost,
                CurrentHealth,
                out int survivingHealth))
        {
            CurrentHealth = survivingHealth;
            return;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - cost);
    }

    public bool TryHeal(int amount)
    {
        if (amount <= 0 || CurrentHealth <= 0 || CurrentHealth >= MaxHealth)
        {
            return false;
        }

        CurrentHealth = (int)Math.Min(
            MaxHealth,
            (long)CurrentHealth + amount);
        return true;
    }

    public bool TryIncreaseMaxHealth(int amount)
    {
        if (amount <= 0 || CurrentHealth <= 0 || MaxHealth >= int.MaxValue)
        {
            return false;
        }

        int increase = (int)Math.Min(
            amount,
            (long)int.MaxValue - MaxHealth);
        MaxHealth += increase;
        CurrentHealth += increase;
        return true;
    }

    public bool TryAddGold(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        CurrentGold = (int)Math.Min(
            int.MaxValue,
            (long)CurrentGold + amount);
        return true;
    }
}
