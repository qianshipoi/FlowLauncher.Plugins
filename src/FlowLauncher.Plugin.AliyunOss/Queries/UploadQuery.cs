using Flow.Launcher.Plugin;

using FlowLauncher.Plugin.AliyunOss.Services;

using Microsoft.Win32;

using System.IO;
using System.Windows;

namespace FlowLauncher.Plugin.AliyunOss.Queries;

/// <summary>
/// 支持上传，替换文件，替换时备份源文件
/// </summary>
/// <param name="publicApi"></param>
/// <param name="service"></param>
internal class UploadQuery(IPublicAPI publicApi, OssService service)
{
    private const string ForwardSlash = "/";

    public async Task<List<Result>> ExecuteAsync(Query query, CancellationToken token = default)
    {
        var bucketName = query.SearchTerms[0];
        var objectName = query.SearchTerms[1];

        if (objectName.EndsWith(ForwardSlash))
        {
            return
            [
                ResultBuilder.Create("无法上传").Subtitle($"对象名称不能以 {ForwardSlash} 结尾").Build()
            ];
        }

        var fileExist = await service.ExistsAsync(bucketName, objectName, token);

        var uploadTitle = fileExist ? "替换文件" : "上传文件";

        return
        [
            ResultBuilder.Create(uploadTitle).AsyncAction(ctx => UploadAsync(bucketName, objectName, token)).Build(),
            ResultBuilder.Create("备份并替换文件").AsyncAction(ctx => BackupAndReplaceAsync(bucketName, objectName, token)).Build()
        ];
    }

    private async ValueTask<bool> BackupAndReplaceAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        var backupKey = $"{key}.bak.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        await service.CopyObjectAsync(bucket, key, bucket, backupKey, cancellationToken);
        await UploadAsync(bucket, key, cancellationToken);
        return true;
    }

    private async ValueTask<bool> UploadAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        await publicApi.ShowProgressBoxAsync("上传中...", async (reportProcess) => {
            var fileName = Path.GetFileName(key);
            var ext = Path.GetExtension(fileName);
            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                DefaultExt = ext,
                Filter = string.IsNullOrWhiteSpace(ext) ? "所有文件|*.*" : $"{ext} 文件|*{ext}|所有文件|*.*",
            };
            var result = dialog.ShowDialog();
            if (result != true) return;
            var filePath = dialog.FileName;
            await service.UploadFileAsync(bucket, key, filePath, reportProcess, cancellationToken);

            var dailogResult = publicApi.ShowMsgBox("上传完毕，是否复制下载链接", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (dailogResult == MessageBoxResult.Yes)
            {
                var url = await service.PresignDownloadUrlAsync(bucket, key, cancellationToken);
                publicApi.CopyToClipboard(url);
            }
        });
        return true;
    }
}
