using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.PaperTodo.Models;
using FlowLauncher.Plugin.PaperTodo.Services;
using FlowLauncher.Plugin.PaperTodo.ViewModels;
using FlowLauncher.Plugin.PaperTodo.Views;

using System.IO;
using System.Windows;

namespace FlowLauncher.Plugin.PaperTodo;

public class Main : IAsyncPlugin, IContextMenu, ISettingProvider, IDisposable
{
    private const string NoteKeyword = "note";
    private const string TodoKeyword = "todo";

    private PluginInitContext _context = default!;
    private Settings _settings = default!;
    private AppPaths _paths = default!;
    private PaperDatabase _database = default!;
    private NoteImageService _imageService = default!;
    private PaperWindowManager _windowManager = default!;
    private PaperTodoImportService _importService = default!;
    private BackupService _backupService = default!;
    private ReminderService _reminderService = default!;
    private readonly CancellationTokenSource _lifetime = new();

    public async Task InitAsync(PluginInitContext context)
    {
        _context = context;
        RegisterNoteKeyword(context);
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        _paths = new AppPaths();
        _paths.EnsureDirectories();

        try
        {
            _database = await PaperDatabase.OpenAsync(_paths.DatabasePath);
            await _database.InitializeAsync();
            if (!await _database.QuickCheckAsync())
            {
                throw new InvalidDataException("SQLite 数据库未通过完整性检查。");
            }
        }
        catch (Exception ex)
        {
            context.API.LogException(nameof(Main), "PaperTodo database initialization failed", ex);
            context.API.ShowMsgError(
                "PaperTodo 无法启动",
                $"数据库初始化失败：{ex.Message}");
            throw;
        }

        _imageService = new NoteImageService();
        _windowManager = new PaperWindowManager(
            _database,
            _settings,
            _imageService,
            Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "wwwroot"),
            _paths.WebViewDataDirectory);
        _importService = new PaperTodoImportService(_database, new LmdbImageImporter());
        _backupService = new BackupService(_database, _paths, _settings);
        _reminderService = new ReminderService(
            _database,
            _settings,
            text => context.API.ShowMsg("PaperTodo 提醒", TrimForMessage(text, 220)));
        _reminderService.Start();

