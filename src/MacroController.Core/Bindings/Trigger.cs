using MacroController.Core.Input;

namespace MacroController.Core.Bindings;

/// <summary>A physical key or mouse button that a <see cref="Binding"/> can attach to.</summary>
public readonly record struct Trigger(InputDevice Device, int Code);
