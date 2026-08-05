using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nefarius.ViGEm.Client.Targets.DualShock4;

namespace MacroController.Core.Input;

/// <summary>
/// Sends synthetic controller input by driving a virtual Xbox 360 pad / DualShock 4 pad
/// through ViGEmBus (via the Nefarius.ViGEm.Client library). Unlike keyboard/mouse
/// SendInput, there's no OS API to fake XInput/DirectInput state directly - a virtual
/// controller device is the only way a game actually sees synthetic gamepad input.
///
/// ViGEmBus is a separate, user-installed kernel driver (not bundled with this app -
/// see the installer notes). If it isn't installed, every call here fails silently
/// (logged once) rather than crashing macro playback for users who never touch
/// controller steps.
/// </summary>
public static class VirtualGamepadSender
{
    private static readonly object Lock = new();
    private static ViGEmClient? _client;
    private static IXbox360Controller? _xbox;
    private static IDualShock4Controller? _playStation;
    private static bool _driverUnavailable;

    private static bool _dpadUp, _dpadDown, _dpadLeft, _dpadRight;
    private static byte _psSpecialButtons;

    /// <summary>True once a ViGEmBus/virtual-pad failure has been observed. Doesn't
    /// retry - if the driver gets installed later, restarting the app picks it up.</summary>
    public static bool DriverUnavailable => _driverUnavailable;

