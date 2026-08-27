using FlowLauncher.Plugin.PasswordManager.Models;
using FlowLauncher.Plugin.PasswordManager.Services;

using System.IO;

using SQLitePCL;

using Xunit;

namespace FlowLauncher.Plugin.PasswordManager.Tests;

public class PasswordStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PasswordStore _store;

    static PasswordStoreTests()
    {
        Batteries_V2.Init();
    }

    public PasswordStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pwd_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var keyManager = new DBKeyManager(_tempDir);
        _store = new PasswordStore(_tempDir, keyManager.GetOrCreateKey());
    }

    [Fact]
    public void Add_ThenGetById_ReturnsSameEntry()
    {
        var entry = CreateEntry("GitHub", "user@example.com", "P@ssw0rd");
        _store.Add(entry);

        var result = _store.GetById(entry.Id);

        Assert.NotNull(result);
        Assert.Equal(entry.Id, result.Id);
        Assert.Equal(entry.Title, result.Title);
        Assert.Equal(entry.Username, result.Username);
        Assert.Equal(entry.EncryptedPassword, result.EncryptedPassword);
    }

    [Fact]
    public void Search_ByTitle_FindsEntry()
    {
        _store.Add(CreateEntry("GitHub Account", "user1", "pass1"));
        _store.Add(CreateEntry("GitLab Account", "user2", "pass2"));
        _store.Add(CreateEntry("AWS Console", "user3", "pass3"));

        var results = _store.Search("Git");

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Title == "GitHub Account");
        Assert.Contains(results, r => r.Title == "GitLab Account");
    }

    [Fact]
    public void Search_ByUsername_FindsEntry()
    {
        _store.Add(CreateEntry("Service A", "admin@example.com", "pass1"));
        _store.Add(CreateEntry("Service B", "user@example.com", "pass2"));

        var results = _store.Search("admin@");

        Assert.Single(results);
        Assert.Equal("Service A", results[0].Title);
    }

    [Fact]
    public void Search_ByTags_FindsEntry()
    {
        _store.Add(CreateEntry("Work Email", "work@co.com", "p1", tags: "work,email"));
        _store.Add(CreateEntry("Personal Email", "me@home.com", "p2", tags: "personal"));

        var results = _store.Search("work");

        Assert.Single(results);
        Assert.Equal("Work Email", results[0].Title);
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        _store.Add(CreateEntry("GitHub", "user", "pass"));

        var results = _store.Search("nonexistent");

        Assert.Empty(results);
    }

    [Fact]
    public void Delete_RemovesEntry()
    {
        var entry = CreateEntry("To Delete", "user", "pass");
        _store.Add(entry);

        Assert.True(_store.Delete(entry.Id));
        Assert.Null(_store.GetById(entry.Id));
    }

    [Fact]
    public void Delete_NonExistent_ReturnsFalse()
    {
        Assert.False(_store.Delete("nonexistent-id"));
    }

    [Fact]
    public void Update_ModifiesEntry()
    {
        var entry = CreateEntry("Original", "user", "pass");
        _store.Add(entry);

        var updated = entry with { Title = "Updated Title", Tags = "new-tag" };
        _store.Update(updated);

        var result = _store.GetById(entry.Id);
        Assert.NotNull(result);
        Assert.Equal("Updated Title", result.Title);
        Assert.Equal("new-tag", result.Tags);
    }

    [Fact]
    public void GetRecent_ReturnsCorrectCount()
    {
        _store.Add(CreateEntry("First", "u1", "p1"));
        _store.Add(CreateEntry("Second", "u2", "p2"));
        _store.Add(CreateEntry("Third", "u3", "p3"));

        var results = _store.GetRecent(10);

        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => r.Title == "First");
        Assert.Contains(results, r => r.Title == "Second");
        Assert.Contains(results, r => r.Title == "Third");
    }

    [Fact]
    public void GetRecent_RespectsLimit()
    {
        for (int i = 0; i < 20; i++)
            _store.Add(CreateEntry($"Entry {i}", $"user{i}", $"pass{i}"));

        var results = _store.GetRecent(5);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Exists_SameTitleAndUsername_ReturnsTrue()
    {
        _store.Add(CreateEntry("GitHub", "admin", "pass"));

        Assert.True(_store.Exists("GitHub", "admin"));
    }

    [Fact]
    public void Exists_DifferentUsername_ReturnsFalse()
    {
        _store.Add(CreateEntry("GitHub", "admin", "pass"));

        Assert.False(_store.Exists("GitHub", "user"));
    }

    [Fact]
    public void Touch_DoesNotThrow()
    {
        var entry = CreateEntry("Touch Test", "user", "pass");
        _store.Add(entry);

        _store.Touch(entry.Id);

        var result = _store.GetById(entry.Id);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.UpdatedAt));
    }

    [Fact]
    public void Add_MultipleEntries_AllStored()
    {
        for (int i = 0; i < 100; i++)
            _store.Add(CreateEntry($"Entry {i}", $"user{i}", $"pass{i}"));

        var all = _store.GetAll();
        Assert.Equal(100, all.Count);
    }

    private static PasswordEntry CreateEntry(string title, string username, string password, string tags = "", string url = "")
    {
        return new PasswordEntry
        {
            Title = title,
            Username = username,
            EncryptedPassword = PasswordCrypto.Encrypt(password),
            Tags = tags,
            Url = url,
        };
    }

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
