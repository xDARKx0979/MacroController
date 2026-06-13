using System.IO;
using MacroController.Core.Hooks;
using MacroController.Core.Input;
using MacroController.Core.Storage;

namespace MacroController.App;

internal static class MacroStepDisplay
{
    /// <summary>A small glyph identifying the step's device/action type for the row's icon column.</summary>
    public static string Icon(InputEvent step) => step.Action switch
    {
        ActionType.KeyDown or ActionType.KeyUp => "⌨",
        ActionType.MouseDown or ActionType.MouseUp => "\U0001F5B1",
        ActionType.Wheel => "↕",
        ActionType.HWheel => "↔",
        ActionType.LoopStart or ActionType.LoopEnd => "🔁",
        ActionType.Delay => "⏱",
        ActionType.LaunchApp => "🚀",
        ActionType.RunCommand => "💻",
        ActionType.TypeText => "📝",
        ActionType.CallMacro => "▶",
        _ => "•",
    };

    /// <summary>Human-readable "what this step does", e.g. "A — Press" or "Left Mouse Button — Release".</summary>
    public static string Describe(InputEvent step) => step.Action switch
    {
        ActionType.KeyDown => $"{KeyNames.GetNameFromVirtualKey(step.Code)} — Press",
        ActionType.KeyUp => $"{KeyNames.GetNameFromVirtualKey(step.Code)} — Release",
        ActionType.MouseDown => $"{MouseButtonName(step.Code)} — Press",
        ActionType.MouseUp => $"{MouseButtonName(step.Code)} — Release",
        ActionType.Wheel => step.Code > 0 ? "Mouse Wheel — Up" : "Mouse Wheel — Down",
        ActionType.HWheel => step.Code > 0 ? "Mouse Wheel — Right" : "Mouse Wheel — Left",
        ActionType.LoopStart => $"Loop Start — repeat ×{Math.Max(1, step.Code)}",
        ActionType.LoopEnd => "Loop End",
        ActionType.Delay => "Wait",
        ActionType.LaunchApp => $"Launch {Path.GetFileName(step.Text)}" + (string.IsNullOrEmpty(step.Text2) ? "" : $" {step.Text2}"),
        ActionType.RunCommand => $"Run: {Truncate(step.Text)}",
        ActionType.TypeText => $"Type \"{Truncate(step.Text)}\"",
        ActionType.CallMacro => $"Play macro '{(step.Text is { } id ? MacroLibraryStore.FindById(id)?.Name : null) ?? "(missing)"}'",
        _ => step.Action.ToString(),
    };

    private static string Truncate(string? text, int maxLength = 40)
    {
        text = (text ?? "").Replace('\r', ' ').Replace('\n', ' ');
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    private static string MouseButtonName(int code) => (MouseButton)code switch
    {
        MouseButton.Left => "Left Mouse Button",
        MouseButton.Right => "Right Mouse Button",
        MouseButton.Middle => "Middle Mouse Button",
        MouseButton.X1 => "Mouse Button 4",
        MouseButton.X2 => "Mouse Button 5",
        _ => "Mouse Button",
    };
}
