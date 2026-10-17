using System.Text.RegularExpressions;

namespace FlowLauncher.Plugin.PaperTodo.Services;

internal static partial class MarkdownImageReferences
{
    public const string Prefix = "i:";
    public const string LocalImageBase = "https://papertodo.local/images/";

    [GeneratedRegex(@"\]\(\s*i:(?<id>\d{3,8})\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex(
        @"\]\(\s*https://papertodo\.local/images/(?<id>\d{3,8})\s*\)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LocalUrlRegex();

    public static IReadOnlySet<string> CollectImageIds(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return ReferenceRegex()
            .Matches(markdown)
            .Select(match => match.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static string ToEditorMarkdown(string storedMarkdown) =>
        ReferenceRegex().Replace(
            storedMarkdown,
            match => $"]({LocalImageBase}{match.Groups["id"].Value})");

    public static string ToStoredMarkdown(string editorMarkdown) =>
        LocalUrlRegex().Replace(
            editorMarkdown,
            match => $"]({Prefix}{match.Groups["id"].Value})");

    public static string CreateReference(string imageId) =>
        $"![image|100%](i:{imageId})";

    public static bool IsValidImageId(string? value) =>
        value is { Length: >= 3 and <= 8 } &&
        value.All(character => character is >= '0' and <= '9');
}
