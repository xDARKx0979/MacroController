using System.Xml;
using System.Xml.Linq;
using MacroController.Core.Macros;

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

        var events = new List<RazerMacroEvent>();

        foreach (var evt in macroEvents)
        {
            string type = evt.Element("Type")?.Value ?? "";

            int? makecode = null, keyState = null, mouseButton = null, mouseState = null;

            var keyEvent = evt.Element("KeyEvent");
            if (keyEvent is not null)
            {
                if (int.TryParse(keyEvent.Element("Makecode")?.Value, out int mc)) makecode = mc;
                if (int.TryParse(keyEvent.Element("State")?.Value, out int ks)) keyState = ks;
            }

            var mouseEvent = evt.Element("MouseEvent");
            if (mouseEvent is not null)
            {
                if (int.TryParse(mouseEvent.Element("MouseButton")?.Value, out int mb)) mouseButton = mb;
                if (int.TryParse(mouseEvent.Element("State")?.Value, out int ms)) mouseState = ms;
            }

            events.Add(new RazerMacroEvent(type, evt.Element("Number")?.Value, makecode, keyState, mouseButton, mouseState));
        }

        var (steps, problems) = RazerMacroEventConverter.Convert(events);

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

    private static MacroImportResult Incompatible(string filePath, string name, string reason) => new()
    {
        SourceFile = filePath,
        SourceName = name,
        Vendor = "Razer",
        Status = MacroImportStatus.Incompatible,
        Reason = reason,
    };
}
