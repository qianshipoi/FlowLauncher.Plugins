using DevToolkit.Toolbox.Services;

namespace Toolbox.UnitTest;

public class TimestampParserTests
{
    [Fact]
    public void Parse_同一时刻的秒与毫秒表示_UTC结果一致()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_000_000_000);
        var bySeconds = TimestampParser.Parse("1000000000", now);
        var byMilliseconds = TimestampParser.Parse("1000000000000", now);

        var utcBySeconds = bySeconds.Single(r => r.Description.Contains("UTC")).Value;
        var utcByMilliseconds = byMilliseconds.Single(r => r.Description.Contains("UTC")).Value;
        Assert.Equal(utcBySeconds, utcByMilliseconds);
        Assert.Equal("2001-09-09 01:46:40.000 UTC", utcBySeconds);
    }

    [Fact]
    public void Parse_识别单位()
    {
        Assert.Contains(TimestampParser.Parse("1000000000", DateTimeOffset.Now), r => r.Description.Contains("按秒"));
        Assert.Contains(TimestampParser.Parse("1000000000000", DateTimeOffset.Now), r => r.Description.Contains("按毫秒"));
    }

    [Fact]
    public void Parse_相对时间_过去十分钟()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_000_000_000);
        Assert.Contains(TimestampParser.Parse("999999400", now), r => r.Description == "相对时间" && r.Value == "10 分钟前");
    }

    [Fact]
    public void Humanize_各量级()
    {
        Assert.Equal("刚刚", TimestampParser.Humanize(TimeSpan.Zero));
        Assert.Equal("30 秒前", TimestampParser.Humanize(TimeSpan.FromSeconds(-30)));
        Assert.Equal("5 分钟后", TimestampParser.Humanize(TimeSpan.FromMinutes(5)));
        Assert.Equal("2 小时前", TimestampParser.Humanize(TimeSpan.FromHours(-2)));
        Assert.Equal("3 天后", TimestampParser.Humanize(TimeSpan.FromDays(3)));
    }

    [Fact]
    public void Parse_无效输入_抛出格式异常()
    {
        Assert.Throws<FormatException>(() => TimestampParser.Parse("abc", DateTimeOffset.Now));
    }
}
