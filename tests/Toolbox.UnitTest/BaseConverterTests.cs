using DevToolkit.Toolbox.Services;

namespace Toolbox.UnitTest;

public class BaseConverterTests
{
    [Fact]
    public void Convert_255_四种进制()
    {
        var rows = BaseConverterService.Convert("255");
        Assert.Contains(rows, r => r.Description == "十进制" && r.Value == "255");
        Assert.Contains(rows, r => r.Description == "十六进制" && r.Value == "FF");
        Assert.Contains(rows, r => r.Description == "二进制" && r.Value == "11111111");
        Assert.Contains(rows, r => r.Description == "八进制" && r.Value == "377");
    }

    [Theory]
    [InlineData("0x1f", "31")]
    [InlineData("0b101", "5")]
    [InlineData("0o17", "15")]
    [InlineData("FF", "255")]
    public void Convert_前缀与裸十六进制_自动识别(string input, string expectedDecimal)
    {
        var rows = BaseConverterService.Convert(input);
        Assert.Contains(rows, r => r.Description == "十进制" && r.Value == expectedDecimal);
    }

    [Fact]
    public void Convert_中文字符_码点与UTF8字节()
    {
        var rows = BaseConverterService.Convert("中");
        Assert.Contains(rows, r => r.Description == "字符" && r.Value == "中");
        Assert.Contains(rows, r => r.Description == "十进制码点" && r.Value == "20013");
        Assert.Contains(rows, r => r.Description == "十六进制码点" && r.Value == "U+4E2D");
        Assert.Contains(rows, r => r.Description == "UTF-8 字节" && r.Value == "e4b8ad");
    }

    [Fact]
    public void Convert_码点输入_还原字符()
    {
        Assert.Contains(BaseConverterService.Convert("U+4E2D"), r => r.Description == "字符" && r.Value == "中");
    }

    [Fact]
    public void Convert_无法识别_抛出格式异常()
    {
        Assert.Throws<FormatException>(() => BaseConverterService.Convert("zz"));
    }
}
