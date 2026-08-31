using DevToolkit.SpeechToText;
using Flow.Launcher.Plugin;
using FlowLauncher.Plugin.SpeechToText.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FlowLauncher.Plugin.SpeechToText;

public class Main : IAsyncPlugin, ISettingProvider
{
    private PluginInitContext _context = default!;
    private Settings _settings = default!;
    private SpeechToTextViewModel? _activeVm;
    private BubbleWindow? _bubbleWindow;
    private readonly object _lock = new();

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        return Task.CompletedTask;
    }

    public Control CreateSettingPanel()
    {
        var vm = new SettingsViewModel(_settings, () => _context.API.SaveSettingJsonStorage<Settings>());
        return new SettingsView(vm);
    }

    public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        var search = query.Search?.Trim() ?? string.Empty;

        if (!_settings.IsValid())
        {
            return Task.FromResult<List<Result>>([
                new Result
                {
                    Title = "请先配置 ASR API",
                    SubTitle = "在设置面板中填写 API Key、API Url 和 Model",
                    IcoPath = "favicon.ico",
                    Action = _ => true,
                }
            ]);
        }

        // 检查是否有正在录音的实例
        lock (_lock)
        {
            if (_activeVm is not null && _activeVm.IsRecording)
            {
                return Task.FromResult<List<Result>>([
                    new Result
                    {
                        Title = "🔴 正在录音中…",
                        SubTitle = $"点击停止录音 | {_activeVm.DurationText}",
                        IcoPath = "favicon.ico",
                        Action = _ =>
                        {
                            StopRecording();
                            return false;
                        },
                    }
                ]);
            }
        }

        // 显示历史记录或开始录音
        if (string.IsNullOrWhiteSpace(search))
        {
            return Task.FromResult(BuildDefaultResults(query.ActionKeyword));
        }

        return Task.FromResult(BuildSearchResults(query.ActionKeyword, search));
    }

    private List<Result> BuildDefaultResults(string actionKeyword)
    {
        var results = new List<Result>();

        // 开始录音
        results.Add(new Result
        {
            Title = "🎤 开始语音识别",
            SubTitle = "Enter 开始录音，将弹出识别气泡窗口",
            IcoPath = "favicon.ico",
            Action = _ =>
            {
                StartRecording(actionKeyword);
                return true;
            },
        });

        // 显示最近历史
        var dataDir = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "data");
        var history = new TranscriptionHistory(dataDir);
        var recent = history.GetRecent(5);

        foreach (var record in recent)
        {
            results.Add(new Result
            {
                Title = record.Text.Length > 60 ? record.Text[..60] + "…" : record.Text,
                SubTitle = $"{record.Timestamp:yyyy-MM-dd HH:mm} | {record.Duration:mm\\:ss} | 右键复制",
                IcoPath = "favicon.ico",
                ContextData = record.Text,
                Action = _ =>
                {
                    System.Windows.Clipboard.SetText(record.Text);
                    return true;
                },
            });
        }

        if (recent.Count == 0)
        {
            results.Add(new Result
            {
                Title = "输入关键词搜索历史记录",
                SubTitle = "例如: asr 你好",
                IcoPath = "favicon.ico",
                Action = _ => false,
            });
        }

        return results;
    }

    private List<Result> BuildSearchResults(string actionKeyword, string keyword)
    {
        var results = new List<Result>();

        // 搜索历史记录
        var dataDir = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "data");
        var history = new TranscriptionHistory(dataDir);
        var all = history.GetAll();
        var matched = all.Where(r => r.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(r => r.Timestamp)
                         .Take(10)
                         .ToList();

        // 开始录音（带关键词提示）
        results.Add(new Result
        {
            Title = $"🎤 开始语音识别（关键词：{keyword}）",
            SubTitle = "Enter 开始录音",
            IcoPath = "favicon.ico",
            Action = _ =>
            {
                StartRecording(actionKeyword);
                return true;
            },
        });

        foreach (var record in matched)
        {
            results.Add(new Result
            {
                Title = record.Text.Length > 60 ? record.Text[..60] + "…" : record.Text,
                SubTitle = $"{record.Timestamp:yyyy-MM-dd HH:mm} | {record.Duration:mm\\:ss}",
                IcoPath = "favicon.ico",
                ContextData = record.Text,
                Action = _ =>
                {
                    System.Windows.Clipboard.SetText(record.Text);
                    return true;
                },
            });
        }

        return results;
    }

    private void StartRecording(string actionKeyword)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            lock (_lock)
            {
                if (_activeVm is not null && _activeVm.IsRecording) return;

                var dataDir = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "data");
                _activeVm = new SpeechToTextViewModel(_settings, dataDir);
                _activeVm.RecordingStopped += () =>
                {
                    // 气泡窗口会自行处理停止后的 UI
                };

                _bubbleWindow = new BubbleWindow(_settings, () => _context.API.SaveSettingJsonStorage<Settings>());
                _bubbleWindow.BindViewModel(_activeVm);
                _bubbleWindow.Closed += (_, _) =>
                {
                    lock (_lock)
                    {
                        _activeVm?.Dispose();
                        _activeVm = null;
                        _bubbleWindow = null;
                    }
                };
                _bubbleWindow.Show();

                _activeVm.Start();
            }
        });
    }

    private void StopRecording()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            lock (_lock)
            {
                if (_activeVm is { IsRecording: true })
                    _activeVm.Stop();
            }
        });
    }
}
