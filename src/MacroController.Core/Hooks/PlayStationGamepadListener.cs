using System.Runtime.InteropServices;
using MacroController.Core.Input;
using SharpDX.DirectInput;

namespace MacroController.Core.Hooks;

/// <summary>
/// Polls a PlayStation (DS4/DualSense) controller via DirectInput. PS pads aren't XInput
/// devices, so <see cref="GamepadListener"/> can't see them - Windows only exposes them
/// through the older generic HID/DirectInput path.
///
/// The button/axis layout (L2 on RotationX, R2 on RotationY - not the "obvious" Z/Rz
/// guess) comes from SDL_GameControllerDB (zlib licensed), used only as a factual
/// reference for which index maps to which physical control. This implementation was
/// written from scratch against the SharpDX API - never copy mapping/parsing logic from
/// DS4Windows, which is GPL-3.0 and incompatible with closed-source distribution.
/// </summary>
public sealed class PlayStationGamepadListener : IDisposable
{
    private const int PollIntervalMs = 16; // ~60Hz
    private const uint SonyVendorId = 0x054C;
    private const int RescanEveryNTicks = 120; // ~2s at 60Hz - device enumeration is COM/relatively expensive

    // Common DS4/DualSense DirectInput button order (SDL_GameControllerDB). Index 6/7
    // (L2/R2 as buttons) are intentionally skipped - handled as analog axes below instead.
    private static readonly (int Index, PlayStationButton Button)[] ButtonMap =
    {
        (0, PlayStationButton.Square),
        (1, PlayStationButton.Cross),
        (2, PlayStationButton.Circle),
        (3, PlayStationButton.Triangle),
        (4, PlayStationButton.L1),
        (5, PlayStationButton.R1),
        (8, PlayStationButton.Share),
        (9, PlayStationButton.Options),
        (10, PlayStationButton.L3),
        (11, PlayStationButton.R3),
        (12, PlayStationButton.Home),
    };

    private DirectInput? _directInput;
    private Timer? _timer;
    private Joystick? _joystick;
    private Guid _joystickInstance;
    private int _tick;

    private bool[] _lastButtons = Array.Empty<bool>();
    private bool _lastDPadUp, _lastDPadDown, _lastDPadLeft, _lastDPadRight;
    private bool _lastL2, _lastR2;

    public event EventHandler<GamepadButtonEventArgs>? ButtonDown;
    public event EventHandler<GamepadButtonEventArgs>? ButtonUp;

    public void Start()
    {
        if (_timer is not null)
            return;

        _directInput = new DirectInput();
        RefreshDevices();
        _timer = new Timer(_ => Poll(), null, 0, PollIntervalMs);
    }

    private void Poll()
    {
        if (_directInput is null)
            return;

        if (_tick++ % RescanEveryNTicks == 0)
            RefreshDevices();

        if (_joystick is null)
            return;

        try
        {
            _joystick.Poll();
            var state = _joystick.GetCurrentState();
            ProcessState(state);
        }
        catch
        {
            // Most likely an unacquire caused by focus/cooperative-level churn - try to
            // reacquire next tick rather than tearing the device down over a transient
            // failure.
            try { _joystick.Acquire(); } catch { /* device may have been unplugged */ }
        }
    }

