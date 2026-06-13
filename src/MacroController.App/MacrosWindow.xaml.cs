using System.IO;
using System.Windows;
using Microsoft.Win32;
using MacroController.Core.Macros;
using MacroController.Core.Storage;

namespace MacroController.App;

/// <summary>Lists every saved macro and lets the user create or edit them.</summary>
public partial class MacrosWindow : Window
{
    private readonly MacroRecorder _recorder;

    public MacrosWindow(MacroRecorder recorder)
    {
        InitializeComponent();
        _recorder = recorder;
        RefreshList();
    }

    private void RefreshList()
    {
        MacroList.ItemsSource = MacroLibraryStore.LoadAll()
            .Select(entry => new MacroListItem(entry.FilePath, entry.Macro.Name, entry.Macro.Steps.Count))
            .ToList();
    }

    private void NewMacroButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("Make Macro", "Macro name:", "New Macro") { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value))
            return;

        string path = MacroLibraryStore.CreateNew(dialog.Value.Trim());
        OpenEditor(path);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MacroListItem item)
            OpenEditor(item.FilePath);
    }

    private void OpenEditor(string path)
    {
        var macro = MacroStore.Load(path);
        var editor = new MacroEditorWindow(macro, path, _recorder) { Owner = this };
        editor.ShowDialog();
        RefreshList();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not MacroListItem item)
            return;

        var result = MessageBox.Show(this, $"Delete macro '{item.Name}'?", "Delete Macro",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
            return;

        File.Delete(item.FilePath);
        RefreshList();
    }

    private void DuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not MacroListItem item)
            return;

        MacroLibraryStore.Duplicate(item.FilePath);
        RefreshList();
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not MacroListItem item)
            return;

        string safeName = string.Concat(item.Name.Split(Path.GetInvalidFileNameChars()));
        var dialog = new SaveFileDialog
        {
            Filter = "Macro files (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"{safeName}.json",
        };
        if (dialog.ShowDialog(this) != true)
            return;

        MacroLibraryStore.Export(item.FilePath, dialog.FileName);
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Macro files (*.json)|*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true)
            return;

        MacroLibraryStore.Import(dialog.FileName);
        RefreshList();
    }
}

internal sealed class MacroListItem
{
    public MacroListItem(string filePath, string name, int stepCount)
    {
        FilePath = filePath;
        Name = name;
        Subtitle = $"{stepCount} step{(stepCount == 1 ? "" : "s")}";
    }

    public string FilePath { get; }
    public string Name { get; }
    public string Subtitle { get; }
}
