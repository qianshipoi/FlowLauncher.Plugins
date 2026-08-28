using System.Diagnostics;

using System.IO;

namespace DevToolkit.OpenDirectory;

/// <summary>路径规范化与资源管理器打开逻辑，宿主无关。</summary>
public static class PathService
{
    /// <summary>
    /// 规范化路径：将连续反斜杠合并为单个，同时保留 UNC 路径前缀。
    /// </summary>
    public static string Normalize(string raw)
    {
        var path = raw.Replace("\\\\", "\\");

        if (raw.StartsWith("\\\\") || raw.StartsWith("//"))
        {
            path = @"\\" + path.TrimStart('\\', '/');
        }

        return path;
    }

    /// <summary>在资源管理器中打开目录；文件路径则打开所在目录并选中文件。</summary>
    public static void OpenInExplorer(string path, string dirToOpen)
    {
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true
            });
        }
        else
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dirToOpen}\"")
            {
                UseShellExecute = true
            });
        }
    }
}
