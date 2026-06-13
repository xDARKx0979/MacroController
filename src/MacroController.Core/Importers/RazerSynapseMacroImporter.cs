using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using MacroController.Core.Input;
using MacroController.Core.Macros;
using MacroController.Core.Native;

namespace MacroController.Core.Importers;

/// <summary>
/// Converts Razer Synapse (2/3) macro export XML files - a root &lt;Macro&gt; element containing
/// &lt;MacroEvents&gt;&lt;MacroEvent&gt; entries for key presses, mouse clicks and delays.
/// </summary>
public sealed class RazerSynapseMacroImporter : IVendorMacroImporter
{
    public string VendorName => "Razer";

    public bool CanParse(string filePath)
    {
        try
        {
            using var reader = XmlReader.Create(filePath);
            return reader.MoveToContent() == XmlNodeType.Element && reader.LocalName == "Macro";
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<MacroImportResult> Parse(string filePath)
    {
        string fallbackName = Path.GetFileNameWithoutExtension(filePath);

        XDocument doc;
        try
        {
            doc = XDocument.Load(filePath);
        }
        catch (Exception ex)
        {
            return [Incompatible(filePath, fallbackName, $"Could not read XML: {ex.Message}")];
        }

        var root = doc.Root;
        if (root is null || root.Name.LocalName != "Macro")
            return [Incompatible(filePath, fallbackName, "Not a Razer Synapse macro file")];

        string name = root.Element("Name")?.Value.Trim() is { Length: > 0 } n ? n : fallbackName;

        var macroEvents = root.Element("MacroEvents")?.Elements("MacroEvent").ToList();
        if (macroEvents is null)
            return [Incompatible(filePath, name, "Missing <MacroEvents>")];

        var steps = new List<InputEvent>();
        var problems = new List<string>();

        foreach (var evt in macroEvents)
        {
            string? type = evt.Element("Type")?.Value;
            switch (type)
            {
                case "actionBar":
                    // Metadata marker recorded alongside the macro, not an action.
                    break;

                case "0":
                    {
                        string number = evt.Element("Number")?.Value ?? "0";
                        if (!double.TryParse(number, CultureInfo.InvariantCulture, out double seconds))
                        {
                            problems.Add($"Unreadable delay value '{number}'");
                            break;
                        }

                        steps.Add(new InputEvent(InputDevice.Keyboard, 0, ActionType.Delay, (int)Math.Round(seconds * 1000)));
                        break;
                    }

                case "1":
                    {
                        var keyEvent = evt.Element("KeyEvent");
                        if (!int.TryParse(keyEvent?.Element("Makecode")?.Value, out int makecode) ||
                            !int.TryParse(keyEvent?.Element("State")?.Value, out int state))
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

                        steps.Add(new InputEvent(InputDevice.Keyboard, vkCode, state == 0 ? ActionType.KeyDown : ActionType.KeyUp, 0));
                        break;
                    }

                case "2":
                    {
                        var mouseEvent = evt.Element("MouseEvent");
                        if (!int.TryParse(mouseEvent?.Element("MouseButton")?.Value, out int button) ||
                            !int.TryParse(mouseEvent?.Element("State")?.Value, out int state))
                        {
                            problems.Add("Malformed mouse event");
                            break;
                        }

                        if (button < 0 || button > (int)MouseButton.X2)
                        {
                            problems.Add($"Unsupported mouse button (index {button})");
                            break;
                        }

                        steps.Add(new InputEvent(InputDevice.Mouse, button, state == 0 ? ActionType.MouseDown : ActionType.MouseUp, 0));
                        break;
                    }

                default:
                    problems.Add($"Unsupported macro event type '{type}'");
                    break;
            }
        }

        if (problems.Count > 0)
            return [Incompatible(filePath, name, string.Join("; ", problems.Distinct()))];

        if (steps.Count == 0)
            return [Incompatible(filePath, name, "No supported actions found")];

        var macro = new Macro { Name = name, Steps = steps };
        return [new MacroImportResult
        {
            SourceFile = filePath,
            SourceName = name,
            Vendor = VendorName,
            Status = MacroImportStatus.Imported,
            Macro = macro,
        }];
    }

    /// <summary>Converts a PS/2 Set-1 "make code" (as recorded by Synapse) to a Win32 virtual-key code.</summary>
    private static int ScanCodeToVirtualKey(int makecode)
    {
        uint vk = NativeMethods.MapVirtualKey((uint)makecode, NativeMethods.MAPVK_VSC_TO_VK);
        if (vk == 0 && makecode is >= 0 and <= 0xFF)
            vk = NativeMethods.MapVirtualKey((uint)(0xE000 | makecode), NativeMethods.MAPVK_VSC_TO_VK_EX);

        return (int)vk;
    }

    private static MacroImportResult Incompatible(string filePath, string name, string reason) => new()
    {
        SourceFile = filePath,
        SourceName = name,
        Vendor = "Razer",
        Status = MacroImportStatus.Incompatible,
        Reason = reason,
    };
}
