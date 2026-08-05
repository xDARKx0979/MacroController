namespace MacroController.Core.Input;

/// <summary>Digital PlayStation (DS4/DualSense) buttons, read via DirectInput. No
/// touchpad click - it's not exposed consistently enough across DS4/DualSense to be
/// worth binding.</summary>
public enum PlayStationButton
{
    Cross,
    Circle,
    Square,
    Triangle,
    L1,
    R1,
    L2,
    R2,
    L3,
    R3,
    Share,
    Options,
    Home,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
}
