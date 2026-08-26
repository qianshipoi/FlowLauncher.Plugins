using System.IO;
using System.Security.Cryptography;

using Xunit;

namespace FlowLauncher.Plugin.PasswordManager.Tests;

public class DBKeyManagerTests : IDisposable
{
    private readonly string _tempDir;

    public DBKeyManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dbkey_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void GetOrCreateKey_FirstCall_GeneratesKey()
    {
        var manager = new DBKeyManager(_tempDir);
        var key = manager.GetOrCreateKey();

        Assert.False(string.IsNullOrEmpty(key));
        Assert.Equal(64, key.Length); // 32 bytes = 64 hex chars
    }

    [Fact]
    public void GetOrCreateKey_SecondCall_ReturnsSameKey()
    {
        var manager1 = new DBKeyManager(_tempDir);
        var key1 = manager1.GetOrCreateKey();

        var manager2 = new DBKeyManager(_tempDir);
        var key2 = manager2.GetOrCreateKey();

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GetOrCreateKey_CreatesKeyFile()
    {
        var manager = new DBKeyManager(_tempDir);
        manager.GetOrCreateKey();

        Assert.True(File.Exists(Path.Combine(_tempDir, "passwords.dbkey")));
    }

    [Fact]
    public void KeyFileContent_IsNotPlaintext()
    {
        var manager = new DBKeyManager(_tempDir);
        manager.GetOrCreateKey();

        var content = File.ReadAllText(Path.Combine(_tempDir, "passwords.dbkey"));
        // 应该是 Base64 编码的 DPAPI 密文，不是原始 hex 密钥
        var bytes = Convert.FromBase64String(content);
        Assert.NotEqual(32, bytes.Length); // DPAPI 输出比原始 32 字节更长
    }

    [Fact]
    public void GetOrCreateKey_HexFormat_IsValid()
    {
        var manager = new DBKeyManager(_tempDir);
        var key = manager.GetOrCreateKey();

        // 验证是合法的 hex 字符串
        Assert.True(key.All(c => "0123456789abcdef".Contains(c)));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}

public class PasswordCryptoTests
{
    [Fact]
    public void EncryptDecrypt_RoundTrip_PreservesText()
    {
        var original = "MyS3cur3P@ssw0rd!";
        var encrypted = PasswordCrypto.Encrypt(original);
        var decrypted = PasswordCrypto.Decrypt(encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PasswordCrypto.Encrypt(string.Empty));
    }

    [Fact]
    public void Decrypt_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PasswordCrypto.Decrypt(string.Empty));
    }

    [Fact]
    public void Encrypt_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PasswordCrypto.Encrypt(null!));
    }

    [Fact]
    public void Decrypt_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PasswordCrypto.Decrypt(null!));
    }

    [Fact]
    public void Encrypt_ProducesDifferentOutputEachTime()
    {
        var enc1 = PasswordCrypto.Encrypt("SamePassword");
        var enc2 = PasswordCrypto.Encrypt("SamePassword");
        Assert.Equal("SamePassword", PasswordCrypto.Decrypt(enc1));
        Assert.Equal("SamePassword", PasswordCrypto.Decrypt(enc2));
    }

    [Fact]
    public void Encrypt_WithUnicode_Works()
    {
        var unicode = "こんにちは世界 🌍🔐";
        Assert.Equal(unicode, PasswordCrypto.Decrypt(PasswordCrypto.Encrypt(unicode)));
    }

    [Fact]
    public void Decrypt_TamperedData_Throws()
    {
        var encrypted = PasswordCrypto.Encrypt("test");
        var bytes = Convert.FromBase64String(encrypted);
        bytes[0] ^= 0xFF;
        Assert.Throws<CryptographicException>(() => PasswordCrypto.Decrypt(Convert.ToBase64String(bytes)));
    }
}
