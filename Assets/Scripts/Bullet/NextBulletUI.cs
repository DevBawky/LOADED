using UnityEngine;
using UnityEngine.UI;

public class NextBulletUI : MonoBehaviour
{
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private Image nextBulletImage;

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
        BulletInstance nextBullet = deckManager == null
            ? null
            : deckManager.PeekNextBullet();

        if (nextBullet != displayedBullet
            || nextBulletImage != null
            && nextBulletImage.sprite != GetPreferredIcon(nextBullet))
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
        Refresh();
    }

    private void Refresh()
    {
        if (nextBulletImage == null)
        {
            return;
        }

        displayedBullet = deckManager == null
            ? null
            : deckManager.PeekNextBullet();
        Sprite sprite = GetPreferredIcon(displayedBullet);
        nextBulletImage.sprite = sprite;
        nextBulletImage.enabled = sprite != null;
        nextBulletImage.preserveAspect = true;
    }

    private void ResolveReferences()
    {
        nextBulletImage ??= GetComponent<Image>();

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

        return bullet.BulletIcon != null
            ? bullet.BulletIcon
            : bullet.CylinderIcon;
    }
}
