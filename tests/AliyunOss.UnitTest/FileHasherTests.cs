using DevToolkit.AliyunOss.Utils;

using System.Diagnostics;
using System.IO;

namespace AliyunOss.UnitTest;

/// <summary>
/// 文件 MD5。哈希随体积线性增长（实测 512MB ≈ 2.7 秒），
/// 上传路径若同步计算会冻住 UI 线程，因此必须走异步版本。
/// </summary>
public class FileHasherTests
{
    private static string CreateTempFile(int megabytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hasher-{Guid.NewGuid():N}.bin");
        using var fs = File.Create(path);
        fs.SetLength((long)megabytes * 1024 * 1024);
        return path;
    }

    [Fact]
    public async Task ComputeMD5Async_MatchesSyncResult()
    {
        var path = CreateTempFile(4);

        try
        {
            var sync = FileHasher.ComputeMD5(path);
            var async = await FileHasher.ComputeMD5Async(path);

            Assert.Equal(sync, async);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ComputeMD5Async_HashesFileContentNotEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hasher-{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(path, "hello-oss");

            var hash = await FileHasher.ComputeMD5Async(path);

            // 内容不同则哈希不同，且为 32 位十六进制
            Assert.Equal(32, hash.Length);
            Assert.NotEqual(FileHasher.ComputeMD5(Path.GetTempFileName()), hash);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ComputeMD5Async_DoesNotBlockCallingThread()
    {
        var path = CreateTempFile(192);

        try
        {
            var sw = Stopwatch.StartNew();
            var task = FileHasher.ComputeMD5Async(path);

            // 同步版会在这里阻塞整个文件哈希的耗时（192MB ≈ 1 秒）；
            // 异步版必须立刻返回未完成的 Task，把重活留在后台
            var returnedAfter = sw.ElapsedMilliseconds;
            Assert.False(task.IsCompleted);
            Assert.True(returnedAfter < 300, $"ComputeMD5Async 阻塞了调用线程 {returnedAfter}ms");

            await task;
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ComputeMD5Async_OnStream_SeeksToBeginning()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hasher-{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(path, "seek-me");
            await using var stream = File.OpenRead(path);

            // 先读到末尾再算：可定位流应自行回到起点，否则会算出"空内容"的哈希
            stream.Seek(0, SeekOrigin.End);

            var fromEnd = await FileHasher.ComputeMD5Async(stream);
            var expected = FileHasher.ComputeMD5(path);

            Assert.Equal(expected, fromEnd);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
