using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class LoadingTransitionController : MonoBehaviour
{
    private const string ResourcePath = "UI/Canvas _ Loading Transition";
    private const int ChamberCount = 6;

    [Header("UI References")]
    [SerializeField] private CanvasGroup transitionCanvasGroup;
    [SerializeField] private Image backgroundFillImage;
    [SerializeField] private RectTransform cylinderTransform;
    [SerializeField] private CanvasGroup cylinderCanvasGroup;
    [SerializeField] private List<Image> bulletImages = new List<Image>();
    [SerializeField] private CanvasGroup loadingTextGroup;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private TMP_Text tipText;

    [Header("Random Bullet Appearance")]
    [Tooltip("비워두면 프리팹의 기본 이미지와 색상을 사용합니다.")]
    [SerializeField] private List<Sprite> bulletSprites = new List<Sprite>();
    [SerializeField] private List<Color> fallbackBulletColors = new List<Color>
    {
        new Color(0.95f, 0.72f, 0.22f),
        new Color(0.85f, 0.25f, 0.20f),
        new Color(0.20f, 0.70f, 0.85f),
        new Color(0.55f, 0.85f, 0.30f),
        new Color(0.80f, 0.45f, 0.90f),
        new Color(0.95f, 0.90f, 0.72f)
    };

    [Header("Timing")]
    [Min(0.01f)]
    [SerializeField] private float bulletLoadDuration = 0.25f;
    [Min(0.01f)]
    [SerializeField] private float cylinderRotationDuration = 0.12f;
    [Min(0f)]
    [SerializeField] private float cylinderRotationStep = 60f;
    [Min(0f)]
    [SerializeField] private float coveredHoldDuration = 2f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Loading Copy")]
    [SerializeField] private string loadingLabel = "LOADING";
    [SerializeField] private List<string> tips = new List<string>
    {
        "탄환의 장전 순서를 확인하세요.",
        "상점에서는 다음 전투를 미리 준비할 수 있습니다.",
        "위험한 적부터 제거하면 피해를 줄일 수 있습니다.",
        "탄환 효과의 조합이 전투의 흐름을 바꿉니다."
    };

    private readonly List<Vector2> bulletRestPositions = new List<Vector2>();
    private readonly List<Vector3> bulletRestScales = new List<Vector3>();
    private readonly List<Quaternion> bulletRestRotations = new List<Quaternion>();
    private Quaternion cylinderRestRotation = Quaternion.identity;
    private float currentCylinderAngle;
    private Coroutine transitionCoroutine;

    public static LoadingTransitionController Instance { get; private set; }
    public static bool IsTransitioning => Instance != null && Instance.transitionCoroutine != null;
    public static event Action<bool> TransitionStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        LoadingTransitionController prefab = Resources.Load<LoadingTransitionController>(ResourcePath);

        if (prefab == null)
        {
            Debug.LogError($"Loading transition prefab was not found at Resources/{ResourcePath}.");
            return;
        }

        Instantiate(prefab);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CacheBulletRestStates();
        ResetPresentation();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        bulletLoadDuration = Mathf.Max(0.01f, bulletLoadDuration);
        cylinderRotationDuration = Mathf.Max(0.01f, cylinderRotationDuration);
        cylinderRotationStep = Mathf.Max(0f, cylinderRotationStep);
        coveredHoldDuration = Mathf.Max(0f, coveredHoldDuration);
    }

    public static bool RunTransition(Action coveredAction, Action completed = null)
    {
        if (!TryGetReadyInstance(out LoadingTransitionController controller))
        {
            coveredAction?.Invoke();
            completed?.Invoke();
            return false;
        }

        return controller.BeginTransition(coveredAction, completed, null, -1);
    }

    public static bool LoadScene(string sceneName, Action completed = null)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("A scene name is required for a loading transition.");
            return false;
        }

        return TryGetReadyInstance(out LoadingTransitionController controller)
            && controller.BeginTransition(null, completed, sceneName, -1);
    }

    public static bool LoadScene(int buildIndex, Action completed = null)
    {
        return TryGetReadyInstance(out LoadingTransitionController controller)
            && controller.BeginTransition(null, completed, null, buildIndex);
    }

    public void LoadSceneByName(string sceneName)
    {
        LoadScene(sceneName);
    }

    public void LoadSceneByBuildIndex(int buildIndex)
    {
        LoadScene(buildIndex);
    }

    private static bool TryGetReadyInstance(out LoadingTransitionController controller)
    {
        controller = Instance;

        if (controller == null)
        {
            Bootstrap();
            controller = Instance;
        }

        if (controller == null)
        {
            Debug.LogError("LoadingTransitionController could not be created.");
            return false;
        }

        if (controller.IsConfigured())
        {
            return true;
        }

        Debug.LogError("Loading transition prefab references are incomplete.", controller);
        return false;
    }

    private bool BeginTransition(
        Action coveredAction,
        Action completed,
        string sceneName,
        int sceneBuildIndex)
    {
        if (transitionCoroutine != null)
        {
            return false;
        }

        transitionCoroutine = StartCoroutine(TransitionRoutine(
            coveredAction,
            completed,
            sceneName,
            sceneBuildIndex));
        return true;
    }

    private IEnumerator TransitionRoutine(
        Action coveredAction,
        Action completed,
        string sceneName,
        int sceneBuildIndex)
    {
        SetInteractionBlocked(true);
        PrepareRandomBullets();
        SetLoadingCopy();
        TransitionStateChanged?.Invoke(true);

        for (int step = 0; step < ChamberCount; step++)
        {
            yield return RotateCylinderTo(step * cylinderRotationStep);
            yield return AnimateBulletLoaded(step, step);
        }

        SetBackgroundProgress(1f);

        coveredAction?.Invoke();

        AsyncOperation sceneOperation = null;

        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            sceneOperation = SceneManager.LoadSceneAsync(sceneName);
        }
        else if (sceneBuildIndex >= 0)
        {
            sceneOperation = SceneManager.LoadSceneAsync(sceneBuildIndex);
        }

        while (sceneOperation != null && !sceneOperation.isDone)
        {
            yield return null;
        }

        float coveredElapsed = 0f;
        while (coveredElapsed < coveredHoldDuration)
        {
            coveredElapsed += GetDeltaTime();
            yield return null;
        }

        for (int step = 0; step < ChamberCount; step++)
        {
            float targetAngle = (ChamberCount + step) * cylinderRotationStep;
            yield return RotateCylinderTo(targetAngle);
            yield return AnimateBulletFired(step, step);
        }

        SetBackgroundProgress(0f);
        SetInteractionBlocked(false);
        SetCylinderAngle(0f);
        transitionCoroutine = null;
        TransitionStateChanged?.Invoke(false);
        completed?.Invoke();
    }

    private IEnumerator AnimateBulletLoaded(int bulletIndex, int step)
    {
        Image bullet = bulletImages[bulletIndex];
        float fillStart = step / (float)ChamberCount;
        float fillEnd = (step + 1f) / ChamberCount;
        float elapsed = 0f;

        bullet.gameObject.SetActive(true);
        bullet.rectTransform.anchoredPosition = bulletRestPositions[bulletIndex];

        while (elapsed < bulletLoadDuration)
        {
            elapsed += GetDeltaTime();
            float progress = Mathf.Clamp01(elapsed / bulletLoadDuration);
            float eased = Smooth(progress);
            bullet.rectTransform.localScale = Vector3.Lerp(
                Vector3.zero,
                bulletRestScales[bulletIndex],
                eased);
            SetImageAlpha(bullet, eased);
            SetBackgroundProgress(Mathf.Lerp(fillStart, fillEnd, eased));
            yield return null;
        }

        bullet.rectTransform.localScale = bulletRestScales[bulletIndex];
        SetImageAlpha(bullet, 1f);
        SetBackgroundProgress(fillEnd);
    }

    private IEnumerator AnimateBulletFired(int bulletIndex, int step)
    {
        Image bullet = bulletImages[bulletIndex];
        Vector2 startPosition = bulletRestPositions[bulletIndex];
        Vector2 direction = startPosition.sqrMagnitude > 0.01f
            ? startPosition.normalized
            : Vector2.up;
        Vector2 endPosition = startPosition + direction * 180f;
        float fillStart = 1f - step / (float)ChamberCount;
        float fillEnd = 1f - (step + 1f) / ChamberCount;
        float elapsed = 0f;

        while (elapsed < bulletLoadDuration)
        {
            elapsed += GetDeltaTime();
            float progress = Mathf.Clamp01(elapsed / bulletLoadDuration);
            float eased = Smooth(progress);
            bullet.rectTransform.anchoredPosition = Vector2.Lerp(startPosition, endPosition, eased);
            bullet.rectTransform.localScale = Vector3.Lerp(
                bulletRestScales[bulletIndex],
                bulletRestScales[bulletIndex] * 0.55f,
                eased);
            SetImageAlpha(bullet, 1f - eased);
            SetBackgroundProgress(Mathf.Lerp(fillStart, fillEnd, eased));
            yield return null;
        }

        bullet.gameObject.SetActive(false);
        bullet.rectTransform.anchoredPosition = startPosition;
        bullet.rectTransform.localScale = bulletRestScales[bulletIndex];
        SetBackgroundProgress(fillEnd);
    }

    private IEnumerator RotateCylinderTo(float targetAngle)
    {
        if (cylinderRotationStep <= 0f || cylinderRotationDuration <= 0f)
        {
            SetCylinderAngle(targetAngle);
            yield break;
        }

        float startAngle = currentCylinderAngle;
        float elapsed = 0f;

        while (elapsed < cylinderRotationDuration)
        {
            elapsed += GetDeltaTime();
            float progress = Mathf.Clamp01(elapsed / cylinderRotationDuration);
            SetCylinderAngle(Mathf.Lerp(startAngle, targetAngle, Smooth(progress)));
            yield return null;
        }

        SetCylinderAngle(targetAngle);
    }

    private void PrepareRandomBullets()
    {
        // Begin one chamber behind so even the first load visibly advances
        // the cylinder into its loading position.
        SetCylinderAngle(-cylinderRotationStep);

        List<int> spriteOrder = new List<int>();
        for (int index = 0; index < bulletSprites.Count; index++)
        {
            if (bulletSprites[index] != null)
            {
                spriteOrder.Add(index);
            }
        }
        Shuffle(spriteOrder);

        for (int index = 0; index < ChamberCount; index++)
        {
            Image bullet = bulletImages[index];
            if (spriteOrder.Count > 0)
            {
                bullet.sprite = bulletSprites[spriteOrder[index % spriteOrder.Count]];
                bullet.color = Color.white;
            }
            else if (fallbackBulletColors.Count > 0)
            {
                bullet.color = fallbackBulletColors[UnityEngine.Random.Range(0, fallbackBulletColors.Count)];
            }

            SetImageAlpha(bullet, 0f);
            bullet.rectTransform.localScale = Vector3.zero;
            bullet.gameObject.SetActive(false);
        }
    }

    private void SetLoadingCopy()
    {
        loadingText.text = loadingLabel;
        tipText.text = tips.Count == 0
            ? string.Empty
            : tips[UnityEngine.Random.Range(0, tips.Count)];
    }

    private void SetBackgroundProgress(float progress)
    {
        float value = Mathf.Clamp01(progress);
        backgroundFillImage.fillAmount = value;
        cylinderCanvasGroup.alpha = value;
        loadingTextGroup.alpha = value;
    }

    private void CacheBulletRestStates()
    {
        bulletRestPositions.Clear();
        bulletRestScales.Clear();
        bulletRestRotations.Clear();

        cylinderRestRotation = cylinderTransform == null
            ? Quaternion.identity
            : cylinderTransform.localRotation;

        foreach (Image bullet in bulletImages)
        {
            bulletRestPositions.Add(bullet == null ? Vector2.zero : bullet.rectTransform.anchoredPosition);
            bulletRestScales.Add(bullet == null ? Vector3.one : bullet.rectTransform.localScale);
            bulletRestRotations.Add(bullet == null ? Quaternion.identity : bullet.rectTransform.localRotation);
        }
    }

    private void SetCylinderAngle(float angle)
    {
        currentCylinderAngle = angle;

        if (cylinderTransform == null)
        {
            return;
        }

        cylinderTransform.localRotation = cylinderRestRotation
            * Quaternion.Euler(0f, 0f, angle);

        for (int index = 0; index < bulletImages.Count; index++)
        {
            Image bullet = bulletImages[index];

            if (bullet != null && index < bulletRestRotations.Count)
            {
                bullet.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle)
                    * bulletRestRotations[index];
            }
        }
    }

    private void ResetPresentation()
    {
        if (backgroundFillImage != null && cylinderCanvasGroup != null
            && loadingTextGroup != null)
        {
            SetBackgroundProgress(0f);
        }

        foreach (Image bullet in bulletImages)
        {
            if (bullet != null)
            {
                bullet.gameObject.SetActive(false);
            }
        }

        SetCylinderAngle(0f);

        if (transitionCanvasGroup != null)
        {
            SetInteractionBlocked(false);
        }
    }

    private void SetInteractionBlocked(bool blocked)
    {
        transitionCanvasGroup.alpha = blocked ? 1f : 0f;
        transitionCanvasGroup.interactable = blocked;
        transitionCanvasGroup.blocksRaycasts = blocked;
    }

    private bool IsConfigured()
    {
        if (transitionCanvasGroup == null || backgroundFillImage == null
            || cylinderTransform == null || cylinderCanvasGroup == null
             || loadingTextGroup == null
            || loadingText == null || tipText == null
            || bulletImages.Count != ChamberCount)
        {
            return false;
        }

        foreach (Image bullet in bulletImages)
        {
            if (bullet == null)
            {
                return false;
            }
        }

        return true;
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private static float Smooth(float value)
    {
        return value * value * (3f - 2f * value);
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private static void Shuffle(List<int> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = UnityEngine.Random.Range(0, index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}
