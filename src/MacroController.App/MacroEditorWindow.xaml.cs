using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using MacroController.Core.Macros;
using MacroController.Core.Storage;
using ActionType = MacroController.Core.Input.ActionType;
using Trigger = MacroController.Core.Bindings.Trigger;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;

namespace MacroController.App;

public partial class MacroEditorWindow : Window
{
    private readonly Macro _macro;
    private readonly MacroRecorder _recorder;
    private string _filePath;
    private Point _dragStart;
    private bool _dragging;
    private bool _countingDown;

    public MacroEditorWindow(Macro macro, string filePath, MacroRecorder recorder)
    {
        InitializeComponent();

        _macro = macro;
        _recorder = recorder;
        _filePath = filePath;

        LoadFromMacro();
    }

    public Macro Macro => _macro;
    public string FilePath => _filePath;

    private void LoadFromMacro()
    {
        NameInput.Text = _macro.Name;
        RepeatCountInput.Text = _macro.RepeatCount.ToString();

        ShortcutText.Text = _macro.Shortcut is { } shortcut ? TriggerDisplay.Describe(shortcut) : "None";

        PlayModeCombo.SelectedIndex = (int)_macro.PlayMode;
        RepeatCountPanel.Visibility = _macro.PlayMode == MacroPlayMode.RepeatCount ? Visibility.Visible : Visibility.Collapsed;

        FixedDelayInput.Text = _macro.StandardDelayMs.ToString();
        RandomDelayMinInput.Text = _macro.RandomDelayMinMs.ToString();
        RandomDelayMaxInput.Text = _macro.RandomDelayMaxMs.ToString();
        DelayModeCombo.SelectedIndex = (int)_macro.DelayMode;
        UpdateDelayModePanels();

        RefreshSteps();
    }

