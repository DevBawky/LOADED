using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class FirstRunGuideRuntimePresentationTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject canvasObject;

    [TearDown]
    public void TearDown()
    {
        if (canvasObject != null)
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    [Test]
    public void BuildInterface_PreservesRuntimeHierarchyAndVideoSettings()
    {
        canvasObject = new GameObject(
            "Guide Test Canvas",
            typeof(RectTransform),
            typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        GameObject controllerObject = new GameObject("Guide Controller");
        controllerObject.transform.SetParent(canvasObject.transform, false);
        controllerObject.SetActive(false);
        FirstRunGuideController controller =
            controllerObject.AddComponent<FirstRunGuideController>();
        SetField(controller, "rootCanvas", canvas);

        typeof(FirstRunGuideController)
            .GetMethod("BuildInterface", PrivateInstance)
            .Invoke(controller, null);

        Transform guideRoot = FindDescendant(
            canvasObject.transform,
            "Guide | First Run");
        Transform card = FindDescendant(
            guideRoot,
            "Panel | Guide Card");
        Transform missionBar = FindDescendant(
            guideRoot,
            "Panel | Guide Mission");
        Transform videoFrame = FindDescendant(
            card,
            "Image | Guide Video Frame");
        Transform videoDisplay = FindDescendant(
            videoFrame,
            "RawImage | Guide Video");
        Transform playerObject = FindDescendant(
            guideRoot,
            "VideoPlayer | First Run Guide");

        Assert.That(guideRoot, Is.Not.Null);
        Assert.That(card, Is.Not.Null);
        Assert.That(missionBar, Is.Not.Null);
        Assert.That(videoFrame, Is.Not.Null);
        Assert.That(videoDisplay, Is.Not.Null);
        Assert.That(playerObject, Is.Not.Null);
        Assert.That(
            FindDescendant(card, "Toggle | Never Show Guide"),
            Is.Not.Null);
        Assert.That(
            FindDescendant(card, "Panel | Warning Sound Demo"),
            Is.Not.Null);
        Assert.That(
            FindDescendant(card, "Panel | Debuff Legend"),
            Is.Not.Null);

        Canvas guideCanvas = guideRoot.GetComponent<Canvas>();
        Assert.That(guideCanvas, Is.Not.Null);
        Assert.That(guideCanvas.overrideSorting, Is.True);
        Assert.That(
            guideCanvas.sortingOrder,
            Is.EqualTo(FirstRunGuideController.GuideSortingOrder));
        Assert.That(guideRoot.GetComponent<GraphicRaycaster>(), Is.Not.Null);

        VideoPlayer player = playerObject.GetComponent<VideoPlayer>();
        Assert.That(player, Is.Not.Null);
        Assert.That(player.playOnAwake, Is.False);
        Assert.That(player.source, Is.EqualTo(VideoSource.Url));
        Assert.That(player.renderMode, Is.EqualTo(VideoRenderMode.APIOnly));
        Assert.That(
            player.audioOutputMode,
            Is.EqualTo(VideoAudioOutputMode.None));
        Assert.That(player.isLooping, Is.True);
        Assert.That(player.waitForFirstFrame, Is.True);
        Assert.That(player.sendFrameReadyEvents, Is.True);
        Assert.That(
            player.timeUpdateMode,
            Is.EqualTo(VideoTimeUpdateMode.UnscaledGameTime));
        Assert.That(
            videoDisplay.GetComponent<AspectRatioFitter>().aspectRatio,
            Is.EqualTo(16f / 9f).Within(0.0001f));

        Assert.That(card.gameObject.activeSelf, Is.False);
        Assert.That(missionBar.gameObject.activeSelf, Is.False);
        Assert.That(guideRoot.gameObject.activeSelf, Is.False);
    }

    [TestCase(0UL, 0UL, 16f / 9f)]
    [TestCase(1920UL, 1080UL, 16f / 9f)]
    [TestCase(16384UL, 4096UL, 2f)]
    public void VideoAspectRatio_UsesFallbackAndClampsDecodedDimensions(
        ulong width,
        ulong height,
        float expected)
    {
        Assert.That(
            FirstRunGuideVideoPresenter.CalculateAspectRatio(width, height),
            Is.EqualTo(expected).Within(0.0001f));
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (child.name == name)
            {
                return child;
            }

            Transform match = FindDescendant(child, name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void SetField(
        object target,
        string fieldName,
        object value)
    {
        target.GetType()
            .GetField(fieldName, PrivateInstance)
            .SetValue(target, value);
    }
}
