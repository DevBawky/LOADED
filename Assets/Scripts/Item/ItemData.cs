using UnityEngine;

public enum ItemEffectType
{
    Heal = 0,
    ReshuffleDeck = 1
}

[CreateAssetMenu(fileName = "New Item", menuName = "Loaded/Item")]
public class ItemData : ScriptableObject
{
    [Header("Basic Information")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [Min(0)]
    [SerializeField] private int price;

    [Header("Immediate Effect")]
    [SerializeField] private ItemEffectType effectType;
    [Min(0)]
    [Tooltip("Heal amount. Ignored by effects that do not use a numeric value.")]
    [SerializeField] private int effectAmount;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int Price => Mathf.Max(0, price);
    public ItemEffectType EffectType => effectType;
    public int EffectAmount => Mathf.Max(0, effectAmount);

    public bool TryApply(PlayerHealth playerHealth, DeckManager deckManager)
    {
        return effectType switch
        {
            ItemEffectType.Heal => playerHealth != null
                && playerHealth.Heal(EffectAmount),
            ItemEffectType.ReshuffleDeck => deckManager != null
                && deckManager.ReshuffleDeck(),
            _ => false
        };
    }
}
