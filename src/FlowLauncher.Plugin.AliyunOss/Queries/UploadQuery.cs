using Flow.Launcher.Plugin;

using DevToolkit.AliyunOss;
using DevToolkit.AliyunOss.Services;
using DevToolkit.AliyunOss.Transfer;
using DevToolkit.AliyunOss.Utils;
using DevToolkit.AliyunOss.Views;

using FlowLauncher.Plugin.AliyunOss.Views;

using System.Windows;

namespace FlowLauncher.Plugin.AliyunOss.Queries;

/// <summary>
/// 支持上传，替换文件，替换时备份源文件
/// </summary>
internal class UploadQuery(IPublicAPI publicApi, OssService service, Settings settings)
{
    private const string ForwardSlash = "/";

    private readonly ITransferHost _host = new FlowTransferHost(publicApi);

    public List<Result> GetTempUploadResult(string bucketName)
    {
        return
        [
            ResultBuilder
                .Create("上传临时文件")
                .Subtitle($"上传到 {bucketName}/tmp/ 目录")
                .Action(_ => ShowUpload(bucketName, "", tempUpload: true))
                .Build()
        ];
    }

    public Task<List<Result>> ExecuteAsync(Query query, CancellationToken token = default)
    {
        if (query.SearchTerms.Length < 1 || string.IsNullOrWhiteSpace(query.SearchTerms[0]))
        {
            return Task.FromResult(new List<Result>
            {
                ResultBuilder.Create("请输入桶名").Subtitle("用法: ossu <桶名> <对象路径>").Build()
            });
        }

        var bucketName = query.SearchTerms[0];

        if (query.SearchTerms.Length < 2 || string.IsNullOrWhiteSpace(query.SearchTerms[1]))
        {
            return Task.FromResult(GetTempUploadResult(bucketName));
        }

        var objectName = OssKey.Normalize(query.SearchTerms[1]);

        // 用户可能只输入斜杠（如 "ossu bucket /"），规范化后没有有效对象名
        if (objectName.Length == 0)
        {
            return Task.FromResult(GetTempUploadResult(bucketName));
        }

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
                    .Action(_ => ShowUpload(bucketName, objectName))
                    .Build(),
                ResultBuilder
                    .Create("备份并替换文件")
                    .Subtitle("先备份已有文件再上传替换")
                    .Action(_ => ShowUpload(bucketName, objectName, forceBackup: true))
                    .Build()
            });
        }

        // 首次查询，不检查文件是否存在
        return Task.FromResult(new List<Result>
        {
            ResultBuilder
                .Create("上传文件")
                .Subtitle("选择文件并上传，如同名文件已存在则提示替换")
                .Action(_ => ShowUpload(bucketName, objectName, checkExists: true))
                .Build(),
            ResultBuilder
                .Create("备份并替换文件")
                .Subtitle("先备份已有文件再上传替换")
                .Action(_ => ShowUpload(bucketName, objectName, forceBackup: true))
                .Build()
        });
    }

    private bool ShowUpload(string bucketName, string objectName, bool checkExists = false, bool forceBackup = false, bool tempUpload = false)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            TransferWindow
                .ForUpload(service, settings, _host, bucketName, objectName, checkExists, forceBackup, tempUpload)
                .Show();
        });
        return false;
    }
}
