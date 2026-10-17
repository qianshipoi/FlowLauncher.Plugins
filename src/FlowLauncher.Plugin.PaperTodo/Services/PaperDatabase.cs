using FlowLauncher.Plugin.PaperTodo.Models;

using Microsoft.Data.Sqlite;

using System.Globalization;
using System.IO;
using System.Text;

namespace FlowLauncher.Plugin.PaperTodo.Services;

internal sealed class PaperDatabase : IAsyncDisposable
{
    private const int SchemaVersion = 1;
    private const string InboxKey = "inbox_paper_id";
    private const int DefaultImageNumber = 1001;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteConnection? _connection;
    private bool _ftsAvailable;
    private bool _disposed;

    private PaperDatabase(string databasePath)
    {
        DatabasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath { get; }

    public static async Task<PaperDatabase> OpenAsync(
        string databasePath,
        CancellationToken token = default)
    {
        var database = new PaperDatabase(databasePath);
        try
        {
            await database.OpenConnectionAsync(token);
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public async Task InitializeAsync(CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            var existingVersion = Convert.ToInt32(
                await ExecuteScalarAsync("PRAGMA user_version;", token),
                CultureInfo.InvariantCulture);
            if (existingVersion > SchemaVersion)
            {
                throw new InvalidDataException(
                    $"数据库版本 {existingVersion} 高于当前插件支持的版本 {SchemaVersion}。");
            }

            await ExecuteNonQueryAsync(
                """
                CREATE TABLE IF NOT EXISTS papers (
                    id             TEXT PRIMARY KEY,
                    type           TEXT NOT NULL CHECK (type IN ('todo', 'note')),
                    title          TEXT NOT NULL,
                    content        TEXT NOT NULL DEFAULT '',
                    x              REAL NOT NULL DEFAULT 180,
                    y              REAL NOT NULL DEFAULT 140,
                    width          REAL NOT NULL DEFAULT 320,
                    height         REAL NOT NULL DEFAULT 380,
                    always_on_top  INTEGER NOT NULL DEFAULT 0,
                    created_at     TEXT NOT NULL,
                    updated_at     TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS todo_items (
                    id                    TEXT PRIMARY KEY,
                    paper_id              TEXT NOT NULL REFERENCES papers(id) ON DELETE CASCADE,
                    text                  TEXT NOT NULL,
                    done                  INTEGER NOT NULL DEFAULT 0,
                    sort_order            INTEGER NOT NULL DEFAULT 0,
                    reminder_at           TEXT NULL,
                    reminder_triggered_at TEXT NULL,
                    linked_paper_id       TEXT NULL REFERENCES papers(id) ON DELETE SET NULL,
                    linked_path           TEXT NULL,
                    linked_path_is_directory INTEGER NULL,
                    created_at            TEXT NOT NULL,
                    updated_at            TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS note_images (
                    id          TEXT PRIMARY KEY,
                    paper_id    TEXT NOT NULL REFERENCES papers(id) ON DELETE CASCADE,
                    mime        TEXT NOT NULL,
                    width       INTEGER NOT NULL,
                    height      INTEGER NOT NULL,
                    sha256      TEXT NOT NULL,
                    byte_length INTEGER NOT NULL,
                    bytes       BLOB NOT NULL,
                    created_at  TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS metadata (
                    key   TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_todo_items_paper_order
                    ON todo_items(paper_id, sort_order);
                CREATE INDEX IF NOT EXISTS idx_todo_items_due
                    ON todo_items(done, reminder_at);
                CREATE INDEX IF NOT EXISTS idx_note_images_paper
                    ON note_images(paper_id);
                CREATE INDEX IF NOT EXISTS idx_papers_type_updated
                    ON papers(type, updated_at DESC);
                """,
                token);

            try
            {
                await ExecuteNonQueryAsync(
                    """
                    CREATE VIRTUAL TABLE IF NOT EXISTS search_index USING fts5(
                        doc_id UNINDEXED,
                        kind UNINDEXED,
                        paper_id UNINDEXED,
                        item_id UNINDEXED,
                        title,
                        body,
                        tokenize='trigram'
                    );
                    """,
                    token);
                _ftsAvailable = true;
            }
            catch (SqliteException)
            {
                _ftsAvailable = false;
            }

            if (existingVersion < SchemaVersion)
            {
                await ExecuteNonQueryAsync($"PRAGMA user_version = {SchemaVersion};", token);
            }
            await EnsureSearchIndexAsync(token);
        });
    }

    public async Task<bool> QuickCheckAsync(CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            await using var command = _connection!.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            var result = Convert.ToString(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        });
    }

