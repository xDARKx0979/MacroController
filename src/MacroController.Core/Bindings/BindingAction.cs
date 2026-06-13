using System.Text.Json.Serialization;
using MacroController.Core.Macros;

namespace MacroController.Core.Bindings;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(RemapAction), "remap")]
[JsonDerivedType(typeof(MacroAction), "macro")]
[JsonDerivedType(typeof(TextAction), "text")]
[JsonDerivedType(typeof(LaunchAppAction), "launchApp")]
public abstract record BindingAction;

/// <summary>Sends a different key while the trigger is held (down maps to down, up to up).</summary>
public sealed record RemapAction(int TargetVirtualKeyCode) : BindingAction;

/// <summary>Plays a macro once per trigger press.</summary>
public sealed record MacroAction(Macro Macro) : BindingAction;

/// <summary>Types literal text once per trigger press.</summary>
public sealed record TextAction(string Text) : BindingAction;

/// <summary>Launches a program, file, or shortcut once per trigger press.</summary>
public sealed record LaunchAppAction(string Path, string? Arguments = null) : BindingAction;