    private void RefreshSteps()
    {
        StepsPanel.Children.Clear();

        if (_macro.Steps.Count == 0)
        {
            StepsPanel.Children.Add(new TextBlock
            {
                Text = "No steps yet. Click Record or \"+ Add Action...\" to get started.",
                Foreground = Brushes.Gray,
                Margin = new Thickness(6),
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        for (int i = 0; i < _macro.Steps.Count; i++)
            StepsPanel.Children.Add(BuildStepRow(i));
    }

    private UIElement BuildStepRow(int index)
    {
        var step = _macro.Steps[index];

        var row = new Grid
        {
            Tag = index,
            AllowDrop = true,
            Margin = new Thickness(0, 1, 0, 1),
            Background = Brushes.Transparent,
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

        var indexText = new TextBlock
        {
            Text = (index + 1).ToString(),
            Foreground = Brushes.Gray,
            Width = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var icon = new TextBlock
        {
            Text = MacroStepDisplay.Icon(step),
            FontSize = 14,
            Width = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var description = new TextBlock
        {
            Text = MacroStepDisplay.Describe(step),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };

        var dragHandle = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Cursor = Cursors.SizeNS,
            Tag = index,
        };
        dragHandle.Children.Add(indexText);
        dragHandle.Children.Add(icon);
        dragHandle.Children.Add(description);
        dragHandle.PreviewMouseLeftButtonDown += StepRow_PreviewMouseLeftButtonDown;
        dragHandle.PreviewMouseMove += StepRow_PreviewMouseMove;
        Grid.SetColumn(dragHandle, 0);
        Grid.SetColumnSpan(dragHandle, 2);

        UIElement column2;
        if (step.Action == ActionType.LoopStart)
        {
            var loopPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var countInput = new TextBox
            {
                Text = Math.Max(1, step.Code).ToString(),
                Width = 40,
                TextAlignment = TextAlignment.Right,
                Tag = index,
                ToolTip = "Number of times to repeat this loop",
            };
            countInput.LostFocus += LoopCountInput_LostFocus;
            countInput.KeyDown += DelayInput_KeyDown;
            loopPanel.Children.Add(new TextBlock { Text = "× ", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
            loopPanel.Children.Add(countInput);
            column2 = loopPanel;
        }
        else if (step.Action == ActionType.Delay)
        {
            var delayPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var delayInput = new TextBox
            {
                Text = step.DelayMs.ToString(),
                Width = 50,
                TextAlignment = TextAlignment.Right,
                Tag = index,
                ToolTip = "How long to wait",
            };
            delayInput.LostFocus += DelayInput_LostFocus;
            delayInput.KeyDown += DelayInput_KeyDown;
            delayPanel.Children.Add(delayInput);
            delayPanel.Children.Add(new TextBlock { Text = " ms", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
            column2 = delayPanel;
        }
        else
        {
            column2 = new Border();
        }
        Grid.SetColumn(column2, 2);

        var removeButton = new Button
        {
            Content = "✕",
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            FontSize = 10,
            Margin = new Thickness(6, 0, 0, 0),
            Tag = index,
            ToolTip = "Remove step",
        };
        removeButton.Click += RemoveStepButton_Click;
        Grid.SetColumn(removeButton, 3);

        row.Children.Add(dragHandle);
        row.Children.Add(column2);
        row.Children.Add(removeButton);
        row.Drop += StepRow_Drop;

        return row;
    }

    private void AddStepButton_Click(object sender, RoutedEventArgs e) => AddStep();

    private void AddStep()
    {
        var dialog = new AddMacroStepWindow(_macro.Id) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is not { } newSteps)
            return;

        _macro.Steps.AddRange(newSteps);
        RefreshSteps();
    }

    private void LoopCountInput_LostFocus(object sender, RoutedEventArgs e)
    {
        var textBox = (TextBox)sender;
        int index = (int)textBox.Tag;
        if (index < 0 || index >= _macro.Steps.Count)
            return;

        if (int.TryParse(textBox.Text, out int count) && count >= 1)
        {
            _macro.Steps[index] = _macro.Steps[index] with { Code = count };
            RefreshSteps();
        }
        else
        {
            textBox.Text = Math.Max(1, _macro.Steps[index].Code).ToString();
        }
    }

    private void RemoveStepButton_Click(object sender, RoutedEventArgs e)
    {
        int index = (int)((Button)sender).Tag;
        _macro.Steps.RemoveAt(index);
        RefreshSteps();
    }

    private void DelayInput_LostFocus(object sender, RoutedEventArgs e)
    {
        var textBox = (TextBox)sender;
        int index = (int)textBox.Tag;
        if (index < 0 || index >= _macro.Steps.Count)
            return;

        if (int.TryParse(textBox.Text, out int delay) && delay >= 0)
            _macro.Steps[index] = _macro.Steps[index] with { DelayMs = delay };
        else
            textBox.Text = _macro.Steps[index].DelayMs.ToString();
    }

    private void DelayInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ((TextBox)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private void StepRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragging = false;
    }

    private void StepRow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragging)
            return;

        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStart.X) < 4 && Math.Abs(current.Y - _dragStart.Y) < 4)
            return;

        _dragging = true;
        var handle = (StackPanel)sender;
        DragDrop.DoDragDrop(handle, (int)handle.Tag, DragDropEffects.Move);
        _dragging = false;
    }

    private void StepRow_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(int)))
            return;

        int sourceIndex = (int)e.Data.GetData(typeof(int));
        int targetIndex = (int)((Grid)sender).Tag;
        if (sourceIndex == targetIndex)
            return;

        var step = _macro.Steps[sourceIndex];
        _macro.Steps.RemoveAt(sourceIndex);
        if (targetIndex > sourceIndex)
            targetIndex--;
        _macro.Steps.Insert(targetIndex, step);
        RefreshSteps();
    }

    private void NameInput_TextChanged(object sender, TextChangedEventArgs e) => _macro.Name = NameInput.Text;

    private async void CaptureShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureShortcutButton.IsEnabled = false;
        CaptureShortcutButton.Content = "Press a key or button...";

        using var capture = new TriggerCapture();
        var trigger = await capture.Result;

        CaptureShortcutButton.IsEnabled = true;
        CaptureShortcutButton.Content = "Capture...";

        if (trigger is { } t)
        {
            _macro.Shortcut = t;
            ShortcutText.Text = TriggerDisplay.Describe(t);
        }
    }

