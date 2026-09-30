using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.PasswordManager.Models;
using FlowLauncher.Plugin.PasswordManager.Services;
using FlowLauncher.Plugin.PasswordManager.ViewModels;
using FlowLauncher.Plugin.PasswordManager.Views;

using SQLitePCL;

using System.IO;
using System.Reflection;
using System.Windows;

namespace FlowLauncher.Plugin.PasswordManager;

public class Main : IAsyncPlugin, IContextMenu, ISettingProvider, IDisposable
{
    /// <summary>生成随机密码的触发词，独立于 pwd，避免占用搜索空间。</summary>
    private const string GeneratorKeyword = "pwdgen";

    private PluginInitContext _context = default!;
    private PasswordStore _store = default!;
    private DBKeyManager _keyManager = default!;
    private ImportExportService _importExport = default!;
    private string _pluginDirectory = default!;
    private Settings _settings = default!;

    private readonly AddFormStateMachine _form = new();

    static Main()
    {
        Batteries_V2.Init();
        RegisterLocalAssemblyResolve();
    }

    /// <summary>
    /// 从插件目录解析依赖程序集。
    /// Flow Launcher 的 Assembly.Load（Load 上下文）不搜索插件目录，
    /// 导致 XAML 加载 iNKORE.* 时抛 FileNotFoundException。
    /// </summary>
    private static void RegisterLocalAssemblyResolve()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var name = new AssemblyName(args.Name);
            if (!name.Name!.StartsWith("iNKORE.", StringComparison.OrdinalIgnoreCase))
                return null;

