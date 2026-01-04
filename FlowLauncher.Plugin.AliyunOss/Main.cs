using AlibabaCloud.OSS.V2;

using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.AliyunOss.Queries;
using FlowLauncher.Plugin.AliyunOss.Services;
using FlowLauncher.Plugin.AliyunOss.ViewModel;
using FlowLauncher.Plugin.AliyunOss.Views;

using System.Windows.Controls;

namespace FlowLauncher.Plugin.AliyunOss;

public class Main : IAsyncPlugin, IReloadable, ISettingProvider
{
    private PluginInitContext _context = default!;
    private OssService _service = default!;
    private Settings _settings = default!;
    private DownloadQuery _downloadQuery = default!;
    private UploadQuery _uploadQuery = default!;

    public Control CreateSettingPanel()
    {
        return new OssSettings(new OssSettingsViewModel(_context.API, _settings));
    }

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _context.API.AddActionKeyword(_context.CurrentPluginMetadata.ID, "ossd");
        _context.API.AddActionKeyword(_context.CurrentPluginMetadata.ID, "ossu");
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        _service = new OssService(_context.API, _settings);
        _downloadQuery = new DownloadQuery(_context.API, _service);
        _uploadQuery = new UploadQuery(_context.API, _service);
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
                    ResultBuilder.Create("ossd").Action(() => _context.API.ChangeQuery("ossd ", true), false).Build(),
                    ResultBuilder.Create("ossu").Action(() => _context.API.ChangeQuery("ossu ", true), false).Build()
                ];
            }

            if (string.IsNullOrWhiteSpace(query.Search) || query.SearchTerms.Length == 1)
            {
                var buckets = await _service.GetBucketsAsync(query.Search ?? string.Empty, token);

                return [.. 
                    buckets
                    .Select(x => 
                        ResultBuilder
                            .Create(x)
                            .Action(() => _context.API.ChangeQuery(query.ActionKeyword + " " + x + " ", true))
                            .Build())
                    ];
            }

            if (query.ActionKeyword == "ossd")
            {
                return await _downloadQuery.ExecuteAsync(query, token);
            }
            else if (query.ActionKeyword == "oosu")
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
                ResultBuilder.Create("请先配置阿里云 OSS 插件设置").Subtitle("按 Enter 打开设置面板").Action(_context.API.OpenSettingDialog).Build()
            ];
        }
        catch (OperationException)
        {
            return [];
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(Main), "Glbal Exception", ex);
            _context.API.ShowMsgError("Aliyun OSS Plugin Error", ex.Message);
            return [];
        }
    }

    public void ReloadData()
    {
        _service.ClearCache();
    }
}
