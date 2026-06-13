using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MacroController.Core.Bindings;
using MacroController.Core.Hooks;
using MacroController.Core.Macros;
using MacroController.Core.Storage;
using Trigger = MacroController.Core.Bindings.Trigger;

namespace MacroController.App;

/// <summary>Modal dialog for creating or editing a single <see cref="Binding"/>.</summary>
public partial class BindingEditWindow : Window
{
    private Trigger? _trigger;
    private int? _remapTarget;
    private Macro? _macro;

    /// <summary>The edited binding, set when the dialog closes with <c>DialogResult == true</c>.</summary>
    public Binding? Result { get; private set; }

    public BindingEditWindow(Binding? existing = null)
    {
        InitializeComponent();

        if (existing is not null)
        {
            _trigger = existing.Trigger;
            TriggerText.Text = TriggerDisplay.Describe(existing.Trigger);

            switch (existing.Action)
            {
                case RemapAction remap:
                    ActionTypeCombo.SelectedIndex = 0;
                    _remapTarget = remap.TargetVirtualKeyCode;
                    RemapTargetText.Text = KeyNames.GetNameFromVirtualKey(remap.TargetVirtualKeyCode);
                    break;
                case TextAction text:
                    ActionTypeCombo.SelectedIndex = 1;
                    TextActionInput.Text = text.Text;
                    break;
                case LaunchAppAction launch:
                    ActionTypeCombo.SelectedIndex = 2;
                    LaunchPathInput.Text = launch.Path;
                    LaunchArgsInput.Text = launch.Arguments ?? "";
                    break;
                case MacroAction macro:
                    ActionTypeCombo.SelectedIndex = 3;
                    _macro = macro.Macro;
                    MacroPathInput.Text = $"{macro.Macro.Name} ({macro.Macro.Steps.Count} steps)";
                    break;
            }
        }
        else
        {
            ActionTypeCombo.SelectedIndex = 0;
        }

        UpdateActionPanel();
    }

    private async void CaptureTriggerButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureTriggerButton.IsEnabled = false;
        CaptureTriggerButton.Content = "Press a key or button...";

        using var capture = new TriggerCapture();
        var trigger = await capture.Result;

        CaptureTriggerButton.IsEnabled = true;
        CaptureTriggerButton.Content = "Capture...";

        if (trigger is { } t)
        {
            _trigger = t;
            TriggerText.Text = TriggerDisplay.Describe(t);
        }
    }

    private async void CaptureTargetButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureTargetButton.IsEnabled = false;
        CaptureTargetButton.Content = "Press a key...";

        using var capture = new KeyCapture();
        var vk = await capture.Result;

        CaptureTargetButton.IsEnabled = true;
        CaptureTargetButton.Content = "Capture...";

        if (vk is { } code)
        {
            _remapTarget = code;
            RemapTargetText.Text = KeyNames.GetNameFromVirtualKey(code);
        }
    }

    private void ActionTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActionPanel();

    private void UpdateActionPanel()
    {
        RemapPanel.Visibility = Visibility.Collapsed;
        TextPanel.Visibility = Visibility.Collapsed;
        LaunchPanel.Visibility = Visibility.Collapsed;
        MacroPanel.Visibility = Visibility.Collapsed;

        switch (ActionTypeCombo.SelectedIndex)
        {
            case 0: RemapPanel.Visibility = Visibility.Visible; break;
            case 1: TextPanel.Visibility = Visibility.Visible; break;
            case 2: LaunchPanel.Visibility = Visibility.Visible; break;
            case 3: MacroPanel.Visibility = Visibility.Visible; break;
        }
    }

    private void BrowseLaunchPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true)
            LaunchPathInput.Text = dialog.FileName;
    }

    private void BrowseMacroPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Macro files (*.json)|*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true)
            return;

        _macro = MacroStore.Load(dialog.FileName);
        MacroPathInput.Text = $"{_macro.Name} ({_macro.Steps.Count} steps)";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_trigger is not { } trigger)
        {
            MessageBox.Show(this, "Capture a trigger first.", "Missing trigger", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BindingAction? action = ActionTypeCombo.SelectedIndex switch
        {
            0 when _remapTarget is { } target => new RemapAction(target),
            1 => new TextAction(TextActionInput.Text),
            2 when !string.IsNullOrWhiteSpace(LaunchPathInput.Text) =>
                new LaunchAppAction(LaunchPathInput.Text, string.IsNullOrWhiteSpace(LaunchArgsInput.Text) ? null : LaunchArgsInput.Text),
            3 when _macro is not null => new MacroAction(_macro),
            _ => null,
        };

        if (action is null)
        {
            MessageBox.Show(this, "Fill in the action details first.", "Incomplete action", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new Binding(trigger, action);
        DialogResult = true;
    }
}
