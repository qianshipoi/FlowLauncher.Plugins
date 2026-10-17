namespace FlowLauncher.Plugin.PaperTodo.Models;

public static class PaperTypes
{
    public const string Todo = "todo";
    public const string Note = "note";
}

public sealed class Paper
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = PaperTypes.Todo;
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public double X { get; set; } = 180;
    public double Y { get; set; } = 140;
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 380;
    public bool AlwaysOnTop { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TodoItem> Items { get; set; } = [];

    public bool IsTodo => string.Equals(Type, PaperTypes.Todo, StringComparison.Ordinal);
    public bool IsNote => string.Equals(Type, PaperTypes.Note, StringComparison.Ordinal);
}

public sealed class TodoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PaperId { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    public int Order { get; set; }
    public DateTimeOffset? ReminderAt { get; set; }
    public DateTimeOffset? ReminderTriggeredAt { get; set; }
    public string? LinkedPaperId { get; set; }
    public string? LinkedPath { get; set; }
    public bool? LinkedPathIsDirectory { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class NoteImage
{
    public required string Id { get; init; }
    public required string PaperId { get; init; }
    public required string Mime { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public required string Sha256 { get; init; }
    public int ByteLength { get; init; }
    public required byte[] Bytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class SearchHit
{
    public required string PaperId { get; init; }
    public required string PaperType { get; init; }
    public required string PaperTitle { get; init; }
    public string? ItemId { get; init; }
    public required string Title { get; init; }
    public required string Snippet { get; init; }
    public bool Done { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class TodoRemovalResult
{
    public required TodoItem Item { get; init; }
    public bool WasDeleted { get; init; }
}
