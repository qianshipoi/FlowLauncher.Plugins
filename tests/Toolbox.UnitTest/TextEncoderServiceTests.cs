using DevToolkit.Toolbox.Services;

namespace Toolbox.UnitTest;

public class TextEncoderServiceTests
{
    [Fact]
    public void GetTransforms_Hello_Base64编码等于标准向量()
    {
        var rows = TextEncoderService.GetTransforms("Hello");
        Assert.Contains(rows, r => r.Description == "Base64 编码" && r.Value == "SGVsbG8=");
    }

    [Fact]
    public void GetTransforms_URL编解码_标准向量()
    {
        Assert.Contains(TextEncoderService.GetTransforms("a b"), r => r.Description == "URL 编码" && r.Value == "a%20b");
        Assert.Contains(TextEncoderService.GetTransforms("a%20b"), r => r.Description == "URL 解码" && r.Value == "a b");
    }

    [Fact]
    public void GetTransforms_解码无变化时省略该行()
    {
        var rows = TextEncoderService.GetTransforms("hello");
        Assert.DoesNotContain(rows, r => r.Description == "URL 解码");
        Assert.DoesNotContain(rows, r => r.Description == "HTML 实体反转义");
    }

    [Fact]
    public void GetTransforms_Hex编解码_标准向量()
    {
        Assert.Contains(TextEncoderService.GetTransforms("AB"), r => r.Description == "十六进制编码" && r.Value == "4142");
        Assert.Contains(TextEncoderService.GetTransforms("41 42"), r => r.Description == "十六进制解码" && r.Value == "AB");
    }

    [Fact]
    public void GetTransforms_Base64解码_合法与非法输入()
    {
        Assert.Contains(TextEncoderService.GetTransforms("SGVsbG8="), r => r.Description == "Base64 解码" && r.Value == "Hello");
        // 长度不是 4 的倍数 / 含非法字符 → 不出解码行
        Assert.DoesNotContain(TextEncoderService.GetTransforms("abc"), r => r.Description == "Base64 解码");
        Assert.DoesNotContain(TextEncoderService.GetTransforms("abc!"), r => r.Description == "Base64 解码");
    }

    [Fact]
    public void GetTransforms_Hex解码_非法输入不出行()
    {
        Assert.DoesNotContain(TextEncoderService.GetTransforms("xyz"), r => r.Description == "十六进制解码");
        Assert.DoesNotContain(TextEncoderService.GetTransforms("414"), r => r.Description == "十六进制解码");
    }

    [Fact]
    public void GetTransforms_HTML实体转义()
    {
        Assert.Contains(TextEncoderService.GetTransforms("<a & b>"), r => r.Description == "HTML 实体转义" && r.Value == "&lt;a &amp; b&gt;");
    }

    [Fact]
    public void GetTransforms_含加号_出现加号转空格的URL解码行()
    {
        Assert.Contains(TextEncoderService.GetTransforms("a+b%20c"), r => r.Description == "URL 解码（+ 转空格）" && r.Value == "a b c");
    }
}
