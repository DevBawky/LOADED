using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the short full-screen color flash shown for a fired bullet.
/// </summary>
[DisallowMultipleComponent]
internal sealed class BulletShotFeedbackView : MonoBehaviour
{
    private const float StartAlpha = 0.2f;

    private Image image;
    private Coroutine fadeCoroutine;

    public void Initialize(Image targetImage)
    {
        if (image == targetImage)
        {
            return;
        }

        StopFade();
        image = targetImage;
        Hide();
    }

    public void Show(BulletInstance bullet, float duration)
    {
        if (image == null || bullet == null)
        {
            return;
        }

        StopFade();

        Color color = bullet.PrimaryLineColor;
        color.a = StartAlpha;
        image.raycastTarget = false;
        image.color = color;
        image.gameObject.SetActive(true);

        if (duration <= 0f)
        {
            Hide();
            return;
        }

        fadeCoroutine = StartCoroutine(Fade(color, duration));
    }

    public void Hide()
    {
        StopFade();

        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = 0f;
        image.raycastTarget = false;
        image.color = color;
        image.gameObject.SetActive(false);
    }

    private IEnumerator Fade(Color color, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            if (image == null)
            {
                fadeCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(
                StartAlpha,
                0f,
                Mathf.Clamp01(elapsed / duration));
            image.color = color;
        }

        fadeCoroutine = null;
        Hide();
    }

    private void OnDisable()
    {
        Hide();
    }

    private void StopFade()
    {
        if (fadeCoroutine == null)
        {
            return;
        }

        StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }
}
