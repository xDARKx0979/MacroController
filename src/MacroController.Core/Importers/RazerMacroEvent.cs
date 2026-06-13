using System.Globalization;
using MacroController.Core.Input;
using MacroController.Core.Macros;
using MacroController.Core.Native;

namespace MacroController.Core.Importers;

/// <summary>
/// A single Razer Synapse macro event, normalized from either the Synapse 2/3 XML export
/// format or the Synapse 4 JSON format - both use the same Type/Makecode/State/MouseButton/
/// Number vocabulary.
/// </summary>
internal readonly record struct RazerMacroEvent(
    string Type,
    string? Number,
    int? Makecode,
    int? KeyState,
    int? MouseButton,
    int? MouseState);

/// <summary>Converts normalized Razer macro events into MacroController <see cref="InputEvent"/> steps.</summary>
internal static class RazerMacroEventConverter
{
    public static (List<InputEvent> Steps, List<string> Problems) Convert(IEnumerable<RazerMacroEvent> events)
    {
        var steps = new List<InputEvent>();
        var problems = new List<string>();

        foreach (var evt in events)
        {
            switch (evt.Type)
            {
                case "actionBar":
                    // Metadata marker recorded alongside the macro, not an action.
                    break;

                case "0":
                    if (evt.Number is null || !double.TryParse(evt.Number, CultureInfo.InvariantCulture, out double seconds))
                    {
                        problems.Add($"Unreadable delay value '{evt.Number}'");
                        break;
                    }

                    steps.Add(new InputEvent(InputDevice.Keyboard, 0, ActionType.Delay, (int)Math.Round(seconds * 1000)));
                    break;

                case "1":
                    if (evt.Makecode is not int makecode || evt.KeyState is not int keyState)
                    {
                        problems.Add("Malformed keyboard event");
                        break;
                    }

                    int vkCode = ScanCodeToVirtualKey(makecode);
                    if (vkCode == 0)
                    {
                        problems.Add($"Unrecognized key (scan code {makecode})");
                        break;
                    }

                    steps.Add(new InputEvent(InputDevice.Keyboard, vkCode, keyState == 0 ? ActionType.KeyDown : ActionType.KeyUp, 0));
                    break;

                case "2":
                    if (evt.MouseButton is not int button || evt.MouseState is not int mouseState)
                    {
                        problems.Add("Malformed mouse event");
                        break;
                    }

                    if (button < 0 || button > (int)MouseButton.X2)
                    {
                        problems.Add($"Unsupported mouse button (index {button})");
                        break;
                    }

                    steps.Add(new InputEvent(InputDevice.Mouse, button, mouseState == 0 ? ActionType.MouseDown : ActionType.MouseUp, 0));
                    break;

                default:
                    problems.Add($"Unsupported macro event type '{evt.Type}'");
                    break;
            }
        }

        return (steps, problems);
    }

    /// <summary>Converts a PS/2 Set-1 "make code" (as recorded by Synapse) to a Win32 virtual-key code.</summary>
    public static int ScanCodeToVirtualKey(int makecode)
    {
        uint vk = NativeMethods.MapVirtualKey((uint)makecode, NativeMethods.MAPVK_VSC_TO_VK);
        if (vk == 0 && makecode is >= 0 and <= 0xFF)
            vk = NativeMethods.MapVirtualKey((uint)(0xE000 | makecode), NativeMethods.MAPVK_VSC_TO_VK_EX);

        return (int)vk;
    }
}
