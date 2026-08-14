using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

    [Header("Damage Screen Flash")]
    [SerializeField] private Color damageVignetteColor =
        new Color(0.8f, 0f, 0f, 1f);
    [SerializeField, Range(0f, 1f)] private float damageVignetteIntensity =
        0.5f;
    [SerializeField, Range(0.01f, 1f)] private float damageVignetteSmoothness =
        0.45f;
    [SerializeField] private bool damageVignetteRounded = true;
    [SerializeField, Range(0f, 1f)] private float damageFlashWeight = 0.65f;
    [SerializeField, Min(0.01f)] private float damageFlashDuration = 0.3f;

    [Header("Runtime State")]
    [SerializeField] private int currentHealth;

    private StatusEffectController statusEffects;
    private CombatFeedbackController combatFeedback;
    private RelicManager relicManager;
    private Volume damageFlashVolume;
    private VolumeProfile damageFlashProfile;
    private float damageFlashElapsed = -1f;

    public event Action<int, int> HealthChanged;
    public event Action Defeated;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDefeated => currentHealth <= 0;
    public RunStatusEffectSaveData CaptureStatusRunState()
    {
        return statusEffects == null
            ? new RunStatusEffectSaveData()
            : statusEffects.CaptureRunState();
    }

    private void Awake()
    {
        statusEffects = GetComponent<StatusEffectController>();
        combatFeedback = FindFirstObjectByType<CombatFeedbackController>();
        relicManager = FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);
        ResolveUIReferences();
        currentHealth = Mathf.Clamp(startingHealth, 0, maxHealth);
        CreateDamageFlashVolume();
        RefreshUI();
    }

    private void Update()
    {
        if (damageFlashElapsed < 0f || damageFlashVolume == null)
        {
            return;
        }

        damageFlashElapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(
            damageFlashElapsed / damageFlashDuration);
        damageFlashVolume.weight = damageFlashWeight
            * (1f - Mathf.SmoothStep(0f, 1f, progress));

        if (progress >= 1f)
        {
            damageFlashElapsed = -1f;
            damageFlashVolume.weight = 0f;
            damageFlashVolume.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (damageFlashProfile != null)
        {
            Destroy(damageFlashProfile);
        }
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
        int previousHealth = currentHealth;
        SetCurrentHealth(ResolveDamageTargetHealth(modifiedDamage));

        if (currentHealth < previousHealth)
        {
            relicManager ??= FindFirstObjectByType<RelicManager>(
                FindObjectsInactive.Include);
            relicManager?.NotifyPlayerHealthLost(
                previousHealth - currentHealth,
                maxHealth);
            SoundManager.PlayHit();
            PlayDamageScreenFlash();
            combatFeedback ??= FindFirstObjectByType<CombatFeedbackController>();
            combatFeedback?.RecordPlayerDamageCameraShake();
        }

        return true;
    }

    public bool ApplyStatusDamage(int damage, bool creditedToPlayer)
    {
        if (damage <= 0 || IsDefeated)
        {
            return false;
        }

        int previousHealth = currentHealth;
        SetCurrentHealth(ResolveDamageTargetHealth(damage));

        if (currentHealth < previousHealth)
        {
            relicManager ??= FindFirstObjectByType<RelicManager>(
                FindObjectsInactive.Include);
            relicManager?.NotifyPlayerHealthLost(
                previousHealth - currentHealth,
                maxHealth);
            SoundManager.PlayHit();
            PlayDamageScreenFlash();
        }

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

    public void RestoreRunHealth(int health, int savedMaxHealth)
    {
        maxHealth = Mathf.Max(1, savedMaxHealth);
        currentHealth = Mathf.Clamp(health, 1, maxHealth);
        RefreshUI();
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void RestoreStatusRunState(RunStatusEffectSaveData state)
    {
        statusEffects?.RestoreRunState(state);
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

    private int ResolveDamageTargetHealth(int damage)
    {
        if (damage <= 0)
        {
            return currentHealth;
        }

        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);

        if (relicManager != null && relicManager.TryPreventLethalDamage(
                damage,
                currentHealth,
                out int survivingHealth))
        {
            return survivingHealth;
        }

        long remainingHealth = (long)currentHealth - damage;
        return remainingHealth <= 0L ? 0 : (int)remainingHealth;
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

    private void ResolveUIReferences()
    {
        if (healthFillImage == null)
        {
            Image[] images = FindObjectsByType<Image>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Image image in images)
            {
                if (image.gameObject.scene.IsValid()
                    && image.name == "Image | Fill Amount")
                {
                    healthFillImage = image;
                    break;
                }
            }
        }

        if (healthText == null)
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (TMP_Text text in texts)
            {
                if (text.gameObject.scene.IsValid()
                    && text.name == "Text | Player HP")
                {
                    healthText = text;
                    break;
                }
            }
        }
    }

    private void CreateDamageFlashVolume()
    {
        GameObject volumeObject = new GameObject(
            "Volume | Player Damage Flash");
        volumeObject.transform.SetParent(transform, false);
        damageFlashVolume = volumeObject.AddComponent<Volume>();
        damageFlashVolume.isGlobal = true;
        damageFlashVolume.priority = 100f;
        damageFlashVolume.weight = 0f;

        damageFlashProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        damageFlashProfile.name = "Player Damage Flash (Runtime)";
        Vignette vignette = damageFlashProfile.Add<Vignette>(true);
        vignette.color.Override(damageVignetteColor);
        vignette.intensity.Override(damageVignetteIntensity);
        vignette.smoothness.Override(damageVignetteSmoothness);
        vignette.rounded.Override(damageVignetteRounded);
        damageFlashVolume.sharedProfile = damageFlashProfile;
        damageFlashVolume.enabled = false;
    }

    private void PlayDamageScreenFlash()
    {
        if (damageFlashVolume == null)
        {
            return;
        }

        damageFlashElapsed = 0f;
        damageFlashVolume.weight = damageFlashWeight;
        damageFlashVolume.enabled = true;
    }
}
