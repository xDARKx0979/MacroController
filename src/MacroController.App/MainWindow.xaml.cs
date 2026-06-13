using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MacroController.Core.Bindings;
using MacroController.Core.Hooks;
using MacroController.Core.Importers;
using MacroController.Core.Input;
using MacroController.Core.Macros;
using MacroController.Core.Storage;
using MacroController.App.Update;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Trigger = MacroController.Core.Bindings.Trigger;

namespace MacroController.App;

public partial class MainWindow : Window
{
    private readonly KeyboardHook _keyboardHook = new();
    private readonly MouseHook _mouseHook = new();
    private readonly MacroRecorder _recorder = new();
    private readonly BindingManager _bindingManager = new();
    private Forms.NotifyIcon? _trayIcon;
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();

        StartupManager.SetEnabled(true);

        _bindingManager.SetBindings(LoadMacroShortcutBindings());
        RefreshList();

        WireHooks();
        InitializeTrayIcon();

        _keyboardHook.Install();
        _mouseHook.Install();

        _ = CheckForUpdatesAsync();
    }

    /// <summary>
    /// Silently checks the update manifest on startup and, if a newer version is
    /// available, downloads it and hands off to MacroController.Patcher.exe before exiting.
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        if (!await Updater.CheckForUpdateAsync())
            return;

        var progressWindow = new UpdateProgressWindow();
        progressWindow.Show();

        var progress = new Progress<double>(progressWindow.SetProgress);

        if (await Updater.DownloadAndLaunchPatcherAsync(progress))
        {
            // The patcher has its own window and takes over from here, so close
            // ours immediately rather than leaving two overlapping status popups.
            // Force a full shutdown (not just MainWindow.Close()) so the process
            // actually exits - otherwise the patcher's "wait for app to close"
            // times out, its file copies fail (still locked), and it relaunches
            // the same unpatched exe, which repeats the whole cycle.
            progressWindow.Close();
            _isExiting = true;
            Application.Current.Shutdown();
        }
        else
        {
            progressWindow.Close();
        }
    }

    private void InitializeTrayIcon()
    {
        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add("Show", null, (_, _) => ShowFromTray());
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "MacroController",
            ContextMenuStrip = contextMenu,
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>Builds global bindings from every saved macro that has a <see cref="Macro.Shortcut"/> assigned.</summary>
    private static List<Binding> LoadMacroShortcutBindings()
    {
        return MacroLibraryStore.LoadAll()
            .Where(entry => entry.Macro.Shortcut is not null)
            .Select(entry => new Binding(entry.Macro.Shortcut!.Value, new MacroAction(entry.Macro)))
            .ToList();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _keyboardHook.Dispose();
        _mouseHook.Dispose();
        _trayIcon?.Dispose();
        base.OnClosed(e);
    }

    private void WireHooks()
    {
        _keyboardHook.KeyDown += (_, e) =>
        {
            if (_bindingManager.HandleDown(new Trigger(InputDevice.Keyboard, e.VirtualKeyCode)))
            {
                e.Handled = true;
                return;
            }

            _recorder.RecordKey(e.VirtualKeyCode, ActionType.KeyDown);
        };

        _keyboardHook.KeyUp += (_, e) =>
        {
            if (_bindingManager.HandleUp(new Trigger(InputDevice.Keyboard, e.VirtualKeyCode)))
            {
                e.Handled = true;
                return;
            }

            _recorder.RecordKey(e.VirtualKeyCode, ActionType.KeyUp);
        };

        _mouseHook.MouseDown += (_, e) =>
        {
            if (_bindingManager.HandleDown(new Trigger(InputDevice.Mouse, (int)e.Button)))
            {
                e.Handled = true;
                return;
            }

            _recorder.RecordMouseButton(e.Button, ActionType.MouseDown);
        };

        _mouseHook.MouseUp += (_, e) =>
        {
            if (_bindingManager.HandleUp(new Trigger(InputDevice.Mouse, (int)e.Button)))
            {
                e.Handled = true;
                return;
            }

            _recorder.RecordMouseButton(e.Button, ActionType.MouseUp);
        };

        _mouseHook.MouseWheel += (_, e) => _recorder.RecordWheel(e.Delta, e.Horizontal);
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
        _bindingManager.SetBindings(LoadMacroShortcutBindings());
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
        _bindingManager.SetBindings(LoadMacroShortcutBindings());
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
        var button = (Button)sender;
        button.ContextMenu!.IsOpen = true;
    }

    private void FileImportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = VendorMacroScanner.OpenFileDialogFilter };
        if (dialog.ShowDialog(this) != true)
            return;

        if (string.Equals(Path.GetExtension(dialog.FileName), ".json", StringComparison.OrdinalIgnoreCase))
        {
            MacroLibraryStore.Import(dialog.FileName);
            RefreshList();
            _bindingManager.SetBindings(LoadMacroShortcutBindings());
            return;
        }

        var importer = VendorMacroScanner.FindImporter(dialog.FileName);
        if (importer is null)
        {
            MessageBox.Show(this, "This file isn't a recognized macro format.", "Import",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var results = importer.Parse(dialog.FileName);
        int imported = 0;
        var lines = new List<string>();

        foreach (var result in results)
        {
            if (result.Status == MacroImportStatus.Imported && result.Macro is not null)
            {
                MacroLibraryStore.ImportConverted(result.Macro);
                imported++;
                lines.Add($"✔ {result.SourceName} - imported");
            }
            else
            {
                lines.Add($"✖ {result.SourceName} - incompatible: {result.Reason}");
            }
        }

        RefreshList();
        _bindingManager.SetBindings(LoadMacroShortcutBindings());

        MessageBox.Show(this, string.Join("\n", lines), imported > 0 ? "Import complete" : "Import failed",
            MessageBoxButton.OK, imported > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void VendorImportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var results = VendorMacroScanner.ScanAndImport();

        var window = new VendorImportResultsWindow(results) { Owner = this };
        window.ShowDialog();

        RefreshList();
        _bindingManager.SetBindings(LoadMacroShortcutBindings());
    }
}

// Excluded from Obfuscar's renaming - its properties are referenced by name
// from MainWindow.xaml's {Binding} expressions, which resolve via reflection
// at runtime and would silently break if renamed.
[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
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
