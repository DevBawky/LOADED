using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActorMotion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform actorTransform;
    [SerializeField] private Transform orientationLockedTransform;

    [Header("Move Motion")]
    [Min(0f)]
    [SerializeField] private float moveDuration = 0.2f;
    [Min(0f)]
    [SerializeField] private float jumpHeight = 0.25f;

    [Header("Rotate Motion")]
    [Min(0f)]
    [SerializeField] private float rotateDuration = 0.2f;

    private Vector3 orientationLockedBaseScale;

    public bool IsAnimating { get; private set; }
    public float MoveDuration => moveDuration;
    public float JumpHeight => jumpHeight;
    public float RotateDuration => rotateDuration;

    private void Awake()
    {
        if (orientationLockedTransform != null)
        {
            orientationLockedBaseScale = orientationLockedTransform.localScale;
        }

        RefreshOrientationLock();
    }

    private void OnDisable()
    {
        IsAnimating = false;
    }

    public IEnumerator MoveTo(Vector3 targetPosition)
    {
        Vector3[] path = { targetPosition };
        yield return MoveAlongPath(path);
    }

    public IEnumerator MoveAlongPath(IReadOnlyList<Vector3> path)
    {
        yield return MoveAlongPath(path, moveDuration);
    }

    public IEnumerator MoveAlongPath(
        IReadOnlyList<Vector3> path,
        float durationPerStep)
    {
        if (actorTransform == null || path == null || path.Count == 0)
        {
            yield break;
        }

        IsAnimating = true;

        for (int pathIndex = 0; pathIndex < path.Count; pathIndex++)
        {
            yield return MoveBetweenPositions(
                path[pathIndex],
                Mathf.Max(0f, durationPerStep));
        }

        IsAnimating = false;
    }

    public IEnumerator FlyTo(Vector3 targetPosition, float duration)
    {
        if (actorTransform == null)
        {
            yield break;
        }

        IsAnimating = true;
        yield return MoveBetweenPositions(
            targetPosition,
            Mathf.Max(0f, duration));
        IsAnimating = false;
    }

    public IEnumerator FlyIntoCollision(
        Vector3 impactPosition,
        Vector3 restingPosition,
        float flightDuration,
        float settleDuration)
    {
        if (actorTransform == null)
        {
            yield break;
        }

        IsAnimating = true;
        yield return MoveBetweenPositions(
            impactPosition,
            Mathf.Max(0f, flightDuration));
        yield return SettleFromImpact(
            restingPosition,
            Mathf.Max(0f, settleDuration));
        IsAnimating = false;
    }

    public IEnumerator RotateToDirection(int direction)
    {
        if (actorTransform == null || direction == 0)
        {
            yield break;
        }

        IsAnimating = true;
        int normalizedDirection = direction > 0 ? 1 : -1;
        Vector3 startScale = actorTransform.localScale;
        float scaleMagnitude = Mathf.Abs(startScale.x);

        if (scaleMagnitude <= Mathf.Epsilon)
        {
            scaleMagnitude = 1f;
        }

        float targetScaleX = scaleMagnitude * normalizedDirection;

        if (rotateDuration <= 0f)
        {
            SetActorScaleX(targetScaleX, normalizedDirection);
            IsAnimating = false;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < rotateDuration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / rotateDuration);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            float scaleX = Mathf.Lerp(startScale.x, targetScaleX, smoothProgress);
            SetActorScaleX(scaleX, normalizedDirection);
        }

        SetActorScaleX(targetScaleX, normalizedDirection);
        IsAnimating = false;
    }

    public void RefreshOrientationLock()
    {
        if (actorTransform == null || orientationLockedTransform == null)
        {
            return;
        }

        int direction = actorTransform.localScale.x >= 0f ? 1 : -1;
        ApplyOrientationLock(direction);
    }

    private IEnumerator MoveBetweenPositions(
        Vector3 targetPosition,
        float duration)
    {
        Vector3 startPosition = actorTransform.position;

        if (duration <= 0f)
        {
            actorTransform.position = targetPosition;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 position = Vector3.Lerp(
                startPosition,
                targetPosition,
                smoothProgress);
            position += Vector3.up
                * (Mathf.Sin(progress * Mathf.PI) * jumpHeight);
            actorTransform.position = position;
        }

        actorTransform.position = targetPosition;
    }

    private IEnumerator SettleFromImpact(
        Vector3 targetPosition,
        float duration)
    {
        Vector3 startPosition = actorTransform.position;

        if (duration <= 0f)
        {
            actorTransform.position = targetPosition;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float horizontalProgress = Mathf.SmoothStep(0f, 1f, progress);
            float fallProgress = progress * progress;
            Vector3 position = Vector3.Lerp(
                startPosition,
                targetPosition,
                horizontalProgress);
            position.y = Mathf.Lerp(
                startPosition.y,
                targetPosition.y,
                fallProgress);
            actorTransform.position = position;
        }

        actorTransform.position = targetPosition;
    }

    private void SetActorScaleX(float scaleX, int fallbackDirection)
    {
        Vector3 localScale = actorTransform.localScale;
        localScale.x = scaleX;
        actorTransform.localScale = localScale;

        int direction = Mathf.Abs(scaleX) <= Mathf.Epsilon
            ? fallbackDirection
            : scaleX > 0f ? 1 : -1;
        ApplyOrientationLock(direction);
    }

    private void ApplyOrientationLock(int actorDirection)
    {
        if (orientationLockedTransform == null)
        {
            return;
        }

        float scaleMagnitude = Mathf.Abs(orientationLockedBaseScale.x);

        if (scaleMagnitude <= Mathf.Epsilon)
        {
            scaleMagnitude = 1f;
        }

        Vector3 localScale = orientationLockedTransform.localScale;
        localScale.x = scaleMagnitude * actorDirection;
        orientationLockedTransform.localScale = localScale;
    }
}
