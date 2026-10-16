using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.S3;
using FlowLauncher.Plugin.S3.Queries;
using FlowLauncher.Plugin.S3.Services;
using FlowLauncher.Plugin.S3.Transfer;
using FlowLauncher.Plugin.S3.ViewModel;
using FlowLauncher.Plugin.S3.ViewModels;
using FlowLauncher.Plugin.S3.Views;

using System.Windows;
using System.Windows.Controls;

namespace FlowLauncher.Plugin.S3;

public class Main : IAsyncPlugin, IReloadable, ISettingProvider
{
    private PluginInitContext _context = default!;
    private S3StorageService _service = default!;
    private Settings _settings = default!;
    private DownloadQuery _downloadQuery = default!;
    private UploadQuery _uploadQuery = default!;
    private ILocalization _loc = default!;

    public Control CreateSettingPanel()
    {
        return new S3SettingsView(
            new S3SettingsViewModel(_settings, () => _context.API.SaveSettingJsonStorage<Settings>()));
    }

    private void EnsureActionKeywords()
    {
        var api = _context.API;
        var pluginId = _context.CurrentPluginMetadata.ID;
        var existing = _context.CurrentPluginMetadata.ActionKeywords ?? [];

        foreach (var keyword in new[] { "s3d", "s3u", "s3b", "s3p" })
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
        _settings.EnsureProfiles();

        if (_settings.TryUpgradeSecrets())
            context.API.SaveSettingJsonStorage<Settings>();

        _service = new S3StorageService(_settings, message =>
            context.API.LogInfo(nameof(Main), message));
        _downloadQuery = new DownloadQuery(_context.API, _service, _settings);
        _uploadQuery = new UploadQuery(_context.API, _service, _settings);
        _loc = new FlowLocalization(_context.API);

        return Task.CompletedTask;
    }

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        try
        {
            if (query.ActionKeyword == "s3")
                return BuildHomeResults();

            if (query.ActionKeyword == "s3p")
                return BuildProfileResults();

            if (string.IsNullOrWhiteSpace(query.Search) || query.SearchTerms.Length == 0)
                return await BuildBucketResultsAsync(query.ActionKeyword, "", token);

            var bucketName = query.SearchTerms[0];
            if (query.ActionKeyword == "s3b")
            {
                var prefix = query.SearchTerms.Length > 1 ? query.SearchTerms[1] : "";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var viewModel = new FileBrowserViewModel(_context.API, _service, _settings);
                    var window = new FileBrowserWindow(viewModel, bucketName, prefix);
                    window.Show();
                });
                return [];
            }

            if (query.ActionKeyword == "s3d")
                return await _downloadQuery.ExecuteAsync(query, token);

            if (query.ActionKeyword == "s3u")
                return await _uploadQuery.ExecuteAsync(query, token);

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
                ResultBuilder
                    .Create(_loc.Get("settings_not_configured"))
                    .Subtitle(_loc.Get("settings_open_panel"))
                    .Action(_context.API.OpenSettingDialog)
                    .Build()
            ];
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(Main), $"S3 query failed: {ex.GetType().Name}", ex);
            _context.API.ShowMsgError("S3 Plugin Error", ex.Message);
            return [];
        }
    }

    private List<Result> BuildHomeResults()
    {
        var profile = _settings.GetActiveProfile();
        return
        [
            ResultBuilder
                .Create("s3d")
                .Subtitle(_loc.Get("oss_download_subtitle"))
                .Action(() => _context.API.ChangeQuery("s3d ", true), false)
                .Build(),
            ResultBuilder
                .Create("s3u")
                .Subtitle(_loc.Get("oss_upload_subtitle"))
                .Action(() => _context.API.ChangeQuery("s3u ", true), false)
                .Build(),
            ResultBuilder
                .Create("s3b")
                .Subtitle(_loc.Get("oss_browser_subtitle"))
                .Action(() => _context.API.ChangeQuery("s3b ", true), false)
                .Build(),
            ResultBuilder
                .Create("s3p")
                .Subtitle($"当前配置：{profile.Name}")
                .Action(() => _context.API.ChangeQuery("s3p ", true), false)
                .Build()
        ];
    }

    private List<Result> BuildProfileResults()
    {
        return _settings.Profiles
            .Select(profile => ResultBuilder
                .Create(string.Equals(profile.Id, _settings.ActiveProfileId, StringComparison.Ordinal)
                    ? $"✓ {profile.Name}"
                    : profile.Name)
                .Subtitle($"{profile.Preset} · {profile.Region} · {profile.Endpoint}")
                .Action(() =>
                {
                    _settings.ActiveProfileId = profile.Id;
                    _context.API.SaveSettingJsonStorage<Settings>();
                    _service.ClearCache();
                    _context.API.ChangeQuery("s3 ", true);
                }, false)
                .Build())
            .ToList();
    }

    private async Task<List<Result>> BuildBucketResultsAsync(
        string actionKeyword,
        string search,
        CancellationToken token)
    {
        try
        {
            var buckets = await _service.GetBucketsAsync(search, token);
            return buckets
                .Select(bucket => ResultBuilder
                    .Create(bucket)
                    .Action(() => _context.API.ChangeQuery($"{actionKeyword} {bucket} ", true), false)
                    .Build())
                .ToList();
        }
        catch (Exception ex) when (IsBucketListUnavailable(ex))
        {
            return
            [
                ResultBuilder
                    .Create("无法列出 Bucket")
                    .Subtitle("当前凭据可能没有 ListBuckets 权限，请直接输入 Bucket 名称")
                    .Build()
            ];
        }
    }

    private static bool IsBucketListUnavailable(Exception ex)
    {
        var text = ex.ToString();
        return text.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("NotImplemented", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("403", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("501", StringComparison.OrdinalIgnoreCase);
    }

    public void ReloadData()
    {
        _service.ClearCache();
    }
}
