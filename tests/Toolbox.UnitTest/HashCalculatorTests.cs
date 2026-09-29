using FlowLauncher.Plugin.Toolbox.Services;

using System.Security.Cryptography;

namespace Toolbox.UnitTest;

public class HashCalculatorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "Toolbox.UnitTest", Guid.NewGuid().ToString("N"));

    public HashCalculatorTests() => Directory.CreateDirectory(_tempDir);

    [Fact]
    public void ComputeHashes_abc_各算法等于标准向量()
    {
        var file = WriteTempFile("abc.txt", "abc");
        var hashes = HashCalculator.ComputeFileHashes(file);

        AssertHash(hashes, "MD5", "900150983cd24fb0d6963f7d28e17f72");
        AssertHash(hashes, "SHA1", "a9993e364706816aba3e25717850c26c9cd0d89d");
        AssertHash(hashes, "SHA256", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        AssertHash(hashes, "SHA384", "cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7");
        AssertHash(hashes, "SHA512", "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f");
        AssertHash(hashes, "CRC32", "352441c2");
    }

    [Fact]
    public void ComputeHashes_空文件_等于标准向量()
    {
        var file = WriteTempFile("empty.bin", string.Empty);
        var hashes = HashCalculator.ComputeFileHashes(file);

        AssertHash(hashes, "MD5", "d41d8cd98f00b204e9800998ecf8427e");
        AssertHash(hashes, "SHA256", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        AssertHash(hashes, "CRC32", "00000000");
    }

    [Fact]
    public void ComputeHashes_超过缓冲区大小的文件_与一次性计算一致()
    {
        // 3MB 数据超过 1MB 流式缓冲区，验证分块拼接正确
        var data = new byte[3 * 1024 * 1024 + 17];
        Random.Shared.NextBytes(data);
        var file = Path.Combine(_tempDir, "large.bin");
        File.WriteAllBytes(file, data);

        var hashes = HashCalculator.ComputeFileHashes(file);

        AssertHash(hashes, "MD5", Convert.ToHexStringLower(MD5.HashData(data)));
        AssertHash(hashes, "SHA256", Convert.ToHexStringLower(SHA256.HashData(data)));
        AssertHash(hashes, "SHA512", Convert.ToHexStringLower(SHA512.HashData(data)));
    }

    [Fact]
    public void ComputeHashes_CRC32标准校验值_123456789()
    {
        var file = WriteTempFile("check.bin", "123456789");
        var hashes = HashCalculator.ComputeFileHashes(file);

        AssertHash(hashes, "CRC32", "cbf43926");
    }

    private string WriteTempFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static void AssertHash(IReadOnlyList<HashResult> hashes, string algorithm, string expected)
    {
        var hash = Assert.Single(hashes, h => h.Algorithm == algorithm);
        Assert.Equal(expected, hash.HashHex);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
