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
public sealed class TransferEngine(OssService service, ILocalization loc)
{

    private readonly ILocalization _loc = loc;

    /// <summary>临时上传使用的对象名（<c>tmp/{guid}{ext}</c>）。</summary>
    public static string BuildTempKey(string filePath) =>
        $"tmp/{Guid.NewGuid():N}{Path.GetExtension(filePath)}";

    /// <summary>
    /// 解析本次上传的目标对象名。常驻拖拽气泡会被反复拖入文件，两种语义要分清：
    /// 用户指定了对象名则每次都传到该路径（同名即覆盖，符合"传到这个位置"的直觉）；
    /// 未指定（临时上传）则为每个文件现场生成随机路径，避免多次拖拽互相覆盖。
    /// </summary>
    /// <param name="fixedKey">用户指定的对象名；空表示临时上传。</param>
    /// <param name="filePath">本地文件路径，用于取扩展名。</param>
    public static string ResolveKey(string? fixedKey, string filePath) =>
        string.IsNullOrWhiteSpace(fixedKey) ? BuildTempKey(filePath) : fixedKey;

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
            ui.SetStatus(_loc.Get("transfer_status_backup"));
            await BackupAsync(bucket, key, cancellationToken);
            return UploadConflictChoice.BackupAndReplace;
        }

        ui.SetStatus(_loc.Get("transfer_status_checking"));
        if (!await service.ExistsAsync(bucket, key, cancellationToken))
        {
            return UploadConflictChoice.Replace;
        }

        var choice = await ui.AskConflictAsync(key, cancellationToken);
        if (choice == UploadConflictChoice.BackupAndReplace)
        {
            ui.SetStatus(_loc.Get("transfer_status_backup"));
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
        ui.SetStatus(_loc.Get("transfer_status_uploading"));
        await service.UploadFileAsync(request.Bucket, request.Key, request.FilePath, progress, cancellationToken);

        ui.SetStatus(_loc.Get("transfer_status_signing"));
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
        ui.SetStatus(_loc.Get("transfer_status_downloading"));
        await DownloadService.DownloadFileAsync(url, filePath, progress, cancellationToken);
        return new TransferOutcome(null, filePath);
    }

    private async Task BackupAsync(string bucket, string key, CancellationToken cancellationToken)
    {
        var backupKey = $"{key}.bak.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        await service.CopyObjectAsync(bucket, key, bucket, backupKey, cancellationToken);
    }
}
