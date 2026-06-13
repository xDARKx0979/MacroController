using System.Xml;
using System.Xml.Linq;
using MacroController.Core.Input;
using MacroController.Core.Macros;

namespace MacroController.Core.Importers;

/// <summary>
/// Converts Logitech Gaming Software / G HUB profile XML files. A profile can contain several
/// &lt;Macro&gt; definitions, each holding a sequence of &lt;Event delay="..."&gt; keystroke or
/// mouse-button steps (the "Cassandra" keystroke macro format).
/// </summary>
public sealed class LogitechMacroImporter : IVendorMacroImporter
{
    public string VendorName => "Logitech";

    public bool CanParse(string filePath)
    {
        try
        {
            using var reader = XmlReader.Create(filePath);
            if (reader.MoveToContent() != XmlNodeType.Element)
                return false;

            if (reader.LocalName is "Persistence" or "Profile" or "Macro")
                return true;

            return reader.NamespaceURI.Contains("logitech.com", StringComparison.OrdinalIgnoreCase);
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

        if (doc.Root is null)
            return [Incompatible(filePath, fallbackName, "Empty XML file")];

        var macroElements = doc.Root.DescendantsAndSelf()
            .Where(e => e.Name.LocalName == "Macro")
            .ToList();

        if (macroElements.Count == 0)
            return [Incompatible(filePath, fallbackName, "Not a recognized Logitech profile or macro file")];

        var results = new List<MacroImportResult>();
        int unnamedIndex = 1;

        foreach (var macroElement in macroElements)
        {
            string name = macroElement.Attribute("name")?.Value
                ?? macroElement.Attribute("Name")?.Value
                ?? macroElement.Element("Name")?.Value
                ?? $"Macro {unnamedIndex++}";

            results.Add(ConvertMacro(filePath, name, macroElement));
        }

        return results;
    }

    private static MacroImportResult ConvertMacro(string filePath, string name, XElement macroElement)
    {
        var events = macroElement.Descendants().Where(e => e.Name.LocalName == "Event").ToList();
        if (events.Count == 0)
        {
            if (macroElement.Descendants().Any(e => e.Name.LocalName == "Lua"))
                return Incompatible(filePath, name, "Uses a Lua script, which isn't supported");

            return Incompatible(filePath, name, "Doesn't use a supported keystroke/button sequence");
        }

        var steps = new List<InputEvent>();
        var problems = new List<string>();

        foreach (var evt in events)
        {
            int delayMs = int.TryParse(evt.Attribute("delay")?.Value ?? evt.Attribute("Delay")?.Value, out int d) ? d : 0;

            var action = evt.Elements().FirstOrDefault();
            if (action is null)
            {
                problems.Add("Empty macro event");
                continue;
            }

            switch (action.Name.LocalName)
            {
                case "KeyDown":
                case "KeyUp":
                    {
                        string? key = action.Attribute("key")?.Value ?? action.Attribute("Key")?.Value;
                        int? vk = LogitechKeyNames.ToVirtualKey(key);
                        if (vk is null)
                        {
                            problems.Add($"Unrecognized key '{key}'");
                            break;
                        }

                        var keyAction = action.Name.LocalName == "KeyDown" ? ActionType.KeyDown : ActionType.KeyUp;
                        steps.Add(new InputEvent(InputDevice.Keyboard, vk.Value, keyAction, delayMs));
                        break;
                    }

                case "MouseButtonDown":
                case "MouseButtonUp":
                case "MouseDown":
                case "MouseUp":
                    {
                        string? buttonText = action.Attribute("button")?.Value ?? action.Attribute("Button")?.Value;
                        if (!int.TryParse(buttonText, out int button) || button < 1 || button > 5)
                        {
                            problems.Add($"Unsupported mouse button '{buttonText}'");
                            break;
                        }

                        var mouseAction = action.Name.LocalName is "MouseButtonDown" or "MouseDown" ? ActionType.MouseDown : ActionType.MouseUp;
                        steps.Add(new InputEvent(InputDevice.Mouse, button - 1, mouseAction, delayMs));
                        break;
                    }

                default:
                    problems.Add($"Unsupported macro action '{action.Name.LocalName}'");
                    break;
            }
        }

        if (problems.Count > 0)
            return Incompatible(filePath, name, string.Join("; ", problems.Distinct()));

        if (steps.Count == 0)
            return Incompatible(filePath, name, "No supported actions found");

        var macro = new Macro { Name = name, Steps = steps };
        return new MacroImportResult
        {
            SourceFile = filePath,
            SourceName = name,
            Vendor = "Logitech",
            Status = MacroImportStatus.Imported,
            Macro = macro,
        };
    }

    private static MacroImportResult Incompatible(string filePath, string name, string reason) => new()
    {
        SourceFile = filePath,
        SourceName = name,
        Vendor = "Logitech",
        Status = MacroImportStatus.Incompatible,
        Reason = reason,
    };
}
