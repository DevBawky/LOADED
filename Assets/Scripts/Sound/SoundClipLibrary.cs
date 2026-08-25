using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[Serializable]
public enum SfxCategory
{
    General = 0,
    Weapon = 1,
    Impact = 2,
    Enemy = 3,
    Status = 4
}

[Serializable]
public sealed class NamedSfxClip
{
    [SerializeField] private string id;
    [SerializeField] private SfxCategory category;
    [SerializeField] private AudioClip clip;
    [SerializeField] private List<AudioClip> variants = new List<AudioClip>();
    [Range(0f, 2f)] [SerializeField] private float volume = 1f;
    [Range(0.01f, 3f)] [SerializeField] private float minPitch = 1f;
    [Range(0.01f, 3f)] [SerializeField] private float maxPitch = 1f;
    public string Id => id;
    public SfxCategory Category => category;
    public float Volume => Mathf.Clamp(volume, 0f, 2f);
    public float RandomPitch => UnityEngine.Random.Range(
        Mathf.Min(minPitch, maxPitch),
        Mathf.Max(minPitch, maxPitch));

    public AudioClip GetRandomClip()
    {
        int validClipCount = clip == null ? 0 : 1;

        if (variants != null)
        {
            foreach (AudioClip variant in variants)
            {
                if (variant != null) validClipCount++;
            }
        }

        if (validClipCount == 0) return null;
        int selectedIndex = UnityEngine.Random.Range(0, validClipCount);

        if (clip != null)
        {
            if (selectedIndex == 0) return clip;
            selectedIndex--;
        }

        foreach (AudioClip variant in variants)
        {
            if (variant == null) continue;
            if (selectedIndex == 0) return variant;
            selectedIndex--;
        }

        return null;
    }
}

[CreateAssetMenu(fileName = "SoundClipLibrary", menuName = "Loaded/Sound Clip Library")]
public sealed class SoundClipLibrary : ScriptableObject
{
    [Header("Audio Mixer Routing")]
    [Tooltip("BGM AudioSource가 출력될 AudioMixerGroup입니다.")]
    [SerializeField] private AudioMixerGroup bgmMixerGroup;
    [Tooltip("모든 동시 재생 SFX AudioSource가 출력될 AudioMixerGroup입니다.")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private AudioMixerGroup weaponMixerGroup;
    [SerializeField] private AudioMixerGroup impactMixerGroup;
    [SerializeField] private AudioMixerGroup enemyMixerGroup;
    [SerializeField] private AudioMixerGroup statusMixerGroup;

    [Header("BGM")]
    [Range(0f, 1f)] [SerializeField] private float bgmVolume = 1f;
    [SerializeField] private List<AudioClip> mainMenuBgm = new List<AudioClip>();
    [SerializeField] private List<AudioClip> nodeMapBgm = new List<AudioClip>();
    [SerializeField] private List<AudioClip> shopBgm = new List<AudioClip>();
    [SerializeField] private List<AudioClip> eventAndTreasureBgm =
        new List<AudioClip>();
    [SerializeField] private List<AudioClip> bossBgm = new List<AudioClip>();
    [SerializeField] private List<AudioClip> gameOverBgm = new List<AudioClip>();
    [SerializeField] private List<AudioClip> normalBattleBgm =
        new List<AudioClip>();
    [SerializeField] private List<AudioClip> eliteBattleBgm =
        new List<AudioClip>();

    [Header("SFX")]
    [Tooltip("Animation Event에서는 이 목록의 id를 PlaySfx(string)에 전달합니다.")]
    [SerializeField] private List<NamedSfxClip> sfx = new List<NamedSfxClip>();

    public AudioMixerGroup BgmMixerGroup => bgmMixerGroup;
    public AudioMixerGroup SfxMixerGroup => sfxMixerGroup;
    public float BgmVolume => Mathf.Clamp01(bgmVolume);
    public IReadOnlyList<AudioClip> MainMenuBgm => mainMenuBgm;
    public IReadOnlyList<AudioClip> NodeMapBgm => nodeMapBgm;
    public IReadOnlyList<AudioClip> ShopBgm => shopBgm;
    public IReadOnlyList<AudioClip> EventAndTreasureBgm =>
        eventAndTreasureBgm;
    public IReadOnlyList<AudioClip> BossBgm => bossBgm;
    public IReadOnlyList<AudioClip> GameOverBgm => gameOverBgm;
    public IReadOnlyList<AudioClip> NormalBattleBgm => normalBattleBgm;
    public IReadOnlyList<AudioClip> EliteBattleBgm => eliteBattleBgm;

    public AudioClip GetSfx(string id)
    {
        return TryGetSfx(id, out AudioClip clip, out _) ? clip : null;
    }

    public bool TryGetSfx(string id, out AudioClip clip, out float volume)
    {
        return TryGetSfx(id, out clip, out volume, out _);
    }

    public bool TryGetSfx(
        string id,
        out AudioClip clip,
        out float volume,
        out float pitch)
    {
        return TryGetSfx(
            id,
            out clip,
            out volume,
            out pitch,
            out _);
    }

    public bool TryGetSfx(
        string id,
        out AudioClip clip,
        out float volume,
        out float pitch,
        out AudioMixerGroup mixerGroup)
    {
        clip = null;
        volume = 1f;
        pitch = 1f;
        mixerGroup = sfxMixerGroup;

        if (sfx == null || string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        string normalizedId = id.Trim();
        foreach (NamedSfxClip entry in sfx)
        {
            if (entry != null
                && string.Equals(entry.Id, normalizedId,
                    StringComparison.OrdinalIgnoreCase))
            {
                clip = entry.GetRandomClip();
                volume = entry.Volume;
                pitch = entry.RandomPitch;
                mixerGroup = GetSfxMixerGroup(entry.Category);
                return clip != null;
            }
        }

        return false;
    }

    private AudioMixerGroup GetSfxMixerGroup(SfxCategory category)
    {
        AudioMixerGroup categoryGroup = category switch
        {
            SfxCategory.Weapon => weaponMixerGroup,
            SfxCategory.Impact => impactMixerGroup,
            SfxCategory.Enemy => enemyMixerGroup,
            SfxCategory.Status => statusMixerGroup,
            _ => sfxMixerGroup
        };

        return categoryGroup != null ? categoryGroup : sfxMixerGroup;
    }
}
