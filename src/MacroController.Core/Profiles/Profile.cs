using MacroController.Core.Bindings;

namespace MacroController.Core.Profiles;

/// <summary>
/// A named set of bindings, optionally scoped to specific foreground processes.
/// </summary>
public class Profile
{
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Process names (without ".exe", case-insensitive) that activate this profile.
    /// An empty list marks the fallback profile, used when no other profile matches
    /// the foreground app.
    /// </summary>
    public List<string> MatchProcessNames { get; set; } = new();

    public List<Binding> Bindings { get; set; } = new();
}
