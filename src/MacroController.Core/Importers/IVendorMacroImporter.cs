namespace MacroController.Core.Importers;

/// <summary>Converts a third-party (Razer/Logitech) macro file into our <see cref="Macros.Macro"/> format.</summary>
public interface IVendorMacroImporter
{
    /// <summary>Display name of the vendor/format this importer handles (e.g. "Razer", "Logitech").</summary>
    string VendorName { get; }

    /// <summary>Cheaply checks whether <paramref name="filePath"/> looks like this vendor's macro format, without fully parsing it.</summary>
    bool CanParse(string filePath);

    /// <summary>
    /// Parses <paramref name="filePath"/>, converting every macro it contains. A single file may
    /// contain multiple macros (e.g. a Logitech profile), so this returns one result per macro found.
    /// Never throws - parse failures are reported as <see cref="MacroImportStatus.Incompatible"/> results.
    /// </summary>
    IReadOnlyList<MacroImportResult> Parse(string filePath);
}
