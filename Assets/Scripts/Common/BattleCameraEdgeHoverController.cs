using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Temporarily moves the battle camera to either end of the board while the
/// pointer is over the matching edge area of the main-game UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCameraEdgeHoverController : MonoBehaviour
{
    private enum HoveredEdge
    {
        None,
        Left,
        Right
    }

    [Header("Hover Areas")]
    [SerializeField] private RectTransform leftArea;
    [SerializeField] private RectTransform rightArea;

    [Header("Camera Framing")]
    [Tooltip("Distance from the screen edge to the centre of the end tile, as a fraction of screen width.")]
    [Range(0f, 0.25f)]
    [SerializeField] private float edgeTileViewportInset = 0.08f;

    [Header("Hover Area Feedback")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumAreaAlpha = 0.5f;
    [Range(0.5f, 1f)]
    [SerializeField] private float maximumAlphaScreenPosition = 0.9f;

    private BoardManager boardManager;
    private Camera targetCamera;
    private CinemachineCamera cinemachineCamera;
    private Canvas rootCanvas;
    private Transform playerTarget;
    private Transform edgeTarget;
    private Image leftAreaImage;
    private Image rightAreaImage;
    private HoveredEdge hoveredEdge;

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!ResolveReferences() || Mouse.current == null)
        {
            RestorePlayerFocus();
            SetImageAlpha(leftAreaImage, 0f);
            SetImageAlpha(rightAreaImage, 0f);
            return;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        UpdateAreaAlpha(pointerPosition.x);

        Camera uiCamera = rootCanvas != null
            && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;

        HoveredEdge nextEdge = HoveredEdge.None;

        if (leftArea.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(
                leftArea,
                pointerPosition,
                uiCamera))
        {
            nextEdge = HoveredEdge.Left;
        }
        else if (rightArea.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(
                rightArea,
                pointerPosition,
                uiCamera))
        {
            nextEdge = HoveredEdge.Right;
        }

        if (nextEdge == HoveredEdge.None)
        {
            RestorePlayerFocus();
            return;
        }

        FocusBoardEdge(nextEdge);
    }

    private void OnDisable()
    {
        RestorePlayerFocus();
        SetImageAlpha(leftAreaImage, 0f);
        SetImageAlpha(rightAreaImage, 0f);
    }

    private void OnDestroy()
    {
        RestorePlayerFocus();

        if (edgeTarget != null)
        {
            Destroy(edgeTarget.gameObject);
        }
    }

    private bool ResolveReferences()
    {
        leftArea ??= transform.Find("Area | Left") as RectTransform;
        rightArea ??= transform.Find("Area | Right") as RectTransform;

        if (leftArea != null)
        {
            leftAreaImage ??= EnsureAreaImage(leftArea);
        }

        if (rightArea != null)
        {
            rightAreaImage ??= EnsureAreaImage(rightArea);
        }

        rootCanvas ??= GetComponentInParent<Canvas>();
        boardManager ??= FindFirstObjectByType<BoardManager>();
        targetCamera ??= Camera.main;

        if (cinemachineCamera == null && targetCamera != null)
        {
            cinemachineCamera = targetCamera.GetComponent<CinemachineCamera>();
        }

        return leftArea != null
            && rightArea != null
            && boardManager != null
            && targetCamera != null
            && cinemachineCamera != null;
    }

    private void UpdateAreaAlpha(float pointerX)
    {
        float halfScreenWidth = Screen.width * 0.5f;

        if (halfScreenWidth <= 0f)
        {
            SetImageAlpha(leftAreaImage, 0f);
            SetImageAlpha(rightAreaImage, 0f);
            return;
        }

        float directionalDistance = Mathf.Abs(pointerX - halfScreenWidth)
            / halfScreenWidth;
        float alpha = Mathf.InverseLerp(
            0f,
            maximumAlphaScreenPosition,
            directionalDistance) * maximumAreaAlpha;

        SetImageAlpha(
            leftAreaImage,
            pointerX < halfScreenWidth ? alpha : 0f);
        SetImageAlpha(
            rightAreaImage,
            pointerX > halfScreenWidth ? alpha : 0f);
    }

    private static Image EnsureAreaImage(RectTransform area)
    {
        Image image = area.GetComponent<Image>();

        if (image == null)
        {
            image = area.gameObject.AddComponent<Image>();
        }

        image.raycastTarget = false;
        SetImageAlpha(image, 0f);
        return image;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void FocusBoardEdge(HoveredEdge edge)
    {
        if (boardManager.BoardCount < 2
            || !boardManager.TryGetTilePosition(0, out Vector3 firstTilePosition)
            || !boardManager.TryGetTilePosition(
                boardManager.BoardCount - 1,
                out Vector3 lastTilePosition))
        {
            RestorePlayerFocus();
            return;
        }

        // Resolve edges by world X rather than tile index so a flipped or
        // rotated board parent can never reverse the UI directions.
        Vector3 leftTilePosition = firstTilePosition.x <= lastTilePosition.x
            ? firstTilePosition
            : lastTilePosition;
        Vector3 rightTilePosition = firstTilePosition.x >= lastTilePosition.x
            ? firstTilePosition
            : lastTilePosition;
        Vector3 tilePosition = edge == HoveredEdge.Left
            ? leftTilePosition
            : rightTilePosition;

        if (hoveredEdge == HoveredEdge.None)
        {
            playerTarget = cinemachineCamera.Follow;
        }

        EnsureEdgeTarget();

        float halfViewWidth = targetCamera.orthographicSize * targetCamera.aspect;
        float screenInset = halfViewWidth * 2f * edgeTileViewportInset;
        float targetX = edge == HoveredEdge.Left
            ? tilePosition.x + halfViewWidth - screenInset
            : tilePosition.x - halfViewWidth + screenInset;

        float boardCenterX = (leftTilePosition.x + rightTilePosition.x) * 0.5f;
        float minimumDirectionalOffset = Mathf.Max(
            0.01f,
            boardManager.BoardDistance * 0.5f);
        targetX = edge == HoveredEdge.Left
            ? Mathf.Min(targetX, boardCenterX - minimumDirectionalOffset)
            : Mathf.Max(targetX, boardCenterX + minimumDirectionalOffset);

        Vector3 targetPosition = playerTarget != null
            ? playerTarget.position
            : tilePosition;
        targetPosition.x = targetX;
        edgeTarget.position = targetPosition;

        cinemachineCamera.Follow = edgeTarget;
        hoveredEdge = edge;
    }

    private void RestorePlayerFocus()
    {
        if (hoveredEdge == HoveredEdge.None)
        {
            return;
        }

        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = playerTarget;
        }

        hoveredEdge = HoveredEdge.None;
        playerTarget = null;
    }

    private void EnsureEdgeTarget()
    {
        if (edgeTarget != null)
        {
            return;
        }

        GameObject targetObject = new GameObject("Camera Edge Hover Target");
        targetObject.hideFlags = HideFlags.HideAndDontSave;
        edgeTarget = targetObject.transform;
    }
}
