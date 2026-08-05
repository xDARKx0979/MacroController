using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace MacroController.App;

/// <summary>
/// Silently (re)installs the ViGEmBus virtual-controller driver that
/// <see cref="MacroController.Core.Input.VirtualGamepadSender"/> depends on, so users
/// never have to find/run it themselves. Runs on every startup - cheap when the
/// installed driver is already current (a couple of registry reads), and picks up
/// driver updates automatically whenever an app update bundles a newer
/// ViGEmBusSetup.exe and bumps <see cref="RequiredVersion"/> to match.
/// </summary>
internal static class DriverInstaller
{
    // Keep this in sync with build/vendor/ViGEmBusSetup.exe's actual version - bump
    // both together whenever the bundled installer is updated.
    private const string RequiredVersion = "1.22.0";
    private const string InstallerFileName = "ViGEmBusSetup.exe";
    private const string DeclinedVersionValueName = "ViGEmBusDeclinedVersion";

    public static void EnsureInstalledAsync() => Task.Run(EnsureInstalled);

    private static void EnsureInstalled()
    {
        try
        {
            if (IsUpToDate())
                return;

            if (WasDeclinedForCurrentVersion())
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

    private static bool IsUpToDate() =>
        GetInstalledVersion() is { } installed && CompareVersions(installed, RequiredVersion) >= 0;

    private static string? GetInstalledVersion()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var uninstallKey = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey is null)
                continue;

            foreach (string subKeyName in uninstallKey.GetSubKeyNames())
            {
                using var subKey = uninstallKey.OpenSubKey(subKeyName);
                if (subKey?.GetValue("DisplayName") is not string displayName)
                    continue;

                if (displayName.Contains("ViGEmBus", StringComparison.OrdinalIgnoreCase))
                    return subKey.GetValue("DisplayVersion") as string;
            }
        }

        return null;
    }

    private static int CompareVersions(string a, string b) =>
        Version.TryParse(a, out var va) && Version.TryParse(b, out var vb)
            ? va.CompareTo(vb)
            : string.CompareOrdinal(a, b);

    /// <summary>Avoids re-prompting UAC on every single launch after the user dismisses
    /// it once for a given driver version. A future app update that bumps
    /// <see cref="RequiredVersion"/> clears this (the stored value won't match the new
    /// version), so it tries again exactly once per driver update.</summary>
    private static bool WasDeclinedForCurrentVersion()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\MacroController", writable: false);
        return key?.GetValue(DeclinedVersionValueName) as string == RequiredVersion;
    }

    private static void RememberDeclined()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\MacroController");
        key.SetValue(DeclinedVersionValueName, RequiredVersion);
    }
}
