namespace DevToolkit.AliyunOss.Transfer;

/// <summary>传输完成后的产物：<c>Link</c> 为可分享的预签名链接，<c>LocalPath</c> 为本地文件路径。</summary>
public sealed record TransferOutcome(string? Link = null, string? LocalPath = null);

/// <summary>上传时目标对象已存在时的用户选择。</summary>
public enum UploadConflictChoice
{
    Cancel,
    Replace,
    BackupAndReplace,
}
