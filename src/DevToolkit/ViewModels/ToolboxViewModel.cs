using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using DevToolkit.Toolbox;

using DevToolkit.Toolbox.Services;

using System.Collections.ObjectModel;

using System.IO;

namespace DevToolkit.ViewModels;

public enum ToolInputMode
{
    None,      // dt：无输入
    Optional,  // guid（名称可选）、pw（长度可选）
    Required,  // enc / base / ts / jwt / cron
    FilePath,  // fhash：文件路径
}

public sealed record ToolDefinition(string Command, string DisplayName, string InputPlaceholder, ToolInputMode InputMode);

/// <summary>工具箱页 VM：工具选择、执行分发与结果收集，复用 DevToolkit.Toolbox 核心服务。</summary>
public partial class ToolboxViewModel : ObservableObject
{
    public static IReadOnlyList<ToolDefinition> Catalog { get; } =
    [
        new("guid", "GUID 生成", "（可选）v3/v5 的名称", ToolInputMode.Optional),
        new("json", "JSON 编辑器", "粘贴 JSON 文本（留空则读剪贴板）", ToolInputMode.Required),
        new("fhash", "文件哈希", "选择或输入文件路径", ToolInputMode.FilePath),
        new("dt", "时间与时间戳", "无需输入，直接执行", ToolInputMode.None),
        new("enc", "编码转换", "输入要转换的文本", ToolInputMode.Required),
        new("pw", "密码生成", "（可选）长度，默认 24", ToolInputMode.Optional),
        new("base", "进制转换", "0x1F / 255 / 单个字符 / U+4E2D", ToolInputMode.Required),
        new("ts", "时间戳转日期", "输入秒或毫秒时间戳", ToolInputMode.Required),
        new("jwt", "JWT 解码", "粘贴 JWT", ToolInputMode.Required),
        new("cron", "Cron 解析", "如 0 9 * * 1-5", ToolInputMode.Required),
    ];

    public ObservableCollection<ToolDefinition> Tools { get; }

    public ObservableCollection<ToolRow> Results { get; } = [];

    [ObservableProperty]
    private ToolDefinition _selectedTool;

    [ObservableProperty]
    private string _input = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isInputEnabled = true;

    [ObservableProperty]
    private bool _filePickerVisible;

    /// <summary>json 工具请求打开编辑器窗口，由 View 订阅实现。</summary>
    public event Action<string>? JsonEditorRequested;

    public ToolboxViewModel(Settings settings)
    {
        _settings = settings;
        Tools = [.. Catalog.Where(t => settings.IsEnabled(t.Command))];
        _selectedTool = Tools[0];
    }

    private readonly Settings _settings;

    partial void OnSelectedToolChanged(ToolDefinition value)
    {
        Results.Clear();
        StatusMessage = string.Empty;
        IsInputEnabled = value.InputMode != ToolInputMode.None;
        FilePickerVisible = value.InputMode == ToolInputMode.FilePath;
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择要计算哈希的文件" };
        if (dialog.ShowDialog() == true)
            Input = dialog.FileName;
    }

    [RelayCommand]
    private void CopyResult(ToolRow row)
    {
        try
        {
            System.Windows.Clipboard.SetText(row.Value);
            StatusMessage = $"已复制：{row.Description}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"复制失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        Results.Clear();
        StatusMessage = string.Empty;

        try
        {
            IReadOnlyList<ToolRow> rows;
            switch (SelectedTool.Command)
            {
                case "guid":
                    rows = BuildGuidRows(Input.Trim());
                    break;

                case "json":
                    RunJson();
                    return;

                case "fhash":
                    rows = [.. (await RunFhashAsync(Input.Trim()))
                        .Select(hash => new ToolRow(hash.Algorithm, hash.HashHex))];
                    break;

                case "dt":
                    rows = [.. TimestampProvider.GetEntries(DateTimeOffset.Now)
                        .Select(entry => new ToolRow(entry.Description, entry.Value))];
                    break;

                case "enc":
                    rows = TextEncoderService.GetTransforms(Input);
                    break;

                case "pw":
                    rows = BuildPasswordRows(Input.Trim());
                    break;

                case "base":
                    rows = BaseConverterService.Convert(Input);
                    break;

                case "ts":
                    rows = TimestampParser.Parse(Input, DateTimeOffset.Now);
                    break;

                case "jwt":
                    rows = JwtDecoderService.Decode(Input, DateTimeOffset.Now);
                    break;

                case "cron":
                    rows = CronParserService.GetInfo(Input, DateTime.Now);
                    break;

                default:
                    StatusMessage = "未知工具";
                    return;
            }

            foreach (var row in rows)
                Results.Add(row);
            StatusMessage = $"完成，共 {rows.Count} 条结果";
        }
        catch (FormatException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"执行失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static IReadOnlyList<ToolRow> BuildGuidRows(string name)
    {
        var results = new List<ToolRow>
        {
            new("GUID v4（随机）", GuidGenerator.NewV4().ToString()),
            new("GUID v7（时间有序）", GuidGenerator.NewV7().ToString()),
            new("GUID v1（时间戳）", GuidGenerator.NewV1().ToString()),
        };

        if (!string.IsNullOrWhiteSpace(name))
        {
            results.Add(new ToolRow($"GUID v3（MD5 · DNS: {name}）", GuidGenerator.NewV3(GuidGenerator.DnsNamespace, name).ToString()));
            results.Add(new ToolRow($"GUID v5（SHA1 · DNS: {name}）", GuidGenerator.NewV5(GuidGenerator.DnsNamespace, name).ToString()));
        }

        return results;
    }

    private static IReadOnlyList<ToolRow> BuildPasswordRows(string lengthText)
    {
        var length = PasswordGeneratorService.DefaultLength;
        if (!string.IsNullOrWhiteSpace(lengthText)
            && (!int.TryParse(lengthText, out length) || length <= 0 || length > PasswordGeneratorService.MaxLength))
            throw new FormatException($"长度需为 1-{PasswordGeneratorService.MaxLength}");
        return PasswordGeneratorService.Generate(length);
    }

    private static async Task<IReadOnlyList<HashResult>> RunFhashAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new FormatException("请先选择或输入文件路径");
        if (!File.Exists(path))
            throw new FormatException($"文件不存在：{path}");
        return await Task.Run(() => HashCalculator.ComputeFileHashes(path));
    }

    private void RunJson()
    {
        var text = Input;
        var fromClipboard = false;
        if (string.IsNullOrWhiteSpace(text))
        {
            try
            {
                text = System.Windows.Clipboard.GetText();
                fromClipboard = !string.IsNullOrWhiteSpace(text);
            }
            catch
            {
                text = string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            StatusMessage = "请先粘贴 JSON，或复制 JSON 后留空执行";
            return;
        }

        JsonEditorRequested?.Invoke(text);
        StatusMessage = $"已打开 JSON 编辑器（{(fromClipboard ? "内容来自剪贴板，" : string.Empty)}{text.Length} 字符）";
    }
}
