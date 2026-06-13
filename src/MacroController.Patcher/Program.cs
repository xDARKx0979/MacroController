using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

// MacroController.Patcher.exe - launched by MacroController.App.exe right before it exits
// to install an update. Waits for the app to exit, copies update_temp/ over the live
// install, cleans up, then relaunches the app.

internal static class Program
{
    private static UpdaterForm? _form;

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        _form = new UpdaterForm();
        _form.Show();
        Application.DoEvents();

        var thread = new Thread(DoUpdate) { IsBackground = false };
        thread.Start();

        Application.Run(_form);
    }

    private static void SetStatus(string text) =>
        _form?.Invoke(new Action(() => _form.SetStatus(text)));

    private static void SetProgress(int percent) =>
        _form?.Invoke(new Action(() => _form.SetProgress(percent)));

    private static void SetIndeterminate() =>
        _form?.Invoke(new Action(() => _form.SetIndeterminate()));

    private static void DoUpdate()
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var appExe = Path.Combine(exeDir, "MacroController.App.exe");
        var tempDir = Path.Combine(exeDir, "update_temp");

        SetIndeterminate();
        SetStatus("Waiting for MacroController to close...");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var processes = Process.GetProcessesByName("MacroController.App");
            if (processes.Length == 0)
                break;

            foreach (var process in processes)
                try { process.WaitForExit(2000); } catch { }

            Thread.Sleep(300);
        }

        SetStatus("Installing update...");
        if (Directory.Exists(tempDir))
        {
            var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
            SetProgress(0);

            for (int i = 0; i < files.Length; i++)
            {
                var source = files[i];
                var relative = source[tempDir.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destination = Path.Combine(exeDir, relative);

                var destinationDir = Path.GetDirectoryName(destination);
                if (destinationDir is not null)
                    Directory.CreateDirectory(destinationDir);

                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        File.Copy(source, destination, overwrite: true);
                        break;
                    }
                    catch
                    {
                        if (attempt == 4)
                            break; // skip files that can't be replaced (e.g. this patcher's own exe)

                        Thread.Sleep(500);
                    }
                }

                SetProgress((i + 1) * 100 / files.Length);
            }

            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }

        var zipPath = Path.Combine(exeDir, "update.zip");
        try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }

        SetIndeterminate();
        SetStatus("Launching MacroController...");
        Thread.Sleep(600);

        if (File.Exists(appExe))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = appExe,
                    WorkingDirectory = exeDir,
                    UseShellExecute = true,
                });
            }
            catch
            {
                // If relaunch fails, the user can still start the app manually.
            }
        }

        _form?.Invoke(new Action(() => _form.Close()));
    }
}

internal sealed class UpdaterForm : Form
{
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;

    public UpdaterForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(24, 26, 32);
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(340, 120);
        TopMost = true;

        var path = new GraphicsPath();
        path.AddArc(0, 0, 20, 20, 180, 90);
        path.AddArc(Width - 20, 0, 20, 20, 270, 90);
        path.AddArc(Width - 20, Height - 20, 20, 20, 0, 90);
        path.AddArc(0, Height - 20, 20, 20, 90, 90);
        path.CloseFigure();
        Region = new Region(path);

        var titleLabel = new Label
        {
            Text = "MacroController",
            ForeColor = Color.FromArgb(124, 92, 252),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 18),
        };

        _statusLabel = new Label
        {
            Text = "Preparing update...",
            ForeColor = Color.FromArgb(200, 200, 210),
            Font = new Font("Segoe UI", 9f),
            AutoSize = false,
            Size = new Size(300, 20),
            Location = new Point(20, 52),
        };

        _progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Size = new Size(300, 6),
            Location = new Point(20, 80),
        };

        var accent = new Panel
        {
            BackColor = Color.FromArgb(79, 142, 247),
            Size = new Size(Width, 3),
            Location = new Point(0, Height - 3),
        };

        Controls.Add(titleLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_progressBar);
        Controls.Add(accent);

        MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
            }
        };
    }

    public void SetStatus(string text)
    {
        if (_statusLabel.InvokeRequired)
            _statusLabel.Invoke(new Action(() => _statusLabel.Text = text));
        else
            _statusLabel.Text = text;
    }

    public void SetProgress(int percent)
    {
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Value = Math.Clamp(percent, 0, 100);
    }

    public void SetIndeterminate()
    {
        _progressBar.Style = ProgressBarStyle.Marquee;
    }
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
