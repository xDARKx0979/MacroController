using MacroController.Core.Macros;

namespace MacroController.Core.Storage;

/// <summary>Manages the collection of saved macros under the <c>macros/</c> directory.</summary>
public static class MacroLibraryStore
{
    public const string MacrosDirectory = "macros";

    /// <summary>Loads every saved macro, paired with the file it was loaded from.</summary>
    public static List<(string FilePath, Macro Macro)> LoadAll()
    {
        if (!Directory.Exists(MacrosDirectory))
            return new();

        return Directory.GetFiles(MacrosDirectory, "*.json")
            .Select(path => (path, MacroStore.Load(path)))
            .OrderBy(entry => entry.Item2.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Creates and saves a new empty macro with the given name, returning its file path.</summary>
    public static string CreateNew(string name)
    {
        Directory.CreateDirectory(MacrosDirectory);

        string path = Path.Combine(MacrosDirectory, $"{Guid.NewGuid():N}.json");
        MacroStore.Save(new Macro { Name = name }, path);
        return path;
    }
}