    private void ProcessState(JoystickState state)
    {
        bool[] buttons = state.Buttons;
        if (_lastButtons.Length != buttons.Length)
            _lastButtons = new bool[buttons.Length];

        foreach (var (index, button) in ButtonMap)
        {
            if (index >= buttons.Length)
                continue;

            bool isDown = buttons[index];
            if (isDown == _lastButtons[index])
                continue;

            _lastButtons[index] = isDown;
            Raise(isDown, (int)button);
        }

        int pov = state.PointOfViewControllers.Length > 0 ? state.PointOfViewControllers[0] : -1;
        bool up = pov != -1 && (pov >= 31500 || pov <= 4500);
        bool right = pov != -1 && pov is >= 4500 and <= 13500;
        bool down = pov != -1 && pov is >= 13500 and <= 22500;
        bool left = pov != -1 && pov is >= 22500 and <= 31500;

        RaiseIfChanged(up, ref _lastDPadUp, (int)PlayStationButton.DPadUp);
        RaiseIfChanged(down, ref _lastDPadDown, (int)PlayStationButton.DPadDown);
        RaiseIfChanged(left, ref _lastDPadLeft, (int)PlayStationButton.DPadLeft);
        RaiseIfChanged(right, ref _lastDPadRight, (int)PlayStationButton.DPadRight);

        // L2/R2: axis range was pinned to -1000..1000 on acquire; resting is negative,
        // fully pressed is positive - best-effort threshold, not verified against every
        // controller/driver combination.
        RaiseIfChanged(state.RotationX > 0, ref _lastL2, (int)PlayStationButton.L2);
        RaiseIfChanged(state.RotationY > 0, ref _lastR2, (int)PlayStationButton.R2);
    }

    private void RaiseIfChanged(bool isDown, ref bool lastState, int code)
    {
        if (isDown == lastState)
            return;

        lastState = isDown;
        Raise(isDown, code);
    }

    private void Raise(bool isDown, int code)
    {
        if (isDown)
            ButtonDown?.Invoke(this, new GamepadButtonEventArgs(code));
        else
            ButtonUp?.Invoke(this, new GamepadButtonEventArgs(code));
    }

    /// <summary>Attaches to the first Sony DirectInput game controller found, or drops the
    /// current one if it was unplugged. Called on Start() and roughly every 2s thereafter.</summary>
    private void RefreshDevices()
    {
        if (_directInput is null)
            return;

        // This runs on a background Timer (on Start() and every ~2s after) - an
        // unhandled exception here (e.g. a transient COM error during enumeration) is
        // fatal to the whole process, not just this scan.
        try
        {
            RefreshDevicesCore();
        }
        catch
        {
        }
    }

    private void RefreshDevicesCore()
    {
        var devices = _directInput!.GetDevices(SharpDX.DirectInput.DeviceType.Gamepad, DeviceEnumerationFlags.AttachedOnly)
            .Concat(_directInput.GetDevices(SharpDX.DirectInput.DeviceType.Joystick, DeviceEnumerationFlags.AttachedOnly));

        DeviceInstance? sonyDevice = null;
        foreach (var device in devices)
        {
            uint vendorId = BitConverter.ToUInt32(device.ProductGuid.ToByteArray(), 0) & 0xFFFF;
            if (vendorId == SonyVendorId)
            {
                sonyDevice = device;
                break;
            }
        }

        if (sonyDevice is null)
        {
            if (_joystick is not null)
                DropCurrentDevice();
            return;
        }

        if (_joystick is not null && _joystickInstance == sonyDevice.InstanceGuid)
            return; // already attached to this device

        if (_joystick is not null)
            DropCurrentDevice();

        try
        {
            var joystick = new Joystick(_directInput, sonyDevice.InstanceGuid);
            joystick.SetCooperativeLevel(GetDesktopWindow(), CooperativeLevel.Background | CooperativeLevel.NonExclusive);

            foreach (var deviceObject in joystick.GetObjects(DeviceObjectTypeFlags.Axis))
                joystick.GetObjectPropertiesById(deviceObject.ObjectId).Range = new SharpDX.DirectInput.InputRange(-1000, 1000);

            joystick.Acquire();

            _joystick = joystick;
            _joystickInstance = sonyDevice.InstanceGuid;
            _lastButtons = Array.Empty<bool>();
            _lastDPadUp = _lastDPadDown = _lastDPadLeft = _lastDPadRight = false;
            _lastL2 = _lastR2 = false;
        }
        catch
        {
            // Device disappeared or couldn't be acquired between enumeration and here;
            // just try again on the next rescan.
        }
    }

    private void DropCurrentDevice()
    {
        try { _joystick?.Unacquire(); } catch { /* already gone */ }
        _joystick?.Dispose();
        _joystick = null;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        DropCurrentDevice();
        _directInput?.Dispose();
        _directInput = null;
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
