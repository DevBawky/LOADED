using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum PlayerBehaviourAction
{
    MoveLeft = 0,
    MoveRight = 1,
    Rotate = 2,
    Wait = 3,
    Reload = 4,
    Shoot = 5
}

[DisallowMultipleComponent]
public sealed class BehaviourTileActionUI : MonoBehaviour
{
    private const string PanelName = "Panel | Behaviour Tile";
    private const string MoveLeftButtonName = "Button | Move";
    private const string MoveRightButtonName = "Button | Move (1)";
    private const string SceneMoveLeftButtonName = "Button | Move L";
    private const string SceneMoveRightButtonName = "Button | Move R";
    private const string RotateButtonName = "Button | Rotate";
    private const string WaitButtonName = "Button | Wait";
    private const string ReloadButtonName = "Button | Reload";
    private const string ShootButtonName = "Button | Shoot";

    [Header("Action Feedback")]
    [Min(1f)]
    [SerializeField] private float punchScale = 1.08f;
    [Min(0.01f)]
    [SerializeField] private float punchDuration = 0.16f;

    private readonly Dictionary<Button, UnityAction> clickActions =
        new Dictionary<Button, UnityAction>();
    private readonly Dictionary<RectTransform, Coroutine> punchRoutines =
        new Dictionary<RectTransform, Coroutine>();
    private readonly Dictionary<RectTransform, Vector3> restScales =
        new Dictionary<RectTransform, Vector3>();
    private readonly Dictionary<PlayerBehaviourAction, Button> actionButtons =
        new Dictionary<PlayerBehaviourAction, Button>();

    private PlayerMove playerMove;
    private PlayerShoot playerShoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallInLoadedScene()
    {
        InstallIfNeeded();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallIfNeeded();
    }

    private static void InstallIfNeeded()
    {
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (candidate.name == PanelName
                && candidate.GetComponent<BehaviourTileActionUI>() == null)
            {
                candidate.gameObject.AddComponent<BehaviourTileActionUI>();
            }
        }
    }

    private void Awake()
    {
        ResolvePlayerReferences();
    }

    private void OnEnable()
    {
        ResolvePlayerReferences();
        BindButtons();
        SubscribeToActions();
    }

    private void OnDisable()
    {
        UnsubscribeFromActions();
        UnbindButtons();
        StopAllPunches();
    }

    private void OnValidate()
    {
        punchScale = Mathf.Max(1f, punchScale);
        punchDuration = Mathf.Max(0.01f, punchDuration);
    }

    private void ResolvePlayerReferences()
    {
        playerMove ??= FindFirstObjectByType<PlayerMove>(
            FindObjectsInactive.Include);
        playerShoot ??= FindFirstObjectByType<PlayerShoot>(
            FindObjectsInactive.Include);
    }

    private void BindButtons()
    {
        UnbindButtons();

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (!TryGetAction(
                    button.name,
                    out PlayerBehaviourAction actionType,
                    out Action action))
            {
                continue;
            }

            if (action == null)
            {
                continue;
            }

            UnityAction clickAction = () => action.Invoke();
            clickActions.Add(button, clickAction);
            button.onClick.AddListener(clickAction);
            actionButtons[actionType] = button;

            RectTransform rect = button.transform as RectTransform;

            if (rect != null)
            {
                restScales[rect] = rect.localScale;
            }
        }
    }

    private void UnbindButtons()
    {
        foreach (KeyValuePair<Button, UnityAction> binding in clickActions)
        {
            if (binding.Key != null)
            {
                binding.Key.onClick.RemoveListener(binding.Value);
            }
        }

        clickActions.Clear();
        actionButtons.Clear();
    }

    private bool TryGetAction(
        string buttonName,
        out PlayerBehaviourAction actionType,
        out Action action)
    {
        actionType = default;
        action = null;

        switch (buttonName)
        {
            case MoveLeftButtonName:
            case SceneMoveLeftButtonName:
                actionType = PlayerBehaviourAction.MoveLeft;
                if (playerMove == null) return false;
                action = playerMove.MoveLeft;
                return true;
            case MoveRightButtonName:
            case SceneMoveRightButtonName:
                actionType = PlayerBehaviourAction.MoveRight;
                if (playerMove == null) return false;
                action = playerMove.MoveRight;
                return true;
            case RotateButtonName:
                actionType = PlayerBehaviourAction.Rotate;
                if (playerMove == null) return false;
                action = playerMove.Rotate;
                return true;
            case WaitButtonName:
                actionType = PlayerBehaviourAction.Wait;
                if (playerMove == null) return false;
                action = playerMove.Wait;
                return true;
            case ReloadButtonName:
                actionType = PlayerBehaviourAction.Reload;
                if (playerShoot == null) return false;
                action = playerShoot.Reload;
                return true;
            case ShootButtonName:
                actionType = PlayerBehaviourAction.Shoot;
                if (playerShoot == null) return false;
                action = playerShoot.Shoot;
                return true;
            default:
                return false;
        }
    }

    private void SubscribeToActions()
    {
        if (playerMove != null)
        {
            playerMove.BehaviourActionStarted += HandleActionStarted;
        }

        if (playerShoot != null)
        {
            playerShoot.BehaviourActionStarted += HandleActionStarted;
        }
    }

    private void UnsubscribeFromActions()
    {
        if (playerMove != null)
        {
            playerMove.BehaviourActionStarted -= HandleActionStarted;
        }

        if (playerShoot != null)
        {
            playerShoot.BehaviourActionStarted -= HandleActionStarted;
        }
    }

    private void HandleActionStarted(PlayerBehaviourAction actionType)
    {
        if (actionButtons.TryGetValue(actionType, out Button button)
            && button != null)
        {
            PlayPunch(button.transform as RectTransform);
        }
    }

    private void PlayPunch(RectTransform target)
    {
        if (target == null)
        {
            return;
        }

        if (!restScales.TryGetValue(target, out Vector3 restScale))
        {
            restScale = target.localScale;
            restScales[target] = restScale;
        }

        if (punchRoutines.TryGetValue(target, out Coroutine routine)
            && routine != null)
        {
            StopCoroutine(routine);
            target.localScale = restScale;
        }

        punchRoutines[target] = StartCoroutine(PunchRoutine(
            target,
            restScale));
    }

    private IEnumerator PunchRoutine(
        RectTransform target,
        Vector3 restScale)
    {
        float elapsed = 0f;

        while (elapsed < punchDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / punchDuration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            target.localScale = restScale
                * Mathf.Lerp(1f, punchScale, pulse);
        }

        target.localScale = restScale;
        punchRoutines.Remove(target);
    }

    private void StopAllPunches()
    {
        foreach (Coroutine routine in punchRoutines.Values)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        punchRoutines.Clear();

        foreach (KeyValuePair<RectTransform, Vector3> entry in restScales)
        {
            if (entry.Key != null)
            {
                entry.Key.localScale = entry.Value;
            }
        }
    }
}
