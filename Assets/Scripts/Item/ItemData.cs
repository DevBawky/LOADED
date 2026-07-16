using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Loaded/Item")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    public string DisplayName => displayName;
    public Sprite Icon => icon;
}