        try
        {
            await _backupService.CreateDailyBackupAsync();
        }
        catch (Exception ex)
        {
            context.API.LogException(nameof(Main), "PaperTodo daily backup failed", ex);
        }
    }

    public System.Windows.Controls.Control CreateSettingPanel()
    {
        var viewModel = new SettingsViewModel(
            _settings,
            _importService,
            _backupService,
            _paths,
            () => _context.API.SaveSettingJsonStorage<Settings>(),
            () =>
            {
                _windowManager.CloseAll();
                return Task.CompletedTask;
            });
        return new SettingsView(viewModel);
    }

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        try
        {
            var search = query.Search?.Trim() ?? "";
            return string.Equals(query.ActionKeyword, NoteKeyword, StringComparison.OrdinalIgnoreCase)
                ? await HandleNoteQueryAsync(search, token)
                : await HandleTodoQueryAsync(search, token);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(Main), "PaperTodo query failed", ex);
            return
            [
                new Result
                {
                    Title = "PaperTodo 查询失败",
                    SubTitle = ex.Message,
                    IcoPath = "favicon.ico",
                    Action = _ => false,
                }
            ];
        }
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult?.ContextData is TodoResultData todo)
        {
            return BuildTodoContextMenu(todo);
        }
        if (selectedResult?.ContextData is NoteResultData note)
        {
            return BuildNoteContextMenu(note);
        }
        if (selectedResult?.ContextData is PaperResultData paper)
        {
            return BuildPaperContextMenu(paper.Paper);
        }
        return [];
    }

    private async Task<List<Result>> HandleTodoQueryAsync(string search, CancellationToken token)
    {
        if (TryTakeCommand(search, "add", out var addText))
        {
            var lines = TodoTextParser.Split(addText);
            if (lines.Count == 0)
            {
                return [Hint("在 add 后输入待办内容", "例如：todo add 买牛奶")];
            }
            return
            [
                new Result
                {
                    Title = lines.Count == 1
                        ? $"添加到收件箱：{TrimTitle(lines[0], 70)}"
                        : $"添加 {lines.Count} 项到收件箱",
                    SubTitle = lines.Count == 1 ? "按 Enter 保存" : string.Join(" / ", lines.Take(3).Select(line => TrimTitle(line, 28))),
                    IcoPath = "favicon.ico",
                    AsyncAction = async _ =>
                    {
                        try
                        {
                            var inbox = await _database.EnsureInboxAsync(_lifetime.Token);
                            await _database.AppendTodosAsync(
                                inbox.Id,
                                lines.Select(text => new TodoItem { Text = text }).ToArray(),
                                _lifetime.Token);
                            _context.API.ShowMsg("PaperTodo", $"已添加 {lines.Count} 项到收件箱");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            _context.API.ShowMsgError("添加待办失败", ex.Message);
                            return false;
                        }
                    },
                }
            ];
        }

        if (TryTakeCommand(search, "new", out var title))
        {
            return
            [
                new Result
                {
                    Title = string.IsNullOrWhiteSpace(title) ? "新建待办纸" : $"新建待办纸：{TrimTitle(title, 60)}",
                    SubTitle = "打开独立 Todo 窗口",
                    IcoPath = "favicon.ico",
                    AsyncAction = async _ =>
                    {
                        var paper = await _database.CreatePaperAsync(new Paper
                        {
                            Type = PaperTypes.Todo,
                            Title = string.IsNullOrWhiteSpace(title) ? "待办" : TrimTitle(title.Trim(), 80),
                        }, _lifetime.Token);
                        _windowManager.OpenPaper(paper);
                        return true;
                    },
                }
            ];
        }

        if (search.Length == 0)
        {
            return await BuildTodoHomeAsync(token);
        }

        var results = await _database.SearchAsync(search, 30, token);
        var todoHits = results.Where(hit => hit.PaperType == PaperTypes.Todo).ToArray();
        if (todoHits.Length == 0)
        {
            return
            [
                Hint("没有找到匹配的待办", $"输入 todo add {search} 可快速添加"),
            ];
        }

        return [.. todoHits.Select(ToTodoResult)];
    }

    private async Task<List<Result>> BuildTodoHomeAsync(CancellationToken token)
    {
        var inbox = await _database.EnsureInboxAsync(token);
        var papers = await _database.GetRecentTodoPapersAsync(20, token);
        var results = new List<Result>
        {
            new()
            {
                Title = "新建待办纸",
                SubTitle = "打开一个独立的 Todo 窗口",
                IcoPath = "favicon.ico",
                AsyncAction = async _ =>
                {
                    var paper = await _database.CreatePaperAsync(
                        new Paper { Type = PaperTypes.Todo, Title = "待办" },
                        _lifetime.Token);
                    _windowManager.OpenPaper(paper);
                    return true;
                },
            },
            new()
            {
                Title = "打开收件箱",
                SubTitle = "快捷添加的待办会进入这里",
                IcoPath = "favicon.ico",
                ContextData = new PaperResultData(inbox),
                Action = _ =>
                {
                    _windowManager.OpenPaper(inbox);
                    return true;
                },
            },
        };

        var flattened = papers
            .SelectMany(paper => paper.Items
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .Select(item => (Paper: paper, Item: item)))
            .Take(Math.Clamp(_settings.RecentItemsCount, 1, 30));
        foreach (var (paper, item) in flattened)
        {
            results.Add(ToTodoResult(item, paper.Title));
        }

        if (results.Count == 2)
        {
            results.Add(Hint("还没有待办事项", "使用 todo add <文本> 或打开窗口添加"));
        }
        return results;
    }

    private async Task<List<Result>> HandleNoteQueryAsync(string search, CancellationToken token)
    {
        if (TryTakeCommand(search, "add", out var addText))
        {
            var separator = addText.IndexOf('|');
            var addTitle = separator >= 0 ? addText[..separator].Trim() : addText.Trim();
            var content = separator >= 0 ? addText[(separator + 1)..].TrimStart() : "";
            if (string.IsNullOrWhiteSpace(addTitle))
            {
                return [Hint("请输入笔记标题", "例如：note add 周会记录 | 讨论内容")];
            }

            return
            [
                new Result
                {
                    Title = $"新建笔记：{TrimTitle(addTitle, 60)}",
                    SubTitle = string.IsNullOrWhiteSpace(content)
                        ? "按 Enter 创建并打开 WYSIWYG 编辑器"
                        : TrimTitle(content.ReplaceLineEndings(" "), 120),
                    IcoPath = "favicon.ico",
                    AsyncAction = async _ =>
                    {
                        var paper = await CreateNoteAsync(addTitle, content, _lifetime.Token);
                        _windowManager.OpenPaper(paper);
                        return true;
                    },
                }
            ];
        }

        if (TryTakeCommand(search, "new", out var title))
        {
            return
            [
                new Result
                {
                    Title = string.IsNullOrWhiteSpace(title) ? "新建笔记纸" : $"新建笔记纸：{TrimTitle(title, 60)}",
                    SubTitle = "打开离线 WYSIWYG Markdown 编辑器",
                    IcoPath = "favicon.ico",
                    AsyncAction = async _ =>
                    {
                        var paper = await CreateNoteAsync(
                            string.IsNullOrWhiteSpace(title) ? "未命名笔记" : title,
                            "",
                            _lifetime.Token);
                        _windowManager.OpenPaper(paper);
                        return true;
                    },
                }
            ];
        }

        if (search.Length == 0)
        {
            var notes = await _database.GetRecentNotesAsync(
                Math.Clamp(_settings.RecentItemsCount, 1, 30),
                token);
            var results = new List<Result>
            {
                new()
                {
                    Title = "新建笔记纸",
                    SubTitle = "创建并打开 Markdown WYSIWYG 编辑器",
                    IcoPath = "favicon.ico",
                    AsyncAction = async _ =>
                    {
                        var paper = await CreateNoteAsync("未命名笔记", "", _lifetime.Token);
                        _windowManager.OpenPaper(paper);
                        return true;
                    },
                },
            };
            results.AddRange(notes.Select(ToNoteResult));
            if (notes.Count == 0)
            {
                results.Add(Hint("还没有笔记", "使用 note new 或 note add <标题> | <正文>"));
            }
            return results;
        }

        var hits = (await _database.SearchAsync(search, 30, token))
            .Where(hit => hit.PaperType == PaperTypes.Note)
            .ToArray();
        if (hits.Length == 0)
        {
            return [Hint("没有找到匹配的笔记", $"输入 note add {search} | 正文 可新建")];
        }
        return [.. hits.Select(ToNoteResult)];
    }

    private async Task<Paper> CreateNoteAsync(string title, string content, CancellationToken token)
    {
        var normalized = string.IsNullOrWhiteSpace(title) ? "未命名笔记" : title.Trim();
        if (normalized.Length > 80)
        {
            normalized = normalized[..80];
        }
        return await _database.CreatePaperAsync(new Paper
        {
            Type = PaperTypes.Note,
            Title = normalized,
            Content = content.Length > 100_000 ? content[..100_000] : content,
            Width = 780,
            Height = 640,
        }, token);
    }

    private Result ToTodoResult(SearchHit hit)
    {
        return new Result
        {
            Title = TrimTitle(hit.Title, 120),
            SubTitle = $"{(hit.Done ? "已完成" : "待办")} · {hit.PaperTitle} · {hit.Snippet}",
            IcoPath = "favicon.ico",
            ContextData = new TodoResultData(
                hit.PaperId,
                hit.ItemId ?? "",
                hit.Title,
                hit.Done),
            AsyncAction = async _ =>
            {
                await _windowManager.OpenPaperByIdAsync(hit.PaperId);
                return true;
            },
        };
    }

    private Result ToTodoResult(TodoItem item, string paperTitle)
    {
        var reminder = item.ReminderAt is { } at ? $" · 提醒 {at.ToLocalTime():MM-dd HH:mm}" : "";
        return new Result
        {
            Title = TrimTitle(item.Text, 120),
            SubTitle = $"{(item.Done ? "已完成" : "待办")} · {paperTitle}{reminder}",
            IcoPath = "favicon.ico",
            ContextData = new TodoResultData(item.PaperId, item.Id, item.Text, item.Done),
            AsyncAction = async _ =>
            {
                await _windowManager.OpenPaperByIdAsync(item.PaperId);
                return true;
            },
        };
    }

    private Result ToNoteResult(Paper note)
    {
        return new Result
        {
            Title = TrimTitle(note.Title, 100),
            SubTitle = BuildNoteSnippet(note.Content),
            IcoPath = "favicon.ico",
            ContextData = new NoteResultData(note),
            Action = _ =>
            {
                _windowManager.OpenPaper(note);
                return true;
            },
        };
    }

    private Result ToNoteResult(SearchHit hit)
    {
        return new Result
        {
            Title = TrimTitle(hit.Title, 100),
            SubTitle = hit.Snippet,
            IcoPath = "favicon.ico",
            ContextData = new NoteResultData(
                new Paper { Id = hit.PaperId, Type = PaperTypes.Note, Title = hit.Title }),
            AsyncAction = async _ =>
            {
                await _windowManager.OpenPaperByIdAsync(hit.PaperId);
                return true;
            },
        };
    }

    private List<Result> BuildTodoContextMenu(TodoResultData todo)
    {
        return
        [
            new Result
            {
                Title = "编辑待办窗口",
                IcoPath = "favicon.ico",
                AsyncAction = async _ =>
                {
                    await _windowManager.OpenPaperByIdAsync(todo.PaperId);
                    return true;
                },
            },
            new Result
            {
                Title = "设置提醒",
                IcoPath = "favicon.ico",
                AsyncAction = async _ =>
                {
                    var item = await _database.GetTodoItemAsync(todo.ItemId);
                    if (item is null)
                    {
                        return false;
                    }
                    var dialog = new ReminderDialog(item.ReminderAt);
                    if (dialog.ShowDialog() == true)
                    {
                        await _database.SetTodoReminderAsync(todo.ItemId, dialog.Result);
                        _context.API.ChangeQuery($"{TodoKeyword} ", true);
                    }
                    return false;
                },
            },
            new Result
            {
                Title = todo.Done ? "恢复为未完成" : "标记为已完成",
                SubTitle = _settings.CompletionPolicy == TodoCompletionPolicy.Delete && !todo.Done
                    ? "当前设置会在完成后立即清除"
                    : "",
                IcoPath = "favicon.ico",
                AsyncAction = async _ =>
                {
                    try
                    {
                        await _database.SetTodoDoneAsync(
                            todo.ItemId,
                            !todo.Done,
                            _settings.CompletionPolicy);
                        _context.API.ChangeQuery($"{TodoKeyword} ", true);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        _context.API.ShowMsgError("更新待办失败", ex.Message);
                        return false;
                    }
                },
            },
            new Result
            {
                Title = "删除事项",
                IcoPath = "favicon.ico",
                AsyncAction = async _ =>
                {
                    try
                    {
                        await _database.DeleteTodoAsync(todo.ItemId);
                        _context.API.ChangeQuery($"{TodoKeyword} ", true);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        _context.API.ShowMsgError("删除待办失败", ex.Message);
                        return false;
                    }
                },
            },
        ];
    }

    private List<Result> BuildNoteContextMenu(NoteResultData note)
    {
        return
        [
            new Result
            {
                Title = "打开笔记",
                IcoPath = "favicon.ico",
                AsyncAction = async _ =>
                {
                    await _windowManager.OpenPaperByIdAsync(note.Paper.Id);
                    return true;
                },
            },
            new Result
            {
                Title = "重命名笔记",
                IcoPath = "favicon.ico",
                AsyncAction = async _ =>
                {
                    var paper = await _database.GetPaperAsync(note.Paper.Id);
                    if (paper is null)
                    {
                        return false;
                    }
                    var dialog = new TextInputDialog("重命名笔记", "笔记标题", paper.Title);
                    if (dialog.ShowDialog() == true)
                    {
                        await _database.UpdateTitleAsync(paper.Id, dialog.Value);
                        _context.API.ChangeQuery($"{NoteKeyword} ", true);
                    }
                    return false;
                },
            },
            new Result
            {
                Title = "复制 Markdown",
                IcoPath = "favicon.ico",
                AsyncAction = async _ =>
                {
                    var paper = await _database.GetPaperAsync(note.Paper.Id);
                    if (paper is not null)
                    {
                        _context.API.CopyToClipboard(paper.Content);
                        _context.API.ShowMsg("PaperTodo", "Markdown 已复制");
                    }
                    return false;
                },
            },
            new Result
            {
                Title = "删除笔记",
                IcoPath = "favicon.ico",
                AsyncAction = async _ =>
                {
                    var result = MessageBox.Show(
                        $"确定删除笔记“{note.Paper.Title}”吗？",
                        "PaperTodo",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (result == MessageBoxResult.Yes)
                    {
                        await _database.DeletePaperAsync(note.Paper.Id);
                        _context.API.ChangeQuery($"{NoteKeyword} ", true);
                    }
                    return false;
                },
            },
        ];
    }

    private List<Result> BuildPaperContextMenu(Paper paper)
    {
        return
        [
            new Result
            {
                Title = "打开窗口",
                IcoPath = "favicon.ico",
                AsyncAction = async _ =>
                {
                    await _windowManager.OpenPaperByIdAsync(paper.Id);
                    return true;
                },
            },
        ];
    }

    private static Result Hint(string title, string subtitle = "") => new()
    {
        Title = title,
        SubTitle = subtitle,
        IcoPath = "favicon.ico",
        Action = _ => false,
    };

    internal static bool TryTakeCommand(string input, string command, out string rest)
    {
        if (string.Equals(input, command, StringComparison.OrdinalIgnoreCase))
        {
            rest = "";
            return true;
        }
        if (input.StartsWith(command + " ", StringComparison.OrdinalIgnoreCase))
        {
            rest = input[(command.Length + 1)..].Trim();
            return true;
        }
        rest = "";
        return false;
    }

    private static string TrimTitle(string value, int maxLength)
    {
        var normalized = string.Join(
            " ",
            value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }

    private static string TrimForMessage(string value, int maxLength)
    {
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }

    private static string BuildNoteSnippet(string markdown)
    {
        var normalized = string.Join(
            " ",
            markdown.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length == 0)
        {
            return "空笔记";
        }
        return normalized.Length <= 140 ? normalized : normalized[..140] + "...";
    }

    private static void RegisterNoteKeyword(PluginInitContext context)
    {
        var alreadyDeclared = context.CurrentPluginMetadata.ActionKeywords?
            .Contains(NoteKeyword, StringComparer.OrdinalIgnoreCase) == true;
        if (!alreadyDeclared && !context.API.ActionKeywordAssigned(NoteKeyword))
        {
            context.API.AddActionKeyword(context.CurrentPluginMetadata.ID, NoteKeyword);
        }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _reminderService?.Dispose();
        _windowManager?.CloseAll();
        if (_database is not null)
        {
            _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        _lifetime.Dispose();
    }

    private sealed record TodoResultData(
        string PaperId,
        string ItemId,
        string Text,
        bool Done);

    private sealed record NoteResultData(Paper Paper);
    private sealed record PaperResultData(Paper Paper);
}
