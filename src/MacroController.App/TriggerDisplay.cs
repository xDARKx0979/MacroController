using MacroController.Core.Bindings;
using MacroController.Core.Hooks;
using MacroController.Core.Input;

namespace MacroController.App;

/// <summary>Human-readable descriptions of triggers and binding actions for list/log display.</summary>
internal static class TriggerDisplay
{
    public static string Describe(Trigger trigger) => trigger.Device switch
    {
        InputDevice.Keyboard => KeyNames.GetNameFromVirtualKey(trigger.Code),
        InputDevice.Mouse => $"Mouse {(MouseButton)trigger.Code}",
        _ => $"{trigger.Device} {trigger.Code}",
    };

    public static string Describe(BindingAction action) => action switch
    {
        RemapAction remap => $"Remap to {KeyNames.GetNameFromVirtualKey(remap.TargetVirtualKeyCode)}",
        TextAction text => $"Type \"{text.Text}\"",
        LaunchAppAction launch => $"Launch {launch.Path}",
        MacroAction macro => $"Play macro '{macro.Macro.Name}' ({macro.Macro.Steps.Count} steps)",
        _ => action.ToString() ?? "",
    };
}
