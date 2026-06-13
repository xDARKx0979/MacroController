using MacroController.Core.Bindings;

namespace MacroController.Core.Profiles;

/// <summary>
/// Tracks the active <see cref="Profile"/> and keeps a <see cref="BindingManager"/> in
/// sync with it. <see cref="Poll"/> checks the foreground app and auto-switches;
/// <see cref="SetActiveProfile"/> allows manual switching too.
/// </summary>
public sealed class ProfileManager
{
    private readonly BindingManager _bindingManager;
    private readonly List<Profile> _profiles;
    private List<Binding> _globalBindings;

    public Profile ActiveProfile { get; private set; }

    public event EventHandler<Profile>? ActiveProfileChanged;

    public ProfileManager(BindingManager bindingManager, IEnumerable<Profile> profiles, IEnumerable<Binding>? globalBindings = null)
    {
        _bindingManager = bindingManager;
        _profiles = profiles.ToList();
        _globalBindings = globalBindings?.ToList() ?? new();
        if (_profiles.Count == 0)
            throw new ArgumentException("At least one profile is required.", nameof(profiles));

        ActiveProfile = _profiles[0];
        _bindingManager.SetBindings(EffectiveBindings(ActiveProfile));
    }

    /// <summary>Replaces the macro-shortcut bindings that apply regardless of the active profile.</summary>
    public void SetGlobalBindings(IEnumerable<Binding> globalBindings)
    {
        _globalBindings = globalBindings.ToList();
        _bindingManager.SetBindings(EffectiveBindings(ActiveProfile));
    }

    public IReadOnlyList<Profile> Profiles => _profiles;

    /// <summary>Replaces the profile set (e.g. after editing) and re-evaluates the active profile.</summary>
    public void ReplaceProfiles(IEnumerable<Profile> profiles)
    {
        _profiles.Clear();
        _profiles.AddRange(profiles);
        if (_profiles.Count == 0)
            throw new ArgumentException("At least one profile is required.", nameof(profiles));

        ActiveProfile = _profiles[0];
        _bindingManager.SetBindings(EffectiveBindings(ActiveProfile));
        ActiveProfileChanged?.Invoke(this, ActiveProfile);
        Poll();
    }

    /// <summary>Checks the foreground app and switches profile if needed. Call on a timer.</summary>
    public void Poll()
    {
        string? processName = ForegroundApp.GetProcessName();
        SetActiveProfile(FindMatch(processName));
    }

    public void SetActiveProfile(Profile profile)
    {
        if (ReferenceEquals(profile, ActiveProfile))
            return;

        ActiveProfile = profile;
        _bindingManager.SetBindings(EffectiveBindings(profile));
        ActiveProfileChanged?.Invoke(this, profile);
    }

    private Profile FindMatch(string? processName)
    {
        if (processName is not null)
        {
            var specific = _profiles.FirstOrDefault(p =>
                p.MatchProcessNames.Any(name => string.Equals(name, processName, StringComparison.OrdinalIgnoreCase)));
            if (specific is not null)
                return specific;
        }

        return _profiles.FirstOrDefault(p => p.MatchProcessNames.Count == 0) ?? _profiles[0];
    }

    /// <summary>
    /// Merges, in increasing order of precedence: macro-shortcut global bindings, the fallback
    /// ("Default", <see cref="Profile.MatchProcessNames"/> empty) profile's bindings, and the
    /// given profile's own bindings. This lets app-specific profiles define only overrides/additions,
    /// and lets per-macro shortcuts apply everywhere unless a profile explicitly rebinds the same trigger.
    /// </summary>
    private IEnumerable<Binding> EffectiveBindings(Profile profile)
    {
        var merged = new Dictionary<Trigger, Binding>();
        foreach (var binding in _globalBindings)
            merged[binding.Trigger] = binding;

        var fallback = _profiles.FirstOrDefault(p => p.MatchProcessNames.Count == 0);
        if (fallback is not null && !ReferenceEquals(fallback, profile))
            foreach (var binding in fallback.Bindings)
                merged[binding.Trigger] = binding;

        foreach (var binding in profile.Bindings)
            merged[binding.Trigger] = binding;

        return merged.Values;
    }
}
