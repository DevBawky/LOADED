using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour, IStatusEffectTarget
{
    [Header("Health")]
    [Min(1)]
    [SerializeField] private int maxHealth = 100;
    [Min(0)]
    [SerializeField] private int startingHealth = 100;

    [Header("UI References")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private TMP_Text healthText;

    [Header("Runtime State")]
    [SerializeField] private int currentHealth;

    private StatusEffectController statusEffects;

    public event Action<int, int> HealthChanged;
    public event Action Defeated;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDefeated => currentHealth <= 0;

    private void Awake()
    {
        statusEffects = GetComponent<StatusEffectController>();
        currentHealth = Mathf.Clamp(startingHealth, 0, maxHealth);
        RefreshUI();
    }

    public bool ApplyDamage(int damage)
    {
        if (damage <= 0 || IsDefeated)
        {
            return false;
        }

        int modifiedDamage = statusEffects == null
            ? damage
            : statusEffects.ModifyIncomingAttackDamage(damage);
        SetCurrentHealth(currentHealth - modifiedDamage);
        return true;
    }

    public bool ApplyStatusDamage(int damage)
    {
        if (damage <= 0 || IsDefeated)
        {
            return false;
        }

        SetCurrentHealth(currentHealth - damage);
        return true;
    }

    public bool AddStatusEffect(StatusEffectType type, int stacks)
    {
        return !IsDefeated && statusEffects != null
            && statusEffects.Add(type, stacks);
    }

    public int ModifyOutgoingAttackDamage(int damage)
    {
        return statusEffects == null
            ? damage
            : statusEffects.ModifyOutgoingAttackDamage(damage);
    }

    public bool Heal(int amount)
    {
        if (amount <= 0 || IsDefeated || currentHealth >= maxHealth)
        {
            return false;
        }

        SetCurrentHealth(currentHealth + amount);
        return true;
    }

    public bool IncreaseMaxHealth(int amount)
    {
        if (amount <= 0 || IsDefeated || maxHealth >= int.MaxValue)
        {
            return false;
        }

        int increase = (int)Math.Min(amount, (long)int.MaxValue - maxHealth);
        maxHealth += increase;
        currentHealth += increase;
        RefreshUI();
        HealthChanged?.Invoke(currentHealth, maxHealth);
        return true;
    }

    private void SetCurrentHealth(int health)
    {
        int previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(health, 0, maxHealth);

        if (currentHealth == previousHealth)
        {
            return;
        }

        RefreshUI();
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth == 0)
        {
            Defeated?.Invoke();
        }
    }

    private void RefreshUI()
    {
        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = maxHealth <= 0
                ? 0f
                : (float)currentHealth / maxHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";
        }
    }
}
