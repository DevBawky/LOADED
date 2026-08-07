using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuSettingsController : MonoBehaviour
{
    [SerializeField] private Button saveButton;
    [SerializeField] private Button dontSaveButton;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider vfxSlider;
    [SerializeField] private Toggle oldMovieToggle;

    private float bgmSnapshot;
    private float sfxSnapshot;
    private float vfxSnapshot;
    private bool oldMovieSnapshot;
    private bool sessionOpen;
    private bool closeHandled;

    private void OnEnable()
    {
        EventSystem.current?.SetSelectedGameObject(null);
        ResolveControls();
        LoadSavedValues();
        BindControls();
        sessionOpen = true;
        closeHandled = false;
    }

    private void OnDisable()
    {
        UnbindControls();

        if (sessionOpen && !closeHandled)
        {
            RestoreSnapshot();
        }

        sessionOpen = false;
        closeHandled = false;
    }

    private void Update()
    {
        if (Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseWithoutSaving();
        }
    }

    public void SaveAndClose()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        SoundManager.SetBgmVolume(
            bgmSlider == null ? bgmSnapshot : bgmSlider.value);
        SoundManager.SetSfxVolume(
            sfxSlider == null ? sfxSnapshot : sfxSlider.value);
        CombatAccessibilitySettings.SetPresentationIntensity(
            vfxSlider == null ? vfxSnapshot : vfxSlider.value);
        OldMoviePresentationSettings.SetEnabled(
            oldMovieToggle == null ? oldMovieSnapshot : oldMovieToggle.isOn);
        closeHandled = true;
        gameObject.SetActive(false);
    }

    public bool TryCloseWithoutSaving()
    {
        if (!gameObject.activeInHierarchy)
        {
            return false;
        }

        CloseWithoutSaving();
        return true;
    }

    private void CloseWithoutSaving()
    {
        RestoreSnapshot();
        closeHandled = true;
        gameObject.SetActive(false);
    }

    private void LoadSavedValues()
    {
        bgmSnapshot = SoundManager.BgmVolume;
        sfxSnapshot = SoundManager.SfxVolume;
        vfxSnapshot = CombatAccessibilitySettings.PresentationIntensity;
        oldMovieSnapshot = OldMoviePresentationSettings.Enabled;
        bgmSlider?.SetValueWithoutNotify(bgmSnapshot);
        sfxSlider?.SetValueWithoutNotify(sfxSnapshot);
        vfxSlider?.SetValueWithoutNotify(vfxSnapshot);
        oldMovieToggle?.SetIsOnWithoutNotify(oldMovieSnapshot);
    }

    private void RestoreSnapshot()
    {
        SoundManager.PreviewBgmVolume(bgmSnapshot);
        SoundManager.PreviewSfxVolume(sfxSnapshot);
        CombatAccessibilitySettings.PreviewPresentationIntensity(vfxSnapshot);
        OldMoviePresentationSettings.PreviewEnabled(oldMovieSnapshot);
        bgmSlider?.SetValueWithoutNotify(bgmSnapshot);
        sfxSlider?.SetValueWithoutNotify(sfxSnapshot);
        vfxSlider?.SetValueWithoutNotify(vfxSnapshot);
        oldMovieToggle?.SetIsOnWithoutNotify(oldMovieSnapshot);
    }

    private void ResolveControls()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            switch (button.name)
            {
                case "Button_Save":
                case "Button _Save":
                    saveButton = button;
                    break;
                case "Button_DontSave":
                case "Button _DontSave":
                    dontSaveButton = button;
                    break;
            }
        }

        foreach (Slider slider in GetComponentsInChildren<Slider>(true))
        {
            if (HasAncestorNamed(slider.transform, "Layout | BGM"))
            {
                bgmSlider = slider;
            }
            else if (HasAncestorNamed(slider.transform, "Layout | SFX"))
            {
                sfxSlider = slider;
            }
            else if (HasAncestorNamed(slider.transform, "Layout | VFX"))
            {
                vfxSlider = slider;
            }
        }

        foreach (Toggle toggle in GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.name == "Toggle | OnOffMovie"
                && HasAncestorNamed(toggle.transform, "Layout | OnOffMovie"))
            {
                oldMovieToggle = toggle;
                break;
            }
        }
    }

    private void BindControls()
    {
        UnbindControls();
        saveButton?.onClick.AddListener(SaveAndClose);
        dontSaveButton?.onClick.AddListener(CloseWithoutSaving);
        bgmSlider?.onValueChanged.AddListener(SoundManager.PreviewBgmVolume);
        sfxSlider?.onValueChanged.AddListener(SoundManager.PreviewSfxVolume);
        vfxSlider?.onValueChanged.AddListener(
            CombatAccessibilitySettings.PreviewPresentationIntensity);
        oldMovieToggle?.onValueChanged.AddListener(
            OldMoviePresentationSettings.PreviewEnabled);
    }

    private void UnbindControls()
    {
        saveButton?.onClick.RemoveListener(SaveAndClose);
        dontSaveButton?.onClick.RemoveListener(CloseWithoutSaving);
        bgmSlider?.onValueChanged.RemoveListener(SoundManager.PreviewBgmVolume);
        sfxSlider?.onValueChanged.RemoveListener(SoundManager.PreviewSfxVolume);
        vfxSlider?.onValueChanged.RemoveListener(
            CombatAccessibilitySettings.PreviewPresentationIntensity);
        oldMovieToggle?.onValueChanged.RemoveListener(
            OldMoviePresentationSettings.PreviewEnabled);
    }

    private bool HasAncestorNamed(Transform transform, string ancestorName)
    {
        Transform current = transform.parent;

        while (current != null && current != this.transform)
        {
            if (current.name == ancestorName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
