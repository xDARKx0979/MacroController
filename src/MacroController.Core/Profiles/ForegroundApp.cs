using System.Diagnostics;
using MacroController.Core.Native;

namespace MacroController.Core.Profiles;

/// <summary>Looks up the process name owning the current foreground window.</summary>
public static class ForegroundApp
{
    /// <returns>The foreground process name (e.g. "notepad"), or null if it can't be determined.</returns>
    public static string? GetProcessName()
    {
        nint hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == nint.Zero)
            return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0)
            return null;

        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            // Process exited between GetForegroundWindow and GetProcessById.
            return null;
        }
    }
}
