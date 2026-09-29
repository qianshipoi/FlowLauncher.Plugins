using FlowLauncher.Plugin.PasswordManager.Models;

using Microsoft.Data.Sqlite;

using System.IO;

namespace FlowLauncher.Plugin.PasswordManager.Services;

/// <summary>
/// SQLCipher 加密的 SQLite 密码存储。
/// 整个数据库文件使用 SQLCipher 加密，密钥由 DBKeyManager 管理。
/// </summary>
public sealed class PasswordStore : IDisposable
{
    private readonly SqliteConnection _conn;

    /// <param name="pluginDirectory">插件数据目录</param>
    /// <param name="dbKey">SQLCipher hex 密钥</param>
    public PasswordStore(string pluginDirectory, string dbKey)
    {
        var dbPath = Path.Combine(pluginDirectory, "passwords.db");
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Password = dbKey,
        };
        _conn = new SqliteConnection(builder.ConnectionString);
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS passwords (
                id                 TEXT PRIMARY KEY,
                title              TEXT NOT NULL,
                username           TEXT NOT NULL,
                encrypted_password TEXT NOT NULL,
                url                TEXT NOT NULL DEFAULT '',
                tags               TEXT NOT NULL DEFAULT '',
                notes              TEXT NOT NULL DEFAULT '',
                created_at         TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at         TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_passwords_title ON passwords(title);
            CREATE INDEX IF NOT EXISTS idx_passwords_username ON passwords(username);
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 添加密码条目（密码字段自动加密）
    /// </summary>
    public void Add(PasswordEntry entry)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO passwords (id, title, username, encrypted_password, url, tags, notes, created_at, updated_at)
            VALUES (@id, @title, @username, @password, @url, @tags, @notes, @created, @updated);
            """;
        cmd.Parameters.AddWithValue("@id", entry.Id);
        cmd.Parameters.AddWithValue("@title", entry.Title);
        cmd.Parameters.AddWithValue("@username", entry.Username);
        cmd.Parameters.AddWithValue("@password", entry.EncryptedPassword);
        cmd.Parameters.AddWithValue("@url", entry.Url);
        cmd.Parameters.AddWithValue("@tags", entry.Tags);
        cmd.Parameters.AddWithValue("@notes", entry.Notes);
        cmd.Parameters.AddWithValue("@created", entry.CreatedAt);
        cmd.Parameters.AddWithValue("@updated", entry.UpdatedAt);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 更新密码条目
    /// </summary>
    public void Update(PasswordEntry entry)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE passwords
            SET title = @title, username = @username, encrypted_password = @password,
                url = @url, tags = @tags, notes = @notes, updated_at = @updated
            WHERE id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", entry.Id);
        cmd.Parameters.AddWithValue("@title", entry.Title);
        cmd.Parameters.AddWithValue("@username", entry.Username);
        cmd.Parameters.AddWithValue("@password", entry.EncryptedPassword);
        cmd.Parameters.AddWithValue("@url", entry.Url);
        cmd.Parameters.AddWithValue("@tags", entry.Tags);
        cmd.Parameters.AddWithValue("@notes", entry.Notes);
        cmd.Parameters.AddWithValue("@updated", entry.UpdatedAt);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 删除条目
    /// </summary>
    public bool Delete(string id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM passwords WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// 根据ID获取条目
    /// </summary>
    public PasswordEntry? GetById(string id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM passwords WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return ReadEntry(reader);
    }

    /// <summary>
    /// 模糊搜索（标题、用户名、标签）
    /// </summary>
    public List<PasswordEntry> Search(string keyword, int limit = 15)
    {
        using var cmd = _conn.CreateCommand();
        var pattern = $"%{keyword}%";
        cmd.CommandText = """
            SELECT * FROM passwords
            WHERE title LIKE @kw OR username LIKE @kw OR tags LIKE @kw
            ORDER BY updated_at DESC
            LIMIT @limit;
            """;
        cmd.Parameters.AddWithValue("@kw", pattern);
        cmd.Parameters.AddWithValue("@limit", limit);

        return ReadEntries(cmd);
    }

    /// <summary>
    /// 获取最近更新的条目
    /// </summary>
    public List<PasswordEntry> GetRecent(int limit = 10)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM passwords ORDER BY updated_at DESC LIMIT @limit;";
        cmd.Parameters.AddWithValue("@limit", limit);

        return ReadEntries(cmd);
    }

    /// <summary>
    /// 获取所有条目
    /// </summary>
    public List<PasswordEntry> GetAll()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM passwords ORDER BY updated_at DESC;";
        return ReadEntries(cmd);
    }

    /// <summary>
    /// 触碰条目的更新时间（用于记录最近使用）
    /// </summary>
    public void Touch(string id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE passwords SET updated_at = datetime('now') WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 检查是否存在相同标题和用户名的条目
    /// </summary>
    public bool Exists(string title, string username)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM passwords WHERE title = @title AND username = @username";
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@username", username);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static List<PasswordEntry> ReadEntries(SqliteCommand cmd)
    {
        var list = new List<PasswordEntry>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadEntry(reader));
        }
        return list;
    }

    private static PasswordEntry ReadEntry(SqliteDataReader reader)
    {
        return new PasswordEntry
        {
            Id = reader.GetString(0),
            Title = reader.GetString(1),
            Username = reader.GetString(2),
            EncryptedPassword = reader.GetString(3),
            Url = reader.GetString(4),
            Tags = reader.GetString(5),
            Notes = reader.GetString(6),
            CreatedAt = reader.GetString(7),
            UpdatedAt = reader.GetString(8),
        };
    }

    public void Dispose() => _conn.Dispose();
}
