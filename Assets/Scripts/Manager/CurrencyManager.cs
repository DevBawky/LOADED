using System;
using TMPro;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    [Header("Money")]
    [Min(0)]
    [SerializeField] private int startingMoney;
    [SerializeField] private TMP_Text currentMoneyText;

    // Serialized legacy fields remain for prefab compatibility; the presenter owns the new effect.
    [HideInInspector]
    [SerializeField] private GameObject flyingGoldPrefab;
    [HideInInspector]
    [Min(0f)]
    [SerializeField] private float goldSpawnInterval = 0.045f;
    [HideInInspector]
    [Min(0.05f)]
    [SerializeField] private float goldFlightDuration = 0.65f;
    [HideInInspector]
    [SerializeField] private Vector2 goldWaveAmplitudeRange =
        new Vector2(18f, 52f);
    [HideInInspector]
    [SerializeField] private Vector2 goldWaveCycleRange =
        new Vector2(0.75f, 1.5f);

    [Header("Runtime State")]
    [SerializeField] private int currentMoney;
    [SerializeField] private int pendingAnimatedMoney;

    private const string MoneyPanelName = "Panel | Money";
    private const string MoneyTextName = "Text | Current Money";
    private RectTransform moneyPanel;
    private RectTransform rootCanvasRect;
    private Canvas rootCanvas;
    private RelicManager relicManager;
    private GoldRewardPresenter goldPresenter;

    public bool IsRewardPresentationActive => goldPresenter != null && goldPresenter.IsActive;

    private void Update()
    {
        goldPresenter?.Tick(Time.unscaledDeltaTime, GamePauseController.IsPaused);
    }

    private void OnDestroy()
    {
        goldPresenter?.Dispose();
        goldPresenter = null;
    }

    public event Action<int> MoneyChanged;

    public int CurrentMoney => currentMoney;

    private void Awake()
    {
        currentMoney = Mathf.Max(0, startingMoney);
        pendingAnimatedMoney = 0;
        relicManager = FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);
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
        int visualAmount = Mathf.Min(amount, int.MaxValue - currentMoney);
        bool canAnimate = isActiveAndEnabled && moneyPanel != null
            && rootCanvasRect != null && Camera.main != null
            && moneyPanel.gameObject.activeInHierarchy;
        if (canAnimate)
        {
            pendingAnimatedMoney = SaturatingAdd(pendingAnimatedMoney, visualAmount);
        }
        // Balance and observers commit immediately; only the displayed total trails the coins.
        CommitMoney(amount);
        if (canAnimate && visualAmount > 0)
        {
            goldPresenter ??= new GoldRewardPresenter(rootCanvas, moneyPanel,
                currentMoneyText, CompleteRewardPresentation);
            if (!goldPresenter.Add(visualAmount, sourceWorldPosition))
            {
                CompleteRewardPresentation(visualAmount);
            }
        }
        return true;
    }

    private void CompleteRewardPresentation(int amount)
    {
        pendingAnimatedMoney = Mathf.Max(0, pendingAnimatedMoney - amount);
        RefreshText();
    }

    public void FlushPendingMoney()
    {
        goldPresenter?.Clear();
        pendingAnimatedMoney = 0;
        RefreshText();
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

        FlushPendingMoney();
        currentMoney -= amount;
        NotifyMoneyChanged();
        return true;
    }

    public void RestoreRunMoney(int amount)
    {
        FlushPendingMoney();
        currentMoney = Mathf.Max(0, amount);
        pendingAnimatedMoney = 0;
        NotifyMoneyChanged();
    }

    private void NotifyMoneyChanged()
    {
        RefreshText();
        MoneyChanged?.Invoke(currentMoney);
    }

    private void BindPresentation()
    {
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

        if (currentMoneyText == null && moneyPanel != null)
        {
            Transform moneyText = FindDescendant(
                moneyPanel,
                MoneyTextName);
            currentMoneyText = moneyText == null
                ? null
                : moneyText.GetComponent<TMP_Text>();
        }

        if (moneyPanel != null)
        {
            rootCanvas = moneyPanel.GetComponentInParent<Canvas>()?.rootCanvas;
            rootCanvasRect = rootCanvas == null
                ? null
                : rootCanvas.transform as RectTransform;
        }
    }

    private void CommitMoney(int amount)
    {
        currentMoney = SaturatingAdd(currentMoney, amount);
        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);
        relicManager?.NotifyGoldGained(amount);
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
            currentMoneyText.text = $"$ {Mathf.Max(0, currentMoney - pendingAnimatedMoney)}";
        }
    }
}
