using FlowLauncher.Plugin.PaperTodo.Models;
using FlowLauncher.Plugin.PaperTodo.Services;

using LightningDB;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FlowLauncher.Plugin.PaperTodo.Tests;

public sealed class PaperTodoImportTests : IAsyncLifetime
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "papertodo-import-tests-" + Guid.NewGuid().ToString("N"));
    private PaperDatabase _database = null!;
    private PaperTodoImportService _importer = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDirectory);
        _database = await PaperDatabase.OpenAsync(Path.Combine(_tempDirectory, "papertodo.db"));
        await _database.InitializeAsync();
        _importer = new PaperTodoImportService(_database, new LmdbImageImporter());
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task Import_MapsTodoNoteAndImage_ThenSkipsDuplicates()
    {
        var jsonPath = Path.Combine(_tempDirectory, "data.json");
        var imageBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        await CreatePaperTodoDataAsync(jsonPath, imageBytes);

        var first = await _importer.ImportAsync(jsonPath);
        var second = await _importer.ImportAsync(jsonPath);

        Assert.Equal(2, first.ImportedPapers);
        Assert.Equal(1, first.ImportedItems);
        Assert.Equal(1, first.ImportedImages);
        Assert.Equal(2, second.SkippedPapers);
        Assert.Equal(0, second.ImportedImages);

        var note = await _database.GetPaperAsync("note-source");
        Assert.NotNull(note);
        Assert.Equal(PaperTypes.Note, note.Type);
        Assert.Contains("i:1001", note.Content, StringComparison.Ordinal);

        var image = await _database.GetNoteImageAsync("1001");
        Assert.NotNull(image);
        Assert.Equal(imageBytes, image.Bytes);

        var todo = await _database.GetPaperAsync("todo-source");
        Assert.NotNull(todo);
        Assert.Single(todo.Items);
        Assert.Equal("note-source", todo.Items[0].LinkedPaperId);
        Assert.Equal("检查导入", todo.Items[0].Text);
    }

    [Fact]
    public async Task InvalidJson_DoesNotWriteAnyPaper()
    {
        var jsonPath = Path.Combine(_tempDirectory, "bad.json");
        await File.WriteAllTextAsync(jsonPath, "{\"papers\":\"invalid\"}");

        await Assert.ThrowsAsync<InvalidDataException>(() => _importer.ImportAsync(jsonPath));
        Assert.Empty(await _database.GetRecentNotesAsync(10));
        Assert.Empty(await _database.GetRecentTodoPapersAsync(10));
    }

    [Fact]
    public async Task MissingImageDatabase_StillImportsNoteAndReportsWarning()
    {
        var jsonPath = Path.Combine(_tempDirectory, "data-without-lmdb.json");
        await File.WriteAllTextAsync(
            jsonPath,
            """
            {
              "papers": [
                {
                  "id": "note-only",
                  "type": "note",
                  "title": "只有文本",
                  "content": "![image|100%](i:1001)"
                }
              ]
            }
            """);

        var result = await _importer.ImportAsync(jsonPath);

        Assert.Equal(1, result.ImportedPapers);
        Assert.Contains(result.Warnings, warning => warning.Contains("未找到图片库", StringComparison.Ordinal));
        Assert.NotNull(await _database.GetPaperAsync("note-only"));
    }

    [Fact]
    public void Parser_FiltersUnsupportedPaperTypes_AndNormalizesOrder()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "papers": [
                { "id": "todo", "type": "todo", "title": "T", "items": [
                  { "id": "second", "text": "B", "order": 5 },
                  { "id": "first", "text": "A", "order": 2 }
                ]},
                { "id": "plugin", "type": "plugin", "title": "ignored" }
              ]
            }
            """);

        var papers = PaperTodoImportService.ParsePapers(document.RootElement);
        var todo = Assert.Single(papers);
        Assert.Equal(["first", "second"], todo.Items.Select(item => item.Id));
        Assert.Equal([0, 1], todo.Items.Select(item => item.Order));
    }

    private static async Task CreatePaperTodoDataAsync(string jsonPath, byte[] imageBytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();
        var json = JsonSerializer.Serialize(new
        {
            papers = new object[]
            {
                new
                {
                    id = "todo-source",
                    type = "todo",
                    title = "导入待办",
                    x = 10,
                    y = 20,
                    width = 300,
                    height = 380,
                    items = new[]
                    {
                        new
                        {
                            id = "todo-item",
                            text = "检查导入",
                            done = false,
                            order = 0,
                            linkedNoteId = "note-source",
                        },
                    },
                },
                new
                {
                    id = "note-source",
                    type = "note",
                    title = "导入笔记",
                    content = "正文\n\n![image|100%](i:1001)",
                    x = 50,
                    y = 60,
                    width = 600,
                    height = 500,
                },
            },
        });
        await File.WriteAllTextAsync(jsonPath, json);

        var lmdbPath = Path.Combine(Path.GetDirectoryName(jsonPath)!, "note-assets.lmdb");
        var config = new EnvironmentConfiguration
        {
            MapSize = 16 * 1024 * 1024,
            MaxDatabases = 2,
        };
        using var environment = new LightningEnvironment(lmdbPath, config);
        environment.Open(EnvironmentOpenFlags.NoSubDir);
        using var transaction = environment.BeginTransaction();
        using var metadata = transaction.OpenDatabase(
            "meta",
            new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create });
        using var blobs = transaction.OpenDatabase(
            "blob",
            new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create });
        var key = Encoding.ASCII.GetBytes("1001");
        var metadataBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = "1001",
            noteId = "note-source",
            mime = "image/png",
            width = 1,
            height = 1,
            sha256 = hash,
            byteLength = imageBytes.Length,
            createdAt = DateTimeOffset.UtcNow,
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        transaction.Put(metadata, key, metadataBytes);
        transaction.Put(blobs, key, imageBytes);
        transaction.Commit();
    }
}
