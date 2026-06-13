namespace MacroController.Core.Input;

public enum InputDevice
{
    Keyboard,
    Mouse,
}

public enum ActionType
{
    KeyDown,
    KeyUp,
    MouseDown,
    MouseUp,
    MouseMove,
    Wheel,

    /// <summary>Horizontal scroll. Positive = right, negative = left (120 per notch).</summary>
    HWheel,

    /// <summary>Marks the start of a repeating region; <see cref="InputEvent.Code"/> is the repeat count.</summary>
    LoopStart,

    /// <summary>Marks the end of a repeating region started by the nearest preceding <see cref="LoopStart"/>.</summary>
    LoopEnd,

    /// <summary>Waits for <see cref="InputEvent.DelayMs"/> without performing any other action.</summary>
    Delay,

    /// <summary>Launches a program/file/shortcut. <see cref="InputEvent.Text"/> is the path, <see cref="InputEvent.Text2"/> is optional arguments.</summary>
    LaunchApp,

    /// <summary>Runs <see cref="InputEvent.Text"/> as a shell command (via <c>cmd.exe /c</c>).</summary>
    RunCommand,

    /// <summary>Types the literal text in <see cref="InputEvent.Text"/>.</summary>
    TypeText,

    /// <summary>Plays another macro (identified by <see cref="Macros.Macro.Id"/> in <see cref="InputEvent.Text"/>) to completion.</summary>
    CallMacro,
}

/// <summary>One recorded step of a macro: a device action plus the delay that preceded it.</summary>
public record InputEvent(InputDevice Device, int Code, ActionType Action, int DelayMs, string? Text = null, string? Text2 = null);
