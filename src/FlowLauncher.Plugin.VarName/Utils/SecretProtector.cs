using System.Security.Cryptography;
using System.Text;

namespace FlowLauncher.Plugin.VarName.Utils;

/// <summary>
/// API Key 字段级 DPAPI 加密（CurrentUser）。
/// 磁盘上为 <c>dpapi:&lt;Base64&gt;</c>；无此前缀视为旧版明文，读取时原样返回以便自动升级。
/// </summary>
public static class SecretProtector
{
    public const string Prefix = "dpapi:";

    private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("FlowLauncher.Plugin.VarName.ApiKey.v1");

    public static bool IsProtected(string value) =>
        value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        if (IsProtected(plain)) return plain;

        var bytes = Encoding.UTF8.GetBytes(plain);
        var cipher = ProtectedData.Protect(bytes, s_entropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(cipher);
    }

    public static string Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (!IsProtected(value)) return value;

        try
        {
            var cipher = Convert.FromBase64String(value[Prefix.Length..]);
            var plain = ProtectedData.Unprotect(cipher, s_entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new InvalidOperationException("API Key 密文无效或无法解密，请在设置中重新填写。", ex);
        }
    }
}
