using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerCylinderUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform cylinderTransform;
    [SerializeField] private List<Image> bulletImages = new List<Image>();

    [Header("Rotation")]
    [Min(0f)]
    [SerializeField] private float rotationStep = 60f;
    [Min(0f)]
    [SerializeField] private float rotationDuration = 0.15f;

    private DeckManager deckManager;
    private Coroutine rotationCoroutine;
    private int displayedBulletCount;
    private bool isInitialized;
    private bool isSubscribed;

    public int DisplayedBulletCount => displayedBulletCount;

    private void Awake()
    {
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

    private void OnDisable()
    {
        UnsubscribeFromDeck();

        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
            rotationCoroutine = null;
        }
    }

    public void Initialize(DeckManager assignedDeckManager)
    {
        UnsubscribeFromDeck();
        deckManager = assignedDeckManager;
        SubscribeToDeck();
        isInitialized = false;
        RefreshDisplay(false);
    }

    private void HandleDeckStateChanged()
    {
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
