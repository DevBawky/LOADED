using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Unity.Cinemachine;

public class PlayerShoot : MonoBehaviour
{
    private const float BulletFeedbackStartAlpha = 0.2f;

    [SerializeField] private DeckManager deckManager;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Transform firePoint;
    [FormerlySerializedAs("projectilePrefab")]
    [SerializeField] private BulletLine bulletLinePrefab;
    [SerializeField] private CinemachineBasicMultiChannelPerlin recoilNoise;
    [SerializeField] private Transform recoilCameraTransform;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private PlayerCylinderUI cylinderUI;
    [SerializeField] private Image bulletFeedbackImage;
    [Min(0f)]
    [SerializeField] private float shotInterval = 0.2f;

    [Header("Critical")]
    [Range(0f, 100f)]
    [SerializeField] private float criticalChance;

    [Header("Shot Presentation")]
    [Min(0f)]
    [SerializeField] private float maxRandomShotAngle = 5f;

    [Header("Camera Recoil")]
    [Min(0f)]
    [SerializeField] private float cameraRecoilScale = 0.02f;
    [Min(0f)]
    [SerializeField] private float recoilFrequencyGain = 0.8f;
    [Min(0f)]
    [SerializeField] private float recoilAttackDuration = 0.1f;
    [Min(0f)]
    [SerializeField] private float recoilRecoveryDuration = 0.45f;
    [SerializeField] private Vector3 cameraRestPosition = new Vector3(0f, 0f, -10f);

    private int lastActionFrame = -1;
    private bool isFiring;
    private Coroutine cameraRecoilCoroutine;
    private Coroutine bulletFeedbackCoroutine;
    private readonly List<EnemyController> targetBuffer =
        new List<EnemyController>();
    private readonly List<EnemyController> hitBuffer =
        new List<EnemyController>();

    public bool IsFiring => isFiring;
    public float CriticalChance => criticalChance;

    private void Awake()
    {
        if (playerMove != null)
        {
            playerMove.SetShooting(false);
        }

        ResetBulletFeedback();
        ResetCameraRecoil();
    }

    private void Start()
    {
        if (cylinderUI != null)
        {
            cylinderUI.Initialize(deckManager);
        }
    }

    private void OnDisable()
    {
        isFiring = false;

        if (playerMove != null)
        {
            playerMove.SetShooting(false);
        }

        if (cameraRecoilCoroutine != null)
        {
            StopCoroutine(cameraRecoilCoroutine);
            cameraRecoilCoroutine = null;
        }

        if (bulletFeedbackCoroutine != null)
        {
            StopCoroutine(bulletFeedbackCoroutine);
            bulletFeedbackCoroutine = null;
        }

        ResetBulletFeedback();
        ResetCameraRecoil();
    }