    private void ClearShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        _macro.Shortcut = null;
        ShortcutText.Text = "None";
    }

    private void PlayModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _macro.PlayMode = (MacroPlayMode)PlayModeCombo.SelectedIndex;
        RepeatCountPanel.Visibility = _macro.PlayMode == MacroPlayMode.RepeatCount ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RepeatCountInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(RepeatCountInput.Text, out int count) && count >= 1)
            _macro.RepeatCount = count;
        else
            RepeatCountInput.Text = _macro.RepeatCount.ToString();
    }

    private void DelayModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _macro.DelayMode = (MacroDelayMode)DelayModeCombo.SelectedIndex;
        UpdateDelayModePanels();
    }

    private void UpdateDelayModePanels()
    {
        FixedDelayPanel.Visibility = _macro.DelayMode == MacroDelayMode.Fixed ? Visibility.Visible : Visibility.Collapsed;
        RandomDelayPanel.Visibility = _macro.DelayMode == MacroDelayMode.Randomized ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FixedDelayInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(FixedDelayInput.Text, out int delay) && delay >= 0)
            _macro.StandardDelayMs = delay;
        else
            FixedDelayInput.Text = _macro.StandardDelayMs.ToString();
    }

    private void RandomDelayMinInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(RandomDelayMinInput.Text, out int value) && value >= 0 && value <= _macro.RandomDelayMaxMs)
            _macro.RandomDelayMinMs = value;
        else
            RandomDelayMinInput.Text = _macro.RandomDelayMinMs.ToString();
    }

    private void RandomDelayMaxInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(RandomDelayMaxInput.Text, out int value) && value >= _macro.RandomDelayMinMs)
            _macro.RandomDelayMaxMs = value;
        else
            RandomDelayMaxInput.Text = _macro.RandomDelayMaxMs.ToString();
    }

    private async void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder.IsRecording)
        {
            var recorded = _recorder.Stop(_macro.Name);
            _macro.Steps = recorded.Steps;
            RecordButton.Content = "Record";
            StatusText.Text = "";
            RefreshSteps();
            return;
        }

        if (_countingDown)
            return;

        _countingDown = true;
        RecordButton.IsEnabled = false;
        for (int i = 3; i >= 1; i--)
        {
            StatusText.Text = $"Recording starts in {i}...";
            await Task.Delay(1000);
        }

        StatusText.Text = "Recording... perform the actions, then click Stop.";
        _recorder.Start();
        RecordButton.Content = "Stop";
        RecordButton.IsEnabled = true;
        _countingDown = false;
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Macro files (*.json)|*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true)
            return;

        var loaded = MacroStore.Load(dialog.FileName);
        _macro.Name = loaded.Name;
        _macro.Steps = loaded.Steps;
        _macro.Shortcut = loaded.Shortcut;
        _macro.PlayMode = loaded.PlayMode;
        _macro.RepeatCount = loaded.RepeatCount;
        _macro.DelayMode = loaded.DelayMode;
        _macro.StandardDelayMs = loaded.StandardDelayMs;
        _macro.RandomDelayMinMs = loaded.RandomDelayMinMs;
        _macro.RandomDelayMaxMs = loaded.RandomDelayMaxMs;
        _filePath = dialog.FileName;

        LoadFromMacro();
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        PlayButton.IsEnabled = false;
        await MacroPlayer.PlayAsync(_macro);
        PlayButton.IsEnabled = true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        MacroStore.Save(_macro, _filePath);
        DialogResult = true;
    }

    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Macro files (*.json)|*.json|All files (*.*)|*.*", FileName = _filePath };
        if (dialog.ShowDialog(this) != true)
            return;

        _filePath = dialog.FileName;
        MacroStore.Save(_macro, _filePath);
        DialogResult = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_recorder.IsRecording)
            _recorder.Stop(_macro.Name);

        base.OnClosing(e);
    }
}
