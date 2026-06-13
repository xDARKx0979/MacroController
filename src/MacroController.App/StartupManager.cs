using Microsoft.Win32;

namespace MacroController.App;

/// <summary>Registers/unregisters this app to launch at user login via the HKCU Run key.</summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MacroController";

    private static string ExePath => Environment.ProcessPath
        ?? throw new InvalidOperationException("Could not determine the executable path.");

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string existing && string.Equals(existing, ExePath, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
            key.SetValue(ValueName, ExePath);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
