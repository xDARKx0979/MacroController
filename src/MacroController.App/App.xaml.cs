using System.Configuration;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MacroController.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string MutexName = "MacroController-SingleInstance";
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Without this, launching the app while an instance is already running
        // (e.g. it auto-started and the user double-clicks the shortcut again)
        // starts a second process. That second instance can trigger an update
        // and shut itself down, but the first instance keeps the exe/dlls
        // locked, so the patcher waits (and eventually force-kills it) -
        // appearing "stuck".
        _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        // The app installs low-level keyboard/mouse hooks and runs background macro
        // playback tasks; without these handlers, any unhandled exception on the UI
        // thread (e.g. in an async void event handler's post-await continuation) or
        // an unobserved exception from a fire-and-forget Task takes down the entire
        // app with no diagnostics.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash("AppDomain", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogCrash("UnobservedTask", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash("Dispatcher", e.Exception);

        // Keep the app alive rather than crashing - the operation that triggered this
        // (e.g. capturing a bind) simply fails, instead of taking down the whole app.
        e.Handled = true;
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex}\n");
        }
        catch
        {
            // Logging is best-effort only.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }
}

