using FlowLauncher.Plugin.Toolbox.Services;

namespace Toolbox.UnitTest;

public class TimestampProviderTests
{
    [Fact]
    public void GetEntries_返回五类条目()
    {
        var entries = TimestampProvider.GetEntries(new DateTimeOffset(2026, 8, 28, 22, 30, 45, 123, TimeSpan.FromHours(8)));
        Assert.Equal(5, entries.Count);
        Assert.Equal("本地时间", entries[0].Description);
        Assert.Equal("Unix 时间戳（秒）", entries[1].Description);
        Assert.Equal("Unix 时间戳（毫秒）", entries[2].Description);
        Assert.Equal("UTC 时间（ISO 8601）", entries[3].Description);
        Assert.Equal("本地时间（ISO 8601）", entries[4].Description);
    }

    [Fact]
    public void GetEntries_纪元时间_秒与毫秒为零()
    {
        var epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var entries = TimestampProvider.GetEntries(epoch);

        Assert.Equal("0", entries[1].Value);
        Assert.Equal("0", entries[2].Value);
        Assert.Equal("1970-01-01T00:00:00.000Z", entries[3].Value);
    }

    [Fact]
    public void GetEntries_带时区偏移_本地与UTC格式正确()
    {
        var time = new DateTimeOffset(2026, 8, 28, 22, 30, 45, 123, TimeSpan.FromHours(8));
        var entries = TimestampProvider.GetEntries(time);

        Assert.Equal("2026-08-28 22:30:45.123", entries[0].Value);
        Assert.Equal("2026-08-28T14:30:45.123Z", entries[3].Value);
        Assert.Equal("2026-08-28T22:30:45.123+08:00", entries[4].Value);
    }

    [Fact]
    public void GetEntries_Unix时间戳与DateTimeOffset换算一致()
    {
        var time = new DateTimeOffset(2026, 8, 28, 22, 30, 45, 123, TimeSpan.FromHours(8));
        var entries = TimestampProvider.GetEntries(time);

        Assert.Equal(time.ToUnixTimeSeconds().ToString(), entries[1].Value);
        Assert.Equal(time.ToUnixTimeMilliseconds().ToString(), entries[2].Value);
    }
}
