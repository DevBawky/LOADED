using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
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

    public event Action<int, int> HealthChanged;
    public event Action Defeated;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDefeated => currentHealth <= 0;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(startingHealth, 0, maxHealth);
        RefreshUI();
    }

    public bool ApplyDamage(int damage)
    {
        if (damage <= 0 || IsDefeated)
        {
            return false;
        }

        SetCurrentHealth(currentHealth - damage);
        return true;
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
