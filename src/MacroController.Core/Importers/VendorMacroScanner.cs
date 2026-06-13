using MacroController.Core.Storage;

namespace MacroController.Core.Importers;

/// <summary>Discovers and converts Razer macro files from their known on-disk locations, and recognizes Razer/Logitech files for manual import.</summary>
public static class VendorMacroScanner
{
    // Used for manual "File Import" - a user might have a Logitech Gaming Software
    // profile XML even though we don't auto-scan for them (see ScanAndImport).
    private static readonly IVendorMacroImporter[] Importers =
    [
        new RazerSynapseMacroImporter(),
        new LogitechMacroImporter(),
    ];

    // Used for "Razer Import" auto-discovery - scoped to Razer's known storage locations.
    private static readonly IVendorMacroImporter[] ScanImporters =
    [
        new RazerSynapseMacroImporter(),
        new RazerSynapse4MacroImporter(),
    ];

    /// <summary>File filter string for "Open" dialogs covering every format we can import.</summary>
    public const string OpenFileDialogFilter =
        "All supported macros (*.json;*.xml)|*.json;*.xml|" +
        "MacroController macros (*.json)|*.json|" +
        "Razer/Logitech macro exports (*.xml)|*.xml|" +
        "All files (*.*)|*.*";

    /// <summary>Returns the importer that recognizes <paramref name="filePath"/>, or null if none does.</summary>
    public static IVendorMacroImporter? FindImporter(string filePath) =>
        Importers.FirstOrDefault(importer => importer.CanParse(filePath));

    /// <summary>
    /// Scans every known Razer macro storage location, converts every macro found,
    /// saves the compatible ones into the library, and returns a result for each macro found.
    /// </summary>
    public static List<MacroImportResult> ScanAndImport()
    {
        var results = new List<MacroImportResult>();

        // Some sources (e.g. Synapse 4's LevelDB caches) store the same macro in multiple
        // files; SourceId carries a stable identifier (e.g. a GUID) so duplicates are only
        // imported/reported once.
        var seenIds = new HashSet<string>();

        foreach (string file in DiscoverCandidateFiles())
        {
            var importer = ScanImporters.FirstOrDefault(i => i.CanParse(file));
            if (importer is null)
                continue;

            foreach (var result in importer.Parse(file))
            {
                if (result.SourceId is not null && !seenIds.Add(result.SourceId))
                    continue;

                if (result.Status == MacroImportStatus.Imported && result.Macro is not null)
                    MacroLibraryStore.ImportConverted(result.Macro);

                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>Finds candidate macro files under every known Razer macro storage location.</summary>
    public static List<string> DiscoverCandidateFiles()
    {
        var files = new List<string>();

        foreach (string dir in GetCandidateDirectories())
        {
            try
            {
                files.AddRange(Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories));
            }
            catch
            {
                // Inaccessible or vanished mid-scan - skip.
            }
        }

        // Razer Synapse 4 caches its synced macro data as Chromium LevelDB files.
        foreach (string dir in GetSynapse4LeveldbDirectories())
        {
            try
            {
                files.AddRange(Directory.EnumerateFiles(dir, "*.log"));
                files.AddRange(Directory.EnumerateFiles(dir, "*.ldb"));
            }
            catch
            {
                // Inaccessible or vanished mid-scan - skip.
            }
        }

        return files;
    }

    private static List<string> GetCandidateDirectories()
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var dirs = new List<string>();

        void AddIfExists(string path)
        {
            if (Directory.Exists(path))
                dirs.Add(path);
        }

        // Razer Synapse 3 - exported macros and per-account macro storage.
        AddIfExists(Path.Combine(programData, "Razer", "Synapse3"));

        string razerAccounts = Path.Combine(programData, "Razer", "Razer Central", "Accounts");
        if (Directory.Exists(razerAccounts))
        {
            try
            {
                foreach (string account in Directory.EnumerateDirectories(razerAccounts))
                    AddIfExists(Path.Combine(account, "Emily3", "Macros"));
            }
            catch
            {
                // Ignore inaccessible account folders.
            }
        }

        // Razer Synapse 2 (legacy).
        AddIfExists(Path.Combine(appData, "Razer", "Synapse", "Macros"));
        AddIfExists(Path.Combine(localAppData, "Razer", "Synapse", "Macros"));

        return dirs;
    }

    /// <summary>
    /// Razer Synapse 4 ("RazerAppEngine") is an Electron app that syncs macros via
    /// remotestorage.io and caches them locally as Chromium LevelDB files.
    /// </summary>
    private static List<string> GetSynapse4LeveldbDirectories()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string defaultProfile = Path.Combine(localAppData, "Razer", "RazerAppEngine", "User Data", "Default");

        var dirs = new List<string>();

        void AddIfExists(string path)
        {
            if (Directory.Exists(path))
                dirs.Add(path);
        }

        AddIfExists(Path.Combine(defaultProfile, "IndexedDB", "https_apps.razer.com_0.indexeddb.leveldb"));
        AddIfExists(Path.Combine(defaultProfile, "Local Storage", "leveldb"));

        return dirs;
    }
}
