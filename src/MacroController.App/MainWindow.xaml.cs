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
    private const int VK_F1 = 0x70;
    private const int VK_F2 = 0x71;
    private const int VK_F3 = 0x72;
    private const int VK_F4 = 0x73;
    private const int VK_B = 0x42;
    private const int VK_C = 0x43;
    private const int VK_N = 0x4E;
    private const string MacroFilePath = "macro.json";
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

        _trayIcon?.ShowBalloonTip(3000, "MacroController", "Update found - installing now...", Forms.ToolTipIcon.Info);
        await Task.Delay(1500);

        if (await Updater.DownloadAndLaunchPatcherAsync())
        {
            _isExiting = true;
            Close();
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

    /// <summary>Loads profiles.json if present, otherwise falls back to the built-in demo set.</summary>
    private List<Profile> LoadProfiles() => File.Exists(ProfilesFilePath) ? ProfileStore.Load(ProfilesFilePath) : BuildProfiles();

    /// <summary>
    /// Demo profile set: "Default" applies everywhere, "Notepad" overrides F1 while
    /// Notepad is focused - lets us see auto-switching happen live in the log.
    /// </summary>
    private List<Profile> BuildProfiles()
    {
        var defaultProfile = new Profile
        {
            Name = "Default",
            Bindings =
            {
                new Binding(new Trigger(InputDevice.Keyboard, VK_F1), new RemapAction(VK_B)),
                new Binding(new Trigger(InputDevice.Keyboard, VK_F3), new TextAction("Hello from MacroController!")),
                new Binding(new Trigger(InputDevice.Keyboard, VK_F4), new LaunchAppAction("notepad.exe")),
                new Binding(new Trigger(InputDevice.Mouse, (int)MouseButton.X1), new RemapAction(VK_C)),
            },
        };

        var notepadProfile = new Profile
        {
            Name = "Notepad",
            MatchProcessNames = { "notepad" },
            Bindings =
            {
                new Binding(new Trigger(InputDevice.Keyboard, VK_F1), new RemapAction(VK_N)),
            },
        };

        if (File.Exists(MacroFilePath))
        {
            var macro = MacroStore.Load(MacroFilePath);
            defaultProfile.Bindings.Add(new Binding(new Trigger(InputDevice.Keyboard, VK_F2), new MacroAction(macro)));
        }

        return new List<Profile> { defaultProfile, notepadProfile };
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
