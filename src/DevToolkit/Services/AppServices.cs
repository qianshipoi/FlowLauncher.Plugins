using System.IO;

using Microsoft.Extensions.Logging;

using Serilog;

namespace DevToolkit;

/// <summary>
/// 宿主基础设施：数据目录与日志初始化。各模块功能改由插件通过 <see cref="DevToolkit.Services.HostServices"/> 提供。
/// </summary>
public static class AppServices
{
    public static string DataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevToolkit");

    public static string LogsDir => Path.Combine(DataRoot, "Logs");

    public static string AppDir => AppContext.BaseDirectory;

    private static ILoggerFactory? _loggerFactory;

    public static ILoggerFactory LoggerFactory => _loggerFactory ??= Microsoft.Extensions.Logging.LoggerFactory.Create(
        builder => builder.AddSerilog(Log.Logger));

    /// <summary>初始化 Serilog 文件日志（应用启动时调用一次）。</summary>
    public static void InitializeLogging()
    {
        Directory.CreateDirectory(LogsDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(LogsDir, "devtoolkit-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("DevToolkit 启动");
    }

    /// <summary>关闭并刷新日志（应用退出时调用）。</summary>
    public static void ShutdownLogging()
    {
        Log.Information("DevToolkit 退出");
        Log.CloseAndFlush();
    }
}
