using System.IO;

namespace FlowLauncher.Plugin.PaperTodo.Services;

internal sealed class AppPaths
{
    public AppPaths(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FlowLauncher",
            "Settings",
            "Plugins",
            "PaperTodo");
        BackupsDirectory = Path.Combine(DataDirectory, "Backups");
        WebViewDataDirectory = Path.Combine(DataDirectory, "WebView2");
        DatabasePath = Path.Combine(DataDirectory, "papertodo.db");
    }

    public string DataDirectory { get; }
    public string BackupsDirectory { get; }
    public string WebViewDataDirectory { get; }
    public string DatabasePath { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(WebViewDataDirectory);
    }
}
