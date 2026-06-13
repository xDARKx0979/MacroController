using System.Text.Json;
using System.Text.Json.Serialization;
using MacroController.Core.Macros;

namespace MacroController.Core.Storage;

public static class MacroStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Save(Macro macro, string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, JsonSerializer.Serialize(macro, Options));
    }

    public static Macro Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<Macro>(json, Options)
            ?? throw new InvalidDataException($"Could not deserialize macro from '{filePath}'.");
    }
}
