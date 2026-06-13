using System.Windows;
using System.Windows.Input;

namespace MacroController.App;

/// <summary>Simple modal prompt for a single line of text.</summary>
public partial class InputDialog : Window
{
    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();

        Title = title;
        PromptText.Text = prompt;
        ValueInput.Text = defaultValue;

        Loaded += (_, _) =>
        {
            ValueInput.Focus();
            ValueInput.SelectAll();
        };
    }

    public string Value => ValueInput.Text;

    private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void ValueInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            DialogResult = true;
    }
}
