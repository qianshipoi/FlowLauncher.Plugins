namespace DevToolkit.Toolbox.Services;

public sealed record TimestampEntry(string Description, string Value);

/// <summary>生成时间/时间戳条目，纯函数（时间由调用方传入）便于测试。</summary>
public static class TimestampProvider
{
    public static IReadOnlyList<TimestampEntry> GetEntries(DateTimeOffset now)
    {
        return
        [
            new TimestampEntry("本地时间", now.ToString("yyyy-MM-dd HH:mm:ss.fff")),
            new TimestampEntry("Unix 时间戳（秒）", now.ToUnixTimeSeconds().ToString()),
            new TimestampEntry("Unix 时间戳（毫秒）", now.ToUnixTimeMilliseconds().ToString()),
            new TimestampEntry("UTC 时间（ISO 8601）", now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")),
            new TimestampEntry("本地时间（ISO 8601）", now.ToString("yyyy-MM-ddTHH:mm:ss.fffK")),
        ];
    }
}
