using System.IO;

namespace FlowLauncher.Plugin.AliyunOss.Utils;

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
}
