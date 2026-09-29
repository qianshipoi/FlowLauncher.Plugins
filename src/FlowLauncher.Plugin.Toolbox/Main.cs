using FlowLauncher.Plugin.Toolbox;

using FlowLauncher.Plugin.Toolbox;

using FlowLauncher.Plugin.Toolbox.Services;

using FlowLauncher.Plugin.Toolbox.ViewModels;

using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.Toolbox.Views;

using System.IO;

using System.Text.Json;

using System.Windows;

namespace FlowLauncher.Plugin.Toolbox;

public class Main : IAsyncPlugin, IContextMenu, ISettingProvider
{
    private PluginInitContext _context = default!;
    private string _pluginDirectory = default!;
    private Settings _settings = default!;

    private JsonEditorWindow? _jsonEditorWindow;

    private readonly Lock _hashCacheLock = new();
    private (string FilePath, IReadOnlyList<HashResult> Hashes)? _lastHashResult;

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        return Task.CompletedTask;
    }

    public System.Windows.Controls.Control CreateSettingPanel()
    {
        var viewModel = new SettingsViewModel(_settings, () => _context.API.SaveSettingJsonStorage<Settings>());
        return new SettingsView(viewModel);
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        var menus = new List<Result>();

        if (selectedResult?.ContextData is Guid guid)
        {
            menus.Add(CopyMenu("复制大写", guid.ToString("D").ToUpperInvariant()));
            menus.Add(CopyMenu("复制小写无连字符", guid.ToString("N")));
            menus.Add(CopyMenu("复制花括号格式", guid.ToString("B").ToUpperInvariant()));
        }
        else if (selectedResult?.ContextData is ToolRow { Description: "Payload" } payload && _settings.EnableJson)
        {
            menus.Add(new Result
            {
                Title = "在 JSON 编辑器中打开",
                SubTitle = "用 tool json 的编辑器查看、编辑、格式化 Payload",
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    OpenJsonEditor(payload.Value);
                    return true;
                }
            });
        }

        return menus;
    }

    private Result CopyMenu(string title, string value) => new()
    {
        Title = title,
        SubTitle = value,
        IcoPath = "favicon.ico",
        Action = _ =>
        {
            CopyToClipboard(value);
            return true;
        }
    };

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        var search = query.Search?.Trim() ?? string.Empty;
        var parts = search.Split(' ', 2);
        var command = parts[0].ToLowerInvariant();
        var argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        if (Settings.KnownCommands.Contains(command) && !_settings.IsEnabled(command))
            return DisabledHint(command);

        return command switch
        {
            "guid" => HandleGuid(argument),
            "json" => HandleJson(argument),
            "fhash" => await HandleFileHashAsync(argument, query.ActionKeyword, token),
            "dt" => HandleDateTime(),
            "enc" => HandleEnc(argument),
            "pw" => HandlePw(argument),
            "base" => HandleBase(argument),
            "ts" => HandleTs(argument),
            "jwt" => HandleJwt(argument),
            "cron" => HandleCron(argument),
            _ => BuildHelp(command),
        };
    }

    private static readonly (string Command, string Description)[] ToolCatalog =
    [
        ("guid", "生成 GUID（v1/v3/v4/v5/v7），右键菜单支持多种格式"),
        ("json", "在编辑器窗口中查看、编辑、格式化 JSON"),
        ("fhash", "计算文件哈希：MD5/SHA1/SHA256/SHA384/SHA512/CRC32"),
        ("dt", "当前时间与时间戳，Enter 复制"),
        ("enc", "文本编码转换：URL / Base64 / HTML 实体 / 十六进制"),
        ("pw", "生成随机密码与密钥，如 tool pw 32"),
        ("base", "进制转换与字符码点互查，如 tool base 0xFF"),
        ("ts", "时间戳转日期，自动识别秒/毫秒"),
        ("jwt", "解码 JWT：Header/Payload/过期时间"),
        ("cron", "解析 Cron 表达式并预览未来执行时间"),
    ];

    private List<Result> BuildHelp(string unknownCommand = "")
    {
        var results = new List<Result>();

        if (!string.IsNullOrWhiteSpace(unknownCommand))
            results.Add(Hint($"未知命令 \"{unknownCommand}\"，可用命令见下（输入 tool 回车）"));

        var enabledTools = ToolCatalog.Where(tool => _settings.IsEnabled(tool.Command)).ToArray();
        if (enabledTools.Length == 0)
        {
            results.Add(Hint("所有工具均已在插件设置中禁用，请在 Flow Launcher 设置 → 插件 → Toolbox 中开启"));
            return results;
        }

        foreach (var (command, description) in enabledTools)
            results.Add(CommandRow(command, description));
        return results;
    }

    private static List<Result> DisabledHint(string command) =>
    [
        new Result
        {
            Title = $"\"tool {command}\" 已在插件设置中禁用",
            SubTitle = "在 Flow Launcher 设置 → 插件 → Toolbox 中可重新开启",
            IcoPath = "favicon.ico",
            Action = _ => false
        }
    ];

    private Result CommandRow(string command, string description) => new()
    {
        Title = $"tool {command}",
        SubTitle = description,
        IcoPath = "favicon.ico",
        Action = _ =>
        {
            _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} {command} ", true);
            return false;
        }
    };

    private static Result Hint(string text) => new()
    {
        Title = text,
        IcoPath = "favicon.ico",
        Action = _ => false
    };

    private void CopyToClipboard(string value)
    {
        _context.API.CopyToClipboard(value);
        _context.API.ShowMsg("Toolbox", "已复制到剪贴板");
    }

    private List<Result> HandleGuid(string name)
    {
        var results = new List<Result>
        {
            GuidRow("GUID v4（随机）", GuidGenerator.NewV4()),
            GuidRow("GUID v7（时间有序）", GuidGenerator.NewV7()),
            GuidRow("GUID v1（时间戳）", GuidGenerator.NewV1()),
        };

        if (!string.IsNullOrWhiteSpace(name))
        {
            results.Add(GuidRow($"GUID v3（MD5 · DNS: {name}）", GuidGenerator.NewV3(GuidGenerator.DnsNamespace, name)));
            results.Add(GuidRow($"GUID v5（SHA1 · DNS: {name}）", GuidGenerator.NewV5(GuidGenerator.DnsNamespace, name)));
        }
        else
        {
            results.Add(Hint("提示：输入 \"tool guid 名称\" 生成基于名称的 v3/v5"));
        }

        return results;
    }

    private Result GuidRow(string title, Guid guid) => new()
    {
        Title = title,
        SubTitle = guid.ToString(),
        IcoPath = "favicon.ico",
        ContextData = guid,
        Action = _ =>
        {
            CopyToClipboard(guid.ToString());
            return true;
        }
    };

    private List<Result> HandleJson(string content)
    {
        var fromClipboard = false;
        if (string.IsNullOrWhiteSpace(content))
        {
            content = TryGetClipboardText();
            fromClipboard = !string.IsNullOrWhiteSpace(content);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<Result>
            {
                Hint("用法：tool json {\"name\": \"value\"}，或先复制 JSON 再直接输入 \"tool json\""),
            };
        }

        var isValid = IsJsonValid(content, out var error);
        var preview = content.Length > 60
            ? content[..60].ReplaceLineEndings(" ") + "…"
            : content.ReplaceLineEndings(" ");

        return new List<Result>
        {
            new Result
            {
                Title = isValid ? "打开 JSON 编辑器" : "JSON 无效，仍可打开编辑器修复",
                SubTitle = $"{(fromClipboard ? "[来自剪贴板] " : string.Empty)}{preview}  ({content.Length} 字符){(isValid ? string.Empty : $"  ⚠ {error}")}",
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    OpenJsonEditor(content);
                    return true;
                }
            },
        };
    }

    private string TryGetClipboardText()
    {
        // QueryAsync 在后台线程执行，读剪贴板需回到 STA 主线程
        return Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                return System.Windows.Clipboard.GetText();
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    private static bool IsJsonValid(string text, out string error)
    {
        error = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void OpenJsonEditor(string json)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_jsonEditorWindow is null)
            {
                var webRoot = Path.Combine(_pluginDirectory, "wwwroot");
                var userDataFolder = Path.Combine(_pluginDirectory, "WebView2Cache");
                _jsonEditorWindow = new JsonEditorWindow(webRoot, userDataFolder, json);
                _jsonEditorWindow.Closed += (_, _) => _jsonEditorWindow = null;
                _jsonEditorWindow.Show();
            }
            else
            {
                _jsonEditorWindow.Activate();
                _jsonEditorWindow.UpdateJson(json);
            }
        });
    }

    private async Task<List<Result>> HandleFileHashAsync(string argument, string actionKeyword, CancellationToken token)
    {
        var path = argument.Trim('"');
        if (string.IsNullOrWhiteSpace(path))
            return BuildFileHashUsage(actionKeyword);

        if (!File.Exists(path))
        {
            return new List<Result>
            {
                Hint($"文件不存在：{path}"),
            };
        }

        lock (_hashCacheLock)
        {
            if (_lastHashResult is { } cached && cached.FilePath == path)
                return BuildHashResults(path, cached.Hashes);
        }

        var hashes = await Task.Run(() => HashCalculator.ComputeFileHashes(path, token), token);

        lock (_hashCacheLock)
            _lastHashResult = (path, hashes);

        return BuildHashResults(path, hashes);
    }

    private List<Result> BuildFileHashUsage(string actionKeyword)
    {
        return new List<Result>
        {
            new Result
            {
                Title = "选择文件…",
                SubTitle = "打开文件对话框，计算 MD5/SHA1/SHA256/SHA384/SHA512/CRC32",
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    var picked = PickFile();
                    if (picked is not null)
                        _context.API.ChangeQuery($"{actionKeyword} fhash \"{picked}\"", true);
                    return false;
                }
            },
            Hint("用法：tool fhash C:\\path\\to\\file"),
        };
    }

    private string? PickFile()
    {
        return Application.Current.Dispatcher.Invoke<string?>(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择要计算哈希的文件" };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        });
    }

    private List<Result> BuildHashResults(string path, IReadOnlyList<HashResult> hashes)
    {
        var results = new List<Result>
        {
            new Result
            {
                Title = "复制全部哈希值",
                SubTitle = $"{Path.GetFileName(path)} · 每行一条（算法: 哈希）",
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    var text = string.Join(Environment.NewLine, hashes.Select(h => $"{h.Algorithm}: {h.HashHex}"));
                    _context.API.CopyToClipboard(text);
                    _context.API.ShowMsg("Toolbox", $"已复制 {hashes.Count} 条哈希值");
                    return true;
                }
            },
        };

        foreach (var hash in hashes)
        {
            var value = hash.HashHex;
            results.Add(new Result
            {
                Title = hash.Algorithm,
                SubTitle = value,
                IcoPath = "favicon.ico",
                ContextData = value,
                Action = _ =>
                {
                    _context.API.CopyToClipboard(value);
                    _context.API.ShowMsg("Toolbox", $"已复制 {hash.Algorithm}");
                    return true;
                }
            });
        }

        return results;
    }

    private List<Result> HandleDateTime()
    {
        return TimestampProvider.GetEntries(DateTimeOffset.Now)
            .Select(entry => new Result
            {
                Title = entry.Value,
                SubTitle = $"{entry.Description} · Enter 复制",
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    CopyToClipboard(entry.Value);
                    return true;
                }
            })
            .ToList();
    }

    private List<Result> HandleEnc(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<Result> { Hint("用法：tool enc <文本>，显示 URL / Base64 / HTML 实体 / 十六进制等编解码结果") };
        return ToCopyRows(TextEncoderService.GetTransforms(input));
    }

    private List<Result> HandlePw(string input)
    {
        var length = PasswordGeneratorService.DefaultLength;
        if (!string.IsNullOrWhiteSpace(input)
            && (!int.TryParse(input, out length) || length <= 0 || length > PasswordGeneratorService.MaxLength))
            return new List<Result> { Hint($"用法：tool pw [长度]，长度 1-{PasswordGeneratorService.MaxLength}") };
        return ToCopyRows(PasswordGeneratorService.Generate(length));
    }

    private List<Result> HandleBase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<Result> { Hint("用法：tool base <值>，支持 0x/0b/0o 前缀、十进制、裸十六进制、单字符与 U+码点") };
        try
        {
            return ToCopyRows(BaseConverterService.Convert(input));
        }
        catch (FormatException ex)
        {
            return new List<Result> { Hint(ex.Message) };
        }
    }

    private List<Result> HandleTs(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<Result> { Hint("用法：tool ts <时间戳>，自动识别秒/毫秒并转日期") };
        try
        {
            return ToCopyRows(TimestampParser.Parse(input, DateTimeOffset.Now));
        }
        catch (FormatException ex)
        {
            return new List<Result> { Hint(ex.Message) };
        }
    }

    private List<Result> HandleJwt(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<Result> { Hint("用法：tool jwt <token>，解码 Header/Payload 并显示过期时间（Payload 可右键进 JSON 编辑器）") };
        try
        {
            return ToCopyRows(JwtDecoderService.Decode(input, DateTimeOffset.Now));
        }
        catch (FormatException ex)
        {
            return new List<Result> { Hint(ex.Message) };
        }
    }

    private List<Result> HandleCron(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<Result> { Hint("用法：tool cron <表达式>，如 tool cron 0 9 * * 1-5") };
        try
        {
            return ToCopyRows(CronParserService.GetInfo(input, DateTime.Now));
        }
        catch (FormatException ex)
        {
            return new List<Result> { Hint(ex.Message) };
        }
    }

    private List<Result> ToCopyRows(IReadOnlyList<ToolRow> rows) =>
        rows.Select(row => new Result
        {
            Title = row.Value,
            SubTitle = $"{row.Description} · Enter 复制",
            IcoPath = "favicon.ico",
            ContextData = row,
            Action = _ =>
            {
                CopyToClipboard(row.Value);
                return true;
            }
        }).ToList();
}
