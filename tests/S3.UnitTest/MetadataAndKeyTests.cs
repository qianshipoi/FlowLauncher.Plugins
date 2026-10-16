using FlowLauncher.Plugin.S3;
using FlowLauncher.Plugin.S3.Models;
using FlowLauncher.Plugin.S3.Transfer;
using FlowLauncher.Plugin.S3.Utils;

namespace S3.UnitTest;

public class MetadataAndKeyTests
{
    [Theory]
    [InlineData("x-amz-meta-file-md5", "file-md5")]
    [InlineData("X-OSS-META-File-MD5", "file-md5")]
    [InlineData("source", "source")]
    public void NormalizesMetadataKeys(string input, string expected)
    {
        Assert.Equal(expected, MetadataKey.Normalize(input));
    }

    [Theory]
    [InlineData("/a//b\\c", "a/b/c")]
    [InlineData("  /tmp/file.txt  ", "tmp/file.txt")]
    public void NormalizesObjectKeys(string input, string expected)
    {
        Assert.Equal(expected, ObjectKey.Normalize(input));
    }

    [Fact]
    public void FixedUploadKeyStaysStable()
    {
        Assert.Equal("dir/file.txt", TransferEngine.ResolveKey("dir/file.txt", @"C:\one.txt"));
        Assert.Equal("dir/file.txt", TransferEngine.ResolveKey("dir/file.txt", @"C:\two.txt"));
    }

    [Fact]
    public void TemporaryUploadKeysAreUniqueAndPreserveExtension()
    {
        var first = TransferEngine.ResolveKey("", @"C:\one.txt");
        var second = TransferEngine.ResolveKey(null, @"C:\two.txt");

        Assert.StartsWith("tmp/", first);
        Assert.StartsWith("tmp/", second);
        Assert.EndsWith(".txt", first);
        Assert.EndsWith(".txt", second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ConcurrencyTokenChangesWhenUserMetadataChanges()
    {
        var first = new ObjectMetadata(
            "etag",
            1,
            DateTimeOffset.UnixEpoch,
            new Dictionary<string, string> { ["source"] = "one" },
            null,
            null,
            null,
            null,
            null);
        var second = first with
        {
            UserMetadata = new Dictionary<string, string> { ["source"] = "two" }
        };

        Assert.NotEqual(first.ConcurrencyToken, second.ConcurrencyToken);
    }
}
