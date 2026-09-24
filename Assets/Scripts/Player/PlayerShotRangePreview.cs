using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presents a loaded bullet's reachable cells without changing combat state.
/// BoardTile owns the shared grid shader and renderer lifetime.
/// </summary>
internal sealed class PlayerShotRangePreview
{
    private readonly Transform owner;
    private readonly BoardManager boardManager;
    private readonly WaveManager waveManager;
    private readonly RelicManager relicManager;
    private readonly PlayerMove playerMove;
    private readonly List<EnemyController> targets = new List<EnemyController>();
    private readonly List<Vector2Int> visibleCells = new List<Vector2Int>();

    public PlayerShotRangePreview(Transform owner, Transform firePoint,
        BoardManager boardManager, WaveManager waveManager, RelicManager relicManager)
    {
        this.owner = owner;
        this.boardManager = boardManager;
        this.waveManager = waveManager;
        this.relicManager = relicManager;
        playerMove = owner == null ? null : owner.GetComponent<PlayerMove>();
    }

    public bool Show(IReadOnlyList<BulletInstance> loadedBullets, int loadedBulletIndex)
    {
        Hide();
        if (owner == null || boardManager == null
            || !TryResolveLoadedShot(loadedBullets, loadedBulletIndex,
                out BulletInstance bullet, out int direction))
        {
            return false;
        }

        int lane = playerMove == null ? 0 : playerMove.CurrentLaneIndex;
        if (!boardManager.TryGetTileIndex(owner.position, lane, out int playerTile))
        {
            return false;
        }

        Color color = bullet.SecondaryLineColor;
        if (BulletEffectUtility.IsBoardWideShot(bullet))
        {
            for (int row = 0; row < boardManager.LaneCount; row++)
            {
                for (int tile = 0; tile < boardManager.BoardCount; tile++)
                {
                    if (tile != playerTile || row != lane)
                    {
                        ShowCell(tile, row, color);
                    }
                }
            }
            return visibleCells.Count > 0;
        }

        int range = relicManager == null ? bullet.MaxRange : relicManager.GetShotRange(bullet);
        range = Mathf.Clamp(range, 0, boardManager.BoardCount - 1);
        targets.Clear();
        waveManager?.GetEnemiesInDirection(owner.position, lane, direction, range, targets);
        int targetIndex = 0;
        float reachAlpha = 1f;
        for (int distance = 1; distance <= range; distance++)
        {
            int tile = playerTile + direction * distance;
            if (tile < 0 || tile >= boardManager.BoardCount)
            {
                break;
            }
            Color tileColor = color;
            if (reachAlpha < 1f) tileColor.a = 0.5f;
            ShowCell(tile, lane, tileColor);

            if (targetIndex >= targets.Count
                || !boardManager.TryGetTileIndex(targets[targetIndex].transform.position,
                    lane, out int targetTile) || targetTile != tile)
            {
                continue;
            }

            float chance = GetPenetrationChance(bullet, targetIndex++);
            if (chance <= 0f)
            {
                break;
            }
            // Uncertain reach has one consistent opacity, even across multiple rolls.
            if (reachAlpha >= 1f && chance < 100f)
            {
                reachAlpha = 0.5f;
            }
        }
        return visibleCells.Count > 0;
    }

    private void ShowCell(int tile, int lane, Color color)
    {
        if (boardManager.SetTilePreviewColor(tile, lane, color))
        {
            visibleCells.Add(new Vector2Int(tile, lane));
        }
    }

    public void Hide()
    {
        if (boardManager != null)
        {
            foreach (Vector2Int cell in visibleCells)
            {
                boardManager.SetTilePreviewColor(cell.x, cell.y, null);
            }
        }
        visibleCells.Clear();
    }

    public void Dispose()
    {
        Hide();
    }

    private static float GetPenetrationChance(BulletInstance bullet, int hitIndex)
    {
        if (bullet == null || hitIndex < 0 || hitIndex >= bullet.PenetrationChances.Count)
        {
            return 0f;
        }
        PenetrationChanceData chance = bullet.PenetrationChances[hitIndex];
        return chance == null ? 0f : Mathf.Clamp(chance.Chance, 0f, 100f);
    }

    internal static bool TryResolveLoadedShot(
        IReadOnlyList<BulletInstance> loadedBullets,
        int loadedBulletIndex,
        int facingDirection,
        out BulletInstance resolvedBullet,
        out int shotDirection)
    {
        resolvedBullet = null;
        shotDirection = facingDirection >= 0 ? 1 : -1;

        if (loadedBullets == null
            || loadedBulletIndex < 0
            || loadedBulletIndex >= loadedBullets.Count)
        {
            return false;
        }

        BulletInstance previousResolvedBullet = null;
        int resolvedFacingDirection = shotDirection;

        for (int index = loadedBullets.Count - 1;
             index >= loadedBulletIndex;
             index--)
        {
            resolvedBullet = BulletEffectUtility.ResolveShot(
                loadedBullets[index],
                previousResolvedBullet);

            if (resolvedBullet == null)
            {
                return false;
            }

            if (index == loadedBulletIndex)
            {
                shotDirection = BulletEffectUtility.ResolveShotDirection(
                    resolvedBullet,
                    resolvedFacingDirection);
                return true;
            }

            resolvedFacingDirection =
                BulletEffectUtility.ResolveFacingDirectionAfterShot(
                    resolvedBullet,
                    resolvedFacingDirection);
            previousResolvedBullet = resolvedBullet;
        }

        return false;
    }

    private bool TryResolveLoadedShot(
        IReadOnlyList<BulletInstance> loadedBullets,
        int loadedBulletIndex,
        out BulletInstance resolvedBullet,
        out int shotDirection)
    {
        int facingDirection = owner.localScale.x >= 0f ? 1 : -1;
        return TryResolveLoadedShot(
            loadedBullets,
            loadedBulletIndex,
            facingDirection,
            out resolvedBullet,
            out shotDirection);
    }

}
