using DevToolkit.AliyunOss.Models;
using DevToolkit.AliyunOss.Services;
using DevToolkit.AliyunOss.Transfer;

using System.IO;

namespace DevToolkit.AliyunOss;

/// <summary>上传请求。对象名在打开文件选择框后即可确定，界面因此能预先显示最终目标。</summary>
public sealed record UploadRequest(string Bucket, string Key, string FilePath);

/// <summary>
/// 上传/下载的业务编排：冲突处理、备份、上传、生成访问链接。
/// 不依赖任何 WPF 或宿主 API，进度与交互通过参数回传。
/// </summary>
public sealed class TransferEngine(OssService service)
{
    /// <summary>临时上传使用的对象名（<c>tmp/{guid}{ext}</c>）。</summary>
    public static string BuildTempKey(string filePath) =>
        $"tmp/{Guid.NewGuid():N}{Path.GetExtension(filePath)}";

    /// <summary>上传前处理同名对象：返回用户选择，备份分支会先完成备份。</summary>
    public async Task<UploadConflictChoice> ResolveConflictAsync(
        string bucket,
        string key,
        bool forceBackup,
        ITransferUi ui,
        CancellationToken cancellationToken = default)
    {
        if (forceBackup)
        {
            ui.SetStatus("正在备份原文件…");
            await BackupAsync(bucket, key, cancellationToken);
            return UploadConflictChoice.BackupAndReplace;
        }

        ui.SetStatus("正在检查同名对象…");
        if (!await service.ExistsAsync(bucket, key, cancellationToken))
        {
            return UploadConflictChoice.Replace;
        }

        var choice = await ui.AskConflictAsync(key, cancellationToken);
        if (choice == UploadConflictChoice.BackupAndReplace)
        {
            ui.SetStatus("正在备份原文件…");
            await BackupAsync(bucket, key, cancellationToken);
        }

        return choice;
    }

    /// <summary>上传文件并在结束后生成 GET 预签名访问链接。</summary>
    public async Task<TransferOutcome> UploadAsync(
        UploadRequest request,
        ITransferUi ui,
        IProgress<TransferProgress> progress,
        CancellationToken cancellationToken = default)
    {
        ui.SetStatus("正在上传…");
        await service.UploadFileAsync(request.Bucket, request.Key, request.FilePath, progress, cancellationToken);

        ui.SetStatus("正在生成访问链接…");
        var link = await service.PresignDownloadUrlAsync(request.Bucket, request.Key, cancellationToken);

        return new TransferOutcome(link);
    }

    /// <summary>下载到指定本地路径。</summary>
    public async Task<TransferOutcome> DownloadAsync(
        string url,
        string filePath,
        ITransferUi ui,
        IProgress<TransferProgress> progress,
        CancellationToken cancellationToken = default)
    {
        ui.SetStatus("正在下载…");
        await DownloadService.DownloadFileAsync(url, filePath, progress, cancellationToken);
        return new TransferOutcome(null, filePath);
    }

    private async Task BackupAsync(string bucket, string key, CancellationToken cancellationToken)
    {
        var backupKey = $"{key}.bak.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        await service.CopyObjectAsync(bucket, key, bucket, backupKey, cancellationToken);
    }
}
