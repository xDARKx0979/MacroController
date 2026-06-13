using System.Text;
using System.Text.Json;
using MacroController.Core.Macros;

namespace MacroController.Core.Importers;

/// <summary>
/// Extracts Razer Synapse 4 ("RazerAppEngine") macros from the Chromium LevelDB files that
/// cache the app's remotestorage.io-synced macro data. Synapse 4 stores each macro as a
/// JSON-escaped object (embedded as a string value inside the LevelDB record) using the same
/// Type/Makecode/State/MouseButton/Number event vocabulary as the Synapse 2/3 XML export
/// format, so events are converted via the shared <see cref="RazerMacroEventConverter"/>.
///
/// Rather than parse the LevelDB/Snappy container format, this scans the raw bytes of the
/// (small) *.log and *.ldb files for the escaped "macroEvents":[ ... ] marker and the
/// "guid"/"name" fields that precede it in the same JSON object.
/// </summary>
public sealed class RazerSynapse4MacroImporter : IVendorMacroImporter
{
    public string VendorName => "Razer";

    // These markers match the escaped bytes as they appear inside the LevelDB record,
    // e.g. ...\"macroEvents\":[{\"Type\":1,...},...] - each \" is the 2-byte sequence
    // backslash-quote.
    private const string EventsMarker = "\\\"macroEvents\\\":[";
    private const string NameMarker = "\\\"name\\\":\\\"";
    private const string GuidMarker = "\\\"guid\\\":\\\"";
    private const string EscapedQuote = "\\\"";

    public bool CanParse(string filePath)
    {
        string ext = Path.GetExtension(filePath);
        if (!string.Equals(ext, ".log", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".ldb", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            return ReadAsLatin1(filePath).Contains(EventsMarker, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<MacroImportResult> Parse(string filePath)
    {
        string content;
        try
        {
            content = ReadAsLatin1(filePath);
        }
        catch (Exception ex)
        {
            return [Incompatible(filePath, Path.GetFileNameWithoutExtension(filePath), null, $"Could not read file: {ex.Message}")];
        }

        // Keyed by GUID so later (newer) entries in this append-only WAL overwrite earlier ones.
        var byGuid = new Dictionary<string, MacroImportResult>();

        int cursor = 0;
        while (true)
        {
            int eventsIdx = content.IndexOf(EventsMarker, cursor, StringComparison.Ordinal);
            if (eventsIdx < 0)
                break;

            int arrayStart = eventsIdx + EventsMarker.Length;
            int arrayEnd = arrayStart <= content.Length ? content.IndexOf(']', arrayStart) : -1;
            if (arrayEnd < 0)
                break;

            string? guid = FindFollowingField(content, GuidMarker, cursor, eventsIdx, 36);
            string? name = FindFollowingField(content, NameMarker, cursor, eventsIdx, maxLength: null);

            cursor = arrayEnd + 1;

            if (guid is null || !Guid.TryParse(guid, out _))
                continue;

            string macroName = name is { Length: > 0 } ? name : guid;
            string eventsJson = "[" + content[arrayStart..arrayEnd].Replace(EscapedQuote, "\"") + "]";

            byGuid[guid] = ParseMacro(filePath, guid, macroName, eventsJson);
        }

        return byGuid.Values.ToList();
    }

    private static MacroImportResult ParseMacro(string filePath, string guid, string macroName, string eventsJson)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(eventsJson);
        }
        catch (JsonException ex)
        {
            return Incompatible(filePath, macroName, guid, $"Could not parse macro JSON: {ex.Message}");
        }

        using (doc)
        {
            var events = new List<RazerMacroEvent>();
            foreach (var el in doc.RootElement.EnumerateArray())
                events.Add(ReadEvent(el));

            var (steps, problems) = RazerMacroEventConverter.Convert(events);

            if (problems.Count > 0)
                return Incompatible(filePath, macroName, guid, string.Join("; ", problems.Distinct()));

            if (steps.Count == 0)
                return Incompatible(filePath, macroName, guid, "No supported actions found");

            var macro = new Macro { Name = macroName, Steps = steps };
            return new MacroImportResult
            {
                SourceFile = filePath,
                SourceName = macroName,
                Vendor = "Razer",
                Status = MacroImportStatus.Imported,
                Macro = macro,
                SourceId = guid,
            };
        }
    }

    private static RazerMacroEvent ReadEvent(JsonElement el)
    {
        string type = el.TryGetProperty("Type", out var typeEl)
            ? typeEl.ValueKind == JsonValueKind.String ? typeEl.GetString() ?? "" : typeEl.GetRawText()
            : "";

        string? number = el.TryGetProperty("Number", out var numberEl)
            ? numberEl.ValueKind == JsonValueKind.String ? numberEl.GetString() : numberEl.GetRawText()
            : null;

        int? makecode = null, keyState = null;
        if (el.TryGetProperty("KeyEvent", out var keyEvent) && keyEvent.ValueKind == JsonValueKind.Object)
        {
            makecode = GetInt(keyEvent, "Makecode");
            keyState = GetInt(keyEvent, "State");
        }

        int? mouseButton = null, mouseState = null;
        if (el.TryGetProperty("MouseEvent", out var mouseEvent) && mouseEvent.ValueKind == JsonValueKind.Object)
        {
            mouseButton = GetInt(mouseEvent, "MouseButton");
            mouseState = GetInt(mouseEvent, "State");
        }

        return new RazerMacroEvent(type, number, makecode, keyState, mouseButton, mouseState);
    }

    private static int? GetInt(JsonElement obj, string property) =>
        obj.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i)
            ? i
            : null;

    /// <summary>
    /// Looks for <paramref name="marker"/> somewhere in <c>content[searchFrom..before)</c> and, if found,
    /// returns the string value that follows it up to the next escaped quote (or up to
    /// <paramref name="maxLength"/> characters, whichever comes first).
    /// </summary>
    private static string? FindFollowingField(string content, string marker, int searchFrom, int before, int? maxLength)
    {
        if (before <= searchFrom)
            return null;

        int markerIdx = content.LastIndexOf(marker, before - 1, before - searchFrom, StringComparison.Ordinal);
        if (markerIdx < 0)
            return null;

        int valueStart = markerIdx + marker.Length;
        int valueEnd = content.IndexOf(EscapedQuote, valueStart, StringComparison.Ordinal);
        if (valueEnd < 0)
            return null;

        int length = valueEnd - valueStart;
        if (maxLength is int max && length > max)
            length = max;

        return content.Substring(valueStart, length);
    }

    private static string ReadAsLatin1(string filePath)
    {
        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.Latin1);
        return reader.ReadToEnd();
    }

    private static MacroImportResult Incompatible(string filePath, string name, string? guid, string reason) => new()
    {
        SourceFile = filePath,
        SourceName = name,
        Vendor = "Razer",
        Status = MacroImportStatus.Incompatible,
        Reason = reason,
        SourceId = guid,
    };
}
