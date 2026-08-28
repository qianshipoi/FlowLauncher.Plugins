using DevToolkit.Toolbox.Services;

namespace Toolbox.UnitTest;

public class PasswordGeneratorTests
{
    [Fact]
    public void Generate_三行结果且长度正确()
    {
        var rows = PasswordGeneratorService.Generate(32);
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(32, r.Value.Length));
    }

    [Fact]
    public void Generate_强密码包含四类字符()
    {
        var password = PasswordGeneratorService.Generate(32).First(r => r.Description.StartsWith("强密码")).Value;
        Assert.Contains(password, c => char.IsLower(c));
        Assert.Contains(password, c => char.IsUpper(c));
        Assert.Contains(password, c => char.IsDigit(c));
        Assert.Contains(password, c => !char.IsLetterOrDigit(c));
    }

    [Fact]
    public void Generate_无易混淆密码_不含0O1lI且仅字母数字()
    {
        var password = PasswordGeneratorService.Generate(64).First(r => r.Description.Contains("易混淆")).Value;
        Assert.All(password, c => Assert.False("0O1lI".Contains(c)));
        Assert.All(password, c => Assert.True(char.IsLetterOrDigit(c)));
    }

    [Fact]
    public void Generate_Hex密钥_仅含小写十六进制()
    {
        var hex = PasswordGeneratorService.Generate(40).First(r => r.Description.Contains("Hex")).Value;
        Assert.Matches("^[0-9a-f]{40}$", hex);
    }

    [Fact]
    public void Generate_多次调用结果不同()
    {
        var first = PasswordGeneratorService.Generate(24)[0].Value;
        var second = PasswordGeneratorService.Generate(24)[0].Value;
        Assert.NotEqual(first, second);
    }
}
