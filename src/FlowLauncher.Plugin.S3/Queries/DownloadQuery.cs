using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.S3;
using FlowLauncher.Plugin.S3.Services;
using FlowLauncher.Plugin.S3.Transfer;
using FlowLauncher.Plugin.S3.Utils;
using FlowLauncher.Plugin.S3.Views;

using System.Windows;

namespace FlowLauncher.Plugin.S3.Queries;

internal class DownloadQuery(IPublicAPI publicApi, S3StorageService service, Settings settings)
{
    private const string StatusNotFound = "notfound";
    private const string StatusDetail = "detail";

    private readonly ITransferHost _host = new FlowTransferHost(publicApi);
    private readonly ILocalization _loc = new FlowLocalization(publicApi);

    private string T(string key, params object[] args) => _loc.Get(key, args);

    public Task<List<Result>> ExecuteAsync(Query query, CancellationToken token)
    {
        if (query.SearchTerms.Length < 1 || string.IsNullOrWhiteSpace(query.SearchTerms[0]))
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder.Create(T("ossd_input_bucket")).Subtitle(T("ossd_usage")).Build()
            });
        }

        var bucketName = query.SearchTerms[0];

        // 末尾状态关键字（由 ChangeQuery 写入），其余部分拼成对象名，兼容带空格的路径
        var parts = query.SearchTerms.Skip(1).ToList();
        string? status = null;
        if (parts.Count > 0 && parts[^1] is StatusNotFound or StatusDetail)
        {
            status = parts[^1];
            parts.RemoveAt(parts.Count - 1);
        }

        while (parts.Count > 0 && string.IsNullOrWhiteSpace(parts[^1]))
        {
            parts.RemoveAt(parts.Count - 1);
        }

        var usage = T("ossd_usage");

        // 仅桶名（或对象名为空）→ 提示输入对象路径，避免 SearchTerms[1] 越界
        if (parts.Count == 0)
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder
                    .Create(T("ossd_input_object"))
                    .Subtitle(usage)
                    .Build()
            });
        }

        var objectName = ObjectKey.Normalize(string.Join(' ', parts));

        // 用户可能只输入了斜杠（如 "s3d bucket /"），规范化后为空
        if (objectName.Length == 0)
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder
                    .Create(T("ossd_input_object"))
                    .Subtitle(usage)
                    .Build()
            });
        }

        if (objectName.EndsWith('/'))
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder.Create(T("ossd_object_slash")).Build()
            });
        }

        // 文件不存在
        if (status == StatusNotFound)
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder.Create(T("ossd_not_found")).Subtitle($"{bucketName}/{objectName}").Build()
            });
        }

        // 文件已确认存在，显示下载和复制链接
        if (status == StatusDetail)
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder
                    .Create(T("ossd_action_download"))
                    .Subtitle($"{bucketName}/{objectName}")
                    .Action(_ =>
                    {
                        async void DoDownload()
                        {
                            try
                            {
                                var url = await service.PresignDownloadUrlAsync(bucketName, objectName);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    TransferWindow.ForDownload(service, settings, _host, url, _loc).Show();
                                });
                            }
                            catch (Exception ex) { publicApi.ShowMsgError(T("ossd_download_failed"), ex.Message); }
                        }
                        DoDownload();
                        return false;
                    })
                    .Build(),
                ResultBuilder
                    .Create(T("ossd_action_copy_link"))
                    .Action(_ =>
                    {
                        async void DoCopy()
                        {
                            try
                            {
                                var url = await service.PresignDownloadUrlAsync(bucketName, objectName);
                                publicApi.CopyToClipboard(url);
                                publicApi.ShowMsg(T("ossd_copy_done"), T("ossd_copy_done_subtitle"));
                            }
                            catch (Exception ex) { publicApi.ShowMsgError(T("transfer_status_failed"), ex.Message); }
                        }
                        DoCopy();
                        return false;
                    })
                    .Build()
            });
        }

        // 查询阶段，检查文件是否存在
        return Task.FromResult(new List<Result>
        {
            ResultBuilder
                .Create(T("ossd_action_query"))
                .Subtitle(T("ossd_query_hint", $"{bucketName}/{objectName}"))
                .Action(_ =>
                {
                    async void DoCheck()
                    {
                        try
                        {
                            var exists = await service.ExistsAsync(bucketName, objectName);
                            if (!exists)
                            {
                                publicApi.ChangeQuery($"s3d {bucketName} {objectName} {StatusNotFound}", true);
                                return;
                            }
                            publicApi.ChangeQuery($"s3d {bucketName} {objectName} {StatusDetail}", true);
                        }
                        catch (Exception ex) { publicApi.ShowMsgError(T("ossd_query_failed"), ex.Message); }
                    }
                    DoCheck();
                    return false;
                })
                .Build()
        });
    }
}
