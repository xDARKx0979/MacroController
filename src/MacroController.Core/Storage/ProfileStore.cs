using System.Text.Json;
using System.Text.Json.Serialization;
using MacroController.Core.Profiles;

namespace MacroController.Core.Storage;

public static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Save(IEnumerable<Profile> profiles, string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, JsonSerializer.Serialize(profiles.ToList(), Options));
    }

    public static List<Profile> Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<Profile>>(json, Options)
            ?? throw new InvalidDataException($"Could not deserialize profiles from '{filePath}'.");
    }
}