            var pluginDir = Path.GetDirectoryName(typeof(Main).Assembly.Location);
            var dllPath = Path.Combine(pluginDir!, name.Name + ".dll");
            return File.Exists(dllPath) ? Assembly.LoadFrom(dllPath) : null;
        };
    }

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        RegisterGeneratorKeyword(context);
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        _pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
        _keyManager = new DBKeyManager(_pluginDirectory);
        _store = new PasswordStore(_pluginDirectory, _keyManager.GetOrCreateKey());
        _importExport = new ImportExportService(_store, _pluginDirectory);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 注册随机密码生成的触发词。
    /// 必须先查 ActionKeywordAssigned / 已有 ActionKeywords，否则每次 InitAsync 都会重复注册。
    /// </summary>
    private static void RegisterGeneratorKeyword(PluginInitContext context)
    {
        var alreadyDeclared = context.CurrentPluginMetadata.ActionKeywords?
            .Contains(GeneratorKeyword, StringComparer.OrdinalIgnoreCase) == true;

        if (!alreadyDeclared && !context.API.ActionKeywordAssigned(GeneratorKeyword))
        {
            context.API.AddActionKeyword(context.CurrentPluginMetadata.ID, GeneratorKeyword);
        }
    }

    public System.Windows.Controls.Control CreateSettingPanel()
    {
        var viewModel = new SettingsViewModel(_settings, _importExport, () => _context.API.SaveSettingJsonStorage<Settings>());
        return new SettingsView(viewModel);
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult?.ContextData is GeneratedPassword generated)
            return BuildGeneratorContextMenu(generated);

        if (selectedResult?.ContextData is not PasswordEntry entry)
            return new List<Result>();

        return new List<Result>
        {
            new Result
            {
                Title = "复制用户名",
                SubTitle = entry.Username,
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    _context.API.CopyToClipboard(entry.Username);
                    _context.API.ShowMsg("Password Manager", "用户名已复制到剪贴板");
                    return true;
                }
            },
            new Result
            {
                Title = "复制密码",
                SubTitle = "••••••••",
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    CopyPasswordToClipboard(entry);
                    return true;
                }
            },
            new Result
            {
                Title = "删除条目",
                SubTitle = $"删除 \"{entry.Title}\" ({entry.Username})",
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    _store.Delete(entry.Id);
                    return true;
                }
            }
        };
    }

    public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        var search = query.Search?.Trim() ?? string.Empty;

        // 独立触发词：生成随机密码
        if (string.Equals(query.ActionKeyword, GeneratorKeyword, StringComparison.OrdinalIgnoreCase))
        {
            if (_form.IsActive)
                _form.Reset();

            return Task.FromResult(GeneratePasswords(search));
        }

        // 打开管理窗口
        if (search.StartsWith("gui", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new List<Result>
            {
                new Result
                {
                    Title = "打开数据管理窗口",
                    SubTitle = "查看、添加、编辑、删除密码条目",
                    IcoPath = "favicon.ico",
                    Action = _ =>
                    {
                        OpenManagerWindow();
                        return true;
                    }
                }
            });
        }

        if (search.StartsWith("add", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(HandleAddFlow(search));
        }

        // 非 add/gui 查询时退出表单模式
        if (_form.IsActive)
            _form.Reset();

        if (string.IsNullOrEmpty(search))
            return Task.FromResult(ShowRecent());

        return Task.FromResult(SearchPasswords(search));
    }

    private void OpenManagerWindow()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var window = new ManagerWindow(_store, _importExport);
            window.Show();
        });
    }

    /// <summary>
    /// 处理生成随机密码的查询。
    /// 默认列出设置中配置的各个长度；若额外输入了长度，则把它作为首项与默认列表一起展示。
    /// </summary>
    private List<Result> GeneratePasswords(string search)
    {
        var set = _settings.GetCharacterSet();

        if (set.BuildPools().Count == 0)
        {
            return
            [
                new Result
                {
                    Title = "没有可用字符",
                    SubTitle = "请在设置中至少勾选一类字符，或减少忽略字符",
                    IcoPath = "favicon.ico",
                    Action = _ =>
                    {
                        _context.API.OpenSettingDialog();
                        return true;
                    }
                }
            ];
        }

        int? requested = int.TryParse(search, out var parsed)
            ? Math.Clamp(parsed, PasswordGeneratorService.MinLength, PasswordGeneratorService.MaxLength)
            : null;

        var lengths = new List<int>();
        if (requested is { } custom)
            lengths.Add(custom);
        lengths.AddRange(_settings.GetGeneratorLengths().Where(l => l != requested));

        var results = lengths
            .Select(length => BuildPasswordResult(
                PasswordGeneratorService.Generate(length, set), length, length == requested, set))
            .ToList();

        results.Add(BuildGeneratorHint(set, requested == null && !string.IsNullOrEmpty(search) ? search : null));
        return results;
    }

    private Result BuildPasswordResult(string password, int length, bool userSpecified, PasswordCharacterSet set)
    {
        var entropy = PasswordStrengthEstimator.Describe(length, set.BuildAlphabet().Length);
        var tag = userSpecified ? "指定长度" : "默认长度";

        return new Result
        {
            Title = password,
            SubTitle = $"{tag} {length} 位 · {entropy} · {set.Describe()}｜Enter 复制，右键可重新生成",
            IcoPath = "favicon.ico",
            CopyText = password,
            ContextData = new GeneratedPassword(length, password, userSpecified),
            Action = _ =>
            {
                _context.API.CopyToClipboard(password);
                _context.API.ShowMsg("Password Manager", $"已复制 {length} 位随机密码到剪贴板");
                return true;
            }
        };
    }

    private Result BuildGeneratorHint(PasswordCharacterSet set, string? invalidInput)
    {
        var title = invalidInput == null
            ? "输入长度可直接生成，如 pwdgen 20"
            : $"无法识别的长度 \"{invalidInput}\"，输入 1-{PasswordGeneratorService.MaxLength} 的数字";

        return new Result
        {
            Title = title,
            SubTitle = $"当前字符集：{set.Describe()}，可在设置中调整长度列表与忽略字符",
            IcoPath = "favicon.ico",
            Action = _ =>
            {
                _context.API.OpenSettingDialog();
                return true;
            }
        };
    }

    private List<Result> BuildGeneratorContextMenu(GeneratedPassword generated)
    {
        return
        [
            new Result
            {
                Title = "复制密码",
                SubTitle = generated.Value,
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    _context.API.CopyToClipboard(generated.Value);
                    _context.API.ShowMsg("Password Manager", $"已复制 {generated.Length} 位随机密码到剪贴板");
                    return true;
                }
            },
            new Result
            {
                Title = "重新生成",
                SubTitle = $"重新生成一个 {generated.Length} 位的随机密码",
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    // 触发词不同，需显式改回 pwdgen 查询并强制刷新
                    _context.API.ChangeQuery(
                        generated.UserSpecified ? $"{GeneratorKeyword} {generated.Length} " : $"{GeneratorKeyword} ",
                        true);
                    return false;
                }
            }
        ];
    }

    private List<Result> HandleAddFlow(string input)
    {
        var rest = input.Length > 3 ? input[3..].Trim() : string.Empty;

        if (!_form.IsActive)
            _form.Start();

        var currentInput = _form.ParseCurrentInput(rest);
        var results = new List<Result>();

        switch (_form.CurrentStep)
        {
            case AddFormStateMachine.Step.Title:
                results.Add(new Result
                {
                    Title = string.IsNullOrEmpty(currentInput) ? "输入标题" : $"标题: {currentInput}",
                    SubTitle = "[1/3] 输入标题后按 Enter 确认",
                    IcoPath = "favicon.ico",
                    Action = _ =>
                    {
                        if (_form.Submit(currentInput))
                            _context.API.ChangeQuery(_form.BuildQueryForStep(_context.CurrentPluginMetadata.ActionKeyword), false);
                        return false;
                    }
                });
                break;

            case AddFormStateMachine.Step.Username:
                results.Add(new Result
                {
                    Title = string.IsNullOrEmpty(currentInput) ? "输入用户名" : $"用户名: {currentInput}",
                    SubTitle = "[2/3] 输入用户名后按 Enter 确认",
                    IcoPath = "favicon.ico",
                    Action = _ =>
                    {
                        if (_form.Submit(currentInput))
                            _context.API.ChangeQuery(_form.BuildQueryForStep(_context.CurrentPluginMetadata.ActionKeyword), false);
                        return false;
                    }
                });
                AddNavigationButtons(results);
                AddCompletedField(results, $"标题: {_form.Title}");
                break;

            case AddFormStateMachine.Step.Password:
                results.Add(new Result
                {
                    Title = string.IsNullOrEmpty(currentInput) ? "输入密码" : "密码: ••••••••",
                    SubTitle = "[3/3] 输入密码后按 Enter 确认",
                    IcoPath = "favicon.ico",
                    Action = _ =>
                    {
                        if (_form.Submit(currentInput))
                            _context.API.ChangeQuery(_form.BuildQueryForStep(_context.CurrentPluginMetadata.ActionKeyword), false);
                        return false;
                    }
                });
                AddNavigationButtons(results);
                AddCompletedField(results, $"标题: {_form.Title}");
                AddCompletedField(results, $"用户名: {_form.Username}");
                break;

            case AddFormStateMachine.Step.Confirm:
                results.Add(new Result
                {
                    Title = "✓ 确认保存",
                    SubTitle = "按 Enter 保存到数据库",
                    IcoPath = "favicon.ico",
                    Action = _ =>
                    {
                        _store.Add(new PasswordEntry
                        {
                            Title = _form.Title,
                            Username = _form.Username,
                            EncryptedPassword = PasswordCrypto.Encrypt(_form.Password),
                        });
                        _context.API.ShowMsg("Password Manager", $"已保存 {_form.Title}");
                        _form.Reset();
                        _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} ", true);
                        return false;
                    }
                });
                AddNavigationButtons(results);
                results.Add(new Result
                {
                    Title = $"标题: {_form.Title}  用户名: {_form.Username}",
                    SubTitle = "密码: ••••••••",
                    IcoPath = "favicon.ico",
                    Action = _ => false
                });
                break;
        }

        return results;
    }

    private void AddCompletedField(List<Result> results, string display)
    {
        results.Add(new Result
        {
            Title = display,
            SubTitle = "✓ 已填写",
            IcoPath = "favicon.ico",
            Action = _ => false
        });
    }

    private void AddNavigationButtons(List<Result> results)
    {
        var kw = _context.CurrentPluginMetadata.ActionKeyword;

        results.Add(new Result
        {
            Title = "← 返回上一步",
            SubTitle = "",
            IcoPath = "favicon.ico",
            Action = _ =>
            {
                _form.GoBack();
                _context.API.ChangeQuery(_form.BuildBackQuery(kw), false);
                return false;
            }
        });

        results.Add(new Result
        {
            Title = "✕ 取消添加",
            SubTitle = "放弃并退出",
            IcoPath = "favicon.ico",
            Action = _ =>
            {
                _form.Reset();
                _context.API.ChangeQuery($"{kw} ", true);
                return false;
            }
        });
    }

    private List<Result> ShowRecent()
    {
        var entries = _store.GetRecent(_settings.RecentItemsCount);
        if (entries.Count == 0)
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "还没有保存任何密码",
                    SubTitle = "输入 \"pwd add\" 添加新密码，或查看帮助",
                    IcoPath = "favicon.ico",
                    Action = _ => false
                },
                new Result
                {
                    Title = "快速添加",
                    SubTitle = "pwd add 标题 | 用户名 | 密码",
                    IcoPath = "favicon.ico",
                    Action = _ =>
                    {
                        _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} add ", true);
                        return false;
                    }
                }
            };
        }

        return entries.Select(e => ToResult(e)).ToList();
    }

    private List<Result> SearchPasswords(string keyword)
    {
        var entries = _store.Search(keyword);
        if (entries.Count == 0)
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "没有找到匹配的密码",
                    SubTitle = $"搜索 \"{keyword}\" 无结果",
                    IcoPath = "favicon.ico",
                    Action = _ => false
                },
                new Result
                {
                    Title = "添加新密码",
                    SubTitle = $"pwd add 标题 | 用户名 | 密码",
                    IcoPath = "favicon.ico",
                    Action = _ =>
                    {
                        _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} add ", true);
                        return false;
                    }
                }
            };
        }

        return entries.Select(e => ToResult(e)).ToList();
    }

    private Result ToResult(PasswordEntry entry)
    {
        var tags = string.IsNullOrWhiteSpace(entry.Tags) ? "" : $" [{entry.Tags}]";
        return new Result
        {
            Title = $"{entry.Title}{tags}",
            SubTitle = $"{entry.Username}  {(string.IsNullOrWhiteSpace(entry.Url) ? "" : entry.Url)}",
            IcoPath = "favicon.ico",
            ContextData = entry,
            Action = _ =>
            {
                CopyPasswordToClipboard(entry);
                return true;
            }
        };
    }

    private void CopyPasswordToClipboard(PasswordEntry entry)
    {
        var plainPassword = PasswordCrypto.Decrypt(entry.EncryptedPassword);
        _context.API.CopyToClipboard(plainPassword);
        _store.Touch(entry.Id);

        _context.API.ShowMsg("Password Manager", $"已复制 {entry.Title} 的密码到剪贴板");

        // 自动清除剪贴板
        if (_settings.ClearClipboardAfterMs > 0)
        {
            _ = ClearClipboardAfterDelay(_settings.ClearClipboardAfterMs);
        }
    }

    private async Task ClearClipboardAfterDelay(int delayMs)
    {
        await Task.Delay(delayMs);
        try
        {
            _context.API.CopyToClipboard(string.Empty);
        }
        catch { /* 剪贴板已被其他程序修改时忽略 */ }
    }

    public void Dispose() => _store?.Dispose();
}
