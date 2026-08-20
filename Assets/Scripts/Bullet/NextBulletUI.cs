using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NextBulletUI : MonoBehaviour
{
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private PlayerShoot playerShoot;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image nextBulletImage;
    [SerializeField] private TMP_Text reloadableBulletCountText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text stackText;

    private BulletInstance displayedBullet;
    private int displayedLevel = -1;
    private string displayedStatusText = string.Empty;
    private bool isSubscribed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void LateUpdate()
    {
        if (playerShoot != null && playerShoot.IsFiring)
        {
            return;
        }

        BulletInstance nextBullet = deckManager == null
            ? null
            : deckManager.PeekNextBullet();

        if (nextBullet != displayedBullet
            || GetUpgradeLevel(nextBullet) != displayedLevel
            || GetStatusText(nextBullet) != displayedStatusText
            || nextBulletImage != null
            && nextBulletImage.sprite != GetPreferredIcon(nextBullet)
            || reloadableBulletCountText != null
            && reloadableBulletCountText.text != GetReloadableCountLabel())
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void HandleDeckStateChanged()
    {
        if (playerShoot != null && playerShoot.IsFiring)
        {
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        displayedBullet = deckManager == null
            ? null
            : deckManager.PeekNextBullet();
        displayedLevel = GetUpgradeLevel(displayedBullet);
        displayedStatusText = GetStatusText(displayedBullet);
        Sprite sprite = GetPreferredIcon(displayedBullet);

        if (nextBulletImage != null)
        {
            nextBulletImage.sprite = sprite;
            nextBulletImage.enabled = sprite != null;
            nextBulletImage.preserveAspect = true;
        }

        if (reloadableBulletCountText != null)
        {
            reloadableBulletCountText.text = GetReloadableCountLabel();
        }

        ApplyUpgradeLevel(displayedBullet);
        ApplyStackCount(displayedBullet);
    }

    private void ResolveReferences()
    {
        nextBulletImage ??= GetComponent<Image>();
        ResolveBulletStatusTexts();
        playerShoot ??= FindFirstObjectByType<PlayerShoot>(
            FindObjectsInactive.Include);
        currencyManager ??= FindFirstObjectByType<CurrencyManager>(
            FindObjectsInactive.Include);
        playerHealth ??= FindFirstObjectByType<PlayerHealth>(
            FindObjectsInactive.Include);

        if (reloadableBulletCountText == null && transform.parent != null)
        {
            Transform countTransform = transform.parent.Find(
                "Text | Reloadable Chip Count");
            reloadableBulletCountText = countTransform == null
                ? null
                : countTransform.GetComponent<TMP_Text>();
        }

        if (deckManager == null)
        {
            DeckManager[] managers = FindObjectsByType<DeckManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            deckManager = managers.Length == 0 ? null : managers[0];
        }
    }

    private void Subscribe()
    {
        if (deckManager == null || isSubscribed)
        {
            return;
        }

        deckManager.StateChanged += HandleDeckStateChanged;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (deckManager != null && isSubscribed)
        {
            deckManager.StateChanged -= HandleDeckStateChanged;
        }

        isSubscribed = false;
    }

    private static Sprite GetPreferredIcon(BulletInstance bullet)
    {
        if (bullet == null)
        {
            return null;
        }

        return bullet.CylinderIcon;
    }

    private void ResolveBulletStatusTexts()
    {
        TMP_Text[] texts = nextBulletImage == null
            ? GetComponentsInChildren<TMP_Text>(true)
            : nextBulletImage.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null)
            {
                continue;
            }

            if (levelText == null && text.name == "Text | Level")
            {
                levelText = text;
            }
            else if (stackText == null && text.name == "Text | Stack")
            {
                stackText = text;
            }
        }
    }

    private void ApplyUpgradeLevel(BulletInstance bullet)
    {
        if (levelText == null)
        {
            return;
        }

        bool hasUpgrade = bullet != null
            && bullet.Data != null
            && bullet.Level > 0;
        levelText.gameObject.SetActive(hasUpgrade);
        levelText.text = hasUpgrade ? $"+{bullet.Level}" : string.Empty;

        if (hasUpgrade)
        {
            levelText.color = bullet.Data.GetUpgradeLevelColor(bullet.Level);
        }
    }

    private void ApplyStackCount(BulletInstance bullet)
    {
        if (stackText == null)
        {
            return;
        }

        string statusText = GetStatusText(bullet);
        stackText.gameObject.SetActive(!string.IsNullOrEmpty(statusText));
        stackText.text = statusText;
    }

    private static int GetUpgradeLevel(BulletInstance bullet)
    {
        return bullet == null ? 0 : bullet.Level;
    }

    private string GetStatusText(BulletInstance bullet)
    {
        if (bullet == null)
        {
            return string.Empty;
        }

        BulletTooltipContext context = BulletTooltipContext.Create(
            deckManager,
            currencyManager,
            playerHealth,
            playerShoot);
        return bullet.GetStatusDisplayText(context);
    }

    private string GetReloadableCountLabel()
    {
        int count = deckManager == null
            ? 0
            : deckManager.ReloadableBulletCount;
        return $"덱 탄환: {count}";
    }
}
