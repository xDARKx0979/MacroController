using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MacroController.Core.Profiles;

namespace MacroController.App;

/// <summary>Modal editor for a single <see cref="Profile"/>: its name, auto-apply window match, and bindings.</summary>
public partial class ProfileEditWindow : Window
{
    private readonly Profile _profile;

    public ProfileEditWindow(Profile profile)
    {
        InitializeComponent();

        _profile = profile;

        ProfileNameInput.Text = profile.Name;

        bool autoApply = profile.MatchProcessNames.Count > 0;
        AutoApplyCheckBox.IsChecked = autoApply;
        WindowNameInput.Text = string.Join(", ", profile.MatchProcessNames);
        WindowNameInput.IsEnabled = autoApply;

        RefreshBindingList();
    }

    private void AutoApplyCheckBox_Changed(object sender, RoutedEventArgs e)
        => WindowNameInput.IsEnabled = AutoApplyCheckBox.IsChecked == true;

    private void RefreshBindingList()
    {
        BindingList.ItemsSource = _profile.Bindings
            .Select(b => $"{TriggerDisplay.Describe(b.Trigger)}  ->  {TriggerDisplay.Describe(b.Action)}")
            .ToList();
    }

    private void AddBinding_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new BindingEditWindow { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is { } binding)
        {
            _profile.Bindings.Add(binding);
            RefreshBindingList();
        }
    }

    private void EditBinding_Click(object sender, RoutedEventArgs e) => EditSelectedBinding();

    private void BindingList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelectedBinding();

    private void EditSelectedBinding()
    {
        if (BindingList.SelectedIndex < 0)
            return;

        int index = BindingList.SelectedIndex;
        var dialog = new BindingEditWindow(_profile.Bindings[index]) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is { } binding)
        {
            _profile.Bindings[index] = binding;
            RefreshBindingList();
        }
    }

    private void RemoveBinding_Click(object sender, RoutedEventArgs e)
    {
        if (BindingList.SelectedIndex < 0)
            return;

        _profile.Bindings.RemoveAt(BindingList.SelectedIndex);
        RefreshBindingList();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _profile.Name = string.IsNullOrWhiteSpace(ProfileNameInput.Text) ? _profile.Name : ProfileNameInput.Text;

        _profile.MatchProcessNames = AutoApplyCheckBox.IsChecked == true
            ? WindowNameInput.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList()
            : new List<string>();

        DialogResult = true;
    }
}
