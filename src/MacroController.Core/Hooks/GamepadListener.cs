using System.Runtime.InteropServices;
using MacroController.Core.Input;

namespace MacroController.Core.Hooks;

public sealed class GamepadButtonEventArgs : EventArgs
{
    internal GamepadButtonEventArgs(int code) => Code = code;
    public int Code { get; }
}

/// <summary>
/// Polls a real Xbox/XInput controller and raises edge-triggered button events. Unlike
/// <see cref="KeyboardHook"/>/<see cref="MouseHook"/>, this isn't an OS hook - there's no
/// way to "swallow" a physical controller's state, so this only ever reports presses, it
/// never blocks them from reaching other apps/games too.
///
/// Scans all 4 XInput slots rather than just slot 0, skipping whichever slot
/// <see cref="VirtualGamepadSender"/>'s own virtual pad occupies. Without that, once a
/// macro with Xbox steps connects the virtual controller, Windows can hand it a low slot
/// (including 0) and this listener would start reading our own synthetic output back
/// instead of the real controller - real button presses (including bind triggers) would
/// then silently stop registering entirely.
/// </summary>
public sealed class GamepadListener : IDisposable
{
    private const int PollIntervalMs = 16; // ~60Hz
    private const byte TriggerThreshold = 30; // matches XINPUT_GAMEPAD_TRIGGER_THRESHOLD

    private static readonly (ushort Mask, XboxButton Button)[] ButtonMap =
    {
        (0x0001, XboxButton.DPadUp),
        (0x0002, XboxButton.DPadDown),
        (0x0004, XboxButton.DPadLeft),
        (0x0008, XboxButton.DPadRight),
        (0x0010, XboxButton.Start),
        (0x0020, XboxButton.Back),
        (0x0040, XboxButton.LeftStick),
        (0x0080, XboxButton.RightStick),
        (0x0100, XboxButton.LeftShoulder),
        (0x0200, XboxButton.RightShoulder),
        (0x1000, XboxButton.A),
        (0x2000, XboxButton.B),
        (0x4000, XboxButton.X),
        (0x8000, XboxButton.Y),
    };

    private Timer? _timer;
    private ushort _lastButtons;
    private bool _lastLeftTrigger;
    private bool _lastRightTrigger;
    private bool _dllMissing;
    private int? _activeUserIndex;

    public event EventHandler<GamepadButtonEventArgs>? ButtonDown;
    public event EventHandler<GamepadButtonEventArgs>? ButtonUp;

    public void Start()
    {
        if (_timer is not null)
            return;

        _timer = new Timer(_ => Poll(), null, 0, PollIntervalMs);
    }

    private void Poll()
    {
        if (_dllMissing)
            return;

        int? virtualSlot = VirtualGamepadSender.XboxUserIndex;

        XINPUT_STATE state = default;
        int result = 1;
        int? foundSlot = null;
        try
        {
            for (uint i = 0; i < 4; i++)
            {
                if (virtualSlot == (int)i)
                    continue;

                result = XInputGetState(i, out state);
                if (result == 0) // ERROR_SUCCESS - a real controller is here
                {
                    foundSlot = (int)i;
                    break;
                }
            }
        }
        catch (DllNotFoundException)
        {
            // xinput9_1_0.dll should always be present since Vista; if it's somehow
            // missing, stop polling instead of retrying (and failing) 60 times a second.
            _dllMissing = true;
            _timer?.Dispose();
            _timer = null;
            return;
        }

        if (foundSlot is null)
        {
            _activeUserIndex = null;

            // Uses (and then zeroes) whatever was still held from the last real
            // controller before it went away, so genuinely-held buttons get a proper
            // release event instead of just vanishing.
            if (_lastButtons != 0 || _lastLeftTrigger || _lastRightTrigger)
                ReleaseAll();
            return;
        }

        if (foundSlot != _activeUserIndex)
        {
            // Switched to a different real-controller slot (plugged in, or Windows
            // reassigned slots) - resync silently instead of diffing against the
            // previous device's stale button state, which would fire bogus events.
            _activeUserIndex = foundSlot;
            _lastButtons = state.Gamepad.wButtons;
            _lastLeftTrigger = state.Gamepad.bLeftTrigger >= TriggerThreshold;
            _lastRightTrigger = state.Gamepad.bRightTrigger >= TriggerThreshold;
        }

        ushort buttons = state.Gamepad.wButtons;
        if (buttons != _lastButtons)
        {
            foreach (var (mask, button) in ButtonMap)
            {
                bool wasDown = (_lastButtons & mask) != 0;
                bool isDown = (buttons & mask) != 0;
                if (isDown == wasDown)
                    continue;

                if (isDown)
                    ButtonDown?.Invoke(this, new GamepadButtonEventArgs((int)button));
                else
                    ButtonUp?.Invoke(this, new GamepadButtonEventArgs((int)button));
            }

            _lastButtons = buttons;
        }

        RaiseTrigger(state.Gamepad.bLeftTrigger >= TriggerThreshold, ref _lastLeftTrigger, XboxButton.LeftTrigger);
        RaiseTrigger(state.Gamepad.bRightTrigger >= TriggerThreshold, ref _lastRightTrigger, XboxButton.RightTrigger);
    }

    private void RaiseTrigger(bool isDown, ref bool lastState, XboxButton button)
    {
        if (isDown == lastState)
            return;

        lastState = isDown;
        if (isDown)
            ButtonDown?.Invoke(this, new GamepadButtonEventArgs((int)button));
        else
            ButtonUp?.Invoke(this, new GamepadButtonEventArgs((int)button));
    }

    private void ReleaseAll()
    {
        foreach (var (mask, button) in ButtonMap)
        {
            if ((_lastButtons & mask) != 0)
                ButtonUp?.Invoke(this, new GamepadButtonEventArgs((int)button));
        }

        if (_lastLeftTrigger)
            ButtonUp?.Invoke(this, new GamepadButtonEventArgs((int)XboxButton.LeftTrigger));
        if (_lastRightTrigger)
            ButtonUp?.Invoke(this, new GamepadButtonEventArgs((int)XboxButton.RightTrigger));

        _lastButtons = 0;
        _lastLeftTrigger = false;
        _lastRightTrigger = false;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    // xinput9_1_0.dll (not 1_3/1_4) - guaranteed present since Vista with no extra
    // runtime dependency, and it exports this call without needing rumble/battery APIs.
    [DllImport("xinput9_1_0.dll")]
    private static extern int XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);
}
