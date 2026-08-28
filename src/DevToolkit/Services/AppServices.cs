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

    public static AiChat.Settings AiChatSettings { get; private set; } =
        LoadJson<AiChat.Settings>("aichat-settings.json") ?? new AiChat.Settings();

    public static void SaveToolboxSettings() => SaveJson("toolbox-settings.json", ToolboxSettings);

    public static void SaveAiChatSettings() => SaveJson("aichat-settings.json", AiChatSettings);

    private static Settings LoadToolboxSettings() =>
        LoadJson<Settings>("toolbox-settings.json") ?? new Settings();

    private static T? LoadJson<T>(string fileName)
    {
        try
        {
            var path = Path.Combine(DataRoot, fileName);
            if (File.Exists(path))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch
        {
            // 设置文件损坏时回退默认值
        }
        return default;
    }

    private static void SaveJson<T>(string fileName, T value)
    {
        Directory.CreateDirectory(DataRoot);
        File.WriteAllText(Path.Combine(DataRoot, fileName), JsonSerializer.Serialize(value, JsonOptions));
    }
}
