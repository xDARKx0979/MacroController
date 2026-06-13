using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MacroController.Core.Input;
using MacroController.Core.Macros;
using MacroController.Core.Storage;
using Trigger = MacroController.Core.Bindings.Trigger;

namespace MacroController.App;

/// <summary>
/// Modal dialog for adding a step to a macro. The left-hand category list mirrors Razer
/// Synapse's macro editor "Add" panel (Delay, Keyboard/Mouse Function, Macro, Launch,
/// Run Command, Text Function, Loop); the right-hand side shows that category's settings.
/// </summary>
public partial class AddMacroStepWindow : Window
{
    private readonly string? _excludeMacroId;
    private List<Macro> _macros = new();
    private Trigger? _keyTrigger;
    private Trigger? _mouseTrigger;

    public AddMacroStepWindow(string? excludeMacroId = null)
    {
        InitializeComponent();
        _excludeMacroId = excludeMacroId;

        PopulateMacroCombo();
        CategoryList.SelectedIndex = 1; // Keyboard Function
        UpdatePanels();
    }

    public List<InputEvent>? Result { get; private set; }

    private void PopulateMacroCombo()
    {
        _macros = MacroLibraryStore.LoadAll()
            .Select(entry => entry.Macro)
            .Where(macro => macro.Id != _excludeMacroId)
            .ToList();

        MacroCombo.ItemsSource = _macros.Select(macro => macro.Name).ToList();
        if (_macros.Count > 0)
            MacroCombo.SelectedIndex = 0;

        MacroCombo.Visibility = _macros.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoMacrosText.Visibility = _macros.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePanels();

    private void MouseActionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateMouseSubPanels();

    private void MouseWheelDirectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateWheelHint();

    private void UpdatePanels()
    {
        int category = CategoryList.SelectedIndex;

        DelayPanel.Visibility = category == 0 ? Visibility.Visible : Visibility.Collapsed;
        KeyboardPanel.Visibility = category == 1 ? Visibility.Visible : Visibility.Collapsed;
        MousePanel.Visibility = category == 2 ? Visibility.Visible : Visibility.Collapsed;
        MacroPanel.Visibility = category == 3 ? Visibility.Visible : Visibility.Collapsed;
        LaunchPanel.Visibility = category == 4 ? Visibility.Visible : Visibility.Collapsed;
        RunCommandPanel.Visibility = category == 5 ? Visibility.Visible : Visibility.Collapsed;
        TextFunctionPanel.Visibility = category == 6 ? Visibility.Visible : Visibility.Collapsed;
        LoopPanel.Visibility = category == 7 ? Visibility.Visible : Visibility.Collapsed;

        if (category == 2)
            UpdateMouseSubPanels();
    }

    private void UpdateMouseSubPanels()
    {
        if (MouseCapturePanel is null)
            return; // fired during InitializeComponent, before later panels are wired up

        int mode = MouseActionCombo.SelectedIndex; // 0 = Click, 1 = Double-Click, 2 = Wheel
        bool isWheel = mode == 2;
        bool isDoubleClick = mode == 1;

        MouseCapturePanel.Visibility = isWheel ? Visibility.Collapsed : Visibility.Visible;
        MouseHoldPanel.Visibility = isWheel ? Visibility.Collapsed : Visibility.Visible;
        MouseHoldLabel.Text = isDoubleClick ? "Hold per click (ms):" : "Hold (ms):";
        MouseGapPanel.Visibility = isDoubleClick ? Visibility.Visible : Visibility.Collapsed;
        MouseWheelPanel.Visibility = isWheel ? Visibility.Visible : Visibility.Collapsed;

        if (isWheel)
            UpdateWheelHint();
    }

    private void UpdateWheelHint()
    {
        if (MouseWheelHintText == null)
            return;

        MouseWheelHintText.Text = MouseWheelDirectionCombo.SelectedIndex == 1
            ? "(+ = right, - = left)"
            : "(+ = up, - = down)";
    }

    private async void KeyCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        KeyCaptureButton.IsEnabled = false;
        KeyCaptureButton.Content = "Press a key or button...";

        using var capture = new TriggerCapture();
        var trigger = await capture.Result;

        KeyCaptureButton.IsEnabled = true;
        KeyCaptureButton.Content = "Capture...";

        if (trigger is { } t)
        {
            _keyTrigger = t;
            KeyCapturedText.Text = TriggerDisplay.Describe(t);
        }
    }

    private async void MouseCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        MouseCaptureButton.IsEnabled = false;
        MouseCaptureButton.Content = "Click a mouse button...";

        using var capture = new TriggerCapture();
        var trigger = await capture.Result;

        MouseCaptureButton.IsEnabled = true;
        MouseCaptureButton.Content = "Capture...";

