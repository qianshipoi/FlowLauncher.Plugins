using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using System.IO;

using DevToolkit.OpenDirectory;

using DevToolkit.Toolbox.Services;

using System.Collections.ObjectModel;

namespace DevToolkit.ViewModels;

/// <summary>打开目录页 VM：路径规范化 + 各变体结果行（ToolRow 为复制语义，打开动作特殊处理）。</summary>
public partial class OpenDirectoryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _inputPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<ToolRow> Results { get; } = [];

    /// <summary>需要"打开"而非"复制"的行，其 Value 为规范化路径。</summary>
    public const string OpenActionDescription = "打开目录";

    [RelayCommand]
    private void Run()
    {
        Results.Clear();
        StatusMessage = string.Empty;

        var raw = InputPath.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            StatusMessage = "请输入文件或目录的绝对路径";
            return;
        }

        var normalized = PathService.Normalize(raw);
        if (!System.IO.Path.IsPathRooted(normalized))
        {
            StatusMessage = "请输入完整的绝对路径";
            return;
        }

        var dirToOpen = Directory.Exists(normalized) ? normalized : System.IO.Path.GetDirectoryName(normalized);
        var isFile = File.Exists(normalized);
        var backslash = normalized;
        var forwardSlash = normalized.Replace('\\', '/');

        if (!string.IsNullOrEmpty(dirToOpen))
        {
            Results.Add(new ToolRow(OpenActionDescription, dirToOpen));
        }

        Results.Add(new ToolRow("复制路径（反斜杠）", backslash));
        Results.Add(new ToolRow("复制路径（正斜杠）", forwardSlash));

        if (raw.Contains('/'))
            Results.Add(new ToolRow("复制路径（反斜杠，原始输入转换）", backslash));

        StatusMessage = isFile ? $"文件：{System.IO.Path.GetFileName(normalized)}" : "目录";
    }
}
