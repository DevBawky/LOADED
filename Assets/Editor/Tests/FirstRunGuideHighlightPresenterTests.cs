using NUnit.Framework;
using UnityEngine;

public sealed class FirstRunGuideHighlightPresenterTests
{
    [Test]
    public void CalculateHighlightRect_CombinesBoundsAndPadding()
    {
        FirstRunGuideHighlightPresenter.CalculateHighlightRect(
            new Vector2(-120f, -30f),
            new Vector2(80f, 50f),
            12f,
            out Vector2 position,
            out Vector2 size);

        Assert.That(position, Is.EqualTo(new Vector2(-20f, 10f)));
        Assert.That(size, Is.EqualTo(new Vector2(224f, 104f)));
    }

    [Test]
    public void CalculateHighlightRect_NormalizesReversedCorners()
    {
        FirstRunGuideHighlightPresenter.CalculateHighlightRect(
            new Vector2(80f, 50f),
            new Vector2(-120f, -30f),
            12f,
            out Vector2 position,
            out Vector2 size);

        Assert.That(position, Is.EqualTo(new Vector2(-20f, 10f)));
        Assert.That(size, Is.EqualTo(new Vector2(224f, 104f)));
    }

    [Test]
    public void CalculatePulseAlpha_PreservesExistingRangeAndMidpoint()
    {
        float minimumTime = 3f * Mathf.PI / 10f;
        float maximumTime = Mathf.PI / 10f;

        Assert.That(
            FirstRunGuideHighlightPresenter.CalculatePulseAlpha(0f),
            Is.EqualTo(0.12f).Within(0.0001f));
        Assert.That(
            FirstRunGuideHighlightPresenter.CalculatePulseAlpha(minimumTime),
            Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(
            FirstRunGuideHighlightPresenter.CalculatePulseAlpha(maximumTime),
            Is.EqualTo(0.19f).Within(0.0001f));
    }
}
