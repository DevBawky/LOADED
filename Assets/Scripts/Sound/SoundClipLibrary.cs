using UnityEngine;

[CreateAssetMenu(
    fileName = "SoundClipLibrary",
    menuName = "Loaded/Sound Clip Library")]
public sealed class SoundClipLibrary : ScriptableObject
{
    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioClip reloadClip;
    [SerializeField] private AudioClip hitClip;

    public AudioClip FireClip => fireClip;
    public AudioClip ReloadClip => reloadClip;
    public AudioClip HitClip => hitClip;
}
