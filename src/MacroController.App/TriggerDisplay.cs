using MacroController.Core.Bindings;
using MacroController.Core.Hooks;
using MacroController.Core.Input;

namespace MacroController.App;

/// <summary>Human-readable descriptions of triggers for list/log display.</summary>
internal static class TriggerDisplay
{
    public static string Describe(Trigger trigger) => trigger.Device switch
    {
        InputDevice.Keyboard => KeyNames.GetNameFromVirtualKey(trigger.Code),
        InputDevice.Mouse => $"Mouse {(MouseButton)trigger.Code}",
        _ => $"{trigger.Device} {trigger.Code}",
    };
}
