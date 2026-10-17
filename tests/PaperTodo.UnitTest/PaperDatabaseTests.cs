using FlowLauncher.Plugin.PaperTodo.Models;
using FlowLauncher.Plugin.PaperTodo.Services;

using System.IO;

namespace FlowLauncher.Plugin.PaperTodo.Tests;

public sealed class PaperDatabaseTests : IAsyncLifetime
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "papertodo-tests-" + Guid.NewGuid().ToString("N"));
    private PaperDatabase _database = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDirectory);
        _database = await PaperDatabase.OpenAsync(Path.Combine(_tempDirectory, "papertodo.db"));
        await _database.InitializeAsync();
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
    public async Task Inbox_IsCreatedOnce_AndAppendPreservesOrder()
    {
        var first = await _database.EnsureInboxAsync();
        var second = await _database.EnsureInboxAsync();
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("收件箱", first.Title);

        var one = await _database.AppendTodoAsync(first.Id, new TodoItem { Text = "第一项" });
        var two = await _database.AppendTodoAsync(first.Id, new TodoItem { Text = "第二项" });
        var items = await _database.GetTodoItemsAsync(first.Id);

        Assert.Equal([one.Id, two.Id], items.Select(item => item.Id));
        Assert.Equal([0, 1], items.Select(item => item.Order));
    }

    [Fact]
    public async Task CompletionPolicy_MoveToBottomAndDelete()
    {
        var inbox = await _database.EnsureInboxAsync();
        var first = await _database.AppendTodoAsync(inbox.Id, new TodoItem { Text = "第一项" });
        var second = await _database.AppendTodoAsync(inbox.Id, new TodoItem { Text = "第二项" });

        await _database.SetTodoDoneAsync(
            first.Id,
            true,
            TodoCompletionPolicy.MoveToBottom);
        var reordered = await _database.GetTodoItemsAsync(inbox.Id);
        Assert.Equal([second.Id, first.Id], reordered.Select(item => item.Id));

        var removed = await _database.SetTodoDoneAsync(
            second.Id,
            true,
            TodoCompletionPolicy.Delete);
        Assert.True(removed.WasDeleted);
        Assert.DoesNotContain(
            await _database.GetTodoItemsAsync(inbox.Id),
            item => item.Id == second.Id);
    }

    [Fact]
    public async Task Search_FindsChineseTodoAndNoteText()
    {
        var todoPaper = await _database.CreatePaperAsync(new Paper
        {
            Type = PaperTypes.Todo,
            Title = "工作",
            Items = [new TodoItem { Text = "整理季度报告" }],
        });
        var note = await _database.CreatePaperAsync(new Paper
        {
            Type = PaperTypes.Note,
            Title = "会议记录",
            Content = "下周讨论季度预算和人员安排",
        });

        var todoHits = await _database.SearchAsync("季度报告");
        var noteHits = await _database.SearchAsync("预算");

        Assert.Contains(todoHits, hit => hit.PaperId == todoPaper.Id && hit.PaperType == PaperTypes.Todo);
        Assert.Contains(noteHits, hit => hit.PaperId == note.Id && hit.PaperType == PaperTypes.Note);
    }

    [Fact]
    public async Task Reminder_IsReturnedOnceUntilMarkedTriggered()
    {
        var inbox = await _database.EnsureInboxAsync();
        var item = await _database.AppendTodoAsync(inbox.Id, new TodoItem
        {
            Text = "提醒事项",
            ReminderAt = DateTimeOffset.Now.AddMinutes(-1),
        });

        var due = await _database.GetDueRemindersAsync(DateTimeOffset.Now);
        Assert.Contains(due, candidate => candidate.Id == item.Id);

        var triggeredAt = DateTimeOffset.Now;
        await _database.MarkReminderTriggeredAsync(item.Id, triggeredAt);
        due = await _database.GetDueRemindersAsync(triggeredAt.AddMinutes(1));
        Assert.DoesNotContain(due, candidate => candidate.Id == item.Id);
    }

    [Fact]
    public async Task GeometryAndTitle_ArePersisted()
    {
        var paper = await _database.CreatePaperAsync(new Paper
        {
            Type = PaperTypes.Todo,
            Title = "原标题",
        });
        await _database.UpdateTitleAsync(paper.Id, "新标题");
        await _database.SaveGeometryAsync(paper.Id, 42, 84, 500, 600, alwaysOnTop: true);

        var refreshed = await _database.GetPaperAsync(paper.Id);
        Assert.NotNull(refreshed);
        Assert.Equal("新标题", refreshed.Title);
        Assert.Equal(42, refreshed.X);
        Assert.Equal(84, refreshed.Y);
        Assert.Equal(500, refreshed.Width);
        Assert.Equal(600, refreshed.Height);
        Assert.True(refreshed.AlwaysOnTop);
    }

    [Fact]
    public async Task BackupRestore_RollsDatabaseBackToSnapshot()
    {
        var settings = new Settings();
        var paths = new AppPaths(_tempDirectory);
        paths.EnsureDirectories();
        var backup = new BackupService(_database, paths, settings);
        var snapshot = Path.Combine(_tempDirectory, "snapshot.db");
        await backup.ExportAsync(snapshot);

        var inbox = await _database.EnsureInboxAsync();
        await _database.AppendTodoAsync(inbox.Id, new TodoItem { Text = "快照之后" });
        await backup.RestoreAsync(snapshot);

        var inboxAfterRestore = await _database.EnsureInboxAsync();
        var items = await _database.GetTodoItemsAsync(inboxAfterRestore.Id);
        Assert.DoesNotContain(items, item => item.Text == "快照之后");
    }

    [Fact]
    public async Task CorruptRestoreFile_IsRejected()
    {
        var corrupt = Path.Combine(_tempDirectory, "corrupt.db");
        await File.WriteAllTextAsync(corrupt, "not a sqlite database");
        await Assert.ThrowsAnyAsync<Exception>(() =>
            BackupService.ValidateDatabaseFileAsync(corrupt));
    }
}
