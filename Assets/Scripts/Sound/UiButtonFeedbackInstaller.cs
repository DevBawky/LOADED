using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Discovers scene buttons and installs reusable audio and visual feedback
/// components. This keeps hierarchy policy out of the audio playback service.
/// </summary>
internal sealed class UiButtonFeedbackInstaller
{
    private const float RescanInterval = 0.5f;

    private static readonly string[] SpecialButtonNames =
    {
        "Button | Refresh",
        "Button | Remove",
        "Button | Upgrade",
        "Button | Move",
        "Button | Move (1)",
        "Button | Move L",
        "Button | Move R",
        "Button | Rotate",
        "Button | Wait",
        "Button | Reload",
        "Button | Shoot"
    };

    private static readonly string[] HoverScaleSpriteNames =
    {
        "Button_Delete",
        "Button_Management",
        "Button_Refresh",
        "Button_Settings",
        "Button_Upgrade"
    };

    private static readonly string[] HoverScaleButtonNames =
    {
        "Button | Go To Battle",
        "Button | Pause"
    };

    private float nextScanTime;

    public void Tick()
    {
        if (Time.unscaledTime >= nextScanTime)
        {
            ScanNow();
        }
    }

    public void ScanNow()
    {
        nextScanTime = Time.unscaledTime + RescanInterval;

        foreach (Button button in UnityEngine.Object.FindObjectsByType<Button>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            BindVisual(button);
            BindAudio(button);
        }
    }

    public void BindAudio(Button button)
    {
        if (button == null || IsSpecialButton(button))
        {
            return;
        }

        UiButtonAudioFeedback feedback =
            button.GetComponent<UiButtonAudioFeedback>();

        if (feedback == null)
        {
            feedback = button.gameObject.AddComponent<UiButtonAudioFeedback>();
        }

        feedback.Initialize(button);
    }

    private static void BindVisual(Button button)
    {
        if (button == null || !ShouldUseHoverScale(button))
        {
            return;
        }

        UiButtonSpriteHoverScale hoverScale =
            button.GetComponent<UiButtonSpriteHoverScale>();

        if (hoverScale == null)
        {
            hoverScale =
                button.gameObject.AddComponent<UiButtonSpriteHoverScale>();
        }

        hoverScale.Initialize(button);
    }

    private static bool ShouldUseHoverScale(Button button)
    {
        foreach (string buttonName in HoverScaleButtonNames)
        {
            if (button.name == buttonName)
            {
                return true;
            }
        }

        Image image = button.targetGraphic as Image;

        if (image == null)
        {
            image = button.GetComponent<Image>();
        }

        Sprite sprite = image == null ? null : image.sprite;

        if (sprite == null)
        {
            return false;
        }

        foreach (string spriteName in HoverScaleSpriteNames)
        {
            if (sprite.name == spriteName
                || sprite.name.StartsWith(
                    spriteName + "_",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSpecialButton(Button button)
    {
        foreach (string specialName in SpecialButtonNames)
        {
            if (button.name == specialName)
            {
                return true;
            }
        }

        return false;
    }
}
