using System.IO;

using SQLitePCL;

using Xunit;

namespace FlowLauncher.Plugin.PasswordManager.Tests;

public class ImportExportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PasswordStore _store;
    private readonly ImportExportService _service;

    static ImportExportServiceTests()
    {
        Batteries_V2.Init();
    }

    public ImportExportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"importexport_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var keyManager = new DBKeyManager(_tempDir);
        _store = new PasswordStore(_tempDir, keyManager.GetOrCreateKey());
        _service = new ImportExportService(_store, _tempDir);
    }

    private void SeedData()
    {
        _store.Add(new PasswordEntry
        {
            Title = "GitHub",
            Username = "user@example.com",
            EncryptedPassword = PasswordCrypto.Encrypt("P@ssw0rd123"),
            Url = "https://github.com",
            Tags = "dev,code",
            Notes = "个人账号",
        });
        _store.Add(new PasswordEntry
        {
            Title = "AWS Console",
            Username = "admin@company.com",
            EncryptedPassword = PasswordCrypto.Encrypt("S3cur3!"),
            Url = "https://aws.amazon.com",
            Tags = "work,cloud",
            Notes = "",
        });
    }

    [Fact]
    public void ExportEncryptedDb_CreatesKeyFile()
    {
        SeedData();
        var exportPath = Path.Combine(_tempDir, "backup.db");

        _service.ExportEncryptedDb(exportPath);

        Assert.True(File.Exists(exportPath));
        Assert.True(File.Exists(Path.ChangeExtension(exportPath, ".dbkey")));
    }

    [Fact]
    public void ExportToExcel_CreatesFileAndReturnsCount()
    {
        SeedData();
        var exportPath = Path.Combine(_tempDir, "export.xlsx");

        var count = _service.ExportToExcel(exportPath);

        Assert.Equal(2, count);
        Assert.True(File.Exists(exportPath));
        Assert.True(new FileInfo(exportPath).Length > 0);
    }

    [Fact]
    public void ExportToExcel_ThrowsWhenNoData()
    {
        var exportPath = Path.Combine(_tempDir, "export.xlsx");
        Assert.Throws<InvalidOperationException>(() => _service.ExportToExcel(exportPath));
    }

    [Fact]
    public void ExportToCsv_CreatesFileAndReturnsCount()
    {
        SeedData();
        var exportPath = Path.Combine(_tempDir, "export.csv");

        var count = _service.ExportToCsv(exportPath);

        Assert.Equal(2, count);
        Assert.True(File.Exists(exportPath));

        var lines = File.ReadAllLines(exportPath);
        Assert.Equal(3, lines.Length); // 表头 + 2 行数据
        Assert.Contains("GitHub", lines[1]);
    }

    [Fact]
    public void ExportToCsv_ThrowsWhenNoData()
    {
        var exportPath = Path.Combine(_tempDir, "export.csv");
        Assert.Throws<InvalidOperationException>(() => _service.ExportToCsv(exportPath));
    }

    [Fact]
    public void ExportToCsv_HandlesSpecialCharacters()
    {
        _store.Add(new PasswordEntry
        {
            Title = "包含,逗号",
            Username = "含\"引号",
            EncryptedPassword = PasswordCrypto.Encrypt("含\n换行"),
        });

        var exportPath = Path.Combine(_tempDir, "special.csv");
        var count = _service.ExportToCsv(exportPath);

        Assert.Equal(1, count);

        // ReadAllLines 会把含换行的字段拆成多行，改用 ReadAllText 验证
        var content = File.ReadAllText(exportPath);
        Assert.Contains("\"包含,逗号\"", content);
        Assert.Contains("\"含\"\"引号\"", content);
    }

    [Fact]
    public void ImportFromExcel_RoundTrip_PreservesData()
    {
        SeedData();
        var exportPath = Path.Combine(_tempDir, "roundtrip.xlsx");
        _service.ExportToExcel(exportPath);

        var entries = _service.ImportFromExcel(exportPath);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Title == "GitHub" && e.Username == "user@example.com");
        Assert.Contains(entries, e => e.Title == "AWS Console" && e.Username == "admin@company.com");

        // 验证密码解密正确
        var github = entries.First(e => e.Title == "GitHub");
        Assert.Equal("P@ssw0rd123", PasswordCrypto.Decrypt(github.EncryptedPassword));
    }

    [Fact]
    public void ImportFromCsv_RoundTrip_PreservesData()
    {
        SeedData();
        var exportPath = Path.Combine(_tempDir, "roundtrip.csv");
        _service.ExportToCsv(exportPath);

        var entries = _service.ImportFromCsv(exportPath);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Title == "GitHub" && e.Username == "user@example.com");
        Assert.Contains(entries, e => e.Title == "AWS Console");

        var aws = entries.First(e => e.Title == "AWS Console");
        Assert.Equal("S3cur3!", PasswordCrypto.Decrypt(aws.EncryptedPassword));
    }

    [Fact]
    public void ImportFromCsv_EmptyFile_ReturnsEmpty()
    {
        var emptyPath = Path.Combine(_tempDir, "empty.csv");
        File.WriteAllText(emptyPath, "标题,用户名,密码,URL,标签,备注");

        var entries = _service.ImportFromCsv(emptyPath);
        Assert.Empty(entries);
    }

    [Fact]
    public void ImportFromCsv_SkipsEmptyRows()
    {
        var csvPath = Path.Combine(_tempDir, "with_empty.csv");
        File.WriteAllLines(csvPath, [
            "标题,用户名,密码,URL,标签,备注",
            "GitHub,user@test.com,pass123,,,",
            ",,,,,",
            "AWS,admin@test.com,pass456,,,",
        ]);

        var entries = _service.ImportFromCsv(csvPath);
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void ImportEntries_WritesToDatabase()
    {
        var entries = new List<PasswordEntry>
        {
            new()
            {
                Title = "Imported 1",
                Username = "user1",
                EncryptedPassword = PasswordCrypto.Encrypt("pass1"),
            },
            new()
            {
                Title = "Imported 2",
                Username = "user2",
                EncryptedPassword = PasswordCrypto.Encrypt("pass2"),
            },
        };

        var count = _service.ImportEntries(entries);
        Assert.Equal(2, count);

        var all = _store.GetAll();
        Assert.Equal(2, all.Count);
    }

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
