using System.Windows;

using Microsoft.Extensions.Logging;

namespace DevToolkit.Abstractions;

/// <summary>
/// 宿主能力接口：插件通过 <see cref="IDevToolkitPlugin.Initialize"/> 获取宿主提供的基础能力。
/// 包括统一日志、插件专属数据目录、按插件命名空间读写 JSON 设置、JSON 编辑器与窗口管理。
/// </summary>
public interface IHostServices
{
    /// <summary>宿主全局数据根目录（%APPDATA%\DevToolkit）。</summary>
    string DataRoot { get; }

    /// <summary>WebView2 静态资源根目录（wwwroot）。</summary>
    string WwwRoot { get; }

    /// <summary>WebView2 缓存目录。</summary>
    string WebView2CacheDir { get; }

    /// <summary>创建指定类别的日志记录器（统一走宿主 Serilog）。</summary>
    ILogger CreateLogger(string categoryName);

    /// <summary>获取插件专属数据目录（DataRoot/plugins/&lt;pluginId&gt;，不存在时自动创建）。</summary>
    string GetPluginDataDirectory(string pluginId);

    /// <summary>
    /// 读取插件 JSON 设置文件。读取时兼容旧路径：DataRoot 根目录下同名文件优先回退，
    /// 避免升级丢失用户既有配置。
    /// </summary>
    T? LoadJsonSettings<T>(string pluginId, string fileName) where T : class, new();

    /// <summary>保存插件 JSON 设置文件到插件专属数据目录。</summary>
    void SaveJsonSettings<T>(string pluginId, string fileName, T settings) where T : class;

    /// <summary>打开宿主 JSON 编辑器窗口（单例复用）。</summary>
    void OpenJsonEditor(string json);

    /// <summary>显示窗口（窗口型插件导航，如密码管理器）。</summary>
    void ShowWindow(Window window);
}
