using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.AliyunOss.Services;
using FlowLauncher.Plugin.AliyunOss.Views;
using System.Windows;

namespace FlowLauncher.Plugin.AliyunOss.Queries;

/// <summary>
/// 支持上传，替换文件，替换时备份源文件
/// </summary>
/// <param name="publicApi"></param>
/// <param name="service"></param>
internal class UploadQuery(IPublicAPI publicApi, OssService service, Settings settings)
{
    private const string ForwardSlash = "/";

    public List<Result> GetTempUploadResult(string bucketName)
    {
        return
        [
            ResultBuilder
                .Create("上传临时文件")
                .Subtitle($"上传到 {bucketName}/tmp/ 目录")
                .Action(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        new UploadHelperWindow(publicApi, service, bucketName, "", tempUpload: true, settings: settings).Show();
                    });
                    return false;
                })
                .Build()
        ];
    }

    public Task<List<Result>> ExecuteAsync(Query query, CancellationToken token = default)
    {
        var bucketName = query.SearchTerms[0];
        publicApi.LogInfo(nameof(UploadQuery), $"[TRACE] ExecuteAsync called. Search='{query.Search}', SearchTerms.Length={query.SearchTerms.Length}, Terms=[{string.Join(", ", query.SearchTerms.Select(t => $"'{t}'"))}]");

        if (query.SearchTerms.Length < 2 || string.IsNullOrWhiteSpace(query.SearchTerms[1]))
        {
            publicApi.LogInfo(nameof(UploadQuery), $"[TRACE] Returning temp upload result for bucket '{bucketName}'");
            return Task.FromResult(GetTempUploadResult(bucketName));
        }

        var objectName = query.SearchTerms[1];
        publicApi.LogInfo(nameof(UploadQuery), $"[TRACE] objectName='{objectName}'");

        if (objectName.EndsWith(ForwardSlash))
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder.Create("无法上传").Subtitle($"对象名称不能以 {ForwardSlash} 结尾").Build()
            });
        }

        // 三个 term 且末尾为空 → 文件已确认存在，显示替换选项
        if (query.SearchTerms.Length == 3)
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder
                    .Create("替换文件")
                    .Subtitle($"{objectName} 已存在，上传并替换")
                    .Action(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            new UploadHelperWindow(publicApi, service, bucketName, objectName, settings: settings).Show();
                        });
                        return false;
                    })
                    .Build(),
                ResultBuilder
                    .Create("备份并替换文件")
                    .Subtitle("先备份已有文件再上传替换")
                    .Action(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            new UploadHelperWindow(publicApi, service, bucketName, objectName, forceBackup: true, settings: settings).Show();
                        });
                        return false;
                    })
                    .Build()
            });
        }

        // 首次查询，不检查文件是否存在
        return Task.FromResult(new List<Result>
        {
            ResultBuilder
                .Create("上传文件")
                .Subtitle("选择文件并上传，如同名文件已存在则提示替换")
                .Action(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        new UploadHelperWindow(publicApi, service, bucketName, objectName, checkExists: true, settings: settings).Show();
                    });
                    return false;
                })
                .Build(),
            ResultBuilder
                .Create("备份并替换文件")
                .Subtitle("先备份已有文件再上传替换")
                .Action(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        new UploadHelperWindow(publicApi, service, bucketName, objectName, settings: settings).Show();
                    });
                    return false;
                })
                .Build()
        });
    }
}
