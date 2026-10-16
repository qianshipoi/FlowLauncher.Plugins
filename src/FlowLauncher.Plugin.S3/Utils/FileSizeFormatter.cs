namespace FlowLauncher.Plugin.S3.Utils;

/// <summary>字节量、速率与剩余时间的人类可读格式化。</summary>
public static class FileSizeFormatter
{
    public static string Format(long bytes) => bytes switch
    {
        < 0 => "",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        < 1024L * 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
        _ => $"{bytes / (1024.0 * 1024 * 1024 * 1024):F2} TB"
    };

    /// <summary>
    /// 紧凑格式，用于圆形气泡中央这类宽度受限处。
    /// 数值达到 100 后省略小数位：大容量下小数没有意义，却会让
    /// 「1024.00 GB / 2048.00 GB」这类文本长到放不进圆内。
    /// </summary>
    public static string FormatCompact(long bytes) => bytes switch
    {
        < 0 => "",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => Trim(bytes / 1024.0, "KB"),
        < 1024L * 1024 * 1024 => Trim(bytes / (1024.0 * 1024), "MB"),
        < 1024L * 1024 * 1024 * 1024 => Trim(bytes / (1024.0 * 1024 * 1024), "GB"),
        _ => Trim(bytes / (1024.0 * 1024 * 1024 * 1024), "TB")
    };

    private static string Trim(double value, string unit)
    {
        // 数值达到 100 后省略小数位：大容量下小数没有意义，却会明显拉长文本
        var digits = value >= 100 ? 0 : 1;
        return $"{value.ToString($"F{digits}", System.Globalization.CultureInfo.InvariantCulture)} {unit}";
    }

    public static string FormatSpeed(double bytesPerSecond) =>
        bytesPerSecond <= 0 ? "" : Format((long)bytesPerSecond) + "/s";
}