    public async Task<Paper?> GetPaperAsync(string paperId, CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            var paper = await ReadPaperAsync(paperId, includeItems: true, token);
            return paper;
        });
    }

    public async Task<IReadOnlyList<Paper>> GetRecentTodoPapersAsync(
        int limit,
        CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            await using var command = _connection!.CreateCommand();
            command.CommandText =
                """
                SELECT id, type, title, content, x, y, width, height, always_on_top, created_at, updated_at
                FROM papers
                WHERE type = 'todo'
                ORDER BY updated_at DESC
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));
            return await ReadPapersAsync(command, includeItems: true, token);
        });
    }

    public async Task<IReadOnlyList<Paper>> GetRecentNotesAsync(
        int limit,
        CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            await using var command = _connection!.CreateCommand();
            command.CommandText =
                """
                SELECT id, type, title, content, x, y, width, height, always_on_top, created_at, updated_at
                FROM papers
                WHERE type = 'note'
                ORDER BY updated_at DESC
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));
            return await ReadPapersAsync(command, includeItems: false, token);
        });
    }

    public async Task<IReadOnlyList<TodoItem>> GetRecentTodoItemsAsync(
        int limit,
        CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            await using var command = _connection!.CreateCommand();
            command.CommandText =
                """
                SELECT ti.id, ti.paper_id, ti.text, ti.done, ti.sort_order,
                       ti.reminder_at, ti.reminder_triggered_at,
                       ti.linked_paper_id, ti.linked_path, ti.linked_path_is_directory,
                       ti.created_at, ti.updated_at
                FROM todo_items ti
                JOIN papers p ON p.id = ti.paper_id
                WHERE TRIM(ti.text) <> ''
                ORDER BY ti.done ASC, p.updated_at DESC, ti.sort_order ASC
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));
            return await ReadTodoItemsAsync(command, token);
        });
    }

    public async Task<IReadOnlyList<TodoItem>> GetTodoItemsAsync(
        string paperId,
        CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            return await ReadTodoItemsByPaperAsync(paperId, token);
        });
    }

    public async Task<TodoItem?> GetTodoItemAsync(string itemId, CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            await using var command = _connection!.CreateCommand();
            command.CommandText =
                """
                SELECT id, paper_id, text, done, sort_order,
                       reminder_at, reminder_triggered_at,
                       linked_paper_id, linked_path, linked_path_is_directory,
                       created_at, updated_at
                FROM todo_items
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", itemId);
            var items = await ReadTodoItemsAsync(command, token);
            return items.Count == 0 ? null : items[0];
        });
    }

    public async Task<Paper> EnsureInboxAsync(CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            var inboxId = await ReadMetadataAsync(InboxKey, token);
            if (!string.IsNullOrWhiteSpace(inboxId))
            {
                var existing = await ReadPaperAsync(inboxId, includeItems: true, token);
                if (existing is { IsTodo: true })
                {
                    return existing;
                }
            }

            var paper = new Paper
            {
                Type = PaperTypes.Todo,
                Title = "收件箱",
                Width = 320,
                Height = 400,
            };
            await InsertPaperLockedAsync(paper, token);
            await WriteMetadataLockedAsync(InboxKey, paper.Id, token);
            await RefreshSearchForPaperLockedAsync(paper.Id, token);
            return paper;
        });
    }

    public async Task<Paper> CreatePaperAsync(Paper paper, CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            var now = DateTimeOffset.UtcNow;
            paper.CreatedAt = paper.CreatedAt == default ? now : paper.CreatedAt;
            paper.UpdatedAt = now;
            await InsertPaperLockedAsync(paper, token);
            foreach (var item in paper.Items)
            {
                item.PaperId = paper.Id;
                item.CreatedAt = item.CreatedAt == default ? now : item.CreatedAt;
                item.UpdatedAt = now;
                await InsertTodoItemLockedAsync(item, token);
            }
            await RefreshSearchForPaperLockedAsync(paper.Id, token);
            return paper;
        });
    }

    public async Task<TodoItem> AppendTodoAsync(
        string paperId,
        TodoItem item,
        CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            var paper = await ReadPaperAsync(paperId, includeItems: false, token, transaction);
            if (paper is not { IsTodo: true })
            {
                throw new InvalidOperationException("目标不是 Todo 纸片。");
            }

            item.PaperId = paperId;
            item.Order = await NextTodoOrderLockedAsync(paperId, token, transaction);
            item.CreatedAt = DateTimeOffset.UtcNow;
            item.UpdatedAt = item.CreatedAt;
            await InsertTodoItemLockedAsync(item, token, transaction);
            await TouchPaperLockedAsync(paperId, token, transaction);
            await RefreshSearchForPaperLockedAsync(paperId, token, transaction);
            await transaction.CommitAsync(token);
            return item;
        });
    }

    public async Task<IReadOnlyList<TodoItem>> AppendTodosAsync(
        string paperId,
        IReadOnlyList<TodoItem> items,
        CancellationToken token = default)
    {
        if (items.Count == 0)
        {
            return [];
        }

        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            var paper = await ReadPaperAsync(paperId, includeItems: false, token, transaction);
            if (paper is not { IsTodo: true })
            {
                throw new InvalidOperationException("目标不是 Todo 纸片。");
            }

            var order = await NextTodoOrderLockedAsync(paperId, token, transaction);
            var now = DateTimeOffset.UtcNow;
            foreach (var item in items)
            {
                item.PaperId = paperId;
                item.Order = order++;
                item.CreatedAt = now;
                item.UpdatedAt = now;
                await InsertTodoItemLockedAsync(item, token, transaction);
            }
            await TouchPaperLockedAsync(paperId, token, transaction);
            await RefreshSearchForPaperLockedAsync(paperId, token, transaction);
            await transaction.CommitAsync(token);
            return items;
        });
    }

    public async Task UpdateTodoTextAsync(
        string itemId,
        string text,
        CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            var item = await ReadTodoItemLockedAsync(itemId, token, transaction)
                ?? throw new InvalidOperationException("待办事项不存在。");
            item.Text = text;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await UpdateTodoItemLockedAsync(item, token, transaction);
            await TouchPaperLockedAsync(item.PaperId, token, transaction);
            await RefreshSearchForPaperLockedAsync(item.PaperId, token, transaction);
            await transaction.CommitAsync(token);
            return true;
        });
    }

    public async Task<TodoRemovalResult> SetTodoDoneAsync(
        string itemId,
        bool done,
        TodoCompletionPolicy policy,
        CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            var item = await ReadTodoItemLockedAsync(itemId, token, transaction)
                ?? throw new InvalidOperationException("待办事项不存在。");
            var original = CloneTodo(item);
            item.Done = done;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            if (done)
            {
                item.ReminderAt = null;
                item.ReminderTriggeredAt = null;
            }

            var deleted = done && policy == TodoCompletionPolicy.Delete;
            if (deleted)
            {
                await DeleteTodoItemLockedAsync(item.Id, token, transaction);
            }
            else
            {
                if (policy == TodoCompletionPolicy.MoveToBottom)
                {
                    item.Order = await NextTodoOrderLockedAsync(item.PaperId, token, transaction);
                }
                await UpdateTodoItemLockedAsync(item, token, transaction);
            }

            await TouchPaperLockedAsync(item.PaperId, token, transaction);
            await RefreshSearchForPaperLockedAsync(item.PaperId, token, transaction);
            await transaction.CommitAsync(token);
            return new TodoRemovalResult
            {
                Item = deleted ? original : item,
                WasDeleted = deleted,
            };
        });
    }

    public async Task ReorderTodoItemsAsync(
        string paperId,
        IReadOnlyList<string> orderedItemIds,
        CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            for (var index = 0; index < orderedItemIds.Count; index++)
            {
                await using var command = _connection!.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    UPDATE todo_items
                    SET sort_order = $order, updated_at = $updated
                    WHERE id = $id AND paper_id = $paperId;
                    """;
                command.Parameters.AddWithValue("$order", index);
                command.Parameters.AddWithValue("$updated", FormatDate(DateTimeOffset.UtcNow));
                command.Parameters.AddWithValue("$id", orderedItemIds[index]);
                command.Parameters.AddWithValue("$paperId", paperId);
                await command.ExecuteNonQueryAsync(token);
            }
            await TouchPaperLockedAsync(paperId, token, transaction);
            await transaction.CommitAsync(token);
        });
    }

    public async Task SetTodoReminderAsync(
        string itemId,
        DateTimeOffset? reminderAt,
        CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            var item = await ReadTodoItemLockedAsync(itemId, token, transaction)
                ?? throw new InvalidOperationException("待办事项不存在。");
            item.ReminderAt = reminderAt;
            item.ReminderTriggeredAt = null;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await UpdateTodoItemLockedAsync(item, token, transaction);
            await TouchPaperLockedAsync(item.PaperId, token, transaction);
            await transaction.CommitAsync(token);
        });
    }

    public async Task SetTodoLinkAsync(
        string itemId,
        string? linkedPaperId,
        string? linkedPath,
        bool? linkedPathIsDirectory,
        CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            var item = await ReadTodoItemLockedAsync(itemId, token, transaction)
                ?? throw new InvalidOperationException("待办事项不存在。");
            item.LinkedPaperId = string.IsNullOrWhiteSpace(linkedPaperId) ? null : linkedPaperId;
            item.LinkedPath = string.IsNullOrWhiteSpace(linkedPath) ? null : linkedPath;
            item.LinkedPathIsDirectory = item.LinkedPath is null ? null : linkedPathIsDirectory;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await UpdateTodoItemLockedAsync(item, token, transaction);
            await TouchPaperLockedAsync(item.PaperId, token, transaction);
            await transaction.CommitAsync(token);
        });
    }

    public async Task DeleteTodoAsync(string itemId, CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            var item = await ReadTodoItemLockedAsync(itemId, token, transaction)
                ?? throw new InvalidOperationException("待办事项不存在。");
            await DeleteTodoItemLockedAsync(item.Id, token, transaction);
            await TouchPaperLockedAsync(item.PaperId, token, transaction);
            await RefreshSearchForPaperLockedAsync(item.PaperId, token, transaction);
            await transaction.CommitAsync(token);
        });
    }

    public async Task UpdateTitleAsync(
        string paperId,
        string title,
        CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            await using var command = _connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "UPDATE papers SET title = $title, updated_at = $updated WHERE id = $id;";
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$updated", FormatDate(DateTimeOffset.UtcNow));
            command.Parameters.AddWithValue("$id", paperId);
            await command.ExecuteNonQueryAsync(token);
            await RefreshSearchForPaperLockedAsync(paperId, token, transaction);
            await transaction.CommitAsync(token);
        });
    }

    public async Task UpdateNoteContentAsync(
        string paperId,
        string content,
        CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            await using var command = _connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE papers
                SET content = $content, updated_at = $updated
                WHERE id = $id AND type = 'note';
                """;
            command.Parameters.AddWithValue("$content", content);
            command.Parameters.AddWithValue("$updated", FormatDate(DateTimeOffset.UtcNow));
            command.Parameters.AddWithValue("$id", paperId);
            if (await command.ExecuteNonQueryAsync(token) == 0)
            {
                throw new InvalidOperationException("笔记不存在。");
            }
            await RefreshSearchForPaperLockedAsync(paperId, token, transaction);
            await transaction.CommitAsync(token);
        });
    }

    public async Task SaveGeometryAsync(
        string paperId,
        double x,
        double y,
        double width,
        double height,
        bool alwaysOnTop,
        CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            await using var command = _connection!.CreateCommand();
            command.CommandText =
                """
                UPDATE papers
                SET x = $x, y = $y, width = $width, height = $height,
                    always_on_top = $top
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$x", x);
            command.Parameters.AddWithValue("$y", y);
            command.Parameters.AddWithValue("$width", Math.Max(240, width));
            command.Parameters.AddWithValue("$height", Math.Max(180, height));
            command.Parameters.AddWithValue("$top", alwaysOnTop ? 1 : 0);
            command.Parameters.AddWithValue("$id", paperId);
            await command.ExecuteNonQueryAsync(token);
        });
    }

    public async Task DeletePaperAsync(string paperId, CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            if (_ftsAvailable)
            {
                await using var fts = _connection!.CreateCommand();
                fts.Transaction = transaction;
                fts.CommandText = "DELETE FROM search_index WHERE paper_id = $paperId;";
                fts.Parameters.AddWithValue("$paperId", paperId);
                await fts.ExecuteNonQueryAsync(token);
            }
            await using var command = _connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM papers WHERE id = $id;";
            command.Parameters.AddWithValue("$id", paperId);
            await command.ExecuteNonQueryAsync(token);
            await transaction.CommitAsync(token);
        });
    }

    public async Task<SearchHit[]> SearchAsync(
        string query,
        int limit = 30,
        CancellationToken token = default)
    {
        var normalized = query.Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            if (_ftsAvailable && normalized.Length >= 3)
            {
                try
                {
                    return await SearchFtsLockedAsync(normalized, limit, token);
                }
                catch (SqliteException)
                {
                    // User input can still be a syntactically invalid FTS expression after quoting.
                }
            }
            return await SearchLikeLockedAsync(normalized, limit, token);
        });
    }

    public async Task<IReadOnlyList<TodoItem>> GetDueRemindersAsync(
        DateTimeOffset now,
        CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            await using var command = _connection!.CreateCommand();
            command.CommandText =
                """
                SELECT id, paper_id, text, done, sort_order,
                       reminder_at, reminder_triggered_at,
                       linked_paper_id, linked_path, linked_path_is_directory,
                       created_at, updated_at
                FROM todo_items
                WHERE done = 0
                  AND reminder_at IS NOT NULL
                  AND reminder_at <= $now
                  AND reminder_triggered_at IS NULL
                ORDER BY reminder_at ASC;
                """;
            command.Parameters.AddWithValue("$now", FormatDate(now));
            return await ReadTodoItemsAsync(command, token);
        });
    }

    public async Task MarkReminderTriggeredAsync(
        string itemId,
        DateTimeOffset triggeredAt,
        CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            await using var command = _connection!.CreateCommand();
            command.CommandText =
                """
                UPDATE todo_items
                SET reminder_triggered_at = $triggered, updated_at = $triggered
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$triggered", FormatDate(triggeredAt));
            command.Parameters.AddWithValue("$id", itemId);
            await command.ExecuteNonQueryAsync(token);
        });
    }

    public async Task<NoteImage> AddNoteImageAsync(
        string paperId,
        byte[] bytes,
        string mime,
        int width,
        int height,
        CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            var next = await NextImageNumberLockedAsync(token, transaction);
            var image = new NoteImage
            {
                Id = next.ToString(CultureInfo.InvariantCulture),
                PaperId = paperId,
                Mime = mime,
                Width = width,
                Height = height,
                Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
                    .ToLowerInvariant(),
                ByteLength = bytes.Length,
                Bytes = bytes,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await InsertNoteImageLockedAsync(image, token, transaction);
            await TouchPaperLockedAsync(paperId, token, transaction);
            await transaction.CommitAsync(token);
            return image;
        });
    }

    public async Task<NoteImage?> GetNoteImageAsync(string imageId, CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            await using var command = _connection!.CreateCommand();
            command.CommandText =
                """
                SELECT id, paper_id, mime, width, height, sha256, byte_length, bytes, created_at
                FROM note_images
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", imageId);
            await using var reader = await command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? ReadNoteImage(reader) : null;
        });
    }

    public async Task PruneUnreferencedImagesAsync(
        string paperId,
        IReadOnlySet<string> referencedImageIds,
        CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            await using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT id FROM note_images WHERE paper_id = $paperId;";
            command.Parameters.AddWithValue("$paperId", paperId);
            var ids = new List<string>();
            await using (var reader = await command.ExecuteReaderAsync(token))
            {
                while (await reader.ReadAsync(token))
                {
                    ids.Add(reader.GetString(0));
                }
            }

            foreach (var imageId in ids.Where(id => !referencedImageIds.Contains(id)))
            {
                await using var delete = _connection!.CreateCommand();
                delete.CommandText = "DELETE FROM note_images WHERE id = $id AND paper_id = $paperId;";
                delete.Parameters.AddWithValue("$id", imageId);
                delete.Parameters.AddWithValue("$paperId", paperId);
                await delete.ExecuteNonQueryAsync(token);
            }
        });
    }

    public async Task<ImportBatchResult> ImportBatchAsync(
        IReadOnlyList<Paper> papers,
        IReadOnlyList<NoteImage> images,
        CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            using var transaction = _connection!.BeginTransaction();
            var importedPapers = 0;
            var skippedPapers = 0;
            var importedItems = 0;
            var importedImages = 0;
            var pendingLinks = new List<(string ItemId, string LinkedPaperId)>();

            foreach (var paper in papers)
            {
                if (await ExistsLockedAsync("papers", "id", paper.Id, token, transaction))
                {
                    skippedPapers++;
                    continue;
                }

                await InsertPaperLockedAsync(paper, token, transaction);
                foreach (var item in paper.Items)
                {
                    var linkedPaperId = item.LinkedPaperId;
                    item.PaperId = paper.Id;
                    item.LinkedPaperId = null;
                    await InsertTodoItemLockedAsync(item, token, transaction);
                    importedItems++;
                    if (!string.IsNullOrWhiteSpace(linkedPaperId))
                    {
                        pendingLinks.Add((item.Id, linkedPaperId));
                    }
                }
                importedPapers++;
            }

            foreach (var (itemId, linkedPaperId) in pendingLinks)
            {
                if (!await ExistsLockedAsync("papers", "id", linkedPaperId, token, transaction))
                {
                    continue;
                }
                await using var update = _connection!.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = "UPDATE todo_items SET linked_paper_id = $linked WHERE id = $id;";
                update.Parameters.AddWithValue("$linked", linkedPaperId);
                update.Parameters.AddWithValue("$id", itemId);
                await update.ExecuteNonQueryAsync(token);
            }

            foreach (var image in images)
            {
                if (await ExistsLockedAsync("note_images", "id", image.Id, token, transaction) ||
                    !await ExistsLockedAsync("papers", "id", image.PaperId, token, transaction))
                {
                    continue;
                }
                await InsertNoteImageLockedAsync(image, token, transaction);
                importedImages++;
            }

            foreach (var paper in papers)
            {
                await RefreshSearchForPaperLockedAsync(paper.Id, token, transaction);
            }

            await transaction.CommitAsync(token);
            return new ImportBatchResult(
                importedPapers,
                skippedPapers,
                importedItems,
                importedImages);
        });
    }

    public async Task<bool> BackupAsync(string destinationPath, CancellationToken token = default)
    {
        return await WithGateAsync(async () =>
        {
            await EnsureConnectionAsync(token);
            await using var command = _connection!.CreateCommand();
            command.CommandText = "VACUUM INTO $path;";
            command.Parameters.AddWithValue("$path", Path.GetFullPath(destinationPath));
            await command.ExecuteNonQueryAsync(token);
            return true;
        });
    }

    public async Task RestoreAsync(
        string sourcePath,
        string emergencyBackupPath,
        CancellationToken token = default)
    {
        await WithGateAsync(async () =>
        {
            await BackupFilesAsync(emergencyBackupPath, token);
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            try
            {
                DeleteDatabaseFiles();
                File.Copy(sourcePath, DatabasePath, overwrite: true);
                await OpenConnectionAsync(token);
                await EnsureConnectionAsync(token);
                if (!await QuickCheckLockedAsync(token))
                {
                    throw new InvalidDataException("恢复的数据库未通过完整性检查。");
                }
            }
            catch
            {
                if (_connection is not null)
                {
                    await _connection.DisposeAsync();
                    _connection = null;
                }
                DeleteDatabaseFiles();
                File.Copy(emergencyBackupPath, DatabasePath, overwrite: true);
                await OpenConnectionAsync(CancellationToken.None);
                throw;
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _gate.WaitAsync();
        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async Task OpenConnectionAsync(CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = 5,
        };
        _connection = new SqliteConnection(builder.ConnectionString);
        await _connection.OpenAsync(token);
        await ExecuteNonQueryAsync(
            """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 5000;
            PRAGMA synchronous = NORMAL;
            """,
            token);
    }

    private async Task EnsureConnectionAsync(CancellationToken token)
    {
        if (_connection is { State: System.Data.ConnectionState.Open })
        {
            return;
        }
        await OpenConnectionAsync(token);
    }

    private async Task EnsureSearchIndexAsync(CancellationToken token)
    {
        if (!_ftsAvailable)
        {
            return;
        }

        var expected = Convert.ToInt64(
            await ExecuteScalarAsync(
                "SELECT (SELECT COUNT(*) FROM papers WHERE type = 'note') + (SELECT COUNT(*) FROM todo_items);",
                token),
            CultureInfo.InvariantCulture);
        var actual = Convert.ToInt64(
            await ExecuteScalarAsync("SELECT COUNT(*) FROM search_index;", token),
            CultureInfo.InvariantCulture);
        if (actual == expected)
        {
            return;
        }

        using var transaction = _connection!.BeginTransaction();
        await using (var clear = _connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM search_index;";
            await clear.ExecuteNonQueryAsync(token);
        }

        await using (var notes = _connection.CreateCommand())
        {
            notes.Transaction = transaction;
            notes.CommandText =
                """
                INSERT INTO search_index(doc_id, kind, paper_id, item_id, title, body)
                SELECT 'note:' || id, 'note', id, '', title, content
                FROM papers
                WHERE type = 'note';
                """;
            await notes.ExecuteNonQueryAsync(token);
        }

        await using (var todos = _connection.CreateCommand())
        {
            todos.Transaction = transaction;
            todos.CommandText =
                """
                INSERT INTO search_index(doc_id, kind, paper_id, item_id, title, body)
                SELECT 'todo:' || ti.id, 'todo', ti.paper_id, ti.id, p.title, ti.text
                FROM todo_items ti
                JOIN papers p ON p.id = ti.paper_id;
                """;
            await todos.ExecuteNonQueryAsync(token);
        }

        await transaction.CommitAsync(token);
    }

    private async Task<SearchHit[]> SearchFtsLockedAsync(
        string query,
        int limit,
        CancellationToken token)
    {
        var match = "\"" + query.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        await using var command = _connection!.CreateCommand();
        command.CommandText =
            """
            SELECT f.paper_id, f.kind, p.title, NULLIF(f.item_id, ''),
                   CASE WHEN f.kind = 'todo' THEN COALESCE(ti.text, '') ELSE p.title END,
                   CASE WHEN f.kind = 'note' THEN f.body ELSE COALESCE(p.title, '') END,
                   COALESCE(ti.done, 0), p.updated_at
            FROM search_index f
            JOIN papers p ON p.id = f.paper_id
            LEFT JOIN todo_items ti ON ti.id = f.item_id
            WHERE search_index MATCH $match
            ORDER BY bm25(search_index), p.updated_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$match", match);
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));
        return await ReadSearchHitsAsync(command, token);
    }

    private async Task<SearchHit[]> SearchLikeLockedAsync(
        string query,
        int limit,
        CancellationToken token)
    {
        await using var command = _connection!.CreateCommand();
        command.CommandText =
            """
            SELECT p.id, p.type, p.title, NULL,
                   p.title, p.content, 0, p.updated_at
            FROM papers p
            WHERE p.type = 'note'
              AND (p.title LIKE $pattern ESCAPE '\' OR p.content LIKE $pattern ESCAPE '\')
            UNION ALL
            SELECT p.id, p.type, p.title, ti.id,
                   ti.text, p.title, ti.done, p.updated_at
            FROM todo_items ti
            JOIN papers p ON p.id = ti.paper_id
            WHERE ti.text LIKE $pattern ESCAPE '\' OR p.title LIKE $pattern ESCAPE '\'
            ORDER BY 8 DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$pattern", $"%{EscapeLike(query)}%");
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));
        return await ReadSearchHitsAsync(command, token);
    }

    private async Task<Paper?> ReadPaperAsync(
        string paperId,
        bool includeItems,
        CancellationToken token,
        SqliteTransaction? transaction = null)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, type, title, content, x, y, width, height,
                   always_on_top, created_at, updated_at
            FROM papers
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", paperId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token))
        {
            return null;
        }
        var paper = ReadPaper(reader);
        await reader.DisposeAsync();
        if (includeItems && paper.IsTodo)
        {
            paper.Items = (await ReadTodoItemsByPaperAsync(paper.Id, token, transaction)).ToList();
        }
        return paper;
    }

    private async Task<List<Paper>> ReadPapersAsync(
        SqliteCommand command,
        bool includeItems,
        CancellationToken token)
    {
        var papers = new List<Paper>();
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
            {
                papers.Add(ReadPaper(reader));
            }
        }

        if (includeItems)
        {
            foreach (var paper in papers.Where(p => p.IsTodo))
            {
                paper.Items = (await ReadTodoItemsByPaperAsync(paper.Id, token)).ToList();
            }
        }
        return papers;
    }

    private async Task<List<TodoItem>> ReadTodoItemsByPaperAsync(
        string paperId,
        CancellationToken token,
        SqliteTransaction? transaction = null)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, paper_id, text, done, sort_order,
                   reminder_at, reminder_triggered_at,
                   linked_paper_id, linked_path, linked_path_is_directory,
                   created_at, updated_at
            FROM todo_items
            WHERE paper_id = $paperId
            ORDER BY sort_order ASC, created_at ASC;
            """;
        command.Parameters.AddWithValue("$paperId", paperId);
        return await ReadTodoItemsAsync(command, token);
    }

    private static async Task<List<TodoItem>> ReadTodoItemsAsync(
        SqliteCommand command,
        CancellationToken token)
    {
        var items = new List<TodoItem>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            items.Add(ReadTodoItem(reader));
        }
        return items;
    }

    private async Task<TodoItem?> ReadTodoItemLockedAsync(
        string itemId,
        CancellationToken token,
        SqliteTransaction transaction)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, paper_id, text, done, sort_order,
                   reminder_at, reminder_triggered_at,
                   linked_paper_id, linked_path, linked_path_is_directory,
                   created_at, updated_at
            FROM todo_items
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", itemId);
        var items = await ReadTodoItemsAsync(command, token);
        return items.Count == 0 ? null : items[0];
    }

    private static async Task<SearchHit[]> ReadSearchHitsAsync(
        SqliteCommand command,
        CancellationToken token)
    {
        var results = new List<SearchHit>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var type = reader.GetString(1);
            var title = reader.GetString(4);
            var snippet = reader.GetString(5);
            results.Add(new SearchHit
            {
                PaperId = reader.GetString(0),
                PaperType = type,
                PaperTitle = reader.GetString(2),
                ItemId = reader.IsDBNull(3) ? null : reader.GetString(3),
                Title = type == PaperTypes.Note ? reader.GetString(2) : title,
                Snippet = BuildSnippet(type == PaperTypes.Note ? snippet : title),
                Done = reader.GetInt64(6) != 0,
                UpdatedAt = ParseDate(reader.GetString(7)),
            });
        }
        return [.. results];
    }

    private async Task InsertPaperLockedAsync(
        Paper paper,
        CancellationToken token,
        SqliteTransaction? transaction = null)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO papers(
                id, type, title, content, x, y, width, height,
                always_on_top, created_at, updated_at)
            VALUES(
                $id, $type, $title, $content, $x, $y, $width, $height,
                $top, $created, $updated);
            """;
        command.Parameters.AddWithValue("$id", paper.Id);
        command.Parameters.AddWithValue("$type", paper.Type);
        command.Parameters.AddWithValue("$title", paper.Title);
        command.Parameters.AddWithValue("$content", paper.Content);
        command.Parameters.AddWithValue("$x", paper.X);
        command.Parameters.AddWithValue("$y", paper.Y);
        command.Parameters.AddWithValue("$width", paper.Width);
        command.Parameters.AddWithValue("$height", paper.Height);
        command.Parameters.AddWithValue("$top", paper.AlwaysOnTop ? 1 : 0);
        command.Parameters.AddWithValue("$created", FormatDate(paper.CreatedAt));
        command.Parameters.AddWithValue("$updated", FormatDate(paper.UpdatedAt));
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task InsertTodoItemLockedAsync(
        TodoItem item,
        CancellationToken token,
        SqliteTransaction? transaction = null)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO todo_items(
                id, paper_id, text, done, sort_order, reminder_at, reminder_triggered_at,
                linked_paper_id, linked_path, linked_path_is_directory, created_at, updated_at)
            VALUES(
                $id, $paperId, $text, $done, $order, $reminder, $triggered,
                $linkedPaper, $linkedPath, $linkedPathDirectory, $created, $updated);
            """;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$paperId", item.PaperId);
        command.Parameters.AddWithValue("$text", item.Text);
        command.Parameters.AddWithValue("$done", item.Done ? 1 : 0);
        command.Parameters.AddWithValue("$order", item.Order);
        command.Parameters.AddWithValue("$reminder", ToDbDate(item.ReminderAt));
        command.Parameters.AddWithValue("$triggered", ToDbDate(item.ReminderTriggeredAt));
        command.Parameters.AddWithValue("$linkedPaper", (object?)item.LinkedPaperId ?? DBNull.Value);
        command.Parameters.AddWithValue("$linkedPath", (object?)item.LinkedPath ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$linkedPathDirectory",
            item.LinkedPathIsDirectory.HasValue ? item.LinkedPathIsDirectory.Value ? 1 : 0 : DBNull.Value);
        command.Parameters.AddWithValue("$created", FormatDate(item.CreatedAt));
        command.Parameters.AddWithValue("$updated", FormatDate(item.UpdatedAt));
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task InsertNoteImageLockedAsync(
        NoteImage image,
        CancellationToken token,
        SqliteTransaction? transaction = null)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO note_images(
                id, paper_id, mime, width, height, sha256, byte_length, bytes, created_at)
            VALUES(
                $id, $paperId, $mime, $width, $height, $sha, $length, $bytes, $created);
            """;
        command.Parameters.AddWithValue("$id", image.Id);
        command.Parameters.AddWithValue("$paperId", image.PaperId);
        command.Parameters.AddWithValue("$mime", image.Mime);
        command.Parameters.AddWithValue("$width", image.Width);
        command.Parameters.AddWithValue("$height", image.Height);
        command.Parameters.AddWithValue("$sha", image.Sha256);
        command.Parameters.AddWithValue("$length", image.ByteLength);
        command.Parameters.AddWithValue("$bytes", image.Bytes);
        command.Parameters.AddWithValue("$created", FormatDate(image.CreatedAt));
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task UpdateTodoItemLockedAsync(
        TodoItem item,
        CancellationToken token,
        SqliteTransaction transaction)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE todo_items
            SET text = $text,
                done = $done,
                sort_order = $order,
                reminder_at = $reminder,
                reminder_triggered_at = $triggered,
                linked_paper_id = $linkedPaper,
                linked_path = $linkedPath,
                linked_path_is_directory = $linkedPathDirectory,
                updated_at = $updated
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$text", item.Text);
        command.Parameters.AddWithValue("$done", item.Done ? 1 : 0);
        command.Parameters.AddWithValue("$order", item.Order);
        command.Parameters.AddWithValue("$reminder", ToDbDate(item.ReminderAt));
        command.Parameters.AddWithValue("$triggered", ToDbDate(item.ReminderTriggeredAt));
        command.Parameters.AddWithValue("$linkedPaper", (object?)item.LinkedPaperId ?? DBNull.Value);
        command.Parameters.AddWithValue("$linkedPath", (object?)item.LinkedPath ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$linkedPathDirectory",
            item.LinkedPathIsDirectory.HasValue ? item.LinkedPathIsDirectory.Value ? 1 : 0 : DBNull.Value);
        command.Parameters.AddWithValue("$updated", FormatDate(item.UpdatedAt));
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task DeleteTodoItemLockedAsync(
        string itemId,
        CancellationToken token,
        SqliteTransaction transaction)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM todo_items WHERE id = $id;";
        command.Parameters.AddWithValue("$id", itemId);
        await command.ExecuteNonQueryAsync(token);
        if (_ftsAvailable)
        {
            await using var fts = _connection!.CreateCommand();
            fts.Transaction = transaction;
            fts.CommandText = "DELETE FROM search_index WHERE item_id = $id;";
            fts.Parameters.AddWithValue("$id", itemId);
            await fts.ExecuteNonQueryAsync(token);
        }
    }

    private async Task TouchPaperLockedAsync(
        string paperId,
        CancellationToken token,
        SqliteTransaction? transaction = null)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE papers SET updated_at = $updated WHERE id = $id;";
        command.Parameters.AddWithValue("$updated", FormatDate(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$id", paperId);
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task RefreshSearchForPaperLockedAsync(
        string paperId,
        CancellationToken token,
        SqliteTransaction? transaction = null)
    {
        if (!_ftsAvailable)
        {
            return;
        }

        await using (var delete = _connection!.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM search_index WHERE paper_id = $paperId;";
            delete.Parameters.AddWithValue("$paperId", paperId);
            await delete.ExecuteNonQueryAsync(token);
        }

        var paper = await ReadPaperAsync(paperId, includeItems: false, token, transaction);
        if (paper is null)
        {
            return;
        }

        if (paper.IsNote)
        {
            await InsertSearchDocumentLockedAsync(
                $"note:{paper.Id}",
                PaperTypes.Note,
                paper.Id,
                "",
                paper.Title,
                paper.Content,
                token,
                transaction);
            return;
        }

        var items = await ReadTodoItemsByPaperAsync(paper.Id, token, transaction);
        foreach (var item in items)
        {
            await InsertSearchDocumentLockedAsync(
                $"todo:{item.Id}",
                PaperTypes.Todo,
                paper.Id,
                item.Id,
                paper.Title,
                item.Text,
                token,
                transaction);
        }
    }

    private async Task InsertSearchDocumentLockedAsync(
        string docId,
        string kind,
        string paperId,
        string itemId,
        string title,
        string body,
        CancellationToken token,
        SqliteTransaction? transaction)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO search_index(doc_id, kind, paper_id, item_id, title, body)
            VALUES($docId, $kind, $paperId, $itemId, $title, $body);
            """;
        command.Parameters.AddWithValue("$docId", docId);
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$paperId", paperId);
        command.Parameters.AddWithValue("$itemId", itemId);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$body", body);
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task<int> NextTodoOrderLockedAsync(
        string paperId,
        CancellationToken token,
        SqliteTransaction transaction)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM todo_items WHERE paper_id = $paperId;";
        command.Parameters.AddWithValue("$paperId", paperId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    private async Task<int> NextImageNumberLockedAsync(
        CancellationToken token,
        SqliteTransaction transaction)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT COALESCE(MAX(CAST(id AS INTEGER)), 1000) + 1 FROM note_images;";
        return Math.Max(
            DefaultImageNumber,
            Convert.ToInt32(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture));
    }

    private async Task<bool> ExistsLockedAsync(
        string table,
        string keyColumn,
        string value,
        CancellationToken token,
        SqliteTransaction transaction)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT 1 FROM {table} WHERE {keyColumn} = $value LIMIT 1;";
        command.Parameters.AddWithValue("$value", value);
        return await command.ExecuteScalarAsync(token) is not null;
    }

    private async Task<string?> ReadMetadataAsync(string key, CancellationToken token)
    {
        await using var command = _connection!.CreateCommand();
        command.CommandText = "SELECT value FROM metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(token) as string;
    }

    private async Task WriteMetadataLockedAsync(
        string key,
        string value,
        CancellationToken token,
        SqliteTransaction? transaction = null)
    {
        await using var command = _connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO metadata(key, value) VALUES($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task BackupFilesAsync(string destinationPath, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        if (_connection is not null)
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(FULL);";
            await command.ExecuteNonQueryAsync(token);
        }
        File.Copy(DatabasePath, destinationPath, overwrite: true);
    }

    private void DeleteDatabaseFiles()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = DatabasePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private async Task<bool> QuickCheckLockedAsync(CancellationToken token)
    {
        await using var command = _connection!.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        var result = Convert.ToString(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
        return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ExecuteNonQueryAsync(string sql, CancellationToken token)
    {
        await using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task<object?> ExecuteScalarAsync(string sql, CancellationToken token)
    {
        await using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(token);
    }

    private async Task<T> WithGateAsync<T>(Func<Task<T>> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WithGateAsync(Func<Task> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Paper ReadPaper(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Type = reader.GetString(1),
        Title = reader.GetString(2),
        Content = reader.GetString(3),
        X = reader.GetDouble(4),
        Y = reader.GetDouble(5),
        Width = reader.GetDouble(6),
        Height = reader.GetDouble(7),
        AlwaysOnTop = reader.GetInt64(8) != 0,
        CreatedAt = ParseDate(reader.GetString(9)),
        UpdatedAt = ParseDate(reader.GetString(10)),
    };

    private static TodoItem ReadTodoItem(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        PaperId = reader.GetString(1),
        Text = reader.GetString(2),
        Done = reader.GetInt64(3) != 0,
        Order = reader.GetInt32(4),
        ReminderAt = ParseNullableDate(reader, 5),
        ReminderTriggeredAt = ParseNullableDate(reader, 6),
        LinkedPaperId = reader.IsDBNull(7) ? null : reader.GetString(7),
        LinkedPath = reader.IsDBNull(8) ? null : reader.GetString(8),
        LinkedPathIsDirectory = reader.IsDBNull(9) ? null : reader.GetInt64(9) != 0,
        CreatedAt = ParseDate(reader.GetString(10)),
        UpdatedAt = ParseDate(reader.GetString(11)),
    };

    private static NoteImage ReadNoteImage(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        PaperId = reader.GetString(1),
        Mime = reader.GetString(2),
        Width = reader.GetInt32(3),
        Height = reader.GetInt32(4),
        Sha256 = reader.GetString(5),
        ByteLength = reader.GetInt32(6),
        Bytes = (byte[])reader.GetValue(7),
        CreatedAt = ParseDate(reader.GetString(8)),
    };

    private static TodoItem CloneTodo(TodoItem item) => new()
    {
        Id = item.Id,
        PaperId = item.PaperId,
        Text = item.Text,
        Done = item.Done,
        Order = item.Order,
        ReminderAt = item.ReminderAt,
        ReminderTriggeredAt = item.ReminderTriggeredAt,
        LinkedPaperId = item.LinkedPaperId,
        LinkedPath = item.LinkedPath,
        LinkedPathIsDirectory = item.LinkedPathIsDirectory,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
    };

    private static string FormatDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static object ToDbDate(DateTimeOffset? value) =>
        value.HasValue ? FormatDate(value.Value) : DBNull.Value;

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateTimeOffset? ParseNullableDate(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ParseDate(reader.GetString(ordinal));

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string BuildSnippet(string value)
    {
        var normalized = string.Join(
            " ",
            value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        normalized = normalized
            .Replace("```", "", StringComparison.Ordinal)
            .Replace("#", "", StringComparison.Ordinal)
            .Replace("**", "", StringComparison.Ordinal)
            .Replace("__", "", StringComparison.Ordinal)
            .Replace("![image|100%](i:", "", StringComparison.Ordinal);
        return normalized.Length <= 180 ? normalized : normalized[..180] + "...";
    }
}

internal sealed record ImportBatchResult(
    int ImportedPapers,
    int SkippedPapers,
    int ImportedItems,
    int ImportedImages);
