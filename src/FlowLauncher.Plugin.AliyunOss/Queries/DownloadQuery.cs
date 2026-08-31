using Flow.Launcher.Plugin;

using DevToolkit.AliyunOss.Services;

using FlowLauncher.Plugin.AliyunOss.Views;

using System.Windows;

namespace FlowLauncher.Plugin.AliyunOss.Queries;

internal class DownloadQuery(IPublicAPI publicApi, OssService service)
{
    public Task<List<Result>> ExecuteAsync(Query query, CancellationToken token)
    {
        var bucketName = query.SearchTerms[0];
        var objectName = query.SearchTerms[1];

        if (objectName.EndsWith('/') || objectName.EndsWith('\\'))
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder.Create("无法下载").Subtitle("对象名称不能以斜杠结尾").Build()
            });
        }

        // 三个 term → 状态分支
        if (query.SearchTerms.Length >= 3)
        {
            // 文件不存在
            if (query.SearchTerms[2] == "notfound")
            {
                return Task.FromResult(new List<Result>
                {
                    ResultBuilder.Create("文件不存在").Subtitle($"{bucketName}/{objectName}").Build()
                });
            }

            // 文件已确认存在，显示下载和复制链接
            if (query.SearchTerms[2] == "detail")
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
                                        new DownloadHelperWindow(publicApi, service, url).Show();
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
        }

        // 两个 term → 查询阶段，检查文件是否存在
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
                                publicApi.ChangeQuery($"ossd {bucketName} {objectName} notfound", true);
                                return;
                            }
                            publicApi.ChangeQuery($"ossd {bucketName} {objectName} detail", true);
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
