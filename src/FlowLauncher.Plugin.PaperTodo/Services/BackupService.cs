using Microsoft.Data.Sqlite;

using System.Globalization;
using System.IO;

namespace FlowLauncher.Plugin.PaperTodo.Services;

internal sealed class BackupService(PaperDatabase database, AppPaths paths, Settings settings)
{
    public async Task<string?> CreateDailyBackupAsync(CancellationToken token = default)
    {
        Directory.CreateDirectory(paths.BackupsDirectory);
        var today = DateTimeOffset.Now.Date;
        var latest = Directory
            .EnumerateFiles(paths.BackupsDirectory, "papertodo-*.db", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTime)
            .FirstOrDefault();
        if (latest is not null && latest.LastWriteTime.Date >= today)
        {
            return null;
        }

        var destination = Path.Combine(
            paths.BackupsDirectory,
            $"papertodo-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        await database.BackupAsync(destination, token);
        PruneBackups();
        return destination;
    }

    public async Task ExportAsync(string destinationPath, CancellationToken token = default)
    {
        if (string.Equals(
                Path.GetFullPath(destinationPath),
                Path.GetFullPath(database.DatabasePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("不能覆盖当前正在使用的数据库。");
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }
        await database.BackupAsync(destinationPath, token);
    }

    public async Task RestoreAsync(string sourcePath, CancellationToken token = default)
    {
        if (string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(database.DatabasePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("不能从当前正在使用的数据库恢复。");
        }
        await ValidateDatabaseFileAsync(sourcePath, token);
        Directory.CreateDirectory(paths.BackupsDirectory);
        var emergencyPath = Path.Combine(
            paths.BackupsDirectory,
            $"before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        await database.RestoreAsync(sourcePath, emergencyPath, token);
        if (!await database.QuickCheckAsync(token))
        {
            throw new InvalidDataException("恢复后的数据库未通过完整性检查。");
        }
    }

    public static async Task ValidateDatabaseFileAsync(string path, CancellationToken token = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("数据库文件不存在。", path);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        };
        await using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(token);
        await using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA quick_check;";
        var status = Convert.ToString(await check.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
        if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("数据库文件未通过完整性检查。");
        }

        await using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        var schemaVersion = Convert.ToInt32(
            await version.ExecuteScalarAsync(token),
            CultureInfo.InvariantCulture);
        if (schemaVersion <= 0 || schemaVersion > 1)
        {
            throw new InvalidDataException("数据库版本不受支持。");
        }

        await using var tables = connection.CreateCommand();
        tables.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name IN ('papers', 'todo_items', 'note_images', 'metadata');
            """;
        var tableCount = Convert.ToInt32(
            await tables.ExecuteScalarAsync(token),
            CultureInfo.InvariantCulture);
        if (tableCount < 4)
        {
            throw new InvalidDataException("数据库缺少 PaperTodo 核心表。");
        }
    }

    private void PruneBackups()
    {
        var retention = Math.Clamp(settings.BackupRetentionCount, 1, 60);
        foreach (var file in Directory
                     .EnumerateFiles(paths.BackupsDirectory, "papertodo-*.db", SearchOption.TopDirectoryOnly)
                     .Select(path => new FileInfo(path))
                     .OrderByDescending(info => info.LastWriteTime)
                     .Skip(retention))
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // A backup cleanup failure must not make the live database unusable.
            }
        }
    }
}
