using Microsoft.Data.Sqlite;

using System.IO;
using System.Text.Json;

namespace DevToolkit.VarName;

public sealed class VarNameCache : IDisposable
{
    private readonly SqliteConnection _conn;

    public VarNameCache(string pluginDirectory)
    {
        Directory.CreateDirectory(pluginDirectory);
        var dbPath = Path.Combine(pluginDirectory, "varname_cache.db");
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS cache (
                chinese_text         TEXT PRIMARY KEY,
                suggestions          TEXT NOT NULL,
                last_selected_value  TEXT,
                created_at           TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS preferences (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        // 迁移：为旧表添加 last_selected_value 列（已存在则忽略）
        MigrateAddColumnIfMissing("cache", "last_selected_value", "TEXT");
    }

    private void MigrateAddColumnIfMissing(string table, string column, string type)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == column) return;
        }

        using var alter = _conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type};";
        alter.ExecuteNonQuery();
    }

    public List<string>? Get(string chinese)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT suggestions FROM cache WHERE chinese_text = @key";
        cmd.Parameters.AddWithValue("@key", chinese);

        var json = cmd.ExecuteScalar() as string;
        if (json is null) return null;

        return JsonSerializer.Deserialize<List<string>>(json);
    }

    public void Set(string chinese, List<string> suggestions)
    {
        var json = JsonSerializer.Serialize(suggestions);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO cache (chinese_text, suggestions, created_at)
            VALUES (@key, @val, datetime('now'));
            """;
        cmd.Parameters.AddWithValue("@key", chinese);
        cmd.Parameters.AddWithValue("@val", json);
        cmd.ExecuteNonQuery();
    }

    public void Remove(string chinese)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM cache WHERE chinese_text = @key";
        cmd.Parameters.AddWithValue("@key", chinese);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 记录用户对某个中文输入选中的变量名
    /// </summary>
    public void SetLastSelectedValue(string chinese, string value)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE cache SET last_selected_value = @val WHERE chinese_text = @key;
            """;
        cmd.Parameters.AddWithValue("@key", chinese);
        cmd.Parameters.AddWithValue("@val", value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 获取用户上次对某个中文输入选中的变量名
    /// </summary>
    public string? GetLastSelectedValue(string chinese)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT last_selected_value FROM cache WHERE chinese_text = @key";
        cmd.Parameters.AddWithValue("@key", chinese);

        return cmd.ExecuteScalar() as string;
    }

    /// <summary>
    /// 记录用户上次选中的命名规范（全局）
    /// </summary>
    public void SetLastNamingConvention(string convention)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO preferences (key, value) VALUES ('last_naming_convention', @val);
            """;
        cmd.Parameters.AddWithValue("@val", convention);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 获取用户上次选中的命名规范（全局）
    /// </summary>
    public string? GetLastNamingConvention()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM preferences WHERE key = 'last_naming_convention'";
        return cmd.ExecuteScalar() as string;
    }

    public void Dispose() => _conn.Dispose();
}
