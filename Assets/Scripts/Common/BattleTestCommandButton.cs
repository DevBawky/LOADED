using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>Serialized GUI binding to the same validated commands as the keyboard console.</summary>
public sealed class BattleTestCommandButton : MonoBehaviour
{
    [SerializeField] private BattleTestConsole console;
    [SerializeField] private BattleTestController controller;
    [SerializeField] private UnityEngine.UI.Button button;
    [SerializeField] private string command;
    [SerializeField] private TMP_InputField[] arguments = System.Array.Empty<TMP_InputField>();
    [SerializeField] private bool closeAfterSuccess;

    private void OnEnable() => button.onClick.AddListener(Execute);
    private void OnDisable() => button.onClick.RemoveListener(Execute);

    private void Execute()
    {
        if (command == "close") { console.SetOpen(false); return; }
        string resolved = command;
        if (command == "ai toggle") resolved = "ai " + (controller.AutomaticTurns ? "off" : "on");
        else if (command == "god toggle") resolved = "god " + (BattleTestContext.Invulnerable ? "off" : "on");
        else if (command == "refill toggle") resolved = "refill " + (controller.EnemyAutoRefill ? "off" : "on");
        else if (arguments.Length > 0)
            resolved = string.Format(CultureInfo.InvariantCulture, command,
                arguments.Select(value => (object)value.text).ToArray());
        string result = console.RunCommand(resolved);
        if (closeAfterSuccess && !result.StartsWith(BattleTestCommandRouter.RejectedPrefix))
            console.SetOpen(false);
    }
}
