using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Unity.Cinemachine;

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Transform firePoint;
    [FormerlySerializedAs("projectilePrefab")]
    [SerializeField] private BulletLine bulletLinePrefab;
    [SerializeField] private CinemachineBasicMultiChannelPerlin recoilNoise;
    [SerializeField] private Transform recoilCameraTransform;
    [SerializeField] private EventSystem eventSystem;
    [Min(0f)]
    [SerializeField] private float shotInterval = 0.2f;

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
    private readonly List<EnemyController> targetBuffer =
        new List<EnemyController>();
    private readonly List<EnemyController> hitBuffer =
        new List<EnemyController>();

    public bool IsFiring => isFiring;

    private void Awake()
    {
        ResetCameraRecoil();
    }

    private void OnDisable()
    {
        if (cameraRecoilCoroutine != null)
        {
            StopCoroutine(cameraRecoilCoroutine);
            cameraRecoilCoroutine = null;
        }

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

        if (deckManager.TryReload())
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

        if (deckManager == null || boardManager == null || waveManager == null
            || firePoint == null || bulletLinePrefab == null)
        {
            Debug.LogError(
                "Deck Manager, Board Manager, Wave Manager, Fire Point, and Bullet Line Prefab must be assigned in the Inspector.",
                this);
            return;
        }

        if (deckManager.LoadedBullets.Count == 0)
        {
            return;
        }

        if (RequiresTurnAfterFiring() && playerMove == null)
        {
            Debug.LogError("Player Move must be assigned when loaded bullets consume a turn.", this);
            return;
        }

        int horizontalDirection = transform.localScale.x >= 0f ? 1 : -1;
        BulletData firstBullet = deckManager.LoadedBullets[0];

        if (firstBullet == null
            || !boardManager.TryGetTileIndex(transform.position, out _))
        {
            return;
        }

        waveManager.GetEnemiesInDirection(
            transform.position,
            horizontalDirection,
            firstBullet.MaxRange,
            targetBuffer);

        if (targetBuffer.Count == 0)
        {
            return;
        }

        StartCoroutine(ShootLoadedBullets(horizontalDirection));
    }

    private IEnumerator ShootLoadedBullets(int horizontalDirection)
    {
        isFiring = true;
        bool firedAnyBullet = false;
        bool consumesTurn = false;

        while (deckManager.LoadedBullets.Count > 0)
        {
            while (GamePauseController.IsPaused)
            {
                yield return null;
            }

            BulletData bulletData = deckManager.LoadedBullets[0];

            if (bulletData == null)
            {
                break;
            }

            waveManager.GetEnemiesInDirection(
                transform.position,
                horizontalDirection,
                bulletData.MaxRange,
                targetBuffer);

            if (targetBuffer.Count == 0)
            {
                break;
            }

            BuildHitTargets(bulletData);
            Vector3 endPoint = hitBuffer[hitBuffer.Count - 1].transform.position;

            BulletLine bulletLine = Instantiate(
                bulletLinePrefab,
                firePoint.position,
                Quaternion.identity);

            if (!bulletLine.Initialize(bulletData, firePoint.position, endPoint))
            {
                Destroy(bulletLine.gameObject);
                break;
            }

            if (!deckManager.TryFireLoadedBullet(out BulletData firedBullet)
                || firedBullet != bulletData)
            {
                Destroy(bulletLine.gameObject);
                break;
            }

            firedAnyBullet = true;
            consumesTurn |= !bulletData.DoesNotConsumeTurn;

            foreach (EnemyController enemy in hitBuffer)
            {
                if (enemy != null)
                {
                    enemy.ApplyDamage(bulletData.Damage);
                }
            }

            GenerateRecoil(bulletData);

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
    }

    private void BuildHitTargets(BulletData bulletData)
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

    private bool RequiresTurnAfterFiring()
    {
        foreach (BulletData bulletData in deckManager.LoadedBullets)
        {
            if (bulletData != null && !bulletData.DoesNotConsumeTurn)
            {
                return true;
            }
        }

        return false;
    }

    private void GenerateRecoil(BulletData bulletData)
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
