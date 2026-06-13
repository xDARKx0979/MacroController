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
}

/// <summary>One recorded step of a macro: a device action plus the delay that preceded it.</summary>
public record InputEvent(InputDevice Device, int Code, ActionType Action, int DelayMs);