    /// <summary>
    /// Probes whether ViGEmBus is actually installed and reachable right now, by trying
    /// to open a throwaway client connection to it. Independent of the cached client
    /// used for real playback - safe to call speculatively (e.g. from a startup driver
    /// installer check) without affecting <see cref="SendGamepadDown"/>/<see cref="SendGamepadUp"/>.
    /// This is deliberately a functional check rather than reading installer metadata
    /// (registry uninstall entries, etc.) - those strings vary across installer
    /// versions/types and are far less reliable than just asking the driver directly.
    /// </summary>
    public static bool IsDriverAvailable()
    {
        try
        {
            using var client = new ViGEmClient();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Pre-connects the virtual pad(s) a caller expects to use, instead of leaving that
    /// to happen lazily on the first macro press. ViGEmBus.Connect() makes Windows
    /// register a brand-new HID device (plays the "device connected" chime and takes a
    /// moment to enumerate) - doing that mid-macro-playback means the macro's own first
    /// button presses can arrive before the device is actually ready and get dropped.
    /// Only connects the device types actually asked for, so someone who's never
    /// recorded a controller step doesn't get a phantom Xbox/DS4 pad sitting connected
    /// (which other games would otherwise see as an extra controller).
    /// </summary>
    public static void WarmUp(bool includeXbox, bool includePlayStation)
    {
        lock (Lock)
        {
            if (includeXbox)
                EnsureXbox();
            if (includePlayStation)
                EnsurePlayStation();
        }
    }

    public static void SendGamepadDown(InputDevice device, int code) => SetButton(device, code, down: true);

    public static void SendGamepadUp(InputDevice device, int code) => SetButton(device, code, down: false);

    private static void SetButton(InputDevice device, int code, bool down)
    {
        lock (Lock)
        {
            if (device == InputDevice.Xbox)
                SetXboxButton((XboxButton)code, down);
            else if (device == InputDevice.PlayStation)
                SetPlayStationButton((PlayStationButton)code, down);
        }
    }

    private static void SetXboxButton(XboxButton button, bool down)
    {
        var pad = EnsureXbox();
        if (pad is null)
            return;

        if (button is XboxButton.LeftTrigger or XboxButton.RightTrigger)
        {
            var slider = button == XboxButton.LeftTrigger ? Xbox360Slider.LeftTrigger : Xbox360Slider.RightTrigger;
            pad.SetSliderValue(slider, down ? (byte)255 : (byte)0);
            return;
        }

        pad.SetButtonState(MapXboxButton(button), down);
    }

    private static void SetPlayStationButton(PlayStationButton button, bool down)
    {
        var pad = EnsurePlayStation();
        if (pad is null)
            return;

        switch (button)
        {
            case PlayStationButton.DPadUp: _dpadUp = down; UpdateDpad(pad); return;
            case PlayStationButton.DPadDown: _dpadDown = down; UpdateDpad(pad); return;
            case PlayStationButton.DPadLeft: _dpadLeft = down; UpdateDpad(pad); return;
            case PlayStationButton.DPadRight: _dpadRight = down; UpdateDpad(pad); return;

            case PlayStationButton.L2:
                pad.SetSliderValue(DualShock4Slider.LeftTrigger, down ? (byte)255 : (byte)0);
                return;
            case PlayStationButton.R2:
                pad.SetSliderValue(DualShock4Slider.RightTrigger, down ? (byte)255 : (byte)0);
                return;

            case PlayStationButton.Home:
                _psSpecialButtons = down
                    ? (byte)(_psSpecialButtons | DualShock4SpecialButton.Ps.Value)
                    : (byte)(_psSpecialButtons & ~DualShock4SpecialButton.Ps.Value);
                pad.SetSpecialButtonsFull(_psSpecialButtons);
                return;

            default:
                pad.SetButtonState(MapPlayStationButton(button), down);
                return;
        }
    }

    private static void UpdateDpad(IDualShock4Controller pad)
    {
        var direction = (_dpadUp, _dpadDown, _dpadLeft, _dpadRight) switch
        {
            (true, false, false, true) => DualShock4DPadDirection.Northeast,
            (true, false, true, false) => DualShock4DPadDirection.Northwest,
            (false, true, false, true) => DualShock4DPadDirection.Southeast,
            (false, true, true, false) => DualShock4DPadDirection.Southwest,
            (true, false, false, false) => DualShock4DPadDirection.North,
            (false, true, false, false) => DualShock4DPadDirection.South,
            (false, false, true, false) => DualShock4DPadDirection.West,
            (false, false, false, true) => DualShock4DPadDirection.East,
            _ => DualShock4DPadDirection.None,
        };

        pad.SetDPadDirection(direction);
    }

    private static Xbox360Button MapXboxButton(XboxButton button) => button switch
    {
        XboxButton.A => Xbox360Button.A,
        XboxButton.B => Xbox360Button.B,
        XboxButton.X => Xbox360Button.X,
        XboxButton.Y => Xbox360Button.Y,
        XboxButton.LeftShoulder => Xbox360Button.LeftShoulder,
        XboxButton.RightShoulder => Xbox360Button.RightShoulder,
        XboxButton.LeftStick => Xbox360Button.LeftThumb,
        XboxButton.RightStick => Xbox360Button.RightThumb,
        XboxButton.Start => Xbox360Button.Start,
        XboxButton.Back => Xbox360Button.Back,
        XboxButton.DPadUp => Xbox360Button.Up,
        XboxButton.DPadDown => Xbox360Button.Down,
        XboxButton.DPadLeft => Xbox360Button.Left,
        XboxButton.DPadRight => Xbox360Button.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(button)),
    };

    private static DualShock4Button MapPlayStationButton(PlayStationButton button) => button switch
    {
        PlayStationButton.Cross => DualShock4Button.Cross,
        PlayStationButton.Circle => DualShock4Button.Circle,
        PlayStationButton.Square => DualShock4Button.Square,
        PlayStationButton.Triangle => DualShock4Button.Triangle,
        PlayStationButton.L1 => DualShock4Button.ShoulderLeft,
        PlayStationButton.R1 => DualShock4Button.ShoulderRight,
        PlayStationButton.L3 => DualShock4Button.ThumbLeft,
        PlayStationButton.R3 => DualShock4Button.ThumbRight,
        PlayStationButton.Share => DualShock4Button.Share,
        PlayStationButton.Options => DualShock4Button.Options,
        _ => throw new ArgumentOutOfRangeException(nameof(button)),
    };

    private static bool EnsureViGEmClient()
    {
        if (_driverUnavailable)
            return false;

        if (_client is not null)
            return true;

        try
        {
            _client = new ViGEmClient();
            return true;
        }
        catch
        {
            // Most likely VigemBusNotFoundException - the ViGEmBus driver isn't
            // installed. Treat controller output as unavailable for this run rather
            // than retrying (and failing) on every subsequent macro step.
            _driverUnavailable = true;
            return false;
        }
    }

    private static IXbox360Controller? EnsureXbox()
    {
        if (_xbox is not null)
            return _xbox;

        if (!EnsureViGEmClient())
            return null;

        try
        {
            var pad = _client!.CreateXbox360Controller();
            pad.Connect();
            _xbox = pad;
        }
        catch
        {
            _driverUnavailable = true;
        }

        return _xbox;
    }

    private static IDualShock4Controller? EnsurePlayStation()
    {
        if (_playStation is not null)
            return _playStation;

        if (!EnsureViGEmClient())
            return null;

        try
        {
            var pad = _client!.CreateDualShock4Controller();
            pad.Connect();
            _playStation = pad;
        }
        catch
        {
            _driverUnavailable = true;
        }

        return _playStation;
    }

    /// <summary>Disconnects any virtual pads that were created. Call on app shutdown.</summary>
    public static void Shutdown()
    {
        lock (Lock)
        {
            try { _xbox?.Disconnect(); } catch { /* best-effort */ }
            try { _playStation?.Disconnect(); } catch { /* best-effort */ }
            _xbox = null;
            _playStation = null;
            _client?.Dispose();
            _client = null;
        }
    }
}
