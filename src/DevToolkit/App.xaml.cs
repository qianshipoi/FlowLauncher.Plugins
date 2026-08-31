using System.Windows;
using Serilog;

namespace DevToolkit;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDir = System.IO.Path.Combine(AppServices.DataRoot, "Logs");
        System.IO.Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                System.IO.Path.Combine(logDir, "devtoolkit-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("DevToolkit 启动");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("DevToolkit 退出");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
