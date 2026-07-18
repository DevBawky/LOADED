using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DebuffIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;

    public void Initialize(Sprite sprite, int stacks)
    {
        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        SetStacks(stacks);
    }

    public void SetStacks(int stacks)
    {
        if (stackText != null)
        {
            stackText.text = Mathf.Max(0, stacks).ToString();
        }
    }
}