        if (trigger is { } t)
        {
            _mouseTrigger = t;
            MouseCapturedText.Text = TriggerDisplay.Describe(t);
        }
    }

    private void LaunchBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true)
            LaunchPathInput.Text = dialog.FileName;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        int category = CategoryList.SelectedIndex;

        switch (category)
        {
            case 0: // Delay
                if (!int.TryParse(WaitInput.Text, out int wait) || wait < 0)
                {
                    ShowError("Enter a non-negative wait time.");
                    return;
                }

                Result = new List<InputEvent> { new(InputDevice.Keyboard, 0, ActionType.Delay, wait) };
                break;

            case 1: // Keyboard Function
                if (_keyTrigger is not { } keyTrigger)
                {
                    ShowError("Capture a key or button first.");
                    return;
                }

                if (!int.TryParse(KeyHoldInput.Text, out int keyHold) || keyHold < 0)
                {
                    ShowError("Enter a non-negative hold duration.");
                    return;
                }

                var (downAction, upAction) = keyTrigger.Device == InputDevice.Keyboard
                    ? (ActionType.KeyDown, ActionType.KeyUp)
                    : (ActionType.MouseDown, ActionType.MouseUp);

                Result = new List<InputEvent>
                {
                    new(keyTrigger.Device, keyTrigger.Code, downAction, 0),
                    new(keyTrigger.Device, keyTrigger.Code, upAction, keyHold),
                };
                break;

            case 2: // Mouse Function
                var mouseSteps = BuildMouseSteps();
                if (mouseSteps is null)
                    return;

                Result = mouseSteps;
                break;

            case 3: // Macro
                if (MacroCombo.SelectedIndex < 0 || MacroCombo.SelectedIndex >= _macros.Count)
                {
                    ShowError("Select a macro to play.");
                    return;
                }

                Result = new List<InputEvent>
                {
                    new(InputDevice.Keyboard, 0, ActionType.CallMacro, 0, Text: _macros[MacroCombo.SelectedIndex].Id),
                };
                break;

            case 4: // Launch
                if (string.IsNullOrWhiteSpace(LaunchPathInput.Text))
                {
                    ShowError("Enter a path to launch.");
                    return;
                }

                Result = new List<InputEvent>
                {
                    new(InputDevice.Keyboard, 0, ActionType.LaunchApp, 0,
                        Text: LaunchPathInput.Text.Trim(),
                        Text2: string.IsNullOrWhiteSpace(LaunchArgsInput.Text) ? null : LaunchArgsInput.Text.Trim()),
                };
                break;

            case 5: // Run Command
                if (string.IsNullOrWhiteSpace(RunCommandInput.Text))
                {
                    ShowError("Enter a command to run.");
                    return;
                }

                Result = new List<InputEvent>
                {
                    new(InputDevice.Keyboard, 0, ActionType.RunCommand, 0, Text: RunCommandInput.Text.Trim()),
                };
                break;

            case 6: // Text Function
                if (string.IsNullOrEmpty(TextFunctionInput.Text))
                {
                    ShowError("Enter text to type.");
                    return;
                }

                Result = new List<InputEvent>
                {
                    new(InputDevice.Keyboard, 0, ActionType.TypeText, 0, Text: TextFunctionInput.Text),
                };
                break;

            case 7: // Loop
                if (!int.TryParse(LoopCountInput.Text, out int loopCount) || loopCount < 1)
                {
                    ShowError("Enter a repeat count of at least 1.");
                    return;
                }

                Result = new List<InputEvent>
                {
                    new(InputDevice.Keyboard, loopCount, ActionType.LoopStart, 0),
                    new(InputDevice.Keyboard, 0, ActionType.LoopEnd, 0),
                };
                break;
        }

        DialogResult = true;
    }

    private List<InputEvent>? BuildMouseSteps()
    {
        int mode = MouseActionCombo.SelectedIndex; // 0 = Click, 1 = Double-Click, 2 = Wheel

        if (mode == 2) // Wheel
        {
            if (!int.TryParse(MouseWheelDeltaInput.Text, out int delta) || delta == 0)
            {
                ShowError("Enter a non-zero wheel delta.");
                return null;
            }

            var wheelAction = MouseWheelDirectionCombo.SelectedIndex == 1 ? ActionType.HWheel : ActionType.Wheel;
            return new List<InputEvent> { new(InputDevice.Mouse, delta, wheelAction, 0) };
        }

        if (_mouseTrigger is not { } trigger)
        {
            ShowError("Capture a mouse button first.");
            return null;
        }

        if (!int.TryParse(MouseHoldInput.Text, out int hold) || hold < 0)
        {
            ShowError("Enter a non-negative hold duration.");
            return null;
        }

        if (mode == 1) // Double-Click
        {
            if (!int.TryParse(MouseGapInput.Text, out int gap) || gap < 0)
            {
                ShowError("Enter a non-negative gap between clicks.");
                return null;
            }

            return new List<InputEvent>
            {
                new(trigger.Device, trigger.Code, ActionType.MouseDown, 0),
                new(trigger.Device, trigger.Code, ActionType.MouseUp, hold),
                new(trigger.Device, trigger.Code, ActionType.MouseDown, gap),
                new(trigger.Device, trigger.Code, ActionType.MouseUp, hold),
            };
        }

        return new List<InputEvent>
        {
            new(trigger.Device, trigger.Code, ActionType.MouseDown, 0),
            new(trigger.Device, trigger.Code, ActionType.MouseUp, hold),
        };
    }

    private void ShowError(string message) =>
        MessageBox.Show(this, message, "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
}
