using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using MacroController.Core.Input;

namespace MacroController.App;

/// <summary>
/// Silently (re)installs the ViGEmBus virtual-controller driver that
/// <see cref="VirtualGamepadSender"/> depends on, so users never have to find/run it
/// themselves. Runs on every startup, but is cheap and a no-op once the driver is
/// actually reachable (<see cref="VirtualGamepadSender.IsDriverAvailable"/> is a real
/// connection attempt, not a registry-metadata guess - installer display strings vary
/// too much across versions/types to be worth trusting for this).
///
/// This can't distinguish "not installed" from "installed but an older version" -
/// ViGEmBus doesn't expose a reliable, version-agnostic way to check that without
/// touching the driver's .sys file directly. In practice that's fine: once the driver
/// answers at all, we leave it alone, so a future ViGEmBusSetup.exe bump only reaches
/// users who install fresh or explicitly re-run setup, not silently on every launch.
/// </summary>
internal static class DriverInstaller
{
    private const string InstallerFileName = "ViGEmBusSetup.exe";
    private const string DeclinedMarkerValueName = "ViGEmBusInstallDeclined";

    public static void EnsureInstalledAsync() => Task.Run(EnsureInstalled);

    private static void EnsureInstalled()
    {
        try
        {
            if (VirtualGamepadSender.IsDriverAvailable())
                return;

            if (WasDeclined())
                return;

            string? appDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (appDir is null)
                return;

            string installerPath = Path.Combine(appDir, InstallerFileName);
            if (!File.Exists(installerPath))
                return;

            var startInfo = new ProcessStartInfo(installerPath)
            {
                Arguments = "/quiet /norestart",
                UseShellExecute = true,
                Verb = "runas", // ViGEmBus's own installer needs admin; this elevates just this child process
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED - user declined the UAC prompt
        {
            RememberDeclined();
        }
        catch
        {
            // Best-effort only. If this fails for any other reason, controller macro
            // *output* stays unavailable (VirtualGamepadSender already fails closed
            // without crashing playback) but nothing else about the app is affected.
        }
    }

    /// <summary>Avoids re-prompting UAC on every single launch after the user dismisses
    /// it once. Cleared automatically the moment <see cref="VirtualGamepadSender.IsDriverAvailable"/>
    /// starts returning true (that check always runs first), so this only suppresses
    /// repeat prompts for someone who's actively chosen not to install it.</summary>
    private static bool WasDeclined()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\MacroController", writable: false);
        return key?.GetValue(DeclinedMarkerValueName) is not null;
    }

    private static void RememberDeclined()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\MacroController");
        key.SetValue(DeclinedMarkerValueName, 1);
    }
}
