using System.Configuration;
using System.Data;
using System.Threading;
using System.Windows;

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

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }
}

