using System.Text.RegularExpressions;

namespace FlowLauncher.Plugin.PaperTodo.Services;

internal static partial class TodoTextParser
{
    [GeneratedRegex(
        @"^\s*(?:(?:[-*+]\s+)?\[[ xX]\]\s*|[-*+]\s+|\d+[.)]\s+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ListPrefixRegex();

    public static IReadOnlyList<string> Split(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        return input
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => ListPrefixRegex().Replace(line, "").Trim())
            .Where(line => line.Length > 0)
            .Select(line => line.Length > 5000 ? line[..5000] : line)
            .Take(200)
            .ToArray();
    }
}
