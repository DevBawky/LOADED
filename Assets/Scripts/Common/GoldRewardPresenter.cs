using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Owns only transient UI. CurrencyManager commits and saves every reward before this runs.
internal sealed class GoldRewardPresenter : IDisposable
{
    private sealed class Coin
    {
        public Image Image;
        public Image Trail;
        public Vector2 Origin;
        public Vector2 Scatter;
        public float Age;
        public float Delay;
        public int Amount;
        public int Index;
    }

    private sealed class Popup
    {
        public TMP_Text Text;
        public Vector2 Origin;
        public float Age;
    }

    private sealed class Spark
    {
        public Image Image;
        public Vector2 Origin;
        public Vector2 Velocity;
        public float Age;
    }

    private const float BurstDuration = 0.18f;
    private const float FlightDuration = 0.46f;
    private const int MaximumCoins = 24;
    private readonly Canvas canvas;
    private readonly RectTransform canvasRect;
    private readonly RectTransform panel;
    private readonly TMP_Text balanceText;
    private readonly Action<int> onArrived;
    private readonly List<Coin> coins = new List<Coin>();
    private readonly List<Popup> popups = new List<Popup>();
    private readonly List<Spark> sparks = new List<Spark>();
    private readonly Image panelImage;
    private readonly Color basePanelColor;
    private readonly Vector3 basePanelScale;
    private readonly Color baseTextColor;
    private readonly Sprite coinSprite;
    private readonly Texture2D coinTexture;
    private TMP_Text gainText;
    private long accumulatedGain;
    private float gainRemaining;
    private float punchRemaining;

    public bool IsActive => coins.Count > 0 || punchRemaining > 0f || gainRemaining > 0.25f;

    public GoldRewardPresenter(Canvas canvas, RectTransform panel, TMP_Text balanceText,
        Action<int> onArrived)
    {
        this.canvas = canvas;
        canvasRect = canvas.transform as RectTransform;
        this.panel = panel;
        this.balanceText = balanceText;
        this.onArrived = onArrived;
        basePanelScale = panel.localScale;
        baseTextColor = balanceText == null ? Color.white : balanceText.color;
        panelImage = panel.GetComponent<Image>();
        basePanelColor = panelImage == null ? Color.white : panelImage.color;
        coinTexture = CreateCoinTexture();
        coinSprite = Sprite.Create(coinTexture, new Rect(0, 0, 48, 48), Vector2.one * 0.5f, 48f);
        coinSprite.name = "Reward Coin (Runtime)";
    }

    internal static int CoinCountForAmount(int amount)
    {
        return amount <= 0 ? 0 : Mathf.Min(amount, amount < 6 ? 2 : amount < 20 ? 3 : 4);
    }

