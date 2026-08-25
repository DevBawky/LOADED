using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the runtime renderer, material lifetime, and geometry used to preview
/// a loaded bullet's reachable path.
/// </summary>
internal sealed class PlayerShotRangePreview
{
    private const float LineWidth = 0.08f;
    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");
    private static readonly int GridColorId =
        Shader.PropertyToID("_GridColor");
    private static readonly int BeamColorId =
        Shader.PropertyToID("_BeamColor");
    private static readonly int DashCountId =
        Shader.PropertyToID("_DashCount");

    private readonly Transform owner;
    private readonly Transform firePoint;
    private readonly BoardManager boardManager;
    private readonly WaveManager waveManager;
    private readonly RelicManager relicManager;
    private readonly List<EnemyController> targets =
        new List<EnemyController>();

    private LineRenderer primaryLine;
    private LineRenderer secondaryLine;
    private Material solidMaterial;
    private Material dashedMaterial;

    public PlayerShotRangePreview(
        Transform owner,
        Transform firePoint,
        BoardManager boardManager,
        WaveManager waveManager,
        RelicManager relicManager)
    {
        this.owner = owner;
        this.firePoint = firePoint;
        this.boardManager = boardManager;
        this.waveManager = waveManager;
        this.relicManager = relicManager;
    }

    public bool Show(
        IReadOnlyList<BulletInstance> loadedBullets,
        int loadedBulletIndex)
    {
        if (owner == null || boardManager == null || loadedBullets == null
            || loadedBulletIndex < 0
            || loadedBulletIndex >= loadedBullets.Count)
        {
            Hide();
            return false;
        }

        bool hasResolvedShot = TryResolveLoadedShot(
            loadedBullets,
            loadedBulletIndex,
            out BulletInstance bullet,
            out int shotDirection);

        if (!hasResolvedShot
            || !boardManager.TryGetTileIndex(
                owner.position,
                out int playerTileIndex))
        {
            Hide();
            return false;
        }

        LineRenderer line = GetOrCreatePrimaryLine();

        if (line == null
            || !TryGetStartPosition(playerTileIndex, out Vector3 startPosition))
        {
            Hide();
            return false;
        }

        ApplyColors(bullet.SecondaryLineColor);

        if (BulletEffectUtility.IsBoardWideShot(bullet))
        {
            return ShowBoardWide(startPosition, line);
        }

        return ShowDirectional(
            bullet,
            playerTileIndex,
            startPosition,
            line,
            shotDirection);
    }

    public void Hide()
    {
        if (primaryLine != null)
        {
            primaryLine.enabled = false;
        }

        if (secondaryLine != null)
        {
            secondaryLine.enabled = false;
        }
    }

    public void Dispose()
    {
        DestroyMaterial(solidMaterial);
        DestroyMaterial(dashedMaterial);
        solidMaterial = null;
        dashedMaterial = null;
    }

    private bool TryGetStartPosition(
        int playerTileIndex,
        out Vector3 startPosition)
    {
        if (firePoint != null)
        {
            startPosition = firePoint.position;
            return true;
        }

        if (boardManager.TryGetTilePosition(
                playerTileIndex,
                out startPosition))
        {
            startPosition.y += 0.15f;
            return true;
        }

        return false;
    }

    private bool ShowBoardWide(
        Vector3 startPosition,
        LineRenderer line)
    {
        LineRenderer otherLine = GetOrCreateSecondaryLine();

        if (otherLine == null
            || !boardManager.TryGetTilePosition(
                0,
                out Vector3 leftEndPosition)
            || !boardManager.TryGetTilePosition(
                boardManager.BoardCount - 1,
                out Vector3 rightEndPosition))
        {
            Hide();
            return false;
        }

        MatchLinePlane(startPosition, ref leftEndPosition);
        MatchLinePlane(startPosition, ref rightEndPosition);
        SetLine(line, startPosition, leftEndPosition, solidMaterial, LineWidth, 1f);
        SetLine(
            otherLine,
            startPosition,
            rightEndPosition,
            solidMaterial,
            LineWidth,
            1f);
        return true;
    }

