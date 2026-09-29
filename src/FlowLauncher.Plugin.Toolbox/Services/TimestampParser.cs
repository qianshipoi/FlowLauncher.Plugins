namespace FlowLauncher.Plugin.Toolbox.Services;

/// <summary>时间戳解析：自动识别秒（≤10^12）/ 毫秒（>10^12），并给出相对时间描述。</summary>
public static class TimestampParser
{
    public static IReadOnlyList<ToolRow> Parse(string input, DateTimeOffset now)
    {
        if (!long.TryParse(input.Trim(), out var value))
            throw new FormatException($"无法识别的时间戳：{input}");

        var isMilliseconds = Math.Abs(value) >= 1_000_000_000_000;
        var dto = isMilliseconds
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : DateTimeOffset.FromUnixTimeSeconds(value);
        var unit = isMilliseconds ? "毫秒" : "秒";

        return
        [
            new ToolRow($"本地时间（按{unit}）", dto.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff")),
            new ToolRow($"UTC 时间（按{unit}）", dto.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'")),
            new ToolRow("相对时间", Humanize(dto - now)),
        ];
    }

    /// <summary>把时间差转为 "3 天前 / 5 小时后 / 刚刚" 风格描述。</summary>
    internal static string Humanize(TimeSpan delta)
    {
        var text = HumanizeMagnitude(delta < TimeSpan.Zero ? -delta : delta);
        if (text == "刚刚")
            return text;
        return delta < TimeSpan.Zero ? $"{text}前" : $"{text}后";
    }

    internal static string HumanizeMagnitude(TimeSpan magnitude)
    {
        return magnitude switch
        {
            { TotalSeconds: < 1 } => "刚刚",
            { TotalMinutes: < 1 } => $"{(int)magnitude.TotalSeconds} 秒",
            { TotalHours: < 1 } => $"{(int)magnitude.TotalMinutes} 分钟",
            { TotalDays: < 1 } => $"{(int)magnitude.TotalHours} 小时",
            { TotalDays: < 30 } => $"{(int)magnitude.TotalDays} 天",
            { TotalDays: < 365 } => $"{(int)(magnitude.TotalDays / 30)} 个月",
            _ => $"{(int)(magnitude.TotalDays / 365)} 年",
        };
    }
}
