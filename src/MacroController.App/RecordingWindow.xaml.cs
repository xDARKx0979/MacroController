using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MacroController.Core.Input;
using MacroController.Core.Macros;

namespace MacroController.App;

/// <summary>
/// Dedicated recording window: counts down, starts the recorder, shows a live feed of
/// every captured step, and hands the recorded steps back to the caller on "Done".
/// </summary>
public partial class RecordingWindow : Window
{
    private readonly MacroRecorder _recorder;

    public List<InputEvent>? Result { get; private set; }

    public RecordingWindow(MacroRecorder recorder)
    {
        InitializeComponent();

        _recorder = recorder;
        _recorder.StepRecorded += Recorder_StepRecorded;

        Loaded += async (_, _) => await StartCountdownAsync();
    }

    private async Task StartCountdownAsync()
    {
        for (int i = 3; i >= 1; i--)
        {
            StatusText.Text = $"Recording starts in {i}...";
            await Task.Delay(1000);
        }

        StatusText.Text = "Recording... perform the actions, then click Done.";
        _recorder.Start();
        DoneButton.IsEnabled = true;
    }

    private void Recorder_StepRecorded(InputEvent step)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var text = step.Action == ActionType.Delay
                ? $"Wait {step.DelayMs} ms"
                : MacroStepDisplay.Describe(step);

            StepsPanel.Children.Add(new TextBlock
            {
                Text = $"{MacroStepDisplay.Icon(step)}  {text}",
                Margin = new Thickness(2, 2, 2, 2),
            });
            StepsScroll.ScrollToEnd();
        });
    }

    private void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        Result = _recorder.Stop("").Steps;
        DialogResult = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _recorder.StepRecorded -= Recorder_StepRecorded;
        if (_recorder.IsRecording)
            _recorder.Stop("");

        base.OnClosing(e);
    }
}
