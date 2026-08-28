using DevToolkit.VarName;

using Flow.Launcher.Plugin;

using System.Windows;

namespace FlowLauncher.Plugin.VarName;

public class Main : IAsyncPlugin, ISettingProvider
{
    private const string Separator = "|";
    private const string RefreshPrefix = "@@refresh@@";
    private const string GeneratePrefix = "@@generate@@";

    private PluginInitContext _context = default!;
    private Settings _settings = new();
    private VarNameService _service = default!;
    private VarNameCache _cache = default!;
    private string _lastSearch = "";

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _context.API.AddActionKeyword(_context.CurrentPluginMetadata.ID, "vname");
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        _service = new VarNameService(_settings);
        _cache = new VarNameCache(context.CurrentPluginMetadata.PluginDirectory);
        return Task.CompletedTask;
    }

    public System.Windows.Controls.Control CreateSettingPanel()
    {
        var vm = new SettingsViewModel(_settings, () => _context.API.SaveSettingJsonStorage<Settings>());
        return new SettingsView(vm);
    }

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        var search = query.Search?.Trim() ?? string.Empty;

        // 步骤 2：已选中候选词，显示命名规范变体
        if (search.Contains(Separator))
        {
            var raw = search[(search.IndexOf(Separator) + 1)..].Trim();
            return NamingVariants(raw);
        }

        // 用户确认生成：调用 AI
        if (search.StartsWith(GeneratePrefix))
        {
            var keyword = search[GeneratePrefix.Length..].Trim();
            return await GenerateAndCacheAsync(query.ActionKeyword, keyword, token);
        }

        // 用户确认刷新：清除缓存后调用 AI
        if (search.StartsWith(RefreshPrefix))
        {
            var keyword = search[RefreshPrefix.Length..].Trim();
            _cache.Remove(keyword);
            return await GenerateAndCacheAsync(query.ActionKeyword, keyword, token);
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            return
            [
                new Result
                {
                    Title = "输入中文生成变量名",
                    SubTitle = "例如: vn 用户管理",
                    IcoPath = "favicon.ico",
                    Action = _ => false
                }
            ];
        }

        if (!_settings.IsValid())
        {
            return
            [
                new Result
                {
                    Title = "请先配置 API",
                    SubTitle = "在设置面板中填写 API Key、API Url 和 Model",
                    IcoPath = "favicon.ico",
                    Action = _ => true
                }
            ];
        }

        // 查缓存
        var cached = _cache.Get(search);
        if (cached is not null)
        {
            return BuildResults(query.ActionKeyword, search, cached, fromCache: true);
        }

        // 缓存未命中 → 提示用户按 Enter 生成（不调用 AI）
        _lastSearch = search;
        return
        [
            new Result
            {
                Title = $"按 Enter 生成 \"{search}\" 的变量名",
                SubTitle = "将调用 AI 生成命名建议",
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{query.ActionKeyword} {GeneratePrefix}{search}", true);
                    return false;
                }
            }
        ];
    }

    private async Task<List<Result>> GenerateAndCacheAsync(string actionKeyword, string search, CancellationToken token)
    {
        try
        {
            var suggestions = await _service.GenerateAsync(search, token);
            if (suggestions.Count == 0)
            {
                return
                [
                    new Result
                    {
                        Title = "未生成结果",
                        SubTitle = "请尝试换一种描述",
                        IcoPath = "favicon.ico",
                        Action = _ => false
                    }
                ];
            }

            _cache.Set(search, suggestions);
            return BuildResults(actionKeyword, search, suggestions, fromCache: false);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            return
            [
                new Result
                {
                    Title = "翻译失败",
                    SubTitle = ex.Message,
                    IcoPath = "favicon.ico",
                    Action = _ => true
                }
            ];
        }
    }

    private List<Result> BuildResults(string actionKeyword, string search, List<string> suggestions, bool fromCache)
    {
        var results = new List<Result>();

        // 缓存命中时，第一项为刷新缓存
        if (fromCache)
        {
            results.Add(new Result
            {
                Title = "🔄 刷新缓存（重新调用 AI）",
                SubTitle = $"重新生成 \"{search}\" 的变量名",
                IcoPath = "favicon.ico",
                Action = _ =>
                {
                    _cache.Remove(search);
                    _context.API.ChangeQuery($"{actionKeyword} {search}", true);
                    return false;
                }
            });
        }

        // 按上次选中排序：上次选中的排最前
        var sorted = fromCache
            ? SortByLastSelected(suggestions, _cache.GetLastSelectedValue(search))
            : suggestions;

        // 候选词列表
        results.AddRange(sorted.Select(s => new Result
        {
            Title = s,
            SubTitle = fromCache ? "（缓存）选择后显示命名规范变体" : "选择后显示命名规范变体",
            IcoPath = "favicon.ico",
            Action = _ =>
            {
                _cache.SetLastSelectedValue(search, s);
                _context.API.ChangeQuery($"{actionKeyword} {search}{Separator}{s}", true);
                return false;
            }
        }));

        return results;
    }

    private static List<string> SortByLastSelected(List<string> suggestions, string? lastSelected)
    {
        if (string.IsNullOrEmpty(lastSelected)) return suggestions;
        return [.. suggestions.OrderByDescending(s => s.Equals(lastSelected, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// 根据原始英文词生成不同命名规范的结果
    /// </summary>
    private List<Result> NamingVariants(string raw)
    {
        var variants = NamingStyleConverter.Variants(raw);

        // 按上次选中的命名规范排序
        var lastConvention = _cache.GetLastNamingConvention();
        var sorted = string.IsNullOrEmpty(lastConvention)
            ? variants
            : [.. variants.OrderByDescending(v => v.Label.Equals(lastConvention, StringComparison.OrdinalIgnoreCase))];

        return [.. sorted.Select(v => new Result
        {
            Title = v.Value,
            SubTitle = v.Label,
            IcoPath = "favicon.ico",
            Action = _ =>
            {
                _cache.SetLastNamingConvention(v.Label);
                Clipboard.SetText(v.Value);
                return true;
            }
        })];
    }
}
