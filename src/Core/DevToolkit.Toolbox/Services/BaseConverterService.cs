using System.Globalization;

using System.Text;

namespace DevToolkit.Toolbox.Services;

/// <summary>进制转换与字符码点互查：支持 0x/0b/0o 前缀、十进制、裸十六进制回退、单字符与 U+码点。</summary>
public static class BaseConverterService
{
    public static IReadOnlyList<ToolRow> Convert(string input)
    {
        input = input.Trim();
        if (input.Length == 0)
            throw new FormatException("输入为空");

        if (input.StartsWith("u+", StringComparison.OrdinalIgnoreCase))
            return FromCodepoint(ParseCodepoint(input[2..], input));

        // 单个非数字字符按字符处理（"5" 这类仍按数字）
        if (input.Length == 1 && !char.IsDigit(input[0]))
            return FromCodepoint(input[0]);

        return FromNumber(ParseNumber(input));
    }

    private static IReadOnlyList<ToolRow> FromCodepoint(int codepoint)
    {
        var text = char.ConvertFromUtf32(codepoint);
        return
        [
            new ToolRow("字符", text),
            new ToolRow("十进制码点", codepoint.ToString(CultureInfo.InvariantCulture)),
            new ToolRow("十六进制码点", $"U+{codepoint:X4}"),
            new ToolRow("UTF-8 字节", System.Convert.ToHexString(Encoding.UTF8.GetBytes(text)).ToLowerInvariant()),
        ];
    }

    private static IReadOnlyList<ToolRow> FromNumber(long value)
    {
        return
        [
            new ToolRow("十进制", value.ToString(CultureInfo.InvariantCulture)),
            new ToolRow("十六进制", System.Convert.ToString(value, 16).ToUpperInvariant()),
            new ToolRow("二进制", System.Convert.ToString(value, 2)),
            new ToolRow("八进制", System.Convert.ToString(value, 8)),
        ];
    }

    private static long ParseNumber(string input)
    {
        var fromBase = 10;
        var text = input;
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) { fromBase = 16; text = input[2..]; }
        else if (input.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) { fromBase = 2; text = input[2..]; }
        else if (input.StartsWith("0o", StringComparison.OrdinalIgnoreCase)) { fromBase = 8; text = input[2..]; }

        if (text.Length > 0)
        {
            if (TryConvert(text, fromBase, out var value)) return value;
            // 十进制解析失败时回退按十六进制，方便直接粘贴 FF 这类值
            if (fromBase == 10 && TryConvert(text, 16, out value)) return value;
        }
        throw new FormatException($"无法识别的输入：{input}");
    }

    private static bool TryConvert(string text, int fromBase, out long value)
    {
        try
        {
            value = System.Convert.ToInt64(text, fromBase);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            value = 0;
            return false;
        }
    }

    private static int ParseCodepoint(string text, string input)
    {
        if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codepoint)
            || codepoint < 0 || codepoint > 0x10FFFF
            || codepoint is >= 0xD800 and <= 0xDFFF)
            throw new FormatException($"无效的 Unicode 码点：{input}");
        return codepoint;
    }
}
