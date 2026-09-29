using Cronos;

namespace FlowLauncher.Plugin.Toolbox.Services;

/// <summary>Cron 表达式解析：中文含义描述 + 未来执行时间预览。</summary>
public static class CronParserService
{
    public const int PreviewCount = 5;

    private static readonly string[] WeekNames = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"];

    public static IReadOnlyList<ToolRow> GetInfo(string expression, DateTime fromLocal)
    {
        var trimmed = expression.Trim();
        var fields = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length is not (5 or 6))
            throw new FormatException("Cron 表达式应为 5 段（分 时 日 月 周）或 6 段（秒 分 时 日 月 周）");

        string description;
        try
        {
            description = Describe(fields);
        }
        catch (FormatException)
        {
            description = trimmed; // 描述器是尽力而为，解析不出就显示原始表达式
        }

        var format = fields.Length == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
        CronExpression cron;
        try
        {
            cron = CronExpression.Parse(trimmed, format);
        }
        catch (CronFormatException ex)
        {
            throw new FormatException($"无效的 Cron 表达式：{ex.Message}");
        }

        // 使用本地时区从查询时刻起计算后续执行时间
        var from = new DateTimeOffset(fromLocal);

        var results = new List<ToolRow> { new("含义", description) };

        for (var i = 1; i <= PreviewCount; i++)
        {
            var next = cron.GetNextOccurrence(from, TimeZoneInfo.Local);
            if (next is null)
            {
                results.Add(new ToolRow($"第 {i} 次及以后", "无更多匹配时间"));
                break;
            }
            var value = next.Value.LocalDateTime;
            results.Add(new ToolRow(
                $"第 {i} 次执行",
                $"{value:yyyy-MM-dd HH:mm:ss}（{WeekNames[(int)value.DayOfWeek]}）"));
            from = next.Value;
        }

        return results;
    }

    /// <summary>尽力生成中文含义描述，如 "0 9 * * 1-5" → "每周一至周五 09:00"。</summary>
    internal static string Describe(string[] fields)
    {
        var second = fields.Length == 6 ? fields[0] : null;
        var minute = fields[^5];
        var hour = fields[^4];
        var day = fields[^3];
        var month = fields[^2];
        var week = fields[^1];

        var parts = new List<string>();
        if (second != null && second != "0")
            parts.Add(second.StartsWith("*/") && int.TryParse(second[2..], out var s) ? $"每 {s} 秒" : $"秒 {second}");

        var time = DescribeTime(minute, hour);
        var dayPart = DescribeDay(day);
        var weekPart = DescribeWeek(week);
        var genericTime = time.StartsWith("每");

        if (weekPart.Length > 0 && !genericTime)
            parts.Add($"{weekPart} {time}");
        else if (dayPart.Length > 0 && !genericTime)
            parts.Add($"{dayPart} {time}");
        else if (dayPart.Length == 0 && weekPart.Length == 0 && !genericTime)
            parts.Add($"每天 {time}");
        else
            parts.Add(time);

        var monthPart = DescribeMonth(month);
        if (monthPart.Length > 0)
            parts.Add(monthPart);

        return string.Join("，", parts);
    }

    private static string DescribeTime(string minute, string hour)
    {
        if (minute == "*" && hour == "*")
            return "每分钟";

        if (hour == "*")
        {
            if (minute.StartsWith("*/") && int.TryParse(minute[2..], out var step))
                return $"每 {step} 分钟";
            return $"每小时第 {minute.Replace(',', '、')} 分";
        }

        if (minute == "*")
            return $"每天 {ExpandList(hour)} 点时段内每分钟";

        if (IsIntList(hour) && IsIntList(minute))
        {
            var times = hour.Split(',')
                .SelectMany(h => minute.Split(',').Select(m => $"{int.Parse(h):00}:{int.Parse(m):00}"));
            return string.Join("、", times);
        }

        return $"时 {hour} · 分 {minute}";
    }

    private static string DescribeDay(string day)
    {
        if (day == "*") return "";
        if (day.StartsWith("*/") && int.TryParse(day[2..], out var step)) return $"每 {step} 日";
        if (day.Contains('/')) return "";
        return $"每月 {ExpandList(day)} 号";
    }

    private static string DescribeWeek(string week)
    {
        if (week == "*" || week.StartsWith("*/")) return "";
        var tokens = week.Split(',').Select(token =>
        {
            var dash = token.IndexOf('-');
            return dash > 0
                ? $"{WeekName(int.Parse(token[..dash]))}至{WeekName(int.Parse(token[(dash + 1)..]))}"
                : WeekName(int.Parse(token));
        });
        return "每" + string.Join("、", tokens);
    }

    private static string DescribeMonth(string month)
    {
        if (month == "*") return "";
        if (month.StartsWith("*/") && int.TryParse(month[2..], out var step)) return $"每 {step} 个月";
        if (month.Contains('/')) return "";
        return $"每年 {ExpandList(month)} 月";
    }

    private static string WeekName(int value) => WeekNames[value % 7];

    private static string ExpandList(string field) => field.Replace(',', '、').Replace("-", " 至 ");

    private static bool IsIntList(string field) =>
        field.Split(',').All(part => part.Length > 0 && part.All(char.IsAsciiDigit));
}
