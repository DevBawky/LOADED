using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
internal sealed class UiButtonAudioFeedback : MonoBehaviour,
    IPointerEnterHandler
{
    private const string SfxId = "UI_Button_Hover_Click";
    private const float ClickPitchMultiplier = 0.9f;

    private Button button;
    private bool clickSubscribed;

    public void Initialize(Button targetButton)
    {
        if (button == targetButton && clickSubscribed)
        {
            return;
        }

        UnsubscribeClick();
        button = targetButton;
        SubscribeClick();
    }

    private void OnEnable()
    {
        SubscribeClick();
    }

    private void OnDisable()
    {
        UnsubscribeClick();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && button.IsActive() && button.IsInteractable())
        {
            SoundManager.PlaySfx(SfxId);
        }
    }

    private void HandleClick()
    {
        SoundManager.PlaySfxPitched(SfxId, ClickPitchMultiplier);
    }

    private void SubscribeClick()
    {
        if (clickSubscribed || button == null)
        {
            return;
        }

        button.onClick.AddListener(HandleClick);
        clickSubscribed = true;
    }

    private void UnsubscribeClick()
    {
        if (!clickSubscribed)
        {
            return;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }

        clickSubscribed = false;
    }
}
