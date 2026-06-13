using System.Windows;
using System.Windows.Controls;
using MacroController.Core.Input;
using Trigger = MacroController.Core.Bindings.Trigger;

namespace MacroController.App;

/// <summary>Modal dialog for manually adding a key-press, mouse-click, or wheel step to a macro.</summary>
public partial class AddMacroStepWindow : Window
{
    private Trigger? _trigger;

    public AddMacroStepWindow()
    {
        InitializeComponent();
        StepTypeCombo.SelectedIndex = 0;
        UpdatePanels();
    }

    public List<InputEvent>? Result { get; private set; }

    private void StepTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePanels();

    private void WheelDirectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateWheelHint();

    private void UpdatePanels()
    {
        bool isWheel = StepTypeCombo.SelectedIndex == 2;
        bool isDoubleClick = StepTypeCombo.SelectedIndex == 3;

        CapturePanel.Visibility = isWheel ? Visibility.Collapsed : Visibility.Visible;
        WheelDirectionPanel.Visibility = isWheel ? Visibility.Visible : Visibility.Collapsed;

        HoldPanel.Visibility = isWheel ? Visibility.Collapsed : Visibility.Visible;
        WheelDeltaPanel.Visibility = isWheel ? Visibility.Visible : Visibility.Collapsed;
        HoldLabel.Text = isDoubleClick ? "Hold per click (ms):" : "Hold (ms):";

        ClickGapPanel.Visibility = isDoubleClick ? Visibility.Visible : Visibility.Collapsed;

        if (isWheel)
            UpdateWheelHint();
    }

    private void UpdateWheelHint()
    {
        if (WheelHintText == null)
            return;

        WheelHintText.Text = WheelDirectionCombo.SelectedIndex == 1
            ? "(+ = right, - = left)"
            : "(+ = up, - = down)";
    }

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureButton.IsEnabled = false;
        CaptureButton.Content = "Press a key or button...";

        using var capture = new TriggerCapture();
        var trigger = await capture.Result;

        CaptureButton.IsEnabled = true;
        CaptureButton.Content = "Capture...";

        if (trigger is { } t)
        {
            _trigger = t;
            CapturedText.Text = TriggerDisplay.Describe(t);
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(DelayBeforeInput.Text, out int delayBefore) || delayBefore < 0)
        {
            MessageBox.Show(this, "Enter a non-negative delay.", "Invalid delay", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (StepTypeCombo.SelectedIndex == 2)
        {
            if (!int.TryParse(WheelDeltaInput.Text, out int delta) || delta == 0)
            {
                MessageBox.Show(this, "Enter a non-zero wheel delta.", "Invalid delta", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var wheelAction = WheelDirectionCombo.SelectedIndex == 1 ? ActionType.HWheel : ActionType.Wheel;
            Result = new List<InputEvent> { new(InputDevice.Mouse, delta, wheelAction, delayBefore) };
            DialogResult = true;
            return;
        }

        if (_trigger is not { } trigger)
        {
            MessageBox.Show(this, "Capture a key or mouse button first.", "Missing input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(HoldInput.Text, out int hold) || hold < 0)
        {
            MessageBox.Show(this, "Enter a non-negative hold duration.", "Invalid hold", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (StepTypeCombo.SelectedIndex == 3)
        {
            if (trigger.Device != InputDevice.Mouse)
            {
                MessageBox.Show(this, "Double-click requires a mouse button.", "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(ClickGapInput.Text, out int gap) || gap < 0)
            {
                MessageBox.Show(this, "Enter a non-negative gap between clicks.", "Invalid gap", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result = new List<InputEvent>
            {
                new(trigger.Device, trigger.Code, ActionType.MouseDown, delayBefore),
                new(trigger.Device, trigger.Code, ActionType.MouseUp, hold),
                new(trigger.Device, trigger.Code, ActionType.MouseDown, gap),
                new(trigger.Device, trigger.Code, ActionType.MouseUp, hold),
            };
            DialogResult = true;
            return;
        }

        var (downAction, upAction) = trigger.Device == InputDevice.Keyboard
            ? (ActionType.KeyDown, ActionType.KeyUp)
            : (ActionType.MouseDown, ActionType.MouseUp);

        Result = new List<InputEvent>
        {
            new(trigger.Device, trigger.Code, downAction, delayBefore),
            new(trigger.Device, trigger.Code, upAction, hold),
        };
        DialogResult = true;
    }
}
