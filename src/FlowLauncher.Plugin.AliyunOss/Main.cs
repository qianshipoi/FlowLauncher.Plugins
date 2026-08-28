using AlibabaCloud.OSS.V2;

using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.AliyunOss.Queries;
using FlowLauncher.Plugin.AliyunOss.Services;
using FlowLauncher.Plugin.AliyunOss.ViewModel;
using FlowLauncher.Plugin.AliyunOss.Views;

using System.IO;
using System.Reflection;
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

    static Main()
    {
        RegisterLocalAssemblyResolve();
    }

    /// <summary>
    /// 从插件目录解析 iNKORE 程序集。
    /// Flow Launcher 的 Assembly.Load 上下文不搜索插件目录，
    /// 导致 XAML 加载 iNKORE 组件时抛 FileNotFoundException。
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

    public Control CreateSettingPanel()
    {
        return new OssSettings(new OssSettingsViewModel(_context.API, _settings));
    }

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _context.API.AddActionKeyword(_context.CurrentPluginMetadata.ID, "ossd");
        _context.API.AddActionKeyword(_context.CurrentPluginMetadata.ID, "ossu");
        _context.API.AddActionKeyword(_context.CurrentPluginMetadata.ID, "ossb");
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        _service = new OssService(_context.API, _settings);
        _downloadQuery = new DownloadQuery(_context.API, _service);
        _uploadQuery = new UploadQuery(_context.API, _service, _settings);
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
                        .Subtitle("下载文件")
                        .Action(() => _context.API.ChangeQuery("ossd ", true), false)
                        .Build(),
                    ResultBuilder
                        .Create("ossu")
                        .Subtitle("上传文件")
                        .Action(() => _context.API.ChangeQuery("ossu ", true), false)
                        .Build(),
                    ResultBuilder
                        .Create("ossb")
                        .Subtitle("打开文件浏览器（列出文件、拖拽上传）")
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
                ResultBuilder.Create("请先配置阿里云 OSS 插件设置").Subtitle("按 Enter 打开设置面板").Action(_context.API.OpenSettingDialog).Build()
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