    private bool ShowDirectional(
        BulletInstance bullet,
        int playerTileIndex,
        Vector3 startPosition,
        LineRenderer line,
        int direction)
    {
        int shotRange = relicManager == null
            ? bullet.MaxRange
            : relicManager.GetShotRange(bullet);
        int endTileIndex = Mathf.Clamp(
            playerTileIndex + direction * shotRange,
            0,
            boardManager.BoardCount - 1);

        if (endTileIndex == playerTileIndex
            || !boardManager.TryGetTilePosition(
                endTileIndex,
                out Vector3 endPosition))
        {
            Hide();
            return false;
        }

        MatchLinePlane(startPosition, ref endPosition);
        targets.Clear();
        waveManager?.GetEnemiesInDirection(
            owner.position,
            direction,
            shotRange,
            targets);

        Vector3 solidEnd = endPosition;
        Vector3 dashedStart = Vector3.zero;
        Vector3 dashedEnd = Vector3.zero;
        bool hasDashedRange = false;
        float dashedAlpha = 1f;

        for (int index = 0; index < targets.Count; index++)
        {
            EnemyController target = targets[index];
            solidEnd = GetEnemyEdge(target, startPosition, direction, false);
            float penetrationChance = GetPenetrationChance(bullet, index);

            if (penetrationChance >= 100f)
            {
                if (index == targets.Count - 1)
                {
                    solidEnd = endPosition;
                }

                continue;
            }

            if (penetrationChance <= 0f)
            {
                break;
            }

            hasDashedRange = true;
            dashedAlpha = penetrationChance / 100f;
            dashedStart = GetEnemyEdge(target, startPosition, direction, true);
            dashedEnd = FindDashedEnd(
                bullet,
                index,
                startPosition,
                endPosition,
                direction);
            break;
        }

        SetLine(line, startPosition, solidEnd, solidMaterial, LineWidth, 1f);

        if (!hasDashedRange)
        {
            if (secondaryLine != null)
            {
                secondaryLine.enabled = false;
            }

            return true;
        }

        LineRenderer dashedLine = GetOrCreateSecondaryLine();

        if (dashedLine == null)
        {
            Hide();
            return false;
        }

        float dashCount = Mathf.Max(
            2f,
            Vector3.Distance(dashedStart, dashedEnd)
            / Mathf.Max(0.01f, boardManager.BoardDistance)
            * 4f);
        dashedMaterial.SetFloat(DashCountId, dashCount);
        SetLine(
            dashedLine,
            dashedStart,
            dashedEnd,
            dashedMaterial,
            LineWidth * 0.5f,
            dashedAlpha);
        return true;
    }

    private Vector3 FindDashedEnd(
        BulletInstance bullet,
        int initialTargetIndex,
        Vector3 startPosition,
        Vector3 fallbackEnd,
        int direction)
    {
        Vector3 end = fallbackEnd;

        for (int index = initialTargetIndex + 1;
             index < targets.Count;
             index++)
        {
            end = GetEnemyEdge(
                targets[index],
                startPosition,
                direction,
                false);
            float chance = GetPenetrationChance(bullet, index);

            if (chance <= 0f)
            {
                break;
            }

            if (index == targets.Count - 1)
            {
                end = fallbackEnd;
            }
        }

        return end;
    }

    private Vector3 GetEnemyEdge(
        EnemyController enemy,
        Vector3 linePosition,
        int direction,
        bool farSide)
    {
        Vector3 position = enemy.transform.position;
        float offset = boardManager.BoardDistance * 0.2f;
        position.x += (farSide ? direction : -direction) * offset;
        MatchLinePlane(linePosition, ref position);
        return position;
    }

