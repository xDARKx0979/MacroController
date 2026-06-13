using MacroController.Core.Native;

namespace MacroController.Core.Hooks;

/// <summary>
/// Low-level keyboard/mouse hooks only deliver events to the thread that installed
/// them, and that thread must run a Win32 message loop. This is a minimal pump for
/// console/background usage (no window required).
/// </summary>
public static class MessageLoop
{
    public static void Run()
    {
        while (NativeMethods.GetMessage(out var msg, nint.Zero, 0, 0) != 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }
    }
}
