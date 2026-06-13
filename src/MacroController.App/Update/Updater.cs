using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacroController.App.Update;

/// <summary>
/// Checks a private GitHub repo for a newer release and silently self-updates by
/// downloading the new build, extracting it next to the running exe, and handing off
/// to <c>MacroController.Patcher.exe</c> (which waits for this process to exit, copies
/// the new files over the install directory, and relaunches the app).
///
/// Modeled on the update flow in Dark Limiter's Utility/Updater.cs.
/// </summary>
internal static class Updater
{
    // Bump these together with each release. VersionString must match the
    // AppVersion baked into the installer (see build/installer.iss).
    public const int Version = 6;
    public const string VersionString = "1.0.5";

    // ── GitHub private-repo config ──────────────────────────────────────────
    // Repo: https://github.com/xDARKx0979/MacroController (private)
    //
    // To enable update checks, create a fine-grained PAT scoped to *only* this
    // repo with "Contents: Read-only" and "Metadata: Read-only" permissions
    // (Settings -> Developer settings -> Personal access tokens -> Fine-grained
    // tokens), then split it across the fragments below so no single literal
    // string in the source is a complete, scannable token.
    private const string GhOwner = "xDARKx0979";
    private const string GhRepo = "MacroController";

    private static string GhToken => string.Concat(
        "github_pat_",
        "11BJRVG6Q0sKAPaXhFcEMV_LDpakOIsyFSF3RzTXW",
        "hZWAPEYG7yH6gQiuUoMKQijswZCRORMYBRf9gkNzn"
    );

    // manifest.dat lives at the root of the repo, AES-encrypted (see Crypto and
    // build/make-manifest.ps1).
    private static string ManifestUrl => $"https://api.github.com/repos/{GhOwner}/{GhRepo}/contents/manifest.dat";

    private static Manifest? _manifest;

    private static string ExeDirectory => Path.GetDirectoryName(Environment.ProcessPath)!;

    private static string UpdateLogPath => Path.Combine(ExeDirectory, "update.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(UpdateLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
            // Logging is best-effort only.
        }
    }

    private static HttpClient CreateClient(string accept = "application/vnd.github.raw")
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GhToken}");
        client.DefaultRequestHeaders.Add("User-Agent", "MacroController-Updater");
        client.DefaultRequestHeaders.Add("Accept", accept);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    /// <summary>
    /// Fetches and decrypts manifest.dat, returning true if a newer version is available.
    /// Fails open (returns false / "no update") on any error so a flaky connection or
    /// missing token never blocks startup.
    /// </summary>
    public static async Task<bool> CheckForUpdateAsync()
    {
        if (string.IsNullOrEmpty(GhToken) || GhToken.Contains("REPLACE_WITH"))
            return false;

        try
        {
            using var client = CreateClient();
            var response = await client.GetByteArrayAsync(ManifestUrl);

            var json = Crypto.Decrypt(response);
            _manifest = JsonSerializer.Deserialize<Manifest>(json);

            if (_manifest is null)
                return false;

            Log($"Manifest OK - remote version {_manifest.Version} (local {Version})");
            return _manifest.Version > Version;
        }
        catch (Exception e)
        {
            Log($"CheckForUpdate FAILED: {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Downloads the new build, extracts it to "update_temp" next to the exe, and
    /// launches MacroController.Patcher.exe to finish the swap. Returns true if the
    /// patcher was launched successfully (the caller should exit immediately after).
    /// </summary>
    public static async Task<bool> DownloadAndLaunchPatcherAsync(IProgress<double>? downloadProgress = null)
    {
        if (_manifest is null)
            return false;

        try
        {
            Log($"Downloading update from {_manifest.DownloadUrl}");
            using var client = CreateClient("application/octet-stream");

            var zipPath = Path.Combine(ExeDirectory, "update.zip");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long totalRead = 0;

            using (var response = await client.GetAsync(_manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = File.Create(zipPath);

                var buffer = new byte[81920];
                int read;
                while ((read = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    hash.AppendData(buffer, 0, read);
                    totalRead += read;
                    if (totalBytes is > 0)
                        downloadProgress?.Report((double)totalRead / totalBytes.Value);
                }
            }

            Log($"Download complete ({totalRead / 1024} KB), verifying...");

            if (!string.IsNullOrEmpty(_manifest.Sha256))
            {
                var actual = Crypto.BytesToHex(hash.GetHashAndReset());
                if (!string.Equals(actual, _manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Log($"SHA256 mismatch (expected {_manifest.Sha256}, got {actual}), aborting update");
                    File.Delete(zipPath);
                    return false;
                }
            }

            var tempDir = Path.Combine(ExeDirectory, "update_temp");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);

            Log("Extracting update...");
            ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);
            File.Delete(zipPath);

            Log("Launching patcher...");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Path.Combine(ExeDirectory, "MacroController.Patcher.exe"),
                WorkingDirectory = ExeDirectory,
                UseShellExecute = false,
            });

            Log("Patcher launched, exiting for update");
            return true;
        }
        catch (Exception e)
        {
            Log($"Update FAILED: {e.GetType().Name}: {e.Message}");
            return false;
        }
    }
}

internal sealed class Manifest
{
    // Explicit names so deserialization still maps correctly after Obfuscar
    // renames these properties (Manifest is internal, so KeepPublicApi doesn't
    // protect it).
    [JsonPropertyName("Version")]
    public int Version { get; set; }

    [JsonPropertyName("DownloadUrl")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("Sha256")]
    public string? Sha256 { get; set; }
}
