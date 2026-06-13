using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MacroController.Core.Input;
using MacroController.Core.Native;

namespace MacroController.Core.Hooks;

public sealed class MouseButtonEventArgs : EventArgs
{
    internal MouseButtonEventArgs(MouseButton button, int x, int y)
    {
        Button = button;
        X = x;
        Y = y;
    }

    public MouseButton Button { get; }
    public int X { get; }
    public int Y { get; }

    /// <summary>Set to true to swallow the click so it never reaches other apps.</summary>
    public bool Handled { get; set; }
}

public sealed class MouseWheelEventArgs : EventArgs
{
    internal MouseWheelEventArgs(int delta, bool horizontal, int x, int y)
    {
        Delta = delta;
        Horizontal = horizontal;
        X = x;
        Y = y;
    }

    /// <summary>Positive = up/right, negative = down/left. One notch is typically 120.</summary>
    public int Delta { get; }
    public bool Horizontal { get; }
    public int X { get; }
    public int Y { get; }

    public bool Handled { get; set; }
}

/// <summary>
/// Wraps a low-level mouse hook (WH_MOUSE_LL). Like <see cref="KeyboardHook"/>, this
/// requires a Win32 message loop running on the thread that calls <see cref="Install"/>.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private readonly LowLevelMouseProc _proc;
    private nint _hookHandle;

    public event EventHandler<MouseButtonEventArgs>? MouseDown;
    public event EventHandler<MouseButtonEventArgs>? MouseUp;
    public event EventHandler<MouseWheelEventArgs>? MouseWheel;

    public MouseHook()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        if (_hookHandle != 0)
            return;

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule!;
        nint moduleHandle = NativeMethods.GetModuleHandle(currentModule.ModuleName);

        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, moduleHandle, 0);
        if (_hookHandle == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Uninstall()
    {
        if (_hookHandle == 0)
            return;

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = 0;
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int message = (int)wParam;
            int x = data.pt.X;
            int y = data.pt.Y;
            int highWord = (short)(data.mouseData >> 16);

            bool handled = message switch
            {
                NativeMethods.WM_LBUTTONDOWN => RaiseButton(MouseDown, MouseButton.Left, x, y),
                NativeMethods.WM_LBUTTONUP => RaiseButton(MouseUp, MouseButton.Left, x, y),
                NativeMethods.WM_RBUTTONDOWN => RaiseButton(MouseDown, MouseButton.Right, x, y),
                NativeMethods.WM_RBUTTONUP => RaiseButton(MouseUp, MouseButton.Right, x, y),
                NativeMethods.WM_MBUTTONDOWN => RaiseButton(MouseDown, MouseButton.Middle, x, y),
                NativeMethods.WM_MBUTTONUP => RaiseButton(MouseUp, MouseButton.Middle, x, y),
                NativeMethods.WM_XBUTTONDOWN => RaiseButton(MouseDown, XButtonFrom(highWord), x, y),
                NativeMethods.WM_XBUTTONUP => RaiseButton(MouseUp, XButtonFrom(highWord), x, y),
                NativeMethods.WM_MOUSEWHEEL => RaiseWheel(highWord, horizontal: false, x, y),
                NativeMethods.WM_MOUSEHWHEEL => RaiseWheel(highWord, horizontal: true, x, y),
                _ => false,
            };

            if (handled)
                return 1;
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static MouseButton XButtonFrom(int highWord) =>
        highWord == NativeMethods.XBUTTON2 ? MouseButton.X2 : MouseButton.X1;

    private bool RaiseButton(EventHandler<MouseButtonEventArgs>? handler, MouseButton button, int x, int y)
    {
        if (handler is null)
            return false;

        var args = new MouseButtonEventArgs(button, x, y);
        handler.Invoke(this, args);
        return args.Handled;
    }

    private bool RaiseWheel(int delta, bool horizontal, int x, int y)
    {
        if (MouseWheel is null)
            return false;

        var args = new MouseWheelEventArgs(delta, horizontal, x, y);
        MouseWheel.Invoke(this, args);
        return args.Handled;
    }

    public void Dispose() => Uninstall();
}
