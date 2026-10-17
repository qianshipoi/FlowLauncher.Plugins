using FlowLauncher.Plugin.PaperTodo.Models;

using System.Globalization;
using System.IO;
using System.Text.Json;

namespace FlowLauncher.Plugin.PaperTodo.Services;

internal sealed class PaperTodoImportService(
    PaperDatabase database,
    LmdbImageImporter imageImporter)
{
    public async Task<PaperTodoImportSummary> ImportAsync(
        string dataJsonPath,
        CancellationToken token = default)
    {
        if (!File.Exists(dataJsonPath))
        {
            throw new FileNotFoundException("PaperTodo 数据文件不存在。", dataJsonPath);
        }

        List<Paper> papers;
        await using (var stream = File.OpenRead(dataJsonPath))
        using (var document = await JsonDocument.ParseAsync(stream, cancellationToken: token))
        {
            papers = ParsePapers(document.RootElement);
        }

        if (papers.Count == 0)
        {
            throw new InvalidDataException("data.json 中没有可导入的 Todo 或 Note。");
        }

        var warnings = new List<string>();
        var notes = papers.Where(paper => paper.IsNote).ToArray();
        var lmdbPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(dataJsonPath))!,
            "note-assets.lmdb");
        var images = imageImporter.ReadReferencedImages(lmdbPath, notes, warnings);
        var databaseResult = await database.ImportBatchAsync(papers, images, token);
        return new PaperTodoImportSummary(
            databaseResult.ImportedPapers,
            databaseResult.SkippedPapers,
            databaseResult.ImportedItems,
            databaseResult.ImportedImages,
            warnings);
    }

    internal static List<Paper> ParsePapers(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(root, "papers", out var papersElement) ||
            papersElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("data.json 缺少 papers 数组。");
        }

        var papers = new List<Paper>();
        foreach (var element in papersElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var type = GetString(element, "type").Trim().ToLowerInvariant();
            if (type is not PaperTypes.Todo and not PaperTypes.Note)
            {
                continue;
            }

            var now = DateTimeOffset.UtcNow;
            var paper = new Paper
            {
                Id = NormalizeId(GetString(element, "id")),
                Type = type,
                Title = type == PaperTypes.Note
                    ? GetString(element, "title").Trim()
                    : GetString(element, "title").Trim(),
                Content = type == PaperTypes.Note ? GetString(element, "content") : "",
                X = GetDouble(element, "x", 180),
                Y = GetDouble(element, "y", 140),
                Width = Math.Max(240, GetDouble(element, "width", type == PaperTypes.Note ? 620 : 320)),
                Height = Math.Max(180, GetDouble(element, "height", type == PaperTypes.Note ? 520 : 380)),
                AlwaysOnTop = GetBool(element, "alwaysOnTop"),
                CreatedAt = now,
                UpdatedAt = now,
            };

            if (type == PaperTypes.Todo &&
                TryGetProperty(element, "items", out var itemsElement) &&
                itemsElement.ValueKind == JsonValueKind.Array)
            {
                var order = 0;
                foreach (var itemElement in itemsElement.EnumerateArray())
                {
                    if (itemElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }
                    var text = GetString(itemElement, "text");
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    var reminderAt = GetNullableDate(itemElement, "reminderAt");
                    paper.Items.Add(new TodoItem
                    {
                        Id = NormalizeId(GetString(itemElement, "id")),
                        PaperId = paper.Id,
                        Text = text.Length > 5000 ? text[..5000] : text,
                        Done = GetBool(itemElement, "done"),
                        Order = GetInt32(itemElement, "order", order++),
                        ReminderAt = reminderAt,
                        ReminderTriggeredAt = GetBool(itemElement, "reminderTriggered")
                            ? now
                            : null,
                        LinkedPaperId = NormalizeNullableId(GetString(itemElement, "linkedNoteId")),
                        LinkedPath = NormalizeNullable(GetString(itemElement, "linkedPath")),
                        LinkedPathIsDirectory = TryGetProperty(itemElement, "linkedPathIsDirectory", out var directory) &&
                                                 directory.ValueKind is JsonValueKind.True or JsonValueKind.False
                            ? directory.GetBoolean()
                            : null,
                        CreatedAt = now,
                        UpdatedAt = now,
                    });
                }

                var sorted = paper.Items.OrderBy(item => item.Order).ToList();
                for (var index = 0; index < sorted.Count; index++)
                {
                    sorted[index].Order = index;
                }
                paper.Items = sorted;
            }

            papers.Add(paper);
        }
        return papers;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string GetString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static bool GetBool(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();

    private static double GetDouble(JsonElement element, string name, double fallback) =>
        TryGetProperty(element, name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var parsed) &&
        double.IsFinite(parsed)
            ? parsed
            : fallback;

    private static int GetInt32(JsonElement element, string name, int fallback) =>
        TryGetProperty(element, name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var parsed)
            ? parsed
            : fallback;

    private static DateTimeOffset? GetNullableDate(JsonElement element, string name)
    {
        var text = GetString(element, name);
        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeId(string value) =>
        string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim();

    private static string? NormalizeNullableId(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeNullable(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed record PaperTodoImportSummary(
    int ImportedPapers,
    int SkippedPapers,
    int ImportedItems,
    int ImportedImages,
    IReadOnlyList<string> Warnings);
