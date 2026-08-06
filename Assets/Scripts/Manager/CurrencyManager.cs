using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CurrencyManager : MonoBehaviour
{
    [Header("Money")]
    [Min(0)]
    [SerializeField] private int startingMoney;
    [SerializeField] private TMP_Text currentMoneyText;

    [Header("Flying Gold Presentation")]
    [SerializeField] private GameObject flyingGoldPrefab;
    [Min(0f)]
    [SerializeField] private float goldSpawnInterval = 0.045f;
    [Min(0.05f)]
    [SerializeField] private float goldFlightDuration = 0.65f;
    [SerializeField] private Vector2 goldWaveAmplitudeRange =
        new Vector2(18f, 52f);
    [SerializeField] private Vector2 goldWaveCycleRange =
        new Vector2(0.75f, 1.5f);

    [Header("Runtime State")]
    [SerializeField] private int currentMoney;
    [SerializeField] private int pendingAnimatedMoney;

    private const string FlyingGoldResourcePath = "UI/Flying Gold";
    private const string MoneyPanelName = "Panel | Money";
    private RectTransform moneyPanel;
    private RectTransform rootCanvasRect;
    private Canvas rootCanvas;
    private Coroutine moneyPanelPunchCoroutine;
    private Vector3 moneyPanelBaseScale = Vector3.one;
    private bool capturedMoneyPanelScale;
    private readonly List<GameObject> spawnedFlyingGold =
        new List<GameObject>();

    public event Action<int> MoneyChanged;

    public int CurrentMoney => currentMoney;

    private void Awake()
    {
        currentMoney = Mathf.Max(0, startingMoney);
        pendingAnimatedMoney = 0;
        BindPresentation();
        RefreshText();
    }

    private void OnDisable()
    {
        FlushPendingMoney();
    }

    public bool AddMoney(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        SoundManager.PlaySfx("SFX_GainGold");
        CommitMoney(amount);
        return true;
    }

    public bool AddMoneyFromWorld(int amount, Vector3 sourceWorldPosition)
    {
        if (amount <= 0)
        {
            return false;
        }

        SoundManager.PlaySfx("SFX_GainGold");
        BindPresentation();

        if (flyingGoldPrefab == null || moneyPanel == null
            || rootCanvasRect == null || Camera.main == null
            || !moneyPanel.gameObject.activeInHierarchy)
        {
            CommitMoney(amount);
            return true;
        }

        pendingAnimatedMoney = SaturatingAdd(pendingAnimatedMoney, amount);

        for (int coinIndex = 0; coinIndex < amount; coinIndex++)
        {
            StartCoroutine(FlyGoldRoutine(
                sourceWorldPosition,
                coinIndex * goldSpawnInterval));
        }

        return true;
    }

    public void FlushPendingMoney()
    {
        StopAllCoroutines();
        moneyPanelPunchCoroutine = null;

        foreach (GameObject coin in spawnedFlyingGold)
        {
            if (coin != null)
            {
                Destroy(coin);
            }
        }

        spawnedFlyingGold.Clear();

        int amountToCommit = pendingAnimatedMoney;
        pendingAnimatedMoney = 0;

        if (amountToCommit > 0)
        {
            CommitMoney(amountToCommit);
        }

        if (moneyPanel != null && capturedMoneyPanelScale)
        {
            moneyPanel.localScale = moneyPanelBaseScale;
        }
    }

    public bool TrySpendMoney(int amount)
    {
        if (amount < 0 || currentMoney < amount)
        {
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        currentMoney -= amount;
        NotifyMoneyChanged();
        return true;
    }

    private void NotifyMoneyChanged()
    {
        RefreshText();
        MoneyChanged?.Invoke(currentMoney);
    }

    private void BindPresentation()
    {
        if (flyingGoldPrefab == null)
        {
            flyingGoldPrefab = Resources.Load<GameObject>(
                FlyingGoldResourcePath);
        }

        if (moneyPanel == null && currentMoneyText != null)
        {
            Transform candidate = currentMoneyText.transform.parent;
            moneyPanel = candidate != null
                && candidate.name == MoneyPanelName
                    ? candidate as RectTransform
                    : FindDescendant(
                        currentMoneyText.canvas.transform,
                        MoneyPanelName) as RectTransform;
        }

        if (moneyPanel == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Canvas canvas in canvases)
            {
                moneyPanel = FindDescendant(
                    canvas.transform,
                    MoneyPanelName) as RectTransform;

                if (moneyPanel != null)
                {
                    break;
                }
            }
        }

        if (moneyPanel != null)
        {
            rootCanvas = moneyPanel.GetComponentInParent<Canvas>()?.rootCanvas;
            rootCanvasRect = rootCanvas == null
                ? null
                : rootCanvas.transform as RectTransform;

            if (!capturedMoneyPanelScale)
            {
                moneyPanelBaseScale = moneyPanel.localScale;
                capturedMoneyPanelScale = true;
            }
        }
    }

    private IEnumerator FlyGoldRoutine(
        Vector3 sourceWorldPosition,
        float delay)
    {
        while (delay > 0f)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                delay -= Time.unscaledDeltaTime;
            }
        }

        if (!TryWorldToCanvasPoint(sourceWorldPosition, out Vector2 start))
        {
            CompleteFlyingCoin(null);
            yield break;
        }

        GameObject coin = Instantiate(
            flyingGoldPrefab,
            rootCanvasRect,
            false);
        spawnedFlyingGold.Add(coin);
        RectTransform coinRect = coin.transform as RectTransform;

        if (coinRect == null)
        {
            CompleteFlyingCoin(coin);
            yield break;
        }

        coinRect.anchorMin = coinRect.anchorMax = new Vector2(0.5f, 0.5f);
        coinRect.anchoredPosition = start;
        coinRect.SetAsLastSibling();
        float duration = goldFlightDuration * UnityEngine.Random.Range(0.8f, 1.2f);
        float waveAmplitude = UnityEngine.Random.Range(
            Mathf.Min(goldWaveAmplitudeRange.x, goldWaveAmplitudeRange.y),
            Mathf.Max(goldWaveAmplitudeRange.x, goldWaveAmplitudeRange.y));
        float waveCycles = UnityEngine.Random.Range(
            Mathf.Min(goldWaveCycleRange.x, goldWaveCycleRange.y),
            Mathf.Max(goldWaveCycleRange.x, goldWaveCycleRange.y));
        float waveDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float elapsed = 0f;

        while (elapsed < duration && coinRect != null
            && moneyPanel != null && rootCanvasRect != null)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            Vector2 target = GetMoneyPanelCanvasPoint();
            Vector2 direct = Vector2.Lerp(start, target, eased);
            Vector2 tangent = target - start;
            Vector2 normal = tangent.sqrMagnitude <= 0.001f
                ? Vector2.up
                : new Vector2(-tangent.y, tangent.x).normalized;
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float wave = Mathf.Sin(progress * Mathf.PI * 2f * waveCycles);
            coinRect.anchoredPosition = direct
                + normal * wave * envelope * waveAmplitude * waveDirection;
            float scale = Mathf.Lerp(0.72f, 1.08f, envelope);
            coinRect.localScale = Vector3.one * scale;
        }

        CompleteFlyingCoin(coin);
    }

    private void CompleteFlyingCoin(GameObject coin)
    {
        pendingAnimatedMoney = Mathf.Max(0, pendingAnimatedMoney - 1);
        CommitMoney(1);

        if (coin != null)
        {
            spawnedFlyingGold.Remove(coin);
            Destroy(coin);
        }

        if (moneyPanel != null)
        {
            if (moneyPanelPunchCoroutine != null)
            {
                StopCoroutine(moneyPanelPunchCoroutine);
            }

            moneyPanel.localScale = moneyPanelBaseScale;
            moneyPanelPunchCoroutine = StartCoroutine(PunchMoneyPanel());
        }
    }

    private IEnumerator PunchMoneyPanel()
    {
        const float duration = 0.13f;
        float elapsed = 0f;

        while (elapsed < duration && moneyPanel != null)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            moneyPanel.localScale = moneyPanelBaseScale
                * (1f + pulse * 0.075f);
        }

        if (moneyPanel != null)
        {
            moneyPanel.localScale = moneyPanelBaseScale;
        }

        moneyPanelPunchCoroutine = null;
    }

    private bool TryWorldToCanvasPoint(
        Vector3 worldPosition,
        out Vector2 canvasPoint)
    {
        canvasPoint = Vector2.zero;
        Camera worldCamera = Camera.main;

        if (worldCamera == null || rootCanvasRect == null)
        {
            return false;
        }

        Vector2 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
        Camera uiCamera = rootCanvas != null
            && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvasRect,
            screenPoint,
            uiCamera,
            out canvasPoint);
    }

    private Vector2 GetMoneyPanelCanvasPoint()
    {
        Camera uiCamera = rootCanvas != null
            && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            uiCamera,
            moneyPanel.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvasRect,
            screenPoint,
            uiCamera,
            out Vector2 target);
        return target;
    }

    private void CommitMoney(int amount)
    {
        currentMoney = SaturatingAdd(currentMoney, amount);
        NotifyMoneyChanged();
    }

    private static int SaturatingAdd(int current, int amount)
    {
        long result = (long)current + amount;
        return (int)Math.Min(int.MaxValue, result);
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    private void RefreshText()
    {
        if (currentMoneyText != null)
        {
            currentMoneyText.text = $"$ {currentMoney}";
        }
    }
}
