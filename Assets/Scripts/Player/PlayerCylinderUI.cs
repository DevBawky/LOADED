using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerCylinderUI : MonoBehaviour
{
    private const string CylinderObjectName = "Image | Cylinder";
    private const string MainGamePanelName = "Panel | MainGame";

    [Header("References")]
    [SerializeField] private RectTransform cylinderTransform;
    [SerializeField] private List<Image> bulletImages = new List<Image>();

    [Header("Rotation")]
    [Min(0f)]
    [SerializeField] private float rotationStep = 60f;
    [Min(0f)]
    [SerializeField] private float rotationDuration = 0.15f;

    [Header("Bullet Reordering")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float requiredOverlap = 0.35f;
    [Min(0f)]
    [SerializeField] private float slotMoveDuration = 0.15f;

    private DeckManager deckManager;
    private PlayerShoot playerShoot;
    private Coroutine rotationCoroutine;
    private readonly List<Vector2> chamberPositions = new List<Vector2>();
    private readonly Dictionary<RectTransform, Coroutine> slotMoveCoroutines =
        new Dictionary<RectTransform, Coroutine>();
    private int displayedBulletCount;
    private bool isInitialized;
    private bool isSubscribed;
    private bool isDraggingBullet;
    private Image draggedBulletImage;
    private int draggedBulletIndex = -1;
    private int previewTargetIndex = -1;
    private int draggedOriginalSiblingIndex;
    private int dragLoadedCount;
    private Vector2 dragPointerOffset;

    public int DisplayedBulletCount => displayedBulletCount;
    public bool IsDragging => isDraggingBullet;

    private void Awake()
    {
        playerShoot = GetComponent<PlayerShoot>();
        ResolveMovedCylinderReferences();
        PrepareBulletSlots();

        foreach (Image bulletImage in bulletImages)
        {
            if (bulletImage != null)
            {
                bulletImage.gameObject.SetActive(false);
            }
        }

        if (cylinderTransform != null)
        {
            cylinderTransform.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        SubscribeToDeck();

        if (deckManager != null)
        {
            RefreshDisplay(false);
        }
    }

    private void LateUpdate()
    {
        if (cylinderTransform != null
            && cylinderTransform.gameObject.activeInHierarchy)
        {
            KeepBulletImagesUpright();
        }
    }

    private void OnDisable()
    {
        CancelBulletDragImmediately();
        UnsubscribeFromDeck();

        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
            rotationCoroutine = null;
        }
    }

    public void Initialize(DeckManager assignedDeckManager)
    {
        ResolveMovedCylinderReferences();
        PrepareBulletSlots();
        UnsubscribeFromDeck();
        deckManager = assignedDeckManager;
        SubscribeToDeck();
        isInitialized = false;
        RefreshDisplay(false);
    }

    private void ResolveMovedCylinderReferences()
    {
        if (cylinderTransform != null
            && HasUsableBulletReferences())
        {
            return;
        }

        RectTransform[] rectTransforms = FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        RectTransform movedCylinder = null;

        foreach (RectTransform candidate in rectTransforms)
        {
            if (candidate.name != CylinderObjectName
                || !HasAncestorNamed(candidate, MainGamePanelName))
            {
                continue;
            }

            // An added scene object is appended after prefab children. If a
            // prefab apply and the scene save temporarily leave both copies,
            // prefer the object the user most recently moved into MainGame.
            if (movedCylinder == null
                || candidate.GetSiblingIndex() > movedCylinder.GetSiblingIndex())
            {
                movedCylinder = candidate;
            }
        }

        if (movedCylinder == null)
        {
            Debug.LogError(
                $"Could not find '{MainGamePanelName}/{CylinderObjectName}'.",
                this);
            return;
        }

        cylinderTransform = movedCylinder;
        bulletImages.Clear();

        for (int childIndex = 0;
             childIndex < movedCylinder.childCount;
             childIndex++)
        {
            Transform child = movedCylinder.GetChild(childIndex);

            if (child.TryGetComponent(out Image bulletImage))
            {
                bulletImages.Add(bulletImage);
            }
        }

        // The original Player prefab stored the chambers from the top slot
        // counter-clockwise. Transform child order is not guaranteed to use
        // that order after moving the cylinder under the Canvas.
        bulletImages.Sort((left, right) =>
            GetChamberOrder(left.rectTransform)
                .CompareTo(GetChamberOrder(right.rectTransform)));
    }

    private static float GetChamberOrder(RectTransform chamber)
    {
        Vector2 position = chamber.anchoredPosition;
        float angle = Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg;
        return Mathf.Repeat(angle - 90f, 360f);
    }

    private void PrepareBulletSlots()
    {
        chamberPositions.Clear();

        foreach (Image bulletImage in bulletImages)
        {
            if (bulletImage == null)
            {
                chamberPositions.Add(Vector2.zero);
                continue;
            }

            chamberPositions.Add(bulletImage.rectTransform.anchoredPosition);
            bulletImage.raycastTarget = true;
            CylinderBulletDragHandler dragHandler =
                bulletImage.GetComponent<CylinderBulletDragHandler>();

            if (dragHandler == null)
            {
                dragHandler = bulletImage.gameObject.AddComponent<
                    CylinderBulletDragHandler>();
            }

            dragHandler.Initialize(this, bulletImage);
        }
    }

    public bool TryGetLoadedBulletAtScreenPosition(
        Vector2 screenPosition,
        Camera eventCamera,
        out BulletInstance bullet)
    {
        bullet = null;

        if (isDraggingBullet || deckManager == null)
        {
            return false;
        }

        int loadedCount = Mathf.Min(
            deckManager.LoadedBullets.Count,
            bulletImages.Count);

        for (int index = loadedCount - 1; index >= 0; index--)
        {
            Image bulletImage = bulletImages[index];

            if (bulletImage != null
                && bulletImage.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(
                    bulletImage.rectTransform,
                    screenPosition,
                    eventCamera))
            {
                bullet = deckManager.LoadedBullets[index];
                return bullet != null;
            }
        }

        return false;
    }

    internal void BeginBulletDrag(
        Image bulletImage,
        PointerEventData eventData)
    {
        if (isDraggingBullet || bulletImage == null || deckManager == null
            || GamePauseController.IsPaused || rotationCoroutine != null
            || playerShoot != null && playerShoot.IsFiring
            || deckManager.LoadedBullets.Count < 2)
        {
            return;
        }

        int bulletIndex = bulletImages.IndexOf(bulletImage);

        if (bulletIndex < 0
            || bulletIndex >= deckManager.LoadedBullets.Count
            || bulletIndex >= chamberPositions.Count)
        {
            return;
        }

        StopAllSlotMoves();
        isDraggingBullet = true;
        draggedBulletImage = bulletImage;
        draggedBulletIndex = bulletIndex;
        previewTargetIndex = -1;
        dragLoadedCount = Mathf.Min(
            deckManager.LoadedBullets.Count,
            bulletImages.Count);
        RectTransform draggedRect = bulletImage.rectTransform;
        draggedOriginalSiblingIndex = draggedRect.GetSiblingIndex();

        if (TryGetLocalPointerPosition(eventData, out Vector2 localPointer))
        {
            dragPointerOffset = draggedRect.anchoredPosition - localPointer;
        }
        else
        {
            dragPointerOffset = Vector2.zero;
        }

        draggedRect.SetAsLastSibling();
    }

    internal void DragBullet(
        Image bulletImage,
        PointerEventData eventData)
    {
        if (!isDraggingBullet || bulletImage != draggedBulletImage
            || !TryGetLocalPointerPosition(eventData, out Vector2 localPointer))
        {
            return;
        }

        RectTransform draggedRect = draggedBulletImage.rectTransform;
        draggedRect.anchoredPosition = localPointer + dragPointerOffset;
        SetPreviewTarget(FindOverlappingLoadedSlot(
            draggedRect.anchoredPosition));
    }

    internal void EndBulletDrag(
        Image bulletImage,
        PointerEventData eventData)
    {
        if (!isDraggingBullet || bulletImage != draggedBulletImage)
        {
            return;
        }

        DragBullet(bulletImage, eventData);
        int targetIndex = FindOverlappingLoadedSlot(
            draggedBulletImage.rectTransform.anchoredPosition);
        bool committed = targetIndex >= 0
            && targetIndex == previewTargetIndex
            && deckManager != null
            && deckManager.TrySwapLoadedBullets(
                draggedBulletIndex,
                targetIndex);

        if (committed)
        {
            Image displacedImage = bulletImages[targetIndex];
            bulletImages[draggedBulletIndex] = displacedImage;
            bulletImages[targetIndex] = draggedBulletImage;
            StartSlotMove(
                displacedImage.rectTransform,
                chamberPositions[draggedBulletIndex]);
            StartSlotMove(
                draggedBulletImage.rectTransform,
                chamberPositions[targetIndex]);
        }
        else
        {
            RestorePreviewTarget();
            StartSlotMove(
                draggedBulletImage.rectTransform,
                chamberPositions[draggedBulletIndex]);
        }

        draggedBulletImage.rectTransform.SetSiblingIndex(
            draggedOriginalSiblingIndex);
        isDraggingBullet = false;
        draggedBulletImage = null;
        draggedBulletIndex = -1;
        previewTargetIndex = -1;
        dragLoadedCount = 0;

        if (committed)
        {
            RefreshDisplay(false);
        }
    }

    private bool TryGetLocalPointerPosition(
        PointerEventData eventData,
        out Vector2 localPointer)
    {
        localPointer = Vector2.zero;

        return cylinderTransform != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cylinderTransform,
                eventData.position,
                eventData.pressEventCamera,
                out localPointer);
    }

    private int FindOverlappingLoadedSlot(Vector2 draggedPosition)
    {
        if (draggedBulletImage == null)
        {
            return -1;
        }

        float closestDistance = float.MaxValue;
        int closestIndex = -1;
        float draggedRadius = GetImageRadius(draggedBulletImage);

        for (int index = 0; index < dragLoadedCount; index++)
        {
            if (index == draggedBulletIndex || index >= chamberPositions.Count)
            {
                continue;
            }

            Image targetImage = bulletImages[index];

            if (targetImage == null)
            {
                continue;
            }

            float distance = Vector2.Distance(
                draggedPosition,
                chamberPositions[index]);
            float overlapDistance = (draggedRadius
                + GetImageRadius(targetImage)) * (1f - requiredOverlap);

            if (distance <= overlapDistance && distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = index;
            }
        }

        return closestIndex;
    }

    private static float GetImageRadius(Image image)
    {
        RectTransform rect = image.rectTransform;
        float width = rect.rect.width * Mathf.Abs(rect.localScale.x);
        float height = rect.rect.height * Mathf.Abs(rect.localScale.y);
        return Mathf.Min(width, height) * 0.5f;
    }

    private void SetPreviewTarget(int targetIndex)
    {
        if (targetIndex == previewTargetIndex)
        {
            return;
        }

        RestorePreviewTarget();
        previewTargetIndex = targetIndex;

        if (previewTargetIndex >= 0)
        {
            StartSlotMove(
                bulletImages[previewTargetIndex].rectTransform,
                chamberPositions[draggedBulletIndex]);
        }
    }

    private void RestorePreviewTarget()
    {
        if (previewTargetIndex < 0
            || previewTargetIndex >= bulletImages.Count
            || previewTargetIndex >= chamberPositions.Count)
        {
            previewTargetIndex = -1;
            return;
        }

        Image previewImage = bulletImages[previewTargetIndex];

        if (previewImage != null)
        {
            StartSlotMove(
                previewImage.rectTransform,
                chamberPositions[previewTargetIndex]);
        }

        previewTargetIndex = -1;
    }

    private void StartSlotMove(RectTransform target, Vector2 destination)
    {
        if (target == null)
        {
            return;
        }

        StopSlotMove(target);

        if (slotMoveDuration <= 0f)
        {
            target.anchoredPosition = destination;
            return;
        }

        slotMoveCoroutines[target] = StartCoroutine(
            MoveSlot(target, destination));
    }

    private IEnumerator MoveSlot(RectTransform target, Vector2 destination)
    {
        Vector2 start = target.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < slotMoveDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / slotMoveDuration);
            target.anchoredPosition = Vector2.LerpUnclamped(
                start,
                destination,
                Mathf.SmoothStep(0f, 1f, progress));
        }

        target.anchoredPosition = destination;
        slotMoveCoroutines.Remove(target);
    }

    private void StopSlotMove(RectTransform target)
    {
        if (target != null
            && slotMoveCoroutines.TryGetValue(target, out Coroutine coroutine))
        {
            StopCoroutine(coroutine);
            slotMoveCoroutines.Remove(target);
        }
    }

    private void StopAllSlotMoves()
    {
        foreach (Coroutine coroutine in slotMoveCoroutines.Values)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }

        slotMoveCoroutines.Clear();
    }

    private void CancelBulletDragImmediately()
    {
        StopAllSlotMoves();

        if (draggedBulletImage != null
            && draggedBulletIndex >= 0
            && draggedBulletIndex < chamberPositions.Count)
        {
            draggedBulletImage.rectTransform.anchoredPosition =
                chamberPositions[draggedBulletIndex];
            draggedBulletImage.rectTransform.SetSiblingIndex(
                draggedOriginalSiblingIndex);
        }

        if (previewTargetIndex >= 0
            && previewTargetIndex < bulletImages.Count
            && previewTargetIndex < chamberPositions.Count
            && bulletImages[previewTargetIndex] != null)
        {
            bulletImages[previewTargetIndex].rectTransform.anchoredPosition =
                chamberPositions[previewTargetIndex];
        }

        isDraggingBullet = false;
        draggedBulletImage = null;
        draggedBulletIndex = -1;
        previewTargetIndex = -1;
        dragLoadedCount = 0;
    }

    private bool HasUsableBulletReferences()
    {
        if (bulletImages == null || bulletImages.Count == 0)
        {
            return false;
        }

        foreach (Image bulletImage in bulletImages)
        {
            if (bulletImage == null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAncestorNamed(Transform child, string ancestorName)
    {
        Transform current = child.parent;

        while (current != null)
        {
            if (current.name == ancestorName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void HandleDeckStateChanged()
    {
        if (isDraggingBullet)
        {
            return;
        }

        RefreshDisplay(true);
    }

    private void HandleLoadedBulletsCleared()
    {
        RefreshDisplay(false);
    }

    private void RefreshDisplay(bool animateRotation)
    {
        if (cylinderTransform == null)
        {
            return;
        }

        int loadedCount = deckManager == null
            ? 0
            : Mathf.Min(deckManager.LoadedBullets.Count, bulletImages.Count);

        for (int imageIndex = 0;
             imageIndex < bulletImages.Count;
             imageIndex++)
        {
            Image bulletImage = bulletImages[imageIndex];

            if (bulletImage == null)
            {
                continue;
            }

            bool isLoaded = imageIndex < loadedCount;
            bulletImage.gameObject.SetActive(isLoaded);

            if (isLoaded)
            {
                ApplyBulletImage(
                    bulletImage,
                    deckManager.LoadedBullets[imageIndex]);
            }
        }

        if (!isInitialized)
        {
            displayedBulletCount = loadedCount;
            SetCylinderAngle(loadedCount > 0
                ? -(loadedCount - 1) * rotationStep
                : 0f);
            cylinderTransform.gameObject.SetActive(loadedCount > 0);
            isInitialized = true;
            return;
        }

        int previousCount = displayedBulletCount;
        displayedBulletCount = loadedCount;

        if (loadedCount > 0)
        {
            cylinderTransform.gameObject.SetActive(true);

            if (previousCount == 0)
            {
                SetCylinderAngle(0f);
            }
        }

        float currentAngle = cylinderTransform.localEulerAngles.z;
        float targetAngle = currentAngle;

        if (loadedCount > previousCount)
        {
            int addedBulletCount = loadedCount - previousCount;

            if (previousCount == 0)
            {
                addedBulletCount--;
            }

            targetAngle -= Mathf.Max(0, addedBulletCount) * rotationStep;
        }
        else if (loadedCount < previousCount)
        {
            targetAngle += (previousCount - loadedCount) * rotationStep;
        }

        StartCylinderRotation(
            targetAngle,
            animateRotation,
            loadedCount == 0);
    }

    private void ApplyBulletImage(Image bulletImage, BulletInstance bulletData)
    {
        Sprite cylinderIcon = bulletData == null
            ? null
            : bulletData.CylinderIcon;
        bulletImage.sprite = cylinderIcon;
        bulletImage.color = new Color(1f, 1f, 1f, 1f);
        bulletImage.preserveAspect = true;
        bulletImage.enabled = cylinderIcon != null;
    }

    private void StartCylinderRotation(
        float targetAngle,
        bool animateRotation,
        bool hideWhenComplete)
    {
        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
            rotationCoroutine = null;
        }

        if (!animateRotation || rotationDuration <= 0f)
        {
            SetCylinderAngle(targetAngle);

            if (hideWhenComplete)
            {
                cylinderTransform.gameObject.SetActive(false);
                SetCylinderAngle(0f);
            }

            return;
        }

        rotationCoroutine = StartCoroutine(
            RotateCylinder(targetAngle, hideWhenComplete));
    }

    private IEnumerator RotateCylinder(float targetAngle, bool hideWhenComplete)
    {
        float startAngle = cylinderTransform.localEulerAngles.z;
        float elapsedTime = 0f;

        while (elapsedTime < rotationDuration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / rotationDuration);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            SetCylinderAngle(Mathf.LerpAngle(
                startAngle,
                targetAngle,
                smoothProgress));
        }

        SetCylinderAngle(targetAngle);

        if (hideWhenComplete)
        {
            cylinderTransform.gameObject.SetActive(false);
            SetCylinderAngle(0f);
        }

        rotationCoroutine = null;
    }

    private void SetCylinderAngle(float angle)
    {
        Vector3 localEulerAngles = cylinderTransform.localEulerAngles;
        localEulerAngles.z = angle;
        cylinderTransform.localEulerAngles = localEulerAngles;
        KeepBulletImagesUpright();
    }

    private void KeepBulletImagesUpright()
    {
        foreach (Image bulletImage in bulletImages)
        {
            if (bulletImage != null)
            {
                bulletImage.rectTransform.rotation = Quaternion.identity;
            }
        }
    }

    private void SubscribeToDeck()
    {
        if (deckManager == null || isSubscribed)
        {
            return;
        }

        deckManager.StateChanged += HandleDeckStateChanged;
        deckManager.LoadedBulletsCleared += HandleLoadedBulletsCleared;
        isSubscribed = true;
    }

    private void UnsubscribeFromDeck()
    {
        if (deckManager != null && isSubscribed)
        {
            deckManager.StateChanged -= HandleDeckStateChanged;
            deckManager.LoadedBulletsCleared -= HandleLoadedBulletsCleared;
        }

        isSubscribed = false;
    }
}

public sealed class CylinderBulletDragHandler : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private PlayerCylinderUI cylinderUI;
    private Image bulletImage;

    public void Initialize(PlayerCylinderUI owner, Image image)
    {
        cylinderUI = owner;
        bulletImage = image;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        cylinderUI?.BeginBulletDrag(bulletImage, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        cylinderUI?.DragBullet(bulletImage, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        cylinderUI?.EndBulletDrag(bulletImage, eventData);
    }
}
