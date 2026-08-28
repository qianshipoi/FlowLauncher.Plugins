using System.IO;

using System.Text.Json;

using DevToolkit.Toolbox;

namespace DevToolkit;

/// <summary>App 级共享服务：数据目录、Toolbox 设置持久化、WebView2 缓存路径。</summary>
public static class AppServices
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevToolkit");

    public static string WebView2CacheDir => Path.Combine(DataRoot, "WebView2Cache");

    public static string WwwRoot => Path.Combine(AppContext.BaseDirectory, "wwwroot");

    public static Settings ToolboxSettings { get; private set; } = LoadToolboxSettings();

    public static void SaveToolboxSettings()
    {
        Directory.CreateDirectory(DataRoot);
        File.WriteAllText(
            Path.Combine(DataRoot, "toolbox-settings.json"),
            JsonSerializer.Serialize(ToolboxSettings, JsonOptions));
    }

    private static Settings LoadToolboxSettings()
    {
        try
        {
            var path = Path.Combine(DataRoot, "toolbox-settings.json");
            if (File.Exists(path))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings();
        }
        catch
        {
            // 设置文件损坏时回退默认值
        }
        return new Settings();
    }
}
