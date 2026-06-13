using System.Windows;
using System.Windows.Input;

namespace MacroController.App;

/// <summary>Small borderless popup showing download/install progress for the auto-updater.</summary>
public partial class UpdateProgressWindow : Window
{
    public UpdateProgressWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string text) => StatusText.Text = text;

    public void SetProgress(double fraction)
    {
        ProgressBarControl.IsIndeterminate = false;
        ProgressBarControl.Value = Math.Clamp(fraction * 100, 0, 100);
    }

    public void SetIndeterminate() => ProgressBarControl.IsIndeterminate = true;

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}
