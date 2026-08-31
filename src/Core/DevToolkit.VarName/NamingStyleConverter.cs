using System.Text.RegularExpressions;

namespace DevToolkit.VarName;

/// <summary>命名规范转换：将英文词组转为 camelCase/PascalCase/snake_case 等变体。</summary>
public static class NamingStyleConverter
{
    /// <summary>全部命名规范变体（Label + 转换结果）。</summary>
    public static IReadOnlyList<(string Label, string Value)> Variants(string raw)
    {
        var words = SplitToWords(raw);

        return
        [
            ("camelCase", ToCamelCase(words)),
            ("PascalCase", ToPascalCase(words)),
            ("snake_case", ToSnakeCase(words)),
            ("kebab-case", ToKebabCase(words)),
            ("UPPER_SNAKE_CASE", ToUpperSnakeCase(words)),
        ];
    }

    /// <summary>拆分为小写单词：兼容 camelCase / PascalCase / snake_case / kebab-case / 空格分隔。</summary>
    public static List<string> SplitToWords(string input)
    {
        var normalized = Regex.Replace(input, @"[_\-\s]+", " ");
        // 在大小写边界插入空格
        normalized = Regex.Replace(normalized, @"([a-z])([A-Z])", "$1 $2");
        return [.. normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())];
    }

    public static string ToCamelCase(List<string> words)
    {
        if (words.Count == 0) return string.Empty;
        return words[0] + string.Concat(words.Skip(1).Select(Capitalize));
    }

    public static string ToPascalCase(List<string> words)
        => string.Concat(words.Select(Capitalize));

    public static string ToSnakeCase(List<string> words)
        => string.Join('_', words);

    public static string ToKebabCase(List<string> words)
        => string.Join('-', words);

    public static string ToUpperSnakeCase(List<string> words)
        => string.Join('_', words.Select(w => w.ToUpperInvariant()));

    private static string Capitalize(string word)
        => string.IsNullOrEmpty(word) ? word : char.ToUpperInvariant(word[0]) + word[1..];
}
