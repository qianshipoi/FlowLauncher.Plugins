using System.Windows;
using System.Windows.Threading;

using DevToolkit.Services;

using Serilog;

namespace DevToolkit;

public partial class App : Application
{
    private HostServices? _hostServices;
    private PluginManager? _pluginManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppServices.InitializeLogging();

        // 全局异常捕获：任何未处理异常记录到日志，避免静默崩溃
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "AppDomain 未处理异常");

        base.OnStartup(e);

        _hostServices = new HostServices(AppServices.DataRoot, AppServices.LoggerFactory);
        _pluginManager = new PluginManager(_hostServices, BuiltInPlugins.Create());

        var mainWindow = new MainWindow(_pluginManager, _hostServices);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "UI 线程未处理异常");
        e.Handled = true; // 记录后继续运行，避免闪退
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pluginManager?.Dispose();
        AppServices.ShutdownLogging();
        base.OnExit(e);
    }
}
