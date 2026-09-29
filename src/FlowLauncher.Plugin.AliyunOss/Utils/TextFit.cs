namespace FlowLauncher.Plugin.AliyunOss.Utils;

/// <summary>
/// 按可用宽度为文字挑选字号。
/// 圆形气泡中央宽度有限，字节数这类长文本（如「1024.00 GB / 2048.00 GB」）
/// 用固定字号必然溢出，需按长度缩号。
/// </summary>
public static class TextFit
{
    /// <summary>
    /// 数字、字母、空格与标点混排下的经验平均字宽（相对字号）。
    /// 取 0.6 偏保守：宁可略小也不溢出。
    /// </summary>
    private const double AverageGlyphWidthRatio = 0.6;

    /// <summary>
    /// 返回能放进 <paramref name="availableWidth"/> 的最大字号，并收敛到 [min, max]。
    /// </summary>
    public static double FontSizeFor(string? text, double availableWidth, double min, double max)
    {
        if (string.IsNullOrEmpty(text)) return max;

        var fitted = availableWidth / (text.Length * AverageGlyphWidthRatio);
        return Math.Clamp(fitted, min, max);
    }
}
