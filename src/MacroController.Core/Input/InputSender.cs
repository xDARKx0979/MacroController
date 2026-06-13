using System.ComponentModel;
using System.Runtime.InteropServices;
using MacroController.Core.Native;

namespace MacroController.Core.Input;

/// <summary>
/// Sends synthetic keyboard input via <c>SendInput</c>. Output produced this way is
/// indistinguishable from real hardware input to other applications and games.
/// </summary>
public static class InputSender
{
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;

    public static void SendKeyDown(int virtualKeyCode) => SendKey(virtualKeyCode, keyUp: false);

    public static void SendKeyUp(int virtualKeyCode) => SendKey(virtualKeyCode, keyUp: true);

    public static void SendKeyPress(int virtualKeyCode)
    {
        SendKeyDown(virtualKeyCode);
        SendKeyUp(virtualKeyCode);
    }

    private static void SendKey(int virtualKeyCode, bool keyUp)
    {
        // Populate the hardware scan code too, even though wVk alone is enough for
        // SendInput to work - some games/anti-cheat validate it, and it lets the
        // low-level hook resolve a proper key name for synthetic events.
        ushort scanCode = (ushort)NativeMethods.MapVirtualKey((uint)virtualKeyCode, NativeMethods.MAPVK_VK_TO_VSC);

        var input = new INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new InputUnion
            {
                Keyboard = new KEYBDINPUT
                {
                    wVk = (ushort)virtualKeyCode,
                    wScan = scanCode,
                    dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0,
                    time = 0,
                    dwExtraInfo = nint.Zero,
                },
            },
        };

        Send(input);
    }

    /// <summary>
    /// Types literal text. Characters reachable via a real key + modifier combo on
    /// the current keyboard layout are sent as genuine key events (most reliable);
    /// anything else (e.g. emoji) falls back to raw Unicode key events.
    /// </summary>
    public static void SendText(string text)
    {
        foreach (char c in text)
            SendChar(c);
    }

    private static void SendChar(char c)
    {
        short vkScan = NativeMethods.VkKeyScan(c);
        if (vkScan == -1)
        {
            SendUnicodeChar(c, keyUp: false);
            SendUnicodeChar(c, keyUp: true);
            Thread.Sleep(1);
            return;
        }

        int virtualKeyCode = vkScan & 0xFF;
        int shiftState = (vkScan >> 8) & 0xFF;

        bool shift = (shiftState & 0x01) != 0;
        bool ctrl = (shiftState & 0x02) != 0;
        bool alt = (shiftState & 0x04) != 0;

        // Small pauses around modifier transitions: without them, the target app's
        // keyboard state can lag behind a same-instant SendInput burst and the
        // character is sent with the wrong (or no) modifier applied.
        if (shift) { SendKeyDown(VK_SHIFT); Thread.Sleep(1); }
        if (ctrl) { SendKeyDown(VK_CONTROL); Thread.Sleep(1); }
        if (alt) { SendKeyDown(VK_MENU); Thread.Sleep(1); }

        SendKeyPress(virtualKeyCode);

        if (alt) { Thread.Sleep(1); SendKeyUp(VK_MENU); }
        if (ctrl) { Thread.Sleep(1); SendKeyUp(VK_CONTROL); }
        if (shift) { Thread.Sleep(1); SendKeyUp(VK_SHIFT); }

        // Pause between characters too - without it, plain (unmodified) characters
        // are sent back-to-back with zero delay and the target app's input queue
        // can drop some of them on longer strings.
        Thread.Sleep(1);
    }

    private static void SendUnicodeChar(char c, bool keyUp)
    {
        var input = new INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new InputUnion
            {
                Keyboard = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE | (keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0),
                    time = 0,
                    dwExtraInfo = nint.Zero,
                },
            },
        };

        Send(input);
    }

    public static void SendMouseButtonDown(MouseButton button) => SendMouseButton(button, down: true);

    public static void SendMouseButtonUp(MouseButton button) => SendMouseButton(button, down: false);

    /// <summary>Sends a vertical wheel notch. Positive = up, negative = down (120 per notch).</summary>
    public static void SendMouseWheel(int delta) => SendMouseInput(NativeMethods.MOUSEEVENTF_WHEEL, unchecked((uint)delta));

    /// <summary>Sends a horizontal wheel notch. Positive = right, negative = left (120 per notch).</summary>
    public static void SendMouseHWheel(int delta) => SendMouseInput(NativeMethods.MOUSEEVENTF_HWHEEL, unchecked((uint)delta));

    private static void SendMouseButton(MouseButton button, bool down)
    {
        var (flag, mouseData) = button switch
        {
            MouseButton.Left => (down ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_LEFTUP, 0u),
            MouseButton.Right => (down ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_RIGHTUP, 0u),
            MouseButton.Middle => (down ? NativeMethods.MOUSEEVENTF_MIDDLEDOWN : NativeMethods.MOUSEEVENTF_MIDDLEUP, 0u),
            MouseButton.X1 => (down ? NativeMethods.MOUSEEVENTF_XDOWN : NativeMethods.MOUSEEVENTF_XUP, (uint)NativeMethods.XBUTTON1),
            MouseButton.X2 => (down ? NativeMethods.MOUSEEVENTF_XDOWN : NativeMethods.MOUSEEVENTF_XUP, (uint)NativeMethods.XBUTTON2),
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        };

        SendMouseInput(flag, mouseData);
    }

    private static void SendMouseInput(uint flags, uint mouseData)
    {
        var input = new INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new InputUnion
            {
                Mouse = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = mouseData,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = nint.Zero,
                },
            },
        };

        Send(input);
    }

    private static void Send(INPUT input)
    {
        var inputs = new[] { input };
        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
