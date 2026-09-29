using AlibabaCloud.OSS.V2;
using FlowLauncher.Plugin.AliyunOss;

using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.AliyunOss.Queries;
using FlowLauncher.Plugin.AliyunOss.Services;
using FlowLauncher.Plugin.AliyunOss.Transfer;
using FlowLauncher.Plugin.AliyunOss.ViewModel;
using FlowLauncher.Plugin.AliyunOss.Views;

using System.Windows;
using System.Windows.Controls;

namespace FlowLauncher.Plugin.AliyunOss;

public class Main : IAsyncPlugin, IReloadable, ISettingProvider
{
    private PluginInitContext _context = default!;
    private OssService _service = default!;
    private Settings _settings = default!;
    private DownloadQuery _downloadQuery = default!;
    private UploadQuery _uploadQuery = default!;
    private ILocalization _loc = default!;

    public Control CreateSettingPanel()
    {
        return new OssSettings(new OssSettingsViewModel(_settings, () => _context.API.SaveSettingJsonStorage<Settings>()));
    }

    /// <summary>
    /// 补齐子命令触发词（ossd/ossu/ossb）。
    /// 必须先查 ActionKeywordAssigned / 已有 ActionKeywords，否则每次 Init 都会重复注册。
    /// </summary>
    private void EnsureActionKeywords()
    {
        var api = _context.API;
        var pluginId = _context.CurrentPluginMetadata.ID;
        var existing = _context.CurrentPluginMetadata.ActionKeywords ?? [];

        foreach (var keyword in new[] { "ossd", "ossu", "ossb" })
        {
            if (existing.Contains(keyword, StringComparer.OrdinalIgnoreCase))
                continue;
            if (api.ActionKeywordAssigned(keyword))
                continue;
            api.AddActionKeyword(pluginId, keyword);
        }
    }

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        EnsureActionKeywords();
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        // 旧版配置规范化：ID 还原明文、Secret 升级为 DPAPI 密文
        if (_settings.TryUpgradeSecrets())
        {
            context.API.SaveSettingJsonStorage<Settings>();
        }
        _service = new OssService(_settings);
        _downloadQuery = new DownloadQuery(_context.API, _service, _settings);
        _uploadQuery = new UploadQuery(_context.API, _service, _settings);
        _loc = new FlowLocalization(_context.API);
        return Task.CompletedTask;
    }

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        try
        {
            if (query.ActionKeyword == "oss")
            {
                return
                [
                    ResultBuilder
                        .Create("ossd")
                        .Subtitle(_loc.Get("oss_download_subtitle"))
                        .Action(() => _context.API.ChangeQuery("ossd ", true), false)
                        .Build(),
                    ResultBuilder
                        .Create("ossu")
                        .Subtitle(_loc.Get("oss_upload_subtitle"))
                        .Action(() => _context.API.ChangeQuery("ossu ", true), false)
                        .Build(),
                    ResultBuilder
                        .Create("ossb")
                        .Subtitle(_loc.Get("oss_browser_subtitle"))
                        .Action(() => _context.API.ChangeQuery("ossb ", true), false)
                        .Build()
                ];
            }

            if (string.IsNullOrWhiteSpace(query.Search) || query.SearchTerms.Length == 1)
            {
                var searchTerm = query.Search ?? string.Empty;
                var buckets = await _service.GetBucketsAsync(searchTerm, token);

                // 完全匹配桶名 → 直接进入对应流程
                if (query.SearchTerms.Length == 1 &&
                    buckets.Any(b => b.Equals(query.SearchTerms[0], StringComparison.OrdinalIgnoreCase)))
                {
                    if (query.ActionKeyword == "ossb")
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var vm = new FileBrowserViewModel(_context.API, _service, _settings);
                            var window = new FileBrowserWindow(vm, query.SearchTerms[0], "");
                            window.Show();
                        });
                        return [];
                    }

                    if (query.ActionKeyword is "ossd" or "ossu")
                    {
                        return query.ActionKeyword == "ossd"
                            ? await _downloadQuery.ExecuteAsync(query, token)
                            : await _uploadQuery.ExecuteAsync(query, token);
                    }
                }

                return [.. 
                    buckets
                    .Select(x => 
                        ResultBuilder
                            .Create(x)
                            .Action(() => _context.API.ChangeQuery(query.ActionKeyword + " " + x + " ", true), false)
                            .Build())
                    ];
            }

            if (query.ActionKeyword == "ossb")
            {
                var bucketName = query.SearchTerms[0];
                var prefix = query.SearchTerms.Length > 1 ? query.SearchTerms[1] : "";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var vm = new FileBrowserViewModel(_context.API, _service, _settings);
                    var window = new FileBrowserWindow(vm, bucketName, prefix);
                    window.Show();
                });
                return [];
            }

            if (query.ActionKeyword == "ossd")
            {
                return await _downloadQuery.ExecuteAsync(query, token);
            }
            else if (query.ActionKeyword == "ossu")
            {
                return await _uploadQuery.ExecuteAsync(query, token);
            }

            return [];
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (SettingsNotConfiguredException)
        {
            return 
            [
                ResultBuilder.Create(_loc.Get("settings_not_configured")).Subtitle(_loc.Get("settings_open_panel")).Action(_context.API.OpenSettingDialog).Build()
            ];
        }
        catch (OperationException)
        {
            return [];
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(Main), $"[TRACE] Global Exception: {ex.GetType().Name}: {ex.Message}", ex);
            _context.API.ShowMsgError("Aliyun OSS Plugin Error", ex.Message);
            return [];
        }
    }

    public void ReloadData()
    {
        _service.ClearCache();
    }
}
