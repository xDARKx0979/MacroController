using System.Diagnostics;
using MacroController.Core.Input;

namespace MacroController.Core.Macros;

/// <summary>
/// Captures input events into an <see cref="InputEvent"/> sequence with real
/// inter-event delays. The host application feeds events in from its own hooks via
/// the Record* methods (only while <see cref="IsRecording"/>), so it stays free to
/// filter out the hotkeys that control recording itself.
/// </summary>
public sealed class MacroRecorder
{
    private readonly List<InputEvent> _steps = new();
    private readonly Stopwatch _stopwatch = new();

    public bool IsRecording { get; private set; }

    public void Start()
    {
        _steps.Clear();
        IsRecording = true;
        _stopwatch.Restart();
    }

    public Macro Stop(string name)
    {
        IsRecording = false;
        return new Macro { Name = name, Steps = new List<InputEvent>(_steps) };
    }

    public void RecordKey(int virtualKeyCode, ActionType action)
    {
        if (!IsRecording)
            return;

        Record(InputDevice.Keyboard, virtualKeyCode, action);
    }

    public void RecordMouseButton(MouseButton button, ActionType action)
    {
        if (!IsRecording)
            return;

        Record(InputDevice.Mouse, (int)button, action);
    }

    public void RecordWheel(int delta, bool horizontal = false)
    {
        if (!IsRecording)
            return;

        Record(InputDevice.Mouse, delta, horizontal ? ActionType.HWheel : ActionType.Wheel);
    }

    private void Record(InputDevice device, int code, ActionType action)
    {
        int elapsedMs = (int)_stopwatch.ElapsedMilliseconds;
        _stopwatch.Restart();

        if (elapsedMs > 0)
            _steps.Add(new InputEvent(InputDevice.Keyboard, 0, ActionType.Delay, elapsedMs));

        _steps.Add(new InputEvent(device, code, action, 0));
    }
}
