using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

internal readonly struct EventChoiceButtonState
{
    public EventChoiceButtonState(
        string label,
        bool interactable,
        bool richText,
        bool visible = true)
    {
        Label = label ?? string.Empty;
        Interactable = interactable;
        RichText = richText;
        Visible = visible;
    }

    public string Label { get; }
    public bool Interactable { get; }
    public bool RichText { get; }
    public bool Visible { get; }
}

internal sealed class EventChoiceButtonPresenter
{
    private const int MaximumVisibleChoices = 3;

    private readonly IReadOnlyList<Button> buttons;
    private readonly IReadOnlyList<TMP_Text> labels;
    private readonly Action<Button> clearRewardPreview;
    private readonly Action<Button> bindButtonSound;

    public EventChoiceButtonPresenter(
        IReadOnlyList<Button> buttons,
        IReadOnlyList<TMP_Text> labels,
        Action<Button> clearRewardPreview,
        Action<Button> bindButtonSound)
    {
        this.buttons = buttons ?? Array.Empty<Button>();
        this.labels = labels ?? Array.Empty<TMP_Text>();
        this.clearRewardPreview = clearRewardPreview;
        this.bindButtonSound = bindButtonSound;
    }

    public void PresentChoices(
        IReadOnlyList<EventChoiceButtonState> states,
        Action<int> onSelected,
        Action<Button, int> configureRewardPreview = null)
    {
        int visibleCount = Math.Min(
            MaximumVisibleChoices,
            states?.Count ?? 0);
        for (int index = 0; index < buttons.Count; index++)
        {
            Button button = buttons[index];
            if (button == null)
            {
                continue;
            }

            ResetButton(button);
            bool visible = index < visibleCount && states[index].Visible;
            button.gameObject.SetActive(visible);
            button.interactable = visible && states[index].Interactable;
            if (!visible)
            {
                continue;
            }

            TMP_Text label = GetLabel(index, button);
            if (label != null)
            {
                label.richText = states[index].RichText;
                label.text = states[index].Label;
            }

            int selectedIndex = index;
            configureRewardPreview?.Invoke(button, selectedIndex);
            button.onClick.AddListener(() => onSelected?.Invoke(selectedIndex));
            bindButtonSound?.Invoke(button);
        }
    }

    public void ShowDynamicChoices(
        IReadOnlyList<string> choiceLabels,
        Action<int> onSelected)
    {
        int count = Math.Min(
            MaximumVisibleChoices,
            choiceLabels?.Count ?? 0);
        EventChoiceButtonState[] states =
            new EventChoiceButtonState[count];
        for (int index = 0; index < count; index++)
        {
            states[index] = new EventChoiceButtonState(
                choiceLabels[index],
                true,
                false);
        }

        PresentChoices(states, onSelected);
    }

    public void ShowExternalSelectionControls(
        string confirmLabel,
        Func<bool> confirmAction,
        Action cancelAction)
    {
        for (int index = 0; index < buttons.Count; index++)
        {
            Button button = buttons[index];
            if (button == null)
            {
                continue;
            }

            ResetButton(button);
            bool visible = index < 2;
            button.gameObject.SetActive(visible);
            button.interactable = visible;
            if (!visible)
            {
                continue;
            }

            TMP_Text label = GetLabel(index, button);
            if (index == 0)
            {
                if (label != null)
                {
                    label.text = confirmLabel;
                }

                button.onClick.AddListener(() => confirmAction?.Invoke());
            }
            else
            {
                if (label != null)
                {
                    label.text = "취소";
                }

                button.onClick.AddListener(() => cancelAction?.Invoke());
            }
        }
    }

    public void SetPrimaryLabel(string value)
    {
        if (buttons.Count == 0 || buttons[0] == null)
        {
            return;
        }

        TMP_Text label = GetLabel(0, buttons[0]);
        if (label != null)
        {
            label.text = value;
        }
    }

    public void ShowSingleAction(string labelText, Action action)
    {
        for (int index = 0; index < buttons.Count; index++)
        {
            Button button = buttons[index];
            if (button == null)
            {
                continue;
            }

            ResetButton(button);
            bool visible = index == 0;
            button.gameObject.SetActive(visible);
            button.interactable = visible;
            if (!visible)
            {
                continue;
            }

            TMP_Text label = GetLabel(index, button);
            if (label != null)
            {
                label.text = labelText;
            }

            button.onClick.AddListener(() => action?.Invoke());
        }
    }

    private void ResetButton(Button button)
    {
        button.onClick.RemoveAllListeners();
        clearRewardPreview?.Invoke(button);
    }

    private TMP_Text GetLabel(int index, Button button)
    {
        return index < labels.Count && labels[index] != null
            ? labels[index]
            : button.GetComponentInChildren<TMP_Text>(true);
    }
}
