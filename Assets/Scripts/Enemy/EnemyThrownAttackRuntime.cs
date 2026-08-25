using System.Collections;
using UnityEngine;

internal readonly struct EnemyThrownProjectileFrame
{
    public EnemyThrownProjectileFrame(
        Vector3 position,
        bool reachedDodgeWindow,
        bool completed)
    {
        Position = position;
        ReachedDodgeWindow = reachedDodgeWindow;
        Completed = completed;
    }

    public Vector3 Position { get; }
    public bool ReachedDodgeWindow { get; }
    public bool Completed { get; }
}

internal sealed class EnemyThrownProjectileFlight
{
    private readonly Vector3 startPosition;
    private readonly Vector3 targetPosition;
    private readonly float duration;
    private readonly float arcHeight;
    private readonly float dodgeWindowStartTime;
    private float elapsedTime;

    public EnemyThrownProjectileFlight(
        Vector3 startPosition,
        Vector3 targetPosition,
        float duration,
        float arcHeight,
        float dodgeWindowDuration)
    {
        this.startPosition = startPosition;
        this.targetPosition = targetPosition;
        this.duration = Mathf.Max(0f, duration);
        this.arcHeight = arcHeight;
        dodgeWindowStartTime = Mathf.Max(
            0f,
            this.duration - Mathf.Max(0f, dodgeWindowDuration));
    }

    public bool IsComplete => duration <= 0f || elapsedTime >= duration;
    public bool HasReachedDodgeWindow =>
        elapsedTime >= dodgeWindowStartTime;
    public Vector3 CurrentPosition => CalculatePosition();

    public EnemyThrownProjectileFrame Advance(float deltaTime)
    {
        if (!float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime)
            && deltaTime > 0f)
        {
            elapsedTime = Mathf.Min(duration, elapsedTime + deltaTime);
        }

        return new EnemyThrownProjectileFrame(
            CalculatePosition(),
            HasReachedDodgeWindow,
            IsComplete);
    }

    private Vector3 CalculatePosition()
    {
        float progress = duration <= 0f
            ? 1f
            : Mathf.Clamp01(elapsedTime / duration);
        Vector3 position = Vector3.Lerp(
            startPosition,
            targetPosition,
            progress);
        position += Vector3.up
            * (Mathf.Sin(progress * Mathf.PI) * arcHeight);
        return position;
    }
}

internal sealed class EnemyThrownAttackRuntime
{
    private readonly EnemyController source;
    private readonly EnemyAttackData attackData;
    private readonly BoardManager boardManager;
    private readonly PlayerMove playerMove;
    private readonly PlayerHealth playerHealth;
    private readonly WaveManager waveManager;
    private readonly CombatFeedbackController combatFeedback;
    private readonly EnemyThrownProjectileFlight flight;
    private readonly GameObject projectile;
    private readonly int targetTileIndex;
    private readonly Vector3 targetPosition;
    private readonly int attackDamage;
    private readonly GameObject explosionVfxPrefab;
    private readonly float explosionVfxScale;

    public EnemyThrownAttackRuntime(
        EnemyController source,
        EnemyAttackData attackData,
        BoardManager boardManager,
        PlayerMove playerMove,
        PlayerHealth playerHealth,
        WaveManager waveManager,
        CombatFeedbackController combatFeedback,
        EnemyThrownProjectileFlight flight,
        GameObject projectile,
        int targetTileIndex,
        Vector3 targetPosition,
        int attackDamage,
        GameObject explosionVfxPrefab,
        float explosionVfxScale)
    {
        this.source = source;
        this.attackData = attackData;
        this.boardManager = boardManager;
        this.playerMove = playerMove;
        this.playerHealth = playerHealth;
        this.waveManager = waveManager;
        this.combatFeedback = combatFeedback;
        this.flight = flight;
        this.projectile = projectile;
        this.targetTileIndex = targetTileIndex;
        this.targetPosition = targetPosition;
        this.attackDamage = Mathf.Max(0, attackDamage);
        this.explosionVfxPrefab = explosionVfxPrefab;
        this.explosionVfxScale = explosionVfxScale;
    }

    public bool IsComplete { get; private set; }

