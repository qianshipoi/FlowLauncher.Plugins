using System.IO;

namespace FlowLauncher.Plugin.S3.Utils;

public static class FileHasher
{
    public static string ComputeMD5(string filePath)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return Convert.ToHexStringLower(hash);
    }

    public static string ComputeMD5(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(stream);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// 异步计算文件 MD5。哈希是随体积线性增长的重活（实测 512MB ≈ 2.7 秒），
    /// 同步版在 UI 线程上跑会把气泡连同宿主一起冻住，上传路径一律用这个。
    /// </summary>
    public static async Task<string> ComputeMD5Async(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);

        return await ComputeMD5Async(stream, cancellationToken);
    }

    /// <summary>异步计算流的 MD5；流可定位时先回到起点，算完后停在末尾。</summary>
    public static async Task<string> ComputeMD5Async(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = await md5.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }
}
