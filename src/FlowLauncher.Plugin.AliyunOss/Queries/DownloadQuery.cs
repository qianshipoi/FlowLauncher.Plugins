using Flow.Launcher.Plugin;

using DevToolkit.AliyunOss;
using DevToolkit.AliyunOss.Services;
using DevToolkit.AliyunOss.Transfer;
using DevToolkit.AliyunOss.Utils;
using DevToolkit.AliyunOss.Views;

using FlowLauncher.Plugin.AliyunOss.Views;

using System.Windows;

namespace FlowLauncher.Plugin.AliyunOss.Queries;

internal class DownloadQuery(IPublicAPI publicApi, OssService service, Settings settings)
{
    private const string StatusNotFound = "notfound";
    private const string StatusDetail = "detail";

    private readonly ITransferHost _host = new FlowTransferHost(publicApi);

    public Task<List<Result>> ExecuteAsync(Query query, CancellationToken token)
    {
        if (query.SearchTerms.Length < 1 || string.IsNullOrWhiteSpace(query.SearchTerms[0]))
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder.Create("请输入桶名").Subtitle("用法: ossd <桶名> <对象路径>").Build()
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

        // 仅桶名（或对象名为空）→ 提示输入对象路径，避免 SearchTerms[1] 越界
        if (parts.Count == 0)
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder
                    .Create("请输入对象名称")
                    .Subtitle($"用法: ossd {bucketName} <对象路径>")
                    .Build()
            });
        }

        var objectName = OssKey.Normalize(string.Join(' ', parts));

        // 用户可能只输入了斜杠（如 "ossd bucket /"），规范化后为空
        if (objectName.Length == 0)
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder
                    .Create("请输入对象名称")
                    .Subtitle($"用法: ossd {bucketName} <对象路径>")
                    .Build()
            });
        }

        if (objectName.EndsWith('/'))
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder.Create("无法下载").Subtitle("对象名称不能以斜杠结尾").Build()
            });
        }

        // 文件不存在
        if (status == StatusNotFound)
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder.Create("文件不存在").Subtitle($"{bucketName}/{objectName}").Build()
            });
        }

        // 文件已确认存在，显示下载和复制链接
        if (status == StatusDetail)
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder
                    .Create("下载")
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
                                    TransferWindow.ForDownload(service, settings, _host, url).Show();
                                });
                            }
                            catch (Exception ex) { publicApi.ShowMsgError("下载失败", ex.Message); }
                        }
                        DoDownload();
                        return false;
                    })
                    .Build(),
                ResultBuilder
                    .Create("复制链接")
                    .Subtitle($"生成预签名链接并复制")
                    .Action(_ =>
                    {
                        async void DoCopy()
                        {
                            try
                            {
                                var url = await service.PresignDownloadUrlAsync(bucketName, objectName);
                                publicApi.CopyToClipboard(url);
                                publicApi.ShowMsg("已复制", "预签名链接已复制到剪贴板");
                            }
                            catch (Exception ex) { publicApi.ShowMsgError("操作失败", ex.Message); }
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
                .Create("查询")
                .Subtitle($"{bucketName}/{objectName}，按 Enter 检查文件是否存在")
                .Action(_ =>
                {
                    async void DoCheck()
                    {
                        try
                        {
                            var exists = await service.ExistsAsync(bucketName, objectName);
                            if (!exists)
                            {
                                publicApi.ChangeQuery($"ossd {bucketName} {objectName} {StatusNotFound}", true);
                                return;
                            }
                            publicApi.ChangeQuery($"ossd {bucketName} {objectName} {StatusDetail}", true);
                        }
                        catch (Exception ex) { publicApi.ShowMsgError("查询失败", ex.Message); }
                    }
                    DoCheck();
                    return false;
                })
                .Build()
        });
    }
}
