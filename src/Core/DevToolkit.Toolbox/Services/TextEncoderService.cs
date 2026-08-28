using System.Net;

using System.Text;

namespace DevToolkit.Toolbox.Services;

/// <summary>文本编解码转换（UTF-8）。解码类结果仅在与输入不同且可成功解码时返回。</summary>
public static class TextEncoderService
{
    public static IReadOnlyList<ToolRow> GetTransforms(string input)
    {
        var results = new List<ToolRow>
        {
            new("URL 编码", Uri.EscapeDataString(input)),
            new("HTML 实体转义", WebUtility.HtmlEncode(input)),
            new("十六进制编码", Convert.ToHexString(Encoding.UTF8.GetBytes(input)).ToLowerInvariant()),
            new("Base64 编码", Convert.ToBase64String(Encoding.UTF8.GetBytes(input))),
        };

        AddIfChanged(results, input, "URL 解码", Uri.UnescapeDataString(input));
        if (input.Contains('+'))
            AddIfChanged(results, input, "URL 解码（+ 转空格）", Uri.UnescapeDataString(input.Replace('+', ' ')));
        AddIfChanged(results, input, "HTML 实体反转义", WebUtility.HtmlDecode(input));
        AddIfChanged(results, input, "十六进制解码", TryHexDecode(input));
        AddIfChanged(results, input, "Base64 解码", TryBase64Decode(input));

        return results;
    }

    private static void AddIfChanged(List<ToolRow> results, string input, string description, string? value)
    {
        if (!string.IsNullOrEmpty(value) && value != input)
            results.Add(new ToolRow(description, value));
    }

    /// <summary>接受 "616263"、"61 62 63"、"61:62:63" 等形式；要求合法十六进制且为有效 UTF-8。</summary>
    private static string? TryHexDecode(string input)
    {
        var compact = input.Trim().Replace(" ", "").Replace(":", "");
        if (compact.Length < 2 || compact.Length % 2 != 0 || compact.Any(c => !Uri.IsHexDigit(c)))
            return null;
        return ToUtf8(Convert.FromHexString(compact));
    }

    private static string? TryBase64Decode(string input)
    {
        var compact = input.Trim().Replace('-', '+').Replace('_', '/');
        if (compact.Length == 0 || compact.Length % 4 != 0 || compact.Any(c => !IsBase64Char(c)))
            return null;
        try
        {
            return ToUtf8(Convert.FromBase64String(compact));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? ToUtf8(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        // 含 U+FFFD 说明不是合法 UTF-8 文本，避免输出乱码行
        return text.Contains('\uFFFD') ? null : text;
    }

    private static bool IsBase64Char(char c) =>
        c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '/' or '=';
}