    private static float GetPenetrationChance(
        BulletInstance bullet,
        int hitIndex)
    {
        if (bullet == null || hitIndex < 0
            || hitIndex >= bullet.PenetrationChances.Count)
        {
            return 0f;
        }

        PenetrationChanceData chance = bullet.PenetrationChances[hitIndex];
        return chance == null
            ? 0f
            : Mathf.Clamp(chance.Chance, 0f, 100f);
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

    private LineRenderer GetOrCreatePrimaryLine()
    {
        if (primaryLine != null)
        {
            return primaryLine;
        }

        Shader shader = Shader.Find("Loaded/Enemy Warning Flow");

        if (shader == null)
        {
            Debug.LogWarning(
                "The player bullet range preview shader was not found.",
                owner);
            return null;
        }

        solidMaterial = CreateSolidMaterial(shader);
        dashedMaterial = new Material(solidMaterial)
        {
            name = "Player Bullet Range Preview Dashed (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        dashedMaterial.SetFloat("_DashEnabled", 1f);
        dashedMaterial.SetFloat("_DashFill", 0.72f);
        dashedMaterial.SetFloat("_DashSoftness", 0.04f);
        primaryLine = CreateLine("Line | Bullet Range Preview");
        return primaryLine;
    }

    private LineRenderer GetOrCreateSecondaryLine()
    {
        if (secondaryLine != null)
        {
            return secondaryLine;
        }

        if (GetOrCreatePrimaryLine() == null)
        {
            return null;
        }

        secondaryLine = CreateLine("Line | Bullet Range Preview Secondary");
        return secondaryLine;
    }

    private static Material CreateSolidMaterial(Shader shader)
    {
        Material material = new Material(shader)
        {
            name = "Player Bullet Range Preview (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        material.SetFloat("_BaseIntensity", 0.65f);
        material.SetFloat("_OverallAlpha", 0.62f);
        material.SetFloat("_GridColumns", 14f);
        material.SetFloat("_GridRows", 2f);
        material.SetFloat("_GridLineWidth", 0.08f);
        material.SetFloat("_GridSoftness", 0.025f);
        material.SetFloat("_GridIntensity", 1.8f);
        material.SetFloat("_GridScrollSpeed", 1.6f);
        material.SetFloat("_BeamRepeat", 3f);
        material.SetFloat("_BeamWidth", 0.35f);
        material.SetFloat("_BeamSoftness", 0.14f);
        material.SetFloat("_BeamIntensity", 2.4f);
        material.SetFloat("_BeamScrollSpeed", 0.65f);
        material.SetFloat("_PulseAmount", 0.12f);
        material.SetFloat("_PulseFrequency", 2f);
        material.SetFloat("_EdgeSoftness", 0.22f);
        material.SetFloat("_EndFade", 0.035f);
        material.SetFloat("_DashEnabled", 0f);
        return material;
    }

    private LineRenderer CreateLine(string objectName)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(owner, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.widthMultiplier = LineWidth;
        line.numCapVertices = 2;
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.enabled = false;
        SpriteRenderer playerRenderer =
            owner.GetComponentInChildren<SpriteRenderer>();

        if (playerRenderer != null)
        {
            line.sortingLayerID = playerRenderer.sortingLayerID;
            line.sortingOrder = playerRenderer.sortingOrder + 20;
        }
        else
        {
            line.sortingOrder = 20;
        }

        line.sharedMaterial = solidMaterial;
        return line;
    }

    private void ApplyColors(Color secondaryLineColor)
    {
        ApplyColor(solidMaterial, secondaryLineColor);
        ApplyColor(dashedMaterial, secondaryLineColor);
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        Color baseColor = color;
        Color gridColor = color;
        baseColor.a *= 0.55f;
        gridColor.a *= 0.9f;
        material.SetColor(BaseColorId, baseColor);
        material.SetColor(GridColorId, gridColor);
        material.SetColor(BeamColorId, color);
    }

    private static void SetLine(
        LineRenderer line,
        Vector3 start,
        Vector3 end,
        Material material,
        float width,
        float alpha)
    {
        line.sharedMaterial = material;
        line.widthMultiplier = width;
        Color color = Color.white;
        color.a = Mathf.Clamp01(alpha);
        line.startColor = color;
        line.endColor = color;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.enabled = true;
    }

    private static void MatchLinePlane(
        Vector3 source,
        ref Vector3 target)
    {
        target.y = source.y;
        target.z = source.z;
    }

    private static void DestroyMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(material);
        }
        else
        {
            Object.DestroyImmediate(material);
        }
    }
}
