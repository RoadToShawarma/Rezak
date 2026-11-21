using System.Windows;

namespace PosterMaster;

public partial class App : Application
{
    private SingleInstance? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new SingleInstance();
        if (!_singleInstance.StartOrActivate())
        {
            Shutdown();
            return;
        }

        var window = new MainWindow();
        _singleInstance.Register(window);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
