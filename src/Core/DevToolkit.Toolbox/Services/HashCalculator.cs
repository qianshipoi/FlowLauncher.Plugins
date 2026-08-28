using System.IO;

using System.Security.Cryptography;

namespace DevToolkit.Toolbox.Services;

public sealed record HashResult(string Algorithm, string HashHex);

/// <summary>
/// 单遍流式计算文件哈希：一次读盘同时产出 MD5/SHA1/SHA256/SHA384/SHA512/CRC32，
/// 大文件不会整体载入内存。
/// </summary>
public static class HashCalculator
{
    private const int BufferSize = 1024 * 1024;

    public static IReadOnlyList<HashResult> ComputeFileHashes(string filePath, CancellationToken token = default)
    {
        using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, FileOptions.SequentialScan);
        return ComputeHashes(stream, token);
    }

    public static IReadOnlyList<HashResult> ComputeHashes(Stream stream, CancellationToken token = default)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var sha384 = IncrementalHash.CreateHash(HashAlgorithmName.SHA384);
        using var sha512 = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        var crc32 = new Crc32();

        var buffer = new byte[BufferSize];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            md5.AppendData(buffer, 0, read);
            sha1.AppendData(buffer, 0, read);
            sha256.AppendData(buffer, 0, read);
            sha384.AppendData(buffer, 0, read);
            sha512.AppendData(buffer, 0, read);
            crc32.Append(buffer.AsSpan(0, read));
            token.ThrowIfCancellationRequested();
        }

        return
        [
            new HashResult("MD5", Convert.ToHexStringLower(md5.GetHashAndReset())),
            new HashResult("SHA1", Convert.ToHexStringLower(sha1.GetHashAndReset())),
            new HashResult("SHA256", Convert.ToHexStringLower(sha256.GetHashAndReset())),
            new HashResult("SHA384", Convert.ToHexStringLower(sha384.GetHashAndReset())),
            new HashResult("SHA512", Convert.ToHexStringLower(sha512.GetHashAndReset())),
            new HashResult("CRC32", crc32.GetHashAndReset()),
        ];
    }
}

/// <summary>CRC-32（IEEE 802.3 / zlib，多项式 0xEDB88320）。</summary>
public sealed class Crc32
{
    private static readonly uint[] s_table = BuildTable();

    private uint _crc = 0xFFFFFFFF;

    public void Append(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            _crc = (_crc >> 8) ^ s_table[(_crc ^ b) & 0xFF];
    }

    public string GetHashAndReset()
    {
        var value = _crc ^ 0xFFFFFFFF;
        _crc = 0xFFFFFFFF;
        return value.ToString("x8");
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            table[i] = crc;
        }
        return table;
    }
}
