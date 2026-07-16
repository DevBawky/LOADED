using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Loaded/Item")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
}
