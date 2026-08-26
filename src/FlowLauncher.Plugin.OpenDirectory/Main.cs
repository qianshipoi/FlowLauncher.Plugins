using Flow.Launcher.Plugin;

using System.Diagnostics;
using System.IO;
using System.Windows;

namespace FlowLauncher.Plugin.OpenDirectory;

public class Main : IAsyncPlugin
{
    private PluginInitContext _context = default!;

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        var raw = query.Search?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Task.FromResult(new List<Result>
            {
                new Result
                {
                    Title = "输入路径打开所在目录",
                    SubTitle = @"例如: C:\Users\test\file.txt 或 \\server\share\folder",
                    IcoPath = "favicon.ico",
                    Action = _ => false
                }
            });
        }

        var normalized = NormalizePath(raw);

        if (!Path.IsPathRooted(normalized))
        {
            return Task.FromResult(new List<Result>
            {
                new Result
                {
                    Title = "路径无效",
                    SubTitle = "请输入完整的绝对路径",
                    IcoPath = "favicon.ico",
                    Action = _ => true
                }
            });
        }

        var dirToOpen = Directory.Exists(normalized) ? normalized : Path.GetDirectoryName(normalized);
        var isFile = File.Exists(normalized);
        var backslash = normalized;
        var forwardSlash = normalized.Replace('\\', '/');

        var results = new List<Result>();

        // 打开目录
        if (!string.IsNullOrEmpty(dirToOpen))
        {
            results.Add(new Result
            {
                Title = $"打开目录: {dirToOpen}",
                SubTitle = isFile ? $"文件: {Path.GetFileName(normalized)}" : "直接打开此目录",
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    OpenDirectory(normalized, dirToOpen);
                    return true;
                }
            });
        }

        // 复制规范化路径
        results.Add(new Result
        {
            Title = "复制路径",
            SubTitle = backslash,
            IcoPath = "favicon.ico",
            Action = _ =>
            {
                Clipboard.SetText(backslash);
                _context.API.ShowMsg("已复制到剪切板");
                return true;
            }
        });

        // 反斜杠 → 正斜杠
        results.Add(new Result
        {
            Title = "转为正斜杠 (/) 并复制",
            SubTitle = forwardSlash,
            IcoPath = "favicon.ico",
            Action = _ =>
            {
                Clipboard.SetText(forwardSlash);
                _context.API.ShowMsg("已复制到剪切板");
                return true;
            }
        });

        // 正斜杠 → 反斜杠
        if (raw.Contains('/'))
        {
            results.Add(new Result
            {
                Title = "转为反斜杠 (\\) 并复制",
                SubTitle = backslash,
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    Clipboard.SetText(backslash);
                    _context.API.ShowMsg("已复制到剪切板");
                    return true;
                }
            });
        }

        return Task.FromResult(results);
    }

    /// <summary>
    /// 规范化路径：将连续反斜杠合并为单个，同时保留 UNC 路径前缀
    /// </summary>
    private static string NormalizePath(string raw)
    {
        var path = raw.Replace("\\\\", "\\");

        if (raw.StartsWith("\\\\") || raw.StartsWith("//"))
        {
            path = @"\\" + path.TrimStart('\\', '/');
        }

        return path;
    }

    private static void OpenDirectory(string path, string dirToOpen)
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
