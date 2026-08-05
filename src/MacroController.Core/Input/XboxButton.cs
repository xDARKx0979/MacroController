namespace MacroController.Core.Input;

/// <summary>Digital Xbox/XInput buttons. Analog stick directions are deliberately not
/// represented here - this app's binding model is discrete on/off, and stick tilt
/// doesn't fit that.</summary>
public enum XboxButton
{
    A,
    B,
    X,
    Y,
    LeftShoulder,
    RightShoulder,
    LeftTrigger,
    RightTrigger,
    LeftStick,
    RightStick,
    Start,
    Back,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
}
