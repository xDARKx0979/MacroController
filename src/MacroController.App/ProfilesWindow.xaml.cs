using System.Windows;
using MacroController.Core.Bindings;
using MacroController.Core.Profiles;
using MacroController.Core.Storage;

namespace MacroController.App;

/// <summary>Lists every profile and lets the user create, edit, or remove them.</summary>
public partial class ProfilesWindow : Window
{
    private const string ProfilesFilePath = "profiles.json";

    private readonly List<Profile> _profiles;

    public ProfilesWindow(IEnumerable<Profile> profiles)
    {
        InitializeComponent();

        _profiles = profiles.Select(Clone).ToList();
        RefreshList();
    }

    /// <summary>The saved profile set, valid when the dialog closes with <c>DialogResult == true</c>.</summary>
    public List<Profile> Profiles => _profiles;

    private static Profile Clone(Profile profile) => new()
    {
        Name = profile.Name,
        MatchProcessNames = new List<string>(profile.MatchProcessNames),
        Bindings = new List<Binding>(profile.Bindings),
    };

    private void RefreshList()
    {
        ProfileList.ItemsSource = _profiles.Select(p => new ProfileListItem(p)).ToList();
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not ProfileListItem item)
            return;

        var dialog = new ProfileEditWindow(item.Profile) { Owner = this };
        if (dialog.ShowDialog() == true)
            RefreshList();
    }

    private void NewProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("Make New Profile", "Profile name:", "New Profile") { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value))
            return;

        var profile = new Profile { Name = dialog.Value.Trim() };
        _profiles.Add(profile);
        RefreshList();

        var editor = new ProfileEditWindow(profile) { Owner = this };
        editor.ShowDialog();
        RefreshList();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not ProfileListItem item)
            return;

        if (_profiles.Count == 1)
        {
            MessageBox.Show(this, "At least one profile is required.", "Cannot remove", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(this, $"Delete profile '{item.Profile.Name}'?", "Delete Profile",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
            return;

        _profiles.Remove(item.Profile);
        RefreshList();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ProfileStore.Save(_profiles, ProfilesFilePath);
        DialogResult = true;
    }
}

internal sealed class ProfileListItem
{
    public ProfileListItem(Profile profile) => Profile = profile;

    public Profile Profile { get; }

    public string Name => Profile.Name;

    public string Subtitle => Profile.MatchProcessNames.Count == 0
        ? "Applies globally (fallback)"
        : $"Auto-applies for: {string.Join(", ", Profile.MatchProcessNames)}";
}