    public IEnumerator Resolve()
    {
        try
        {
            EnemyPlayerDodgeWindowState dodgeState = default;
            EnemyPlayerDodgeResolution dodgeResolution = default;
            bool dodgeWindowStarted = false;

            void BeginDodgeWindow()
            {
                if (dodgeWindowStarted)
                {
                    return;
                }

                dodgeWindowStarted = true;
                dodgeState = CapturePlayerDodgeWindow();
            }

            if (flight.IsComplete)
            {
                BeginDodgeWindow();
            }

            while (!flight.IsComplete)
            {
                yield return null;

                if (GamePauseController.IsPaused)
                {
                    continue;
                }

                EnemyThrownProjectileFrame frame = flight.Advance(
                    Time.deltaTime);

                if (frame.ReachedDodgeWindow)
                {
                    BeginDodgeWindow();
                    TryConfirmPlayerDodge(
                        dodgeState,
                        ref dodgeResolution);
                }

                if (projectile != null)
                {
                    projectile.transform.position = frame.Position;
                }
            }

            if (projectile != null)
            {
                projectile.transform.position = targetPosition;
            }

            BeginDodgeWindow();
            TryConfirmPlayerDodge(dodgeState, ref dodgeResolution);
            ResolvePlayerDodgeAtImpact(
                dodgeState,
                ref dodgeResolution);
            ResolveImpact(dodgeResolution.PlayerDodged);
        }
        finally
        {
            IsComplete = true;
        }
    }

    private EnemyPlayerDodgeWindowState CapturePlayerDodgeWindow()
    {
        if (!IsPlayerInTargetTile() || boardManager == null
            || playerMove == null || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerTileIndex))
        {
            return default;
        }

        return new EnemyPlayerDodgeWindowState(
            true,
            playerTileIndex,
            playerMove.transform.position);
    }

    private void TryConfirmPlayerDodge(
        EnemyPlayerDodgeWindowState dodgeState,
        ref EnemyPlayerDodgeResolution resolution)
    {
        if (boardManager == null || playerMove == null
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int currentPlayerTileIndex)
            || !resolution.TryConfirmBeforeImpact(
                dodgeState,
                IsPlayerInTargetTile(),
                currentPlayerTileIndex,
                playerMove.transform.position,
                out int movementDirection))
        {
            return;
        }

        HandlePlayerDodgeConfirmed(
            dodgeState,
            movementDirection);
    }

    private void ResolvePlayerDodgeAtImpact(
        EnemyPlayerDodgeWindowState dodgeState,
        ref EnemyPlayerDodgeResolution resolution)
    {
        int currentPlayerTileIndex = -1;
        Vector3 currentPlayerPosition = playerMove == null
            ? dodgeState.PlayerPosition
            : playerMove.transform.position;

        if (boardManager != null && playerMove != null)
        {
            boardManager.TryGetTileIndex(
                currentPlayerPosition,
                out currentPlayerTileIndex);
        }

        if (resolution.ResolveAtImpact(
                dodgeState,
                IsPlayerInTargetTile(),
                currentPlayerTileIndex,
                currentPlayerPosition,
                out int movementDirection))
        {
            HandlePlayerDodgeConfirmed(
                dodgeState,
                movementDirection);
        }
    }

    private void HandlePlayerDodgeConfirmed(
        EnemyPlayerDodgeWindowState dodgeState,
        int movementDirection)
    {
        if (playerMove == null)
        {
            return;
        }

        EnemyController.TryApplyDodgeExposedToSource(source);
        playerMove.TryNotifyDodgeSucceededDuringAction();
        combatFeedback?.RecordPlayerDodge(
            dodgeState.PlayerPosition,
            movementDirection);
    }

    private bool IsPlayerInTargetTile()
    {
        return targetTileIndex >= 0 && boardManager != null
            && playerMove != null && boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerTileIndex)
            && playerTileIndex == targetTileIndex;
    }

    private void ResolveImpact(bool playerDodged)
    {
        SoundManager.PlaySfx("SFX_Thrower_Bomb");
        TransientVfx.Spawn(
            explosionVfxPrefab,
            targetPosition,
            Quaternion.identity,
            explosionVfxScale);

        if (attackData.AttackEffectPrefab != null
            && attackData.AttackEffectPrefab != explosionVfxPrefab)
        {
            TransientVfx.Spawn(
                attackData.AttackEffectPrefab,
                targetPosition,
                Quaternion.identity);
        }

        bool targetsPlayer = !playerDodged && IsPlayerInTargetTile();
        EnemyController enemyTarget = null;

        if (!targetsPlayer && waveManager != null)
        {
            waveManager.TryGetEnemyAtTile(
                targetTileIndex,
                out enemyTarget);
        }

        EnemyController.ApplyAttackToTarget(
            playerHealth,
            attackData,
            attackDamage,
            enemyTarget,
            targetsPlayer);
        if (source != null)
        {
            source.NotifyAttackExecuted(attackData);
        }
    }
}
