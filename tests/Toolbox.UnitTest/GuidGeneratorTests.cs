using FlowLauncher.Plugin.Toolbox.Services;

namespace Toolbox.UnitTest;

public class GuidGeneratorTests
{
    [Fact]
    public void NewV5_DNS命名空间_pythonorg_等于RFC标准向量()
    {
        var guid = GuidGenerator.NewV5(GuidGenerator.DnsNamespace, "python.org");
        Assert.Equal("886313e1-3b8a-5372-9b90-0c9aee199e5d", guid.ToString());
    }

    [Fact]
    public void NewV3_版本位与变体位正确()
    {
        AssertVersionAndVariant(GuidGenerator.NewV3(GuidGenerator.DnsNamespace, "test"), '3');
    }

    [Fact]
    public void NewV3_同输入结果确定_与v5不同()
    {
        var v3a = GuidGenerator.NewV3(GuidGenerator.DnsNamespace, "abc");
        var v3b = GuidGenerator.NewV3(GuidGenerator.DnsNamespace, "abc");
        var v5 = GuidGenerator.NewV5(GuidGenerator.DnsNamespace, "abc");

        Assert.Equal(v3a, v3b);
        Assert.NotEqual(v3a, v5);
    }

    [Fact]
    public void NewV4_版本位与变体位正确()
    {
        AssertVersionAndVariant(GuidGenerator.NewV4(), '4');
    }

    [Fact]
    public void NewV1_版本位与变体位正确()
    {
        AssertVersionAndVariant(GuidGenerator.NewV1(), '1');
    }

    [Fact]
    public void NewV1_高频生成不重复()
    {
        var guids = new HashSet<Guid>(Enumerable.Range(0, 1000).Select(_ => GuidGenerator.NewV1()));
        Assert.Equal(1000, guids.Count);
    }

    [Fact]
    public void NewV7_版本位与变体位正确()
    {
        AssertVersionAndVariant(GuidGenerator.NewV7(), '7');
    }

    private static void AssertVersionAndVariant(Guid guid, char version)
    {
        var hex = guid.ToString("N");
        Assert.Equal(version, hex[12]);
        Assert.True(hex[16] is '8' or '9' or 'a' or 'b', $"变体位应为 RFC 4122 格式，实际为 {hex[16]}");
    }
}
