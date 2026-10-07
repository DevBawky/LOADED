using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-40)]
public sealed class BattleTestConsole : MonoBehaviour
{
    [SerializeField] private BattleTestController controller;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_InputField input;
    [SerializeField] private TMP_Text output;
    [SerializeField] private TMP_Text summary;
    [SerializeField] private TMP_Text hint;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private GameObject commandPanel;
    [SerializeField] private BattleTestGui gui;
    public event Action<string> CommandExecuted;
    private readonly List<string> lines = new List<string>();
    private readonly List<string> history = new List<string>();
    private int historyIndex;
    private int pageOffset;
    private const int PageLines = 22;
    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (!BattleTestContext.IsActive) { enabled = false; return; }
        panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (input != null) input.onSubmit.AddListener(Submit);
    }
    private void OnDisable()
    {
        if (input != null) input.onSubmit.RemoveListener(Submit);
        BattleTestContext.ConsoleOpen = false;
        if (panel != null) panel.SetActive(false);
    }

    private void Start()
    {
        Append("전투 테스트 · 버튼으로 설정하거나 명령을 입력할 수 있습니다.");
        Append("F1 설정 창 · F2 적 자동/수동 · F3 한 사이클 · F4 무적 · F5 복원 · Ctrl+F5 저장");
        Append("F7 무작위 적 · F8 적 정리 · F9 체력 회복");
        resultText.text = "도감 → 보유 목록으로 드래그 · 우클릭 조작 · 올려두면 설명 · 숫자에 휠로 증감";
        SetOpen(true);
    }

    public void SetOpen(bool open)
    {
        gui.CancelPointer();
        panel.SetActive(open);
        BattleTestContext.ConsoleOpen = open;
        if (open)
        {
            gui.RequestRefresh();
            if (commandPanel.activeSelf) input.ActivateInputField();
        }
        else
        {
            input.DeactivateInputField();
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        }
    }

    private void Update()
    {
        if (!controller.IsReady) return;
        summary.text = controller.Summary;
        hint.text = "전투 테스트  |  F1 설정 창  ·  F2 자동/수동  ·  F3 한 사이클  ·  F4 무적  ·  F5 복원  ·  Ctrl+F5 저장";
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.f1Key.wasPressedThisFrame) { SetOpen(!IsOpen); return; }
        if (keyboard.f2Key.wasPressedThisFrame) Run("ai " + (controller.AutomaticTurns ? "off" : "on"));
        else if (keyboard.f3Key.wasPressedThisFrame) { Run("step"); SetOpen(false); }
        else if (keyboard.f4Key.wasPressedThisFrame) Run("god " + (BattleTestContext.Invulnerable ? "off" : "on"));
        else if (keyboard.f5Key.wasPressedThisFrame) Run(keyboard.ctrlKey.isPressed ? "checkpoint" : "reset");
        else if (keyboard.f7Key.wasPressedThisFrame) Run("random 1");
        else if (keyboard.f8Key.wasPressedThisFrame) Run("clear");
        else if (keyboard.f9Key.wasPressedThisFrame) Run("hp 100 100");
        if (!IsOpen || !commandPanel.activeSelf) return;
        if (keyboard.pageUpKey.wasPressedThisFrame)
        {
            pageOffset = Mathf.Min(Mathf.Max(0, lines.Count - PageLines), pageOffset + PageLines);
            Render();
        }
        if (keyboard.pageDownKey.wasPressedThisFrame) { pageOffset = Mathf.Max(0, pageOffset - PageLines); Render(); }
        if (input.isFocused && history.Count > 0 && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame))
        {
            historyIndex = Mathf.Clamp(historyIndex + (keyboard.upArrowKey.wasPressedThisFrame ? -1 : 1), 0, history.Count);
            input.SetTextWithoutNotify(historyIndex == history.Count ? string.Empty : history[historyIndex]);
            input.caretPosition = input.text.Length;
        }
    }

    private void Submit(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        history.Add(command);
        if (history.Count > 100) history.RemoveAt(0);
        historyIndex = history.Count;
        input.SetTextWithoutNotify(string.Empty);
        if (string.Equals(command.Trim(), "close", StringComparison.OrdinalIgnoreCase) || command.Trim() == "닫기") SetOpen(false);
        else { Run(command); input.ActivateInputField(); }
    }

    public void ShowCommands(bool show)
    {
        commandPanel.SetActive(show);
        if (show) input.ActivateInputField();
        else input.DeactivateInputField();
    }

    private void Run(string command) => RunCommand(command);

    public string RunCommand(string command)
    {
        Append("> " + command);
        string result = controller.ExecuteCommand(command);
        Append(result);
        resultText.text = result.Split('\n')[0];
        resultText.color = result.StartsWith(BattleTestCommandRouter.RejectedPrefix)
            ? new Color(1f, 0.55f, 0.5f) : new Color(0.65f, 1f, 0.86f);
        CommandExecuted?.Invoke(result);
        return result;
    }

    private void Append(string message)
    {
        lines.AddRange((message ?? string.Empty).Split('\n'));
        if (lines.Count > 500) lines.RemoveRange(0, lines.Count - 500);
        pageOffset = 0;
        Render();
    }

    private void Render()
    {
        int start = Mathf.Max(0, lines.Count - PageLines - pageOffset);
        output.text = string.Join("\n", lines.Skip(start).Take(PageLines));
    }
}
