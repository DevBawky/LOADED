using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerCylinderUI : MonoBehaviour
{
    private const string CylinderObjectName = "Image | Cylinder";
    private const string BulletEffectObjectName = "Image | Effect";
    private const string MainGamePanelName = "Panel | MainGame";
    private static readonly int BulletInCylinderParameter =
        Animator.StringToHash("bullet_in_cylinder");

    [Header("References")]
    [SerializeField] private RectTransform cylinderTransform;
    [SerializeField] private List<Image> bulletImages = new List<Image>();
    [SerializeField] private Animator playerAnimator;

    [Header("Rotation")]
    [Min(0f)]
    [SerializeField] private float rotationStep = 60f;
    [Min(0f)]
    [SerializeField] private float rotationDuration = 0.15f;

    [Header("Reload Presentation")]
    [Min(1f)]
    [SerializeField] private float reloadPunchScale = 1.18f;
    [Min(0.01f)]
    [SerializeField] private float reloadPunchDuration = 0.18f;

    [Header("Bullet Reordering")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float requiredOverlap = 0.35f;
    [Min(0f)]
    [SerializeField] private float slotMoveDuration = 0.15f;

    private DeckManager deckManager;
    private PlayerShoot playerShoot;
    private PlayerHealth playerHealth;
    private CurrencyManager currencyManager;
    private Coroutine rotationCoroutine;
    private Coroutine reloadPunchCoroutine;
    private Vector3 cylinderRestScale = Vector3.one;
    // Slot positions are stored in the cylinder parent's local space. Unlike
    // anchoredPosition, these coordinates stay comparable when individual
    // bullet RectTransforms use different anchor presets.
    private readonly List<Vector2> chamberLocalPositions = new List<Vector2>();
    private readonly Dictionary<RectTransform, Coroutine> slotMoveCoroutines =
        new Dictionary<RectTransform, Coroutine>();
    private readonly Dictionary<Image, GameObject> bulletEffects =
        new Dictionary<Image, GameObject>();
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
    private float cylinderTargetAngle;

    public int DisplayedBulletCount => displayedBulletCount;
    public bool IsDragging => isDraggingBullet;

    private void Awake()
    {
        playerShoot = GetComponent<PlayerShoot>();
        playerHealth = GetComponent<PlayerHealth>();
        currencyManager = FindFirstObjectByType<CurrencyManager>();
        playerAnimator ??= GetComponent<Animator>();
        SetAnimatorBulletCount(0);
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
            RefreshBulletEffects();
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

        if (reloadPunchCoroutine != null)
        {
            StopCoroutine(reloadPunchCoroutine);
            reloadPunchCoroutine = null;
        }

        ResetReloadPresentation();
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

    public void PlayReloadPresentation(Color accentColor, float intensity = 1f)
    {
        if (cylinderTransform == null || reloadPunchDuration <= 0f)
        {
            return;
        }

        if (reloadPunchCoroutine != null)
        {
            StopCoroutine(reloadPunchCoroutine);
        }

        reloadPunchCoroutine = StartCoroutine(
            ReloadPunchRoutine(accentColor, Mathf.Max(0f, intensity)));
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
        cylinderRestScale = cylinderTransform.localScale;
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
        Vector2 position = GetLocalPosition(chamber);
        float angle = Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg;
        return Mathf.Repeat(angle - 90f, 360f);
    }

    private void PrepareBulletSlots()
    {
        if (cylinderTransform != null)
        {
            cylinderRestScale = cylinderTransform.localScale;
        }

        chamberLocalPositions.Clear();
        bulletEffects.Clear();

        foreach (Image bulletImage in bulletImages)
        {
            if (bulletImage == null)
            {
                chamberLocalPositions.Add(Vector2.zero);
                continue;
            }

            chamberLocalPositions.Add(
                GetLocalPosition(bulletImage.rectTransform));
            bulletImage.raycastTarget = true;
            CylinderBulletDragHandler dragHandler =
                bulletImage.GetComponent<CylinderBulletDragHandler>();

            if (dragHandler == null)
            {
                dragHandler = bulletImage.gameObject.AddComponent<
                    CylinderBulletDragHandler>();
            }

            dragHandler.Initialize(this, bulletImage);
            PrepareBulletEffect(bulletImage);
        }
    }

    private void PrepareBulletEffect(Image bulletImage)
    {
        Transform effectTransform = bulletImage.transform.Find(
            BulletEffectObjectName);

        if (effectTransform == null)
        {
            return;
        }

        GameObject effectObject = effectTransform.gameObject;
        bulletEffects[bulletImage] = effectObject;
        effectObject.SetActive(false);

        if (effectTransform is RectTransform effectRect)
        {
            // The shader is authored against a square 0..1 UV. Stretching the
            // effect to its bullet guarantees that its circular rim follows
            // the icon even when the chamber UI is resized.
            effectRect.anchorMin = Vector2.zero;
            effectRect.anchorMax = Vector2.one;
            effectRect.anchoredPosition = Vector2.zero;
            effectRect.sizeDelta = Vector2.zero;
            effectRect.localScale = Vector3.one;
        }

        if (effectTransform.TryGetComponent(out Image effectImage))
        {
            effectImage.raycastTarget = false;
            effectImage.preserveAspect = false;
        }

        effectTransform.SetAsLastSibling();
    }

    public bool TryGetLoadedBulletAtScreenPosition(
        Vector2 screenPosition,
        Camera eventCamera,
        out BulletInstance bullet)
    {
        return TryGetLoadedBulletAtScreenPosition(
            screenPosition,
            eventCamera,
            out bullet,
            out _);
    }

    public bool TryGetLoadedBulletAtScreenPosition(
        Vector2 screenPosition,
        Camera eventCamera,
        out BulletInstance bullet,
        out int loadedBulletIndex)
    {
        bullet = null;
        loadedBulletIndex = -1;

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
                loadedBulletIndex = index;
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
            || bulletIndex >= chamberLocalPositions.Count)
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
            dragPointerOffset = GetLocalPosition(draggedRect) - localPointer;
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
        SetLocalPosition(draggedRect, localPointer + dragPointerOffset);
        SetPreviewTarget(FindOverlappingLoadedSlot(
            GetLocalPosition(draggedRect)));
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
            GetLocalPosition(draggedBulletImage.rectTransform));
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
                chamberLocalPositions[draggedBulletIndex]);
            StartSlotMove(
                draggedBulletImage.rectTransform,
                chamberLocalPositions[targetIndex]);
        }
        else
        {
            RestorePreviewTarget();
            StartSlotMove(
                draggedBulletImage.rectTransform,
                chamberLocalPositions[draggedBulletIndex]);
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
            if (index == draggedBulletIndex
                || index >= chamberLocalPositions.Count)
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
                chamberLocalPositions[index]);
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
                chamberLocalPositions[draggedBulletIndex]);
        }
    }

    private void RestorePreviewTarget()
    {
        if (previewTargetIndex < 0
            || previewTargetIndex >= bulletImages.Count
            || previewTargetIndex >= chamberLocalPositions.Count)
        {
            previewTargetIndex = -1;
            return;
        }

        Image previewImage = bulletImages[previewTargetIndex];

        if (previewImage != null)
        {
            StartSlotMove(
                previewImage.rectTransform,
                chamberLocalPositions[previewTargetIndex]);
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
            SetLocalPosition(target, destination);
            return;
        }

        slotMoveCoroutines[target] = StartCoroutine(
            MoveSlot(target, destination));
    }

    private IEnumerator MoveSlot(RectTransform target, Vector2 destination)
    {
        Vector2 start = GetLocalPosition(target);
        float elapsed = 0f;

        while (elapsed < slotMoveDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / slotMoveDuration);
            SetLocalPosition(
                target,
                Vector2.LerpUnclamped(
                    start,
                    destination,
                    Mathf.SmoothStep(0f, 1f, progress)));
        }

        SetLocalPosition(target, destination);
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
            && draggedBulletIndex < chamberLocalPositions.Count)
        {
            SetLocalPosition(
                draggedBulletImage.rectTransform,
                chamberLocalPositions[draggedBulletIndex]);
            draggedBulletImage.rectTransform.SetSiblingIndex(
                draggedOriginalSiblingIndex);
        }

        if (previewTargetIndex >= 0
            && previewTargetIndex < bulletImages.Count
            && previewTargetIndex < chamberLocalPositions.Count
            && bulletImages[previewTargetIndex] != null)
        {
            SetLocalPosition(
                bulletImages[previewTargetIndex].rectTransform,
                chamberLocalPositions[previewTargetIndex]);
        }

        isDraggingBullet = false;
        draggedBulletImage = null;
        draggedBulletIndex = -1;
        previewTargetIndex = -1;
        dragLoadedCount = 0;
    }

    private static Vector2 GetLocalPosition(RectTransform rectTransform)
    {
        Vector3 position = rectTransform.localPosition;
        return new Vector2(position.x, position.y);
    }

    private static void SetLocalPosition(
        RectTransform rectTransform,
        Vector2 position)
    {
        Vector3 localPosition = rectTransform.localPosition;
        localPosition.x = position.x;
        localPosition.y = position.y;
        rectTransform.localPosition = localPosition;
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
        int currentLoadedCount = deckManager == null
            ? 0
            : deckManager.LoadedBullets.Count;
        SetAnimatorBulletCount(currentLoadedCount);

        if (cylinderTransform == null)
        {
            return;
        }

        int loadedCount = Mathf.Min(
            currentLoadedCount,
            bulletImages.Count);

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
            else
            {
                SetBulletEffectActive(bulletImage, false);
            }
        }

        RefreshBulletEffects();

        if (!isInitialized)
        {
            displayedBulletCount = loadedCount;
            cylinderTargetAngle = GetStableCylinderAngle(loadedCount);
            SetCylinderAngle(cylinderTargetAngle);
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

        if (loadedCount == previousCount)
        {
            if (!animateRotation)
            {
                StartCylinderRotation(
                    GetStableCylinderAngle(loadedCount),
                    false,
                    loadedCount == 0);
            }

            return;
        }

        float targetAngle = loadedCount > 0
            ? GetStableCylinderAngle(loadedCount)
            : GetStableCylinderAngle(previousCount)
                + previousCount * rotationStep;

        StartCylinderRotation(
            targetAngle,
            animateRotation,
            loadedCount == 0);
    }

    private float GetStableCylinderAngle(int loadedCount)
    {
        return loadedCount > 0
            ? -(loadedCount - 1) * rotationStep
            : 0f;
    }

    private void SetAnimatorBulletCount(int bulletCount)
    {
        if (playerAnimator == null)
        {
            return;
        }

        playerAnimator.SetInteger(
            BulletInCylinderParameter,
            Mathf.Max(0, bulletCount));
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

    private void RefreshBulletEffects()
    {
        if (deckManager == null)
        {
            foreach (Image bulletImage in bulletImages)
            {
                SetBulletEffectActive(bulletImage, false);
            }

            return;
        }

        IReadOnlyList<BulletInstance> loadedBullets =
            deckManager.LoadedBullets;
        int loadedCount = Mathf.Min(loadedBullets.Count, bulletImages.Count);

        for (int index = 0; index < bulletImages.Count; index++)
        {
            bool active = index < loadedCount
                && ShouldShowBulletEffect(loadedBullets, index);
            SetBulletEffectActive(bulletImages[index], active);
        }
    }

    private void SetBulletEffectActive(Image bulletImage, bool active)
    {
        if (bulletImage != null
            && bulletEffects.TryGetValue(
                bulletImage,
                out GameObject effectObject)
            && effectObject != null
            && effectObject.activeSelf != active)
        {
            effectObject.SetActive(active);
        }
    }

    private bool ShouldShowBulletEffect(
        IReadOnlyList<BulletInstance> loadedBullets,
        int bulletIndex)
    {
        BulletInstance bullet = loadedBullets[bulletIndex];

        if (bullet == null)
        {
            return false;
        }

        // Direct temporary modifiers are always meaningful. Stored damage is
        // deliberately excluded here: it belongs to a distributor and only
        // lights the bullets that distributor will actually enhance.
        if (bullet.TemporaryDamageBonus > 0f
            || bullet.TemporaryCriticalChanceBonus > 0f)
        {
            return true;
        }

        if (HasActiveStackStatBonus(bullet)
            || HasActiveConditionalStatBonus(
                bullet,
                loadedBullets,
                bulletIndex))
        {
            return true;
        }

        return WillReceiveEarlierBulletBuff(loadedBullets, bulletIndex);
    }

    private static bool WillReceiveEarlierBulletBuff(
        IReadOnlyList<BulletInstance> loadedBullets,
        int targetIndex)
    {
        float pendingStackBonus = 0f;

        // The cylinder fires from the highest index down. Simulate only the
        // ordering effects that can enhance a later bullet before it fires.
        for (int sourceIndex = loadedBullets.Count - 1;
             sourceIndex > targetIndex;
             sourceIndex--)
        {
            BulletInstance source = loadedBullets[sourceIndex];

            if (source == null)
            {
                continue;
            }

            BulletEffectData powderEffect = GetEffect(
                source,
                BulletEffectType.PowderPouch);

            if (powderEffect != null && powderEffect.Amount > 0f)
            {
                return true;
            }

            BulletEffectData stackEffect = GetEffect(
                source,
                BulletEffectType.StackNextShot);

            if (stackEffect != null)
            {
                pendingStackBonus += Mathf.Max(0f, stackEffect.Amount);
                continue;
            }

            BulletEffectData distributorEffect = GetEffect(
                source,
                BulletEffectType.Distributor);

            if (distributorEffect != null)
            {
                if (distributorEffect.Amount > 0f
                    && (pendingStackBonus > 0f
                        || source.StoredDamageBonus > 0f))
                {
                    return true;
                }

                pendingStackBonus = 0f;
                continue;
            }

            pendingStackBonus = 0f;
        }

        BulletInstance target = loadedBullets[targetIndex];
        return pendingStackBonus > 0f
            && !HasEffect(target, BulletEffectType.PowderPouch)
            && !HasEffect(target, BulletEffectType.StackNextShot)
            && !HasEffect(target, BulletEffectType.Distributor);
    }

    private static bool HasActiveStackStatBonus(BulletInstance bullet)
    {
        if (bullet.AbilityStacks > 0
            && (HasPositiveEffect(bullet, BulletEffectType.Focus)
                || HasPositiveEffect(
                    bullet,
                    BulletEffectType.Accumulator)))
        {
            return true;
        }

        if (bullet.PermanentStacks > 0
            && (HasPositiveEffect(bullet, BulletEffectType.Devourer)
                || HasPositiveEffect(bullet, BulletEffectType.Legacy)))
        {
            return true;
        }

        // ShotsObservedWhileLoaded increases on every remaining bullet after
        // a shot. It is a stat stack only for the Charge ability.
        return bullet.ShotsObservedWhileLoaded > 0
            && HasPositiveEffect(bullet, BulletEffectType.Charge);
    }

    private bool HasActiveConditionalStatBonus(
        BulletInstance bullet,
        IReadOnlyList<BulletInstance> loadedBullets,
        int bulletIndex)
    {
        // LoadedBullets[0] is the final chamber fired by DeckManager.
        BulletEffectData effect = GetEffect(
            bullet,
            BulletEffectType.Jackpot);

        if (effect != null && effect.Amount > 100f && bulletIndex == 0)
        {
            return true;
        }

        effect = GetEffect(bullet, BulletEffectType.Resonance);

        if (effect != null && effect.Amount > 0f
            && CountOtherLoadedEffects(
                loadedBullets,
                bulletIndex,
                BulletEffectType.Resonance) > 0)
        {
            return true;
        }

        effect = GetEffect(bullet, BulletEffectType.Loader);

        if (effect != null && effect.Amount > 0f
            && deckManager.MaxReloadAmount
                > (playerShoot == null
                    ? loadedBullets.Count
                    : playerShoot.InitialLoadedBulletCount))
        {
            return true;
        }

        effect = GetEffect(bullet, BulletEffectType.Crescendo);

        if (effect != null && effect.Amount > 0f
            && playerShoot != null
            && playerShoot.CriticalShotsThisCylinder > 0)
        {
            return true;
        }

        effect = GetEffect(bullet, BulletEffectType.MixedGrade);

        if (effect != null && effect.Amount > 0f
            && HasOtherLoadedGrade(
                loadedBullets,
                bullet,
                bulletIndex))
        {
            return true;
        }

        effect = GetEffect(bullet, BulletEffectType.Gilded);

        if (effect != null && effect.Amount > 0f
            && currencyManager != null
            && currencyManager.CurrentMoney
                >= Mathf.Max(1, effect.StackCount))
        {
            return true;
        }

        effect = GetEffect(bullet, BulletEffectType.Coagulation);

        if (effect != null && effect.Amount > 0f
            && playerHealth != null
            && playerHealth.MaxHealth > 0
            && 100f * (playerHealth.MaxHealth - playerHealth.CurrentHealth)
                / playerHealth.MaxHealth
                >= Mathf.Max(1, effect.StackCount))
        {
            return true;
        }

        effect = GetEffect(bullet, BulletEffectType.Heart);

        if (effect != null && effect.Amount > 0f
            && playerHealth != null
            && playerHealth.MaxHealth >= Mathf.Max(1, effect.StackCount))
        {
            return true;
        }

        return HasActiveOwnedCollectionBonus(bullet);
    }

    private bool HasActiveOwnedCollectionBonus(BulletInstance bullet)
    {
        foreach (BulletEffectData effect in bullet.Effects)
        {
            if (effect == null || effect.Amount <= 0f)
            {
                continue;
            }

            switch (effect.EffectType)
            {
                case BulletEffectType.Collection:
                    return CountDistinctOwnedBulletTypes() > 0;
                case BulletEffectType.Masterpiece:
                    return CountOwnedGrades(
                        BulletGrade.Ace,
                        BulletGrade.Legendary) > 0;
                case BulletEffectType.MassProduced:
                    return CountOwnedGrades(
                        BulletGrade.Normal,
                        BulletGrade.Rare) > 0;
                case BulletEffectType.Monopoly:
                    return deckManager.Deck.Count
                        + deckManager.LoadedBullets.Count
                        + deckManager.Graveyard.Count > 0;
            }
        }

        return false;
    }

    private int CountDistinctOwnedBulletTypes()
    {
        HashSet<BulletData> types = new HashSet<BulletData>();
        AddOwnedBulletTypes(types, deckManager.Deck);
        AddOwnedBulletTypes(types, deckManager.LoadedBullets);
        AddOwnedBulletTypes(types, deckManager.Graveyard);
        return types.Count;
    }

    private static void AddOwnedBulletTypes(
        HashSet<BulletData> types,
        IReadOnlyList<BulletInstance> bullets)
    {
        foreach (BulletInstance bullet in bullets)
        {
            if (bullet?.Data != null)
            {
                types.Add(bullet.Data);
            }
        }
    }

    private int CountOwnedGrades(BulletGrade first, BulletGrade second)
    {
        return CountGrades(deckManager.Deck, first, second)
            + CountGrades(deckManager.LoadedBullets, first, second)
            + CountGrades(deckManager.Graveyard, first, second);
    }

    private static int CountGrades(
        IReadOnlyList<BulletInstance> bullets,
        BulletGrade first,
        BulletGrade second)
    {
        int count = 0;

        foreach (BulletInstance bullet in bullets)
        {
            if (bullet != null
                && (bullet.Grade == first || bullet.Grade == second))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountOtherLoadedEffects(
        IReadOnlyList<BulletInstance> bullets,
        int targetIndex,
        BulletEffectType effectType)
    {
        int count = 0;

        // When targetIndex fires it has already been removed, so only lower
        // indices are still in DeckManager.LoadedBullets.
        for (int index = 0; index < targetIndex; index++)
        {
            if (HasEffect(bullets[index], effectType))
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasOtherLoadedGrade(
        IReadOnlyList<BulletInstance> bullets,
        BulletInstance target,
        int targetIndex)
    {
        for (int index = 0; index < targetIndex; index++)
        {
            BulletInstance bullet = bullets[index];

            if (bullet != null && bullet.Grade != target.Grade)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPositiveEffect(
        BulletInstance bullet,
        BulletEffectType effectType)
    {
        BulletEffectData effect = GetEffect(bullet, effectType);
        return effect != null && effect.Amount > 0f;
    }

    private static bool HasEffect(
        BulletInstance bullet,
        BulletEffectType effectType)
    {
        return GetEffect(bullet, effectType) != null;
    }

    private static BulletEffectData GetEffect(
        BulletInstance bullet,
        BulletEffectType effectType)
    {
        if (bullet == null)
        {
            return null;
        }

        foreach (BulletEffectData effect in bullet.Effects)
        {
            if (effect != null && effect.EffectType == effectType)
            {
                return effect;
            }
        }

        return null;
    }

    private void StartCylinderRotation(
        float targetAngle,
        bool animateRotation,
        bool hideWhenComplete)
    {
        float previousTargetAngle = cylinderTargetAngle;

        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
            rotationCoroutine = null;
        }

        cylinderTargetAngle = targetAngle;

        if (!animateRotation || rotationDuration <= 0f)
        {
            SetCylinderAngle(targetAngle);

            if (hideWhenComplete)
            {
                cylinderTransform.gameObject.SetActive(false);
                SetCylinderAngle(0f);
                cylinderTargetAngle = 0f;
            }

            return;
        }

        rotationCoroutine = StartCoroutine(
            RotateCylinder(
                previousTargetAngle,
                targetAngle,
                hideWhenComplete));
    }

    private IEnumerator RotateCylinder(
        float previousTargetAngle,
        float targetAngle,
        bool hideWhenComplete)
    {
        float startAngle = previousTargetAngle + Mathf.DeltaAngle(
            previousTargetAngle,
            cylinderTransform.localEulerAngles.z);
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
            SetCylinderAngle(Mathf.LerpUnclamped(
                startAngle,
                targetAngle,
                smoothProgress));
        }

        SetCylinderAngle(targetAngle);

        if (hideWhenComplete)
        {
            cylinderTransform.gameObject.SetActive(false);
            SetCylinderAngle(0f);
            cylinderTargetAngle = 0f;
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

    private IEnumerator ReloadPunchRoutine(Color accentColor, float intensity)
    {
        cylinderRestScale = cylinderTransform.localScale;
        float elapsed = 0f;
        float peakScale = Mathf.Lerp(
            1f,
            reloadPunchScale,
            Mathf.Clamp01(intensity));
        Image newestBulletImage = displayedBulletCount <= 0
            || displayedBulletCount > bulletImages.Count
                ? null
                : bulletImages[displayedBulletCount - 1];
        Color originalBulletColor = newestBulletImage == null
            ? Color.white
            : newestBulletImage.color;
        accentColor.a = 1f;

        while (elapsed < reloadPunchDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / reloadPunchDuration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            float scale = Mathf.Lerp(1f, peakScale, pulse);
            cylinderTransform.localScale = cylinderRestScale * scale;

            if (newestBulletImage != null)
            {
                newestBulletImage.color = Color.Lerp(
                    originalBulletColor,
                    Color.Lerp(Color.white, accentColor, 0.45f),
                    pulse);
            }
        }

        ResetReloadPresentation();
        reloadPunchCoroutine = null;
    }

    private void ResetReloadPresentation()
    {
        if (cylinderTransform != null)
        {
            cylinderTransform.localScale = cylinderRestScale;
        }

        foreach (Image bulletImage in bulletImages)
        {
            if (bulletImage != null)
            {
                bulletImage.color = Color.white;
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
