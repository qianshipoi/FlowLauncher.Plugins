using Flow.Launcher.Plugin;

using System.Windows.Controls;

namespace FlowLauncher.Plugin.Translator;

public class Main : IAsyncPlugin, IContextMenu, ISettingProvider
{
    private PluginInitContext _context = default!;
    private ITranslator _translator = default!;
    private HistoryManager _historyManager = default!;
    private Settings _settings = new();

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        _translator = new AiTanslator(_settings);
        _historyManager = new HistoryManager(context.CurrentPluginMetadata.PluginDirectory);
        return Task.CompletedTask;
    }

    public Control CreateSettingPanel()
    {
        var viewModel = new SettingsViewModel(_context.API, _settings);
        return new SettingsView(viewModel);
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        var sub = selectedResult?.SubTitle ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sub))
            return new List<Result>();

        string original = sub;
        var idx = sub.IndexOf(" (");
        if (idx >= 0)
            original = sub.Substring(0, idx);
        else
        {
            idx = sub.IndexOf(" - ");
            if (idx >= 0)
                original = sub.Substring(0, idx);
        }

        original = original.Trim();
        if (string.IsNullOrEmpty(original))
            return new List<Result>();

        var record = _historyManager.FindByOriginalText(original, "auto", "en");
        if (record == null)
            return new List<Result>();

        return new List<Result>
        {
            new Result
            {
                Title = "删除历史记录",
                SubTitle = record.OriginalText,
                Action = _ =>
                {
                    var removed = _historyManager.Remove(record.Id);
                    if (removed)
                    {
                        _context.API.ChangeQuery(string.Empty, true);
                    }

                    return true;
                }
            }
        };
    }

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        if (!_settings.IsValid())
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "请先配置翻译设置",
                    SubTitle = "按 Enter 打开设置页面",
                    Action = _ =>
                    {
                        _context.API.OpenSettingDialog();
                        return true;
                    }
                }
            };
        }

        if (string.IsNullOrWhiteSpace(query.Search))
        {
            var results = new List<Result>();
            foreach (var r in _historyManager.GetAll().Take(5))
            {
                results.Add(new Result
                {
                    Title = r.TranslatedText,
                    SubTitle = $"{r.OriginalText} ({r.FromLanguage} → {r.ToLanguage}) - {r.TranslatedAt:g}",
                    Action = _ =>
                    {
                        _context.API.CopyToClipboard(r.TranslatedText);
                        return true;
                    },
                });
            }

            return results;
        }

        var cached = _historyManager.FindByOriginalText(query.Search, "auto", "en");
        if (cached != null)
        {
            return new List<Result>
            {
                new Result
                {
                    Title = cached.TranslatedText,
                    SubTitle = $"{query.Search} (来自历史记录)",
                    Action = _ =>
                    {
                        _context.API.CopyToClipboard(cached.TranslatedText);
                        return true;
                    }
                }
            };
        }

        var translated = await _translator.TranslateAsync(query.Search, "auto", "en", token);

        _historyManager.Add(query.Search, translated, "auto", "en");

        return new List<Result>
        {
            new Result
            {
                Title = translated,
                SubTitle = query.Search,
                Action = _ =>
                {
                    _context.API.CopyToClipboard(translated);
                    return true;
                }
            }
        };
    }
}
