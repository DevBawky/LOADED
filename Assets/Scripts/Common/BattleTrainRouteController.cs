using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleTrainRouteController : MonoBehaviour
{
    [Header("Route Points")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform firstDestination;
    [SerializeField] private Transform secondPoint;
    [SerializeField] private Transform secondDestination;

    [Header("Timing")]
    [Min(0f)]
    [SerializeField] private float travelDuration = 35f;
    [Min(0f)]
    [SerializeField] private float destinationWaitDuration = 10f;

    private Coroutine routeRoutine;
    private Vector3 baseLocalScale;

    private void Awake()
    {
        baseLocalScale = transform.localScale;
        if (Mathf.Abs(baseLocalScale.x) <= Mathf.Epsilon)
        {
            baseLocalScale.x = 1f;
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!HasCompleteRoute())
        {
            Debug.LogError(
                $"{nameof(BattleTrainRouteController)} on '{name}' requires "
                + "all four route points to be assigned in the Inspector.",
                this);
            enabled = false;
            return;
        }

        ApplyRoutePoint(startPoint, 1);
        routeRoutine = StartCoroutine(TravelRoute());
    }

    private void OnDisable()
    {
        if (routeRoutine == null)
        {
            return;
        }

        StopCoroutine(routeRoutine);
        routeRoutine = null;
    }

    private IEnumerator TravelRoute()
    {
        while (enabled)
        {
            yield return MoveTo(firstDestination.position);
            yield return WaitAtDestination();

            ApplyRoutePoint(secondPoint, -1);
            yield return MoveTo(secondDestination.position);
            yield return WaitAtDestination();

            ApplyRoutePoint(startPoint, 1);
        }

        routeRoutine = null;
    }

    private IEnumerator MoveTo(Vector3 destination)
    {
        Vector3 origin = transform.position;
        float duration = Mathf.Max(0f, travelDuration);
        if (duration <= Mathf.Epsilon)
        {
            transform.position = destination;
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
            transform.position = Vector3.Lerp(origin, destination, progress);
        }

        transform.position = destination;
    }

    private IEnumerator WaitAtDestination()
    {
        float duration = Mathf.Max(0f, destinationWaitDuration);
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            yield return null;
            if (!GamePauseController.IsPaused)
            {
                elapsedTime += Time.deltaTime;
            }
        }
    }

    private void ApplyRoutePoint(Transform routePoint, int facingDirection)
    {
        transform.position = routePoint.position;

        Vector3 localScale = baseLocalScale;
        localScale.x = Mathf.Abs(baseLocalScale.x)
            * (facingDirection < 0 ? -1f : 1f);
        transform.localScale = localScale;
    }

    private bool HasCompleteRoute()
    {
        return startPoint != null
            && firstDestination != null
            && secondPoint != null
            && secondDestination != null;
    }
}
