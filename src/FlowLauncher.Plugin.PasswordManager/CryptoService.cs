using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FlowLauncher.Plugin.PasswordManager;

/// <summary>
/// SQLCipher 数据库密钥管理。
/// 生成随机密钥用于 SQLCipher 加密数据库，
/// 密钥本身使用 Windows DPAPI 加密后存储在 .dbkey 文件中，
/// 只有当前 Windows 用户可以解密并访问数据库。
/// </summary>
internal sealed class DBKeyManager
{
    private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("FlowLauncher.PasswordManager.DBKey.v1");

    private readonly string _keyFilePath;
    private byte[]? _cachedKey;

    public DBKeyManager(string pluginDirectory)
    {
        _keyFilePath = Path.Combine(pluginDirectory, "passwords.dbkey");
    }

    /// <summary>
    /// 获取 SQLCipher 数据库密钥（hex 编码）。
    /// 如果密钥文件不存在则自动生成。
    /// </summary>
    public string GetOrCreateKey()
    {
        if (_cachedKey != null)
            return Convert.ToHexString(_cachedKey).ToLowerInvariant();

        if (File.Exists(_keyFilePath))
        {
            var encryptedBase64 = File.ReadAllText(_keyFilePath);
            _cachedKey = ProtectedData.Unprotect(
                Convert.FromBase64String(encryptedBase64),
                s_entropy,
                DataProtectionScope.CurrentUser);
        }
        else
        {
            _cachedKey = new byte[32];
            RandomNumberGenerator.Fill(_cachedKey);
            SaveKey();
        }

        return Convert.ToHexString(_cachedKey).ToLowerInvariant();
    }

    private void SaveKey()
    {
        var encrypted = ProtectedData.Protect(_cachedKey!, s_entropy, DataProtectionScope.CurrentUser);
        File.WriteAllText(_keyFilePath, Convert.ToBase64String(encrypted));
    }
}

/// <summary>
/// 单字段 DPAPI 加密/解密（用于密码字段）。
/// 只有当前 Windows 用户可以解密数据。
/// </summary>
internal static class PasswordCrypto
{
    private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("FlowLauncher.PasswordManager.Field.v1");

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = ProtectedData.Protect(plainBytes, s_entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    public static string Decrypt(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            return string.Empty;

        byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
        byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, s_entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
