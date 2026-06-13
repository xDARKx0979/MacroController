using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MacroController.Core.Bindings;
using MacroController.Core.Hooks;
using MacroController.Core.Input;
using MacroController.Core.Macros;
using MacroController.Core.Profiles;
using MacroController.Core.Storage;
using MacroController.App.Update;
using Forms = System.Windows.Forms;
using Trigger = MacroController.Core.Bindings.Trigger;

namespace MacroController.App;

public partial class MainWindow : Window
{
    private const string ProfilesFilePath = "profiles.json";

    private readonly KeyboardHook _keyboardHook = new();
    private readonly MouseHook _mouseHook = new();
    private readonly MacroRecorder _recorder = new();
    private readonly BindingManager _bindingManager = new();
    private readonly ProfileManager _profileManager;
    private readonly DispatcherTimer _profileTimer;
    private Forms.NotifyIcon? _trayIcon;
    private bool _isExiting;
    private bool _suppressStartupEvent;

    public MainWindow()
    {
        InitializeComponent();

        _profileManager = new ProfileManager(_bindingManager, LoadProfiles(), LoadMacroShortcutBindings());
        _profileManager.ActiveProfileChanged += (_, profile) => ActiveProfileText.Text = profile.Name;
        ActiveProfileText.Text = _profileManager.ActiveProfile.Name;

        _suppressStartupEvent = true;
        StartWithWindowsCheckBox.IsChecked = StartupManager.IsEnabled();
        _suppressStartupEvent = false;

        WireHooks();
        InitializeTrayIcon();

        // Polling interval per PLAN.md's suggested 250-500ms range for foreground-app checks.
        _profileTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _profileTimer.Tick += (_, _) => _profileManager.Poll();
        _profileTimer.Start();

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
            progressWindow.SetStatus("Installing update...");
            progressWindow.SetIndeterminate();
            await Task.Delay(500);

            _isExiting = true;
            Close();
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
        contextMenu.Items.Add("Reload Profiles", null, (_, _) => ReloadProfiles());
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

    private void ReloadProfiles()
    {
        _profileManager.ReplaceProfiles(LoadProfiles());
        _profileManager.SetGlobalBindings(LoadMacroShortcutBindings());
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
        Close();
    }

    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressStartupEvent)
            return;

        StartupManager.SetEnabled(StartWithWindowsCheckBox.IsChecked == true);
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
        _profileTimer.Stop();
        _keyboardHook.Dispose();
        _mouseHook.Dispose();
        _trayIcon?.Dispose();
        base.OnClosed(e);
    }

    /// <summary>Loads profiles.json if present, otherwise falls back to a single empty "Default" profile.</summary>
    private List<Profile> LoadProfiles() => File.Exists(ProfilesFilePath) ? ProfileStore.Load(ProfilesFilePath) : new List<Profile> { new() };

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

    private void ProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProfilesWindow(_profileManager.Profiles) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        _profileManager.ReplaceProfiles(dialog.Profiles);
    }

    private void MacrosButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new MacrosWindow(_recorder) { Owner = this };
        dialog.ShowDialog();
        _profileManager.SetGlobalBindings(LoadMacroShortcutBindings());
    }
}
