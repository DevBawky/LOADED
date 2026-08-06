using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NextBulletUI : MonoBehaviour
{
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private PlayerShoot playerShoot;
    [SerializeField] private Image nextBulletImage;
    [SerializeField] private TMP_Text reloadableBulletCountText;

    private BulletInstance displayedBullet;
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
    }

    private void ResolveReferences()
    {
        nextBulletImage ??= GetComponent<Image>();
        playerShoot ??= FindFirstObjectByType<PlayerShoot>(
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

    private string GetReloadableCountLabel()
    {
        int count = deckManager == null
            ? 0
            : deckManager.ReloadableBulletCount;
        return $"덱 탄환: {count}";
    }
}
