using System.Globalization;
using MacroController.Core.Input;
using MacroController.Core.Macros;

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

                    // Despite the name, Synapse records this as a Windows virtual-key code
                    // (e.g. 53 = VK_5 = "5", 32 = VK_SPACE = "Space"), not a PS/2 scan code.
                    if (makecode is <= 0 or > 0xFF)
                    {
                        problems.Add($"Unrecognized key (code {makecode})");
                        break;
                    }

                    steps.Add(new InputEvent(InputDevice.Keyboard, makecode, keyState == 0 ? ActionType.KeyDown : ActionType.KeyUp, 0));
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
}
