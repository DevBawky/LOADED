using UnityEngine;

public interface IPlayerBulletBlocker
{
    int TileIndex { get; }
    Vector3 WorldPosition { get; }
    bool IsBulletBlocking { get; }
    void HandlePlayerBulletImpact();
}