    private void Update()
    {
        if (GamePauseController.IsPaused || isFiring)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame)
            {
                Reload();
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                Shoot();
                return;
            }
        }

        Mouse mouse = Mouse.current;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame
            && (eventSystem == null || !eventSystem.IsPointerOverGameObject()))
        {
            Shoot();
        }
    }

    public void Reload()
    {
        if (GamePauseController.IsPaused || isFiring || !TryBeginAction())
        {
            return;
        }

        if (deckManager == null || playerMove == null)
        {
            Debug.LogError("Deck Manager and Player Move must be assigned in the Inspector.", this);
            return;
        }

        if (!playerMove.CanStartAction)
        {
            return;
        }

        if (deckManager.TryReload(out BulletInstance loadedBullet)
            && (loadedBullet == null || !loadedBullet.DoesNotConsumeTurn))
        {
            playerMove.CompleteTurn();
        }
    }

    public void Shoot()
    {
        if (GamePauseController.IsPaused || isFiring || !TryBeginAction())
        {
            return;
        }

        if (deckManager == null || playerMove == null || playerHealth == null
            || boardManager == null || waveManager == null || firePoint == null
            || bulletLinePrefab == null)
        {
            Debug.LogError(
                "Deck Manager, Player Move, Player Health, Board Manager, Wave Manager, Fire Point, and Bullet Line Prefab must be assigned in the Inspector.",
                this);
            return;
        }

        if (!playerMove.CanStartAction)
        {
            return;
        }

        if (deckManager.LoadedBullets.Count == 0)
        {
            return;
        }

        int horizontalDirection = transform.localScale.x >= 0f ? 1 : -1;
        int firstBulletIndex = deckManager.LoadedBullets.Count - 1;
        BulletInstance firstBullet = deckManager.LoadedBullets[firstBulletIndex];

        if (firstBullet == null
            || !boardManager.TryGetTileIndex(transform.position, out _))
        {
            return;
        }

        StartCoroutine(ShootLoadedBullets(horizontalDirection));
    }

    private IEnumerator ShootLoadedBullets(int horizontalDirection)
    {
        isFiring = true;
        playerMove.SetShooting(true);
        bool firedAnyBullet = false;
        bool consumesTurn = false;

        while (deckManager.LoadedBullets.Count > 0)
        {
            while (GamePauseController.IsPaused)
            {
                yield return null;
            }

            int bulletIndex = deckManager.LoadedBullets.Count - 1;
            BulletInstance bulletData = deckManager.LoadedBullets[bulletIndex];

            if (bulletData == null)
            {
                break;
            }

            waveManager.GetEnemiesInDirection(
                transform.position,
                horizontalDirection,
                bulletData.MaxRange,
                targetBuffer);

            Vector3 endPoint;

            if (targetBuffer.Count > 0)
            {
                BuildHitTargets(bulletData);
                endPoint = hitBuffer[hitBuffer.Count - 1].transform.position;
            }
            else
            {
                hitBuffer.Clear();
                endPoint = GetMissEndPoint(
                    horizontalDirection,
                    bulletData.MaxRange);
            }

            Vector3 shotStartPoint = firePoint.position;
            Vector3 shotEndPoint = GetShotLineEndPoint(
                shotStartPoint,
                endPoint);

            BulletLine bulletLine = Instantiate(
                bulletLinePrefab,
                shotStartPoint,
                Quaternion.identity);

            if (!bulletLine.Initialize(
                    bulletData,
                    shotStartPoint,
                    shotEndPoint))
            {
                Destroy(bulletLine.gameObject);
                break;
            }

            if (!deckManager.TryFireLoadedBullet(out BulletInstance firedBullet)
                || firedBullet != bulletData)
            {
                Destroy(bulletLine.gameObject);
                break;
            }

            firedAnyBullet = true;
            consumesTurn |= !bulletData.DoesNotConsumeTurn;

            ShowBulletFeedback(bulletData);
            GenerateRecoil(bulletData);
            bool isCritical = RollCritical();
            yield return ApplyHitResults(
                bulletData,
                horizontalDirection,
                isCritical);

            if (deckManager.LoadedBullets.Count > 0 && shotInterval > 0f)
            {
                yield return WaitForShotInterval();
            }
            else
            {
                yield return null;
            }
        }

        if (firedAnyBullet && consumesTurn)
        {
            playerMove.CompleteTurn();
        }

        isFiring = false;
        playerMove.SetShooting(false);
    }

    private void BuildHitTargets(BulletInstance bulletData)
    {
        hitBuffer.Clear();
        hitBuffer.Add(targetBuffer[0]);
        int hitCount = 1;

        for (int targetIndex = 1;
             targetIndex < targetBuffer.Count
             && hitCount < bulletData.MaxHitCount;
             targetIndex++)
        {
            if (!bulletData.RollPenetrationAfterHit(hitCount))
            {
                break;
            }

            hitBuffer.Add(targetBuffer[targetIndex]);
            hitCount++;
        }
    }

    private IEnumerator ApplyHitResults(
        BulletInstance bulletData,
        int horizontalDirection,
        bool isCritical)
    {
        if (bulletData == null || hitBuffer.Count == 0)
        {
            yield break;
        }

        int attackDamage = CalculateAttackDamage(
            bulletData,
            isCritical);

        for (int hitIndex = 0; hitIndex < hitBuffer.Count; hitIndex++)
        {
            EnemyController enemy = hitBuffer[hitIndex];

            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                continue;
            }

            int appliedDamage = enemy.ApplyAttackDamage(
                attackDamage,
                isCritical);

            IReadOnlyList<BulletEffectData> effects = bulletData.Effects;

            for (int effectIndex = 0;
                 effectIndex < effects.Count;
                effectIndex++)
            {
                BulletEffectData effect = effects[effectIndex];

                if (effect == null)
                {
                    continue;
                }

                bool canApplyToDefeatedTarget =
                    effect.EffectType == BulletEffectType.LifeSteal;

                if (!canApplyToDefeatedTarget
                    && (enemy == null || enemy.CurrentHealth <= 0))
                {
                    continue;
                }

                if (!effect.RollActivation())
                {
                    continue;
                }

                yield return ApplyBulletEffect(
                    effect,
                    enemy,
                    horizontalDirection,
                    appliedDamage);
            }
        }
    }

    private IEnumerator ApplyBulletEffect(
        BulletEffectData effect,
        EnemyController enemy,
        int horizontalDirection,
        int appliedDamage)
    {
        if (effect == null)
        {
            yield break;
        }

        if (effect.EffectType == BulletEffectType.LifeSteal)
        {
            playerHealth.Heal(appliedDamage);

            if (appliedDamage > 0 && enemy != null)
            {
                enemy.ShowLifeStealStatus();
            }

            yield break;
        }

        if (enemy == null || enemy.CurrentHealth <= 0)
        {
            yield break;
        }

        switch (effect.EffectType)
        {
            case BulletEffectType.Poison:
                enemy.AddStatusEffect(
                    StatusEffectType.Poison,
                    effect.StackCount);
                break;
            case BulletEffectType.Stun:
                enemy.AddStatusEffect(
                    StatusEffectType.Stun,
                    effect.StackCount);
                break;
            case BulletEffectType.Mark:
                enemy.AddStatusEffect(
                    StatusEffectType.Mark,
                    effect.StackCount);
                break;
            case BulletEffectType.Knockback:
                yield return playerMove.PushEnemyFromBullet(
                    enemy,
                    horizontalDirection,
                    effect.KnockbackDistance);
                break;
            case BulletEffectType.PositionSwap:
                yield return playerMove.SwapPositionWithEnemy(enemy);
                break;
            case BulletEffectType.Weakness:
                enemy.AddStatusEffect(
                    StatusEffectType.Weakness,
                    effect.StackCount);
                break;
        }
    }

    private int CalculateAttackDamage(
        BulletInstance bulletData,
        bool isCritical)
    {
        if (bulletData == null || bulletData.Damage <= 0)
        {
            return 0;
        }

        int damage = bulletData.Damage;

        if (isCritical)
        {
            damage = Mathf.CeilToInt(
                damage * bulletData.CriticalDamageMultiplier);
        }

        return playerHealth.ModifyOutgoingAttackDamage(damage);
    }

    private bool RollCritical()
    {
        return CanTriggerCritical(UnityEngine.Random.Range(0f, 100f));
    }

    public bool CanTriggerCritical(float roll)
    {
        float chance = Mathf.Clamp(criticalChance, 0f, 100f);
        return chance >= 100f
            || chance > 0f && roll >= 0f && roll < chance;
    }

    private Vector3 GetMissEndPoint(int horizontalDirection, int maxRange)
    {
        if (boardManager.TryGetRangedTilePosition(
                transform.position,
                horizontalDirection,
                maxRange,
                out Vector3 rangedTilePosition))
        {
            return rangedTilePosition;
        }

        float fallbackDistance = Mathf.Max(
            boardManager.BoardDistance * Mathf.Max(1, maxRange),
            0.01f);
        return firePoint.position
            + Vector3.right * horizontalDirection * fallbackDistance;
    }

    private Vector3 GetShotLineEndPoint(
        Vector3 startPoint,
        Vector3 targetEndPoint)
    {
        Vector3 horizontalEndPoint = new Vector3(
            targetEndPoint.x,
            startPoint.y,
            startPoint.z);
        Vector3 horizontalShotVector = horizontalEndPoint - startPoint;
        float angleLimit = Mathf.Max(0f, maxRandomShotAngle);
        float randomAngle = UnityEngine.Random.Range(
            -angleLimit,
            angleLimit);
        Vector3 angledShotVector = Quaternion.AngleAxis(
            randomAngle,
            Vector3.forward) * horizontalShotVector;
        return startPoint + angledShotVector;
    }

    private IEnumerator WaitForShotInterval()
    {
        float elapsedTime = 0f;

        while (elapsedTime < shotInterval)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                elapsedTime += Time.deltaTime;
            }
        }
    }

    private void ShowBulletFeedback(BulletInstance bulletData)
    {
        if (bulletFeedbackImage == null || bulletData == null)
        {
            return;
        }

        if (bulletFeedbackCoroutine != null)
        {
            StopCoroutine(bulletFeedbackCoroutine);
            bulletFeedbackCoroutine = null;
        }

        Color feedbackColor = bulletData.PrimaryLineColor;
        feedbackColor.a = BulletFeedbackStartAlpha;
        bulletFeedbackImage.raycastTarget = false;
        bulletFeedbackImage.color = feedbackColor;
        bulletFeedbackImage.gameObject.SetActive(true);

        if (shotInterval <= 0f)
        {
            ResetBulletFeedback();
            return;
        }

        bulletFeedbackCoroutine = StartCoroutine(
            FadeBulletFeedback(feedbackColor));
    }

    private IEnumerator FadeBulletFeedback(Color startColor)
    {
        float fadeDuration = shotInterval;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            if (bulletFeedbackImage == null)
            {
                bulletFeedbackCoroutine = null;
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / fadeDuration);
            startColor.a = Mathf.Lerp(
                BulletFeedbackStartAlpha,
                0f,
                progress);
            bulletFeedbackImage.color = startColor;
        }

        ResetBulletFeedback();
        bulletFeedbackCoroutine = null;
    }

    private void ResetBulletFeedback()
    {
        if (bulletFeedbackImage == null)
        {
            return;
        }

        Color feedbackColor = bulletFeedbackImage.color;
        feedbackColor.a = 0f;
        bulletFeedbackImage.raycastTarget = false;
        bulletFeedbackImage.color = feedbackColor;
        bulletFeedbackImage.gameObject.SetActive(false);
    }

    private void GenerateRecoil(BulletInstance bulletData)
    {
        if (bulletData.RecoilStrength <= 0f || cameraRecoilScale <= 0f)
        {
            return;
        }

        if (recoilNoise == null || recoilCameraTransform == null)
        {
            Debug.LogError(
                "Recoil Noise and Recoil Camera Transform must be assigned in the Inspector.",
                this);
            return;
        }

        if (cameraRecoilCoroutine != null)
        {
            StopCoroutine(cameraRecoilCoroutine);
        }

        float targetAmplitudeGain = bulletData.RecoilStrength * cameraRecoilScale;
        cameraRecoilCoroutine = StartCoroutine(
            PlayCameraRecoil(targetAmplitudeGain));
    }

    private IEnumerator PlayCameraRecoil(float targetAmplitudeGain)
    {
        float currentAmplitudeGain = recoilNoise.AmplitudeGain;
        recoilNoise.FrequencyGain = recoilFrequencyGain;

        yield return ChangeAmplitudeGain(
            currentAmplitudeGain,
            targetAmplitudeGain,
            recoilAttackDuration);
        yield return ChangeAmplitudeGain(
            targetAmplitudeGain,
            0f,
            recoilRecoveryDuration,
            true);

        ResetCameraRecoil();
        cameraRecoilCoroutine = null;
    }

    private IEnumerator ChangeAmplitudeGain(
        float startGain,
        float targetGain,
        float duration,
        bool returnCameraToRestPosition = false)
    {
        Vector3 startCameraPosition = recoilCameraTransform.position;

        if (duration <= 0f)
        {
            recoilNoise.AmplitudeGain = targetGain;

            if (returnCameraToRestPosition)
            {
                recoilCameraTransform.position = cameraRestPosition;
            }

            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            yield return null;
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float smoothProgress = GetSmootherStep(progress);
            recoilNoise.AmplitudeGain = Mathf.Lerp(
                startGain,
                targetGain,
                smoothProgress);

            if (returnCameraToRestPosition)
            {
                recoilCameraTransform.position = Vector3.Lerp(
                    startCameraPosition,
                    cameraRestPosition,
                    smoothProgress);
            }
        }

        recoilNoise.AmplitudeGain = targetGain;

        if (returnCameraToRestPosition)
        {
            recoilCameraTransform.position = cameraRestPosition;
        }
    }

    private float GetSmootherStep(float progress)
    {
        progress = Mathf.Clamp01(progress);
        return progress * progress * progress
            * (progress * (progress * 6f - 15f) + 10f);
    }

    private void ResetCameraRecoil()
    {
        if (recoilNoise != null)
        {
            recoilNoise.AmplitudeGain = 0f;
            recoilNoise.FrequencyGain = 0f;
        }

        if (recoilCameraTransform != null)
        {
            recoilCameraTransform.position = cameraRestPosition;
        }
    }

    private bool TryBeginAction()
    {
        if (lastActionFrame == Time.frameCount)
        {
            return false;
        }

        lastActionFrame = Time.frameCount;
        return true;
    }
}
