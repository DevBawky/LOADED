using UnityEngine;

/// <summary>Enemy Animation Event에서 문자열 ID로 SFX를 재생합니다.</summary>
public sealed class EnemyAnimationSfx : MonoBehaviour
{
    public void PlaySfx(string sfxId) => SoundManager.PlaySfx(sfxId);
}
