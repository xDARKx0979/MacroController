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
        var macro = new Macro { Name = name };
        return SaveAsNew(macro);
    }

    /// <summary>Finds a saved macro by its <see cref="Macro.Id"/>, or null if none matches.</summary>
    public static Macro? FindById(string id)
    {
        foreach (var (_, macro) in LoadAll())
            if (macro.Id == id)
                return macro;

        return null;
    }

    /// <summary>Saves a copy of the macro at <paramref name="filePath"/> as a new library entry named "(copy)", returning its file path.</summary>
    public static string Duplicate(string filePath)
    {
        var macro = MacroStore.Load(filePath);
        macro.Id = Guid.NewGuid().ToString("N");
        macro.Name = $"{macro.Name} (copy)";
        return SaveAsNew(macro);
    }

    /// <summary>Copies a saved macro's JSON file to <paramref name="destinationPath"/> for sharing/backup.</summary>
    public static void Export(string filePath, string destinationPath) => File.Copy(filePath, destinationPath, overwrite: true);

    /// <summary>Loads a macro JSON file from elsewhere and adds it to the library with a fresh <see cref="Macro.Id"/>, returning its new file path.</summary>
    public static string Import(string sourcePath)
    {
        var macro = MacroStore.Load(sourcePath);
        macro.Id = Guid.NewGuid().ToString("N");
        return SaveAsNew(macro);
    }

    private static string SaveAsNew(Macro macro)
    {
        Directory.CreateDirectory(MacrosDirectory);

        string path = Path.Combine(MacrosDirectory, $"{macro.Id}.json");
        MacroStore.Save(macro, path);
        return path;
    }
}
