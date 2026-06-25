using System.Threading;
using System.Windows;

namespace CodexStatus.Tray;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private TrayApplicationController? _controller;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _mutex = new Mutex(initiallyOwned: true, @"Local\CodexStatus.Tray", out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _controller = new TrayApplicationController();
        _controller.Start();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _controller?.Dispose();
        if (_ownsMutex)
        {
            _mutex?.ReleaseMutex();
        }

        _mutex?.Dispose();
    }
}
