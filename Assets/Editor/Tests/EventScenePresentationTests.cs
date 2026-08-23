using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EventScenePresentationTests
{
    private readonly List<GameObject> createdObjects =
        new List<GameObject>();
    private readonly List<Sprite> createdSprites = new List<Sprite>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
        }

        foreach (Sprite sprite in createdSprites)
        {
            if (sprite != null)
            {
                UnityEngine.Object.DestroyImmediate(sprite);
            }
        }

        createdObjects.Clear();
        createdSprites.Clear();
    }

    [Test]
    public void PresentChoices_ShowsAtMostThreeAndPreservesSelectedIndex()
    {
        CreateChoiceViews(4, out Button[] buttons, out TMP_Text[] labels);
        int selectedIndex = -1;
        int clearedCount = 0;
        int previewCount = 0;
        int soundCount = 0;
        EventChoiceButtonPresenter presenter = new EventChoiceButtonPresenter(
            buttons,
            labels,
            _ => clearedCount++,
            _ => soundCount++);
        EventChoiceButtonState[] states =
        {
            new EventChoiceButtonState("First", true, true),
            new EventChoiceButtonState("Second", false, true),
            new EventChoiceButtonState("Third", true, false),
            new EventChoiceButtonState("Ignored", true, false)
        };

        presenter.PresentChoices(
            states,
            index => selectedIndex = index,
            (_, _) => previewCount++);

        Assert.That(buttons[0].gameObject.activeSelf, Is.True);
        Assert.That(buttons[1].interactable, Is.False);
        Assert.That(buttons[2].gameObject.activeSelf, Is.True);
        Assert.That(buttons[3].gameObject.activeSelf, Is.False);
        Assert.That(labels[0].text, Is.EqualTo("First"));
        Assert.That(labels[0].richText, Is.True);
        Assert.That(labels[2].richText, Is.False);
        Assert.That(clearedCount, Is.EqualTo(4));
        Assert.That(previewCount, Is.EqualTo(3));
        Assert.That(soundCount, Is.EqualTo(3));

        buttons[2].onClick.Invoke();

        Assert.That(selectedIndex, Is.EqualTo(2));
    }

    [Test]
    public void ShowExternalSelectionControls_UsesFirstTwoButtonsOnly()
    {
        CreateChoiceViews(3, out Button[] buttons, out TMP_Text[] labels);
        bool confirmed = false;
        bool cancelled = false;
        EventChoiceButtonPresenter presenter = new EventChoiceButtonPresenter(
            buttons,
            labels,
            _ => { },
            _ => { });

        presenter.ShowExternalSelectionControls(
            "Confirm",
            () => confirmed = true,
            () => cancelled = true);

        Assert.That(labels[0].text, Is.EqualTo("Confirm"));
        Assert.That(labels[1].text, Is.EqualTo("취소"));
        Assert.That(buttons[2].gameObject.activeSelf, Is.False);

        buttons[0].onClick.Invoke();
        buttons[1].onClick.Invoke();

        Assert.That(confirmed, Is.True);
        Assert.That(cancelled, Is.True);
    }

    [Test]
    public void PresentResult_ShowsTextWithoutThreeReelSymbols()
    {
        CreateDialogue(out TMP_Text dialogue);
        EventResultPresenter presenter = new EventResultPresenter(
            dialogue,
            null,
            Array.Empty<Image>());

        presenter.Present("Result", Array.Empty<string>(), _ => null);

        Assert.That(presenter.ResultText, Is.Not.Null);
        Assert.That(presenter.ResultText.gameObject.activeSelf, Is.True);
        Assert.That(presenter.ResultText.text, Is.EqualTo("Result"));
        Assert.That(presenter.ReelResultImages, Has.Length.EqualTo(3));
        Assert.That(dialogue.rectTransform.anchorMin.y, Is.EqualTo(0.50f));
    }

    [Test]
    public void PresentResult_ShowsThreeReelsInsteadOfText()
    {
        CreateDialogue(out TMP_Text dialogue);
        Sprite sprite = CreateSprite();
        EventResultPresenter presenter = new EventResultPresenter(
            dialogue,
            null,
            Array.Empty<Image>());

        presenter.Present(
            "Hidden result",
            new[] { "A", "B", "C" },
            _ => sprite);

        Assert.That(presenter.ResultText.gameObject.activeSelf, Is.False);
        Assert.That(presenter.ReelResultImages, Has.Length.EqualTo(3));
        Assert.That(presenter.ReelResultImages, Has.All.Matches<Image>(image =>
            image.gameObject.activeSelf
            && image.enabled
            && image.sprite == sprite));
        Assert.That(dialogue.rectTransform.anchorMin.y, Is.EqualTo(0.50f));

        presenter.Present(string.Empty, Array.Empty<string>(), _ => null);

        Assert.That(presenter.ReelResultImages,
            Has.All.Matches<Image>(image => !image.gameObject.activeSelf));
        Assert.That(dialogue.rectTransform.anchorMin.y, Is.EqualTo(0.39f));
    }

    private void CreateChoiceViews(
        int count,
        out Button[] buttons,
        out TMP_Text[] labels)
    {
        buttons = new Button[count];
        labels = new TMP_Text[count];
        for (int index = 0; index < count; index++)
        {
            GameObject buttonObject = new GameObject(
                $"Button {index}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            createdObjects.Add(buttonObject);
            GameObject labelObject = new GameObject(
                $"Label {index}",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            buttons[index] = buttonObject.GetComponent<Button>();
            labels[index] = labelObject.GetComponent<TMP_Text>();
        }
    }

    private void CreateDialogue(out TMP_Text dialogue)
    {
        GameObject root = new GameObject("Root", typeof(RectTransform));
        createdObjects.Add(root);
        GameObject dialogueObject = new GameObject(
            "Dialogue",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        dialogueObject.transform.SetParent(root.transform, false);
        dialogue = dialogueObject.GetComponent<TMP_Text>();
        dialogue.fontSize = 20f;
        dialogue.rectTransform.anchorMin = new Vector2(0f, 0.39f);
    }

    private Sprite CreateSprite()
    {
        Texture2D texture = Texture2D.whiteTexture;
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            Vector2.one * 0.5f);
        createdSprites.Add(sprite);
        return sprite;
    }
}
