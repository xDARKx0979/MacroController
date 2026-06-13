using System.IO;
using System.Windows;
using MacroController.Core.Importers;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MacroController.App;

/// <summary>Shows the outcome of a Razer macro scan-and-import: what was found, imported, or skipped as incompatible.</summary>
public partial class VendorImportResultsWindow : Window
{
    public VendorImportResultsWindow(IReadOnlyList<MacroImportResult> results)
    {
        InitializeComponent();

        if (results.Count == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            ResultsList.Visibility = Visibility.Collapsed;
            return;
        }

        ResultsList.ItemsSource = results.Select(r => new VendorImportResultItem(r)).ToList();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}

// Excluded from Obfuscar's renaming - its properties are referenced by name
// from VendorImportResultsWindow.xaml's {Binding} expressions, which resolve via
// reflection at runtime and would silently break if renamed.
[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed class VendorImportResultItem
{
    private static readonly SolidColorBrush ImportedFill = new(Color.FromRgb(0x6F, 0xCF, 0x97));
    private static readonly SolidColorBrush IncompatibleFill = new(Color.FromRgb(0xE5, 0x73, 0x73));

    public VendorImportResultItem(MacroImportResult result)
    {
        Name = result.SourceName;
        Subtitle = $"{result.Vendor} - {Path.GetFileName(result.SourceFile)}";

        if (result.Status == MacroImportStatus.Imported)
        {
            StatusText = "Imported";
            StatusBrush = ImportedFill;
            Reason = "";
            ReasonVisibility = Visibility.Collapsed;
        }
        else
        {
            StatusText = "Incompatible";
            StatusBrush = IncompatibleFill;
            Reason = result.Reason ?? "";
            ReasonVisibility = Visibility.Visible;
        }
    }

    public string Name { get; }
    public string Subtitle { get; }
    public string StatusText { get; }
    public Brush StatusBrush { get; }
    public string Reason { get; }
    public Visibility ReasonVisibility { get; }
}
