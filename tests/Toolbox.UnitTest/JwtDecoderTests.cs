using System.Text;

using FlowLauncher.Plugin.Toolbox.Services;

namespace Toolbox.UnitTest;

public class JwtDecoderTests
{
    private static string Base64Url(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string BuildToken(string payloadJson, out long nowSeconds)
    {
        nowSeconds = 1_700_000_000;
        var header = Base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = Base64Url(payloadJson);
        return $"{header}.{payload}.signature";
    }

    [Fact]
    public void Decode_标准三段式_解析Header与Payload()
    {
        var payloadJson = "{\"sub\":\"1234567890\",\"name\":\"中文名\",\"exp\":1700003600,\"iat\":1699996400}";
        var token = BuildToken(payloadJson, out var nowSeconds);
        var now = DateTimeOffset.FromUnixTimeSeconds(nowSeconds);

        var rows = JwtDecoderService.Decode(token, now);

        Assert.Contains(rows, r => r.Description == "Header" && r.Value == "{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        Assert.Contains(rows, r => r.Description == "Payload" && r.Value.Contains("\"sub\":\"1234567890\"") && r.Value.Contains("\"name\":\"中文名\""));
        Assert.Contains(rows, r => r.Description == "过期时间" && r.Value.Contains("剩余 1 小时"));
        Assert.Contains(rows, r => r.Description == "签发时间");
    }

    [Fact]
    public void Decode_已过期的token_显示已过期()
    {
        var token = BuildToken("{\"exp\":1699996400}", out var nowSeconds);
        var rows = JwtDecoderService.Decode(token, DateTimeOffset.FromUnixTimeSeconds(nowSeconds));
        Assert.Contains(rows, r => r.Description == "过期时间" && r.Value.Contains("已过期"));
    }

    [Fact]
    public void Decode_两段式_允许缺失签名()
    {
        var token = $"{Base64Url("{\"alg\":\"none\"}")}.{Base64Url("{}")}";
        var rows = JwtDecoderService.Decode(token, DateTimeOffset.Now);
        Assert.Contains(rows, r => r.Description == "Header");
        Assert.Contains(rows, r => r.Description == "Payload" && r.Value == "{}");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("a.b.c.d")]
    [InlineData("!!!!.e30")]
    public void Decode_非法输入_抛出格式异常(string token)
    {
        Assert.Throws<FormatException>(() => JwtDecoderService.Decode(token, DateTimeOffset.Now));
    }
}
