using MacroController.Core.Macros;

namespace MacroController.Core.Importers;

/// <summary>Outcome of attempting to convert a third-party macro into our format.</summary>
public enum MacroImportStatus
{
    /// <summary>Every step converted cleanly; <see cref="MacroImportResult.Macro"/> is ready to save.</summary>
    Imported,

    /// <summary>The file (or one macro within it) couldn't be fully converted; see <see cref="MacroImportResult.Reason"/>.</summary>
    Incompatible,
}

/// <summary>One macro found while scanning or importing a third-party file, and what happened to it.</summary>
public sealed class MacroImportResult
{
    public required string SourceFile { get; init; }

    /// <summary>The macro's name as found in the source file (or a fallback derived from the file name).</summary>
    public required string SourceName { get; init; }

    /// <summary>Display name of the vendor/format this file came from (e.g. "Razer", "Logitech").</summary>
    public required string Vendor { get; init; }

    public required MacroImportStatus Status { get; init; }

    /// <summary>Human-readable explanation when <see cref="Status"/> is <see cref="MacroImportStatus.Incompatible"/>.</summary>
    public string? Reason { get; init; }

    /// <summary>The converted macro, present only when <see cref="Status"/> is <see cref="MacroImportStatus.Imported"/>.</summary>
    public Macro? Macro { get; init; }

    /// <summary>
    /// Stable identifier (e.g. a GUID) for the source macro, used to de-duplicate the same
    /// macro found across multiple source files. Null when the format has no such identifier.
    /// </summary>
    public string? SourceId { get; init; }
}
