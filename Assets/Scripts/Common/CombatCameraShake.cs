using System.Collections;
using UnityEngine;

public sealed class CombatCameraShake : MonoBehaviour
{
    private static CombatCameraShake instance;
    private Coroutine shakeRoutine;
    private Vector3 baseLocalPosition;

    public static void Play(float strength)
    {
        if (strength <= 0f || Camera.main == null)
        {
            return;
        }

        if (instance == null)
        {
            instance = Camera.main.GetComponent<CombatCameraShake>();
        }

        if (instance == null)
        {
            instance = Camera.main.gameObject.AddComponent<CombatCameraShake>();
        }

        instance.StartShake(strength);
    }

    private void Awake()
    {
        instance = this;
        baseLocalPosition = transform.localPosition;
    }

    private void StartShake(float strength)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            RestoreCameraTransform();
        }

        baseLocalPosition = transform.localPosition;
        transform.localRotation = Quaternion.identity;
        shakeRoutine = StartCoroutine(ShakeRoutine(strength));
    }

    private IEnumerator ShakeRoutine(float strength)
    {
        const float duration = 0.18f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * strength * fade;
            transform.localPosition = baseLocalPosition
                + new Vector3(offset.x, offset.y, 0f);
        }

        RestoreCameraTransform();
        shakeRoutine = null;
    }

    private void OnDisable()
    {
        RestoreCameraTransform();
        shakeRoutine = null;

        if (instance == this)
        {
            instance = null;
        }
    }

    private void RestoreCameraTransform()
    {
        transform.localPosition = baseLocalPosition;
        transform.localRotation = Quaternion.identity;
    }
}
