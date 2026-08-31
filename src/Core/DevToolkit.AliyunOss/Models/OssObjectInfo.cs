using System.IO;

namespace DevToolkit.AliyunOss.Models;

public record OssObjectInfo(string Key, long Size, DateTimeOffset LastModified)
{
    public string FileName => Path.GetFileName(Key.TrimEnd('/'));
    public bool IsDirectory => Key.EndsWith('/');
    public string SizeDisplay => IsDirectory ? "" : FormatSize(Size);

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}