    public bool Add(int amount, Vector3 worldPosition)
    {
        Camera camera = Camera.main;
        if (amount <= 0 || camera == null || canvasRect == null || panel == null)
        {
            return false;
        }
        Vector3 screen = camera.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, UiCamera, out Vector2 origin))
        {
            return false;
        }
        int count = Mathf.Min(CoinCountForAmount(amount), MaximumCoins - coins.Count);
        if (count <= 0)
        {
            // Merge a large chain into an existing flight without creating unbounded UI.
            Coin last = coins[coins.Count - 1];
            last.Amount = (int)Math.Min(int.MaxValue, (long)last.Amount + amount);
            return true;
        }
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Reward Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvasRect, false);
            Image image = go.GetComponent<Image>();
            image.sprite = coinSprite;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = Vector2.one * (amount >= 20 ? 32f : 28f);
            image.rectTransform.anchoredPosition = origin;
            var trailObject = new GameObject("Reward Coin Trail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            trailObject.transform.SetParent(canvasRect, false);
            trailObject.transform.SetSiblingIndex(image.transform.GetSiblingIndex());
            var trail = trailObject.GetComponent<Image>();
            trail.raycastTarget = false;
            trail.color = new Color(1f, 0.77f, 0.25f, 0.22f);
            trail.enabled = false;
            float fan = count == 1 ? 0f : i / (float)(count - 1) * 2f - 1f;
            coins.Add(new Coin
            {
                Image = image,
                Trail = trail,
                Origin = origin,
                Scatter = origin + new Vector2(fan * 32f, 24f + (1f - Mathf.Abs(fan)) * 22f),
                Delay = i * 0.045f,
                Amount = amount / count + (i < amount % count ? 1 : 0),
                Index = i
            });
        }
        int sparkCount = Mathf.CeilToInt(5f * CombatAccessibilitySettings.ParticleDensityMultiplier);
        for (int i = 0; i < sparkCount && sparks.Count < 40; i++)
        {
            var go = new GameObject("Reward Spark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvasRect, false);
            var spark = go.GetComponent<Image>();
            spark.color = new Color(1f, 0.85f, 0.38f);
            spark.raycastTarget = false;
            spark.rectTransform.sizeDelta = new Vector2(3f, i % 2 == 0 ? 8f : 4f);
            spark.rectTransform.anchoredPosition = origin;
            spark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 57f);
            float angle = (i * 72f + 18f) * Mathf.Deg2Rad;
            sparks.Add(new Spark { Image = spark, Origin = origin,
                Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 110f });
        }
        if (popups.Count < 8 && balanceText != null)
        {
            TMP_Text text = CreateText("Reward Amount", 27f);
            text.text = $"+{amount}";
            text.rectTransform.anchoredPosition = origin + Vector2.up * 76f;
            popups.Add(new Popup { Text = text, Origin = text.rectTransform.anchoredPosition });
        }
        return true;
    }

    public void Tick(float deltaTime, bool paused)
    {
        if (paused)
        {
            return;
        }
        if (panel == null || canvasRect == null || !panel.gameObject.activeInHierarchy)
        {
            int remaining = 0;
            foreach (Coin coin in coins) remaining = (int)Math.Min(int.MaxValue, (long)remaining + coin.Amount);
            Clear();
            if (remaining > 0) onArrived(remaining);
            return;
        }
        float dt = Mathf.Max(0f, deltaTime);
        Vector2 target = PanelPoint();
        int arrived = 0;
        for (int i = coins.Count - 1; i >= 0; i--)
        {
            Coin coin = coins[i];
            coin.Age += dt;
            float age = Mathf.Max(0f, coin.Age - coin.Delay);
            if (coin.Image == null || age >= BurstDuration + FlightDuration)
            {
                arrived = (int)Math.Min(int.MaxValue, (long)arrived + coin.Amount);
                if (coin.Image != null) Release(coin.Image.gameObject);
                if (coin.Trail != null) Release(coin.Trail.gameObject);
                coins.RemoveAt(i);
                continue;
            }
            RectTransform rect = coin.Image.rectTransform;
            Vector2 previousPosition = rect.anchoredPosition;
            if (age < BurstDuration)
            {
                float t = age / BurstDuration;
                rect.anchoredPosition = Vector2.Lerp(coin.Origin, coin.Scatter, 1f - Mathf.Pow(1f - t, 3f));
                rect.localScale = new Vector3(Mathf.Lerp(0.35f, 1f, t), Mathf.Lerp(0.35f, 1f, t), 1f);
            }
            else
            {
                float t = (age - BurstDuration) / FlightDuration;
                float eased = t * t * t;
                Vector2 bend = Vector2.Lerp(coin.Scatter, target, 0.5f) + Vector2.up * (65f + coin.Index * 12f);
                rect.anchoredPosition = (1f - eased) * (1f - eased) * coin.Scatter
                    + 2f * (1f - eased) * eased * bend + eased * eased * target;
                float size = Mathf.Lerp(1f, 0.55f, t);
                rect.localScale = new Vector3(size * Mathf.Lerp(0.55f, 1f, Mathf.Abs(Mathf.Cos(t * 8f))), size, 1f);
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * (coin.Index % 2 == 0 ? 16f : -16f));
                if (coin.Trail != null)
                {
                    Vector2 motion = rect.anchoredPosition - previousPosition;
                    float length = Mathf.Min(38f, motion.magnitude * 1.4f);
                    coin.Trail.enabled = length > 2f;
                    coin.Trail.rectTransform.sizeDelta = new Vector2(length, 3f);
                    coin.Trail.rectTransform.anchoredPosition = rect.anchoredPosition - motion.normalized * length * 0.5f;
                    coin.Trail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(motion.y, motion.x) * Mathf.Rad2Deg);
                }
            }
        }
        if (arrived > 0)
        {
            onArrived(arrived);
            accumulatedGain += arrived;
            gainRemaining = 0.65f;
            punchRemaining = 0.16f;
            if (gainText == null && balanceText != null) gainText = CreateText("Reward Total", 21f);
            if (gainText != null) gainText.text = $"+{accumulatedGain}";
        }
        punchRemaining = Mathf.Max(0f, punchRemaining - dt);
        float punch = Mathf.Sin(punchRemaining / 0.16f * Mathf.PI)
            * CombatAccessibilitySettings.PresentationIntensity;
        panel.localScale = basePanelScale * (1f + punch * 0.085f);
        if (balanceText != null) balanceText.color = Color.Lerp(baseTextColor, new Color(1f, 0.87f, 0.4f), punch);
        if (panelImage != null) panelImage.color = Color.Lerp(basePanelColor, new Color(1f, 0.8f, 0.32f, basePanelColor.a), punch * 0.4f);
        gainRemaining = Mathf.Max(0f, gainRemaining - dt);
        if (gainText != null)
        {
            float side = target.y > canvasRect.rect.yMax - 75f ? -1f : 1f;
            gainText.rectTransform.anchoredPosition = target + new Vector2(0f, side * 36f + (0.65f - gainRemaining) * 12f);
            gainText.alpha = Mathf.Clamp01(gainRemaining / 0.22f);
        }
        if (gainRemaining <= 0f && coins.Count == 0)
        {
            accumulatedGain = 0;
        }
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            Popup popup = popups[i];
            popup.Age += dt;
            if (popup.Text == null || popup.Age >= 0.5f)
            {
                if (popup.Text != null) Release(popup.Text.gameObject);
                popups.RemoveAt(i);
                continue;
            }
            popup.Text.rectTransform.anchoredPosition = popup.Origin + Vector2.up * popup.Age * 32f;
            popup.Text.alpha = Mathf.Clamp01((0.5f - popup.Age) / 0.2f);
        }
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark spark = sparks[i];
            spark.Age += dt;
            if (spark.Image == null || spark.Age >= 0.28f)
            {
                if (spark.Image != null) Release(spark.Image.gameObject);
                sparks.RemoveAt(i);
                continue;
            }
            spark.Image.rectTransform.anchoredPosition = spark.Origin + spark.Velocity * spark.Age;
            spark.Image.color = new Color(1f, 0.85f, 0.38f, 1f - spark.Age / 0.28f);
        }
    }

    private Camera UiCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
        ? canvas.worldCamera : null;

    private Vector2 PanelPoint()
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(UiCamera, panel.TransformPoint(panel.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, UiCamera, out Vector2 result);
        return result;
    }

    private TMP_Text CreateText(string name, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvasRect, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = balanceText.font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.82f, 0.32f);
        text.raycastTarget = false;
        text.rectTransform.sizeDelta = new Vector2(200f, 36f);
        return text;
    }

    public void Clear()
    {
        foreach (Coin coin in coins)
        {
            if (coin.Image != null) Release(coin.Image.gameObject);
            if (coin.Trail != null) Release(coin.Trail.gameObject);
        }
        foreach (Popup popup in popups) if (popup.Text != null) Release(popup.Text.gameObject);
        foreach (Spark spark in sparks) if (spark.Image != null) Release(spark.Image.gameObject);
        coins.Clear();
        popups.Clear();
        sparks.Clear();
        if (gainText != null) Release(gainText.gameObject);
        gainText = null;
        accumulatedGain = 0;
        gainRemaining = punchRemaining = 0f;
        if (panel != null) panel.localScale = basePanelScale;
        if (balanceText != null) balanceText.color = baseTextColor;
        if (panelImage != null) panelImage.color = basePanelColor;
    }

    public void Dispose()
    {
        Clear();
        Release(coinSprite);
        Release(coinTexture);
    }

    private static void Release(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value);
        else UnityEngine.Object.DestroyImmediate(value);
    }

    private static Texture2D CreateCoinTexture()
    {
        const int size = 48;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "Reward Coin", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x - 23.5f, y - 23.5f) / 23f;
            float radius = p.magnitude;
            Color color = Color.Lerp(new Color(0.76f, 0.35f, 0.035f), new Color(1f, 0.86f, 0.32f), (p.y + 1f) * 0.5f);
            if (radius > 0.82f) color = new Color(1f, 0.83f, 0.27f);
            if (radius > 0.94f || radius > 0.72f && radius < 0.79f) color = new Color(0.54f, 0.25f, 0.03f);
            if (Mathf.Abs(p.x) < 0.095f && Mathf.Abs(p.y) < 0.46f) color = new Color(1f, 0.95f, 0.61f);
            if ((p - new Vector2(-0.43f, 0.48f)).sqrMagnitude < 0.015f) color = new Color(1f, 0.98f, 0.77f);
            color.a = Mathf.Clamp01((1f - radius) * 23f);
            pixels[y * size + x] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
