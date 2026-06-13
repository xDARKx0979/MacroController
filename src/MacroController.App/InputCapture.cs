using MacroController.Core.Bindings;
using MacroController.Core.Hooks;
using MacroController.Core.Input;

namespace MacroController.App;

/// <summary>
/// Temporarily hooks input to capture the next key or mouse button press as a
/// <see cref="Trigger"/>. The hook is installed on construction and removed on
/// disposal - escape cancels the capture.
/// </summary>
internal sealed class TriggerCapture : IDisposable
{
    private const int VK_ESCAPE = 0x1B;

    private readonly KeyboardHook _keyboardHook = new();
    private readonly MouseHook _mouseHook = new();
    private readonly TaskCompletionSource<Trigger?> _tcs = new();

    public Task<Trigger?> Result => _tcs.Task;

    public TriggerCapture()
    {
        _keyboardHook.KeyDown += (_, e) =>
        {
            e.Handled = true;
            _tcs.TrySetResult(e.VirtualKeyCode == VK_ESCAPE ? null : new Trigger(InputDevice.Keyboard, e.VirtualKeyCode));
        };
        _mouseHook.MouseDown += (_, e) =>
        {
            e.Handled = true;
            _tcs.TrySetResult(new Trigger(InputDevice.Mouse, (int)e.Button));
        };

        _keyboardHook.Install();
        _mouseHook.Install();
    }

    public void Dispose()
    {
        _keyboardHook.Dispose();
        _mouseHook.Dispose();
    }
}

/// <summary>Like <see cref="TriggerCapture"/> but only captures a keyboard virtual-key code (for remap targets).</summary>
internal sealed class KeyCapture : IDisposable
{
    private const int VK_ESCAPE = 0x1B;

    private readonly KeyboardHook _hook = new();
    private readonly TaskCompletionSource<int?> _tcs = new();

    public Task<int?> Result => _tcs.Task;

    public KeyCapture()
    {
        _hook.KeyDown += (_, e) =>
        {
            e.Handled = true;
            _tcs.TrySetResult(e.VirtualKeyCode == VK_ESCAPE ? null : e.VirtualKeyCode);
        };

        _hook.Install();
    }

    public void Dispose() => _hook.Dispose();
}
