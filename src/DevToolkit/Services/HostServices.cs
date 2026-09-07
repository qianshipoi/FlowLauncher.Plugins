using System.IO;
using System.Text.Json;
using System.Windows;

using DevToolkit.Abstractions;
using DevToolkit.Core.Wpf;

using Microsoft.Extensions.Logging;

namespace DevToolkit.Services;

/// <summary>
/// 宿主能力服务：为插件提供数据目录、日志、设置持久化、JSON 编辑器与窗口管理。
/// 实现 <see cref="IHostServices"/> 契约，由 <see cref="PluginManager"/> 注入各插件。
/// </summary>
public sealed class HostServices : IHostServices
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private JsonEditorWindow? _jsonEditorWindow;

    public string DataRoot { get; }

    public string WwwRoot { get; }

    public string WebView2CacheDir { get; }

    public HostServices(string dataRoot, ILoggerFactory loggerFactory)
    {
        DataRoot = dataRoot;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(nameof(HostServices));
        WwwRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        WebView2CacheDir = Path.Combine(dataRoot, "WebView2Cache");
    }

    public ILogger CreateLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);

    public string GetPluginDataDirectory(string pluginId)
    {
        var dir = Path.Combine(DataRoot, "plugins", pluginId);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public T? LoadJsonSettings<T>(string pluginId, string fileName) where T : class, new()
    {
        // 兼容旧路径：升级前各模块设置存于 DataRoot 根目录，同名旧文件优先回退，避免丢失用户配置
        var legacyPath = Path.Combine(DataRoot, fileName);
        var pluginPath = Path.Combine(DataRoot, "plugins", pluginId, fileName);
        try
        {
            var path = File.Exists(pluginPath) ? pluginPath : File.Exists(legacyPath) ? legacyPath : null;
            if (path is not null)
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取设置失败: {File}", fileName);
        }
        return null;
    }

    public void SaveJsonSettings<T>(string pluginId, string fileName, T settings) where T : class
    {
        var dir = Path.Combine(DataRoot, "plugins", pluginId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), JsonSerializer.Serialize(settings, JsonOptions));
    }

    public void OpenJsonEditor(string json)
    {
        if (_jsonEditorWindow is null)
        {
            _jsonEditorWindow = new JsonEditorWindow(WwwRoot, WebView2CacheDir, json);
            _jsonEditorWindow.Closed += (_, _) => _jsonEditorWindow = null;
            _jsonEditorWindow.Show();
        }
        else
        {
            _jsonEditorWindow.Activate();
            _jsonEditorWindow.UpdateJson(json);
        }
    }

    public void ShowWindow(Window window) => window.Show();
}
