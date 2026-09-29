using System.Security.Cryptography;
using System.Text;

namespace FlowLauncher.Plugin.AliyunOss.Utils;

/// <summary>
/// AccessKey 字段级 DPAPI 加密（CurrentUser）。
/// 磁盘上为 <c>dpapi:&lt;Base64&gt;</c>；无此前缀视为旧版明文，读取时原样返回以便自动升级。
/// </summary>
public static class SecretProtector
{
    public const string Prefix = "dpapi:";

    private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("FlowLauncher.Plugin.AliyunOss.AccessKey.v1");

    public static bool IsProtected(string value) =>
        value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>加密为带前缀的密文；空值原样返回；已加密则不重复加密。</summary>
    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        if (IsProtected(plain)) return plain;

        var bytes = Encoding.UTF8.GetBytes(plain);
        var cipher = ProtectedData.Protect(bytes, s_entropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(cipher);
    }

    /// <summary>解密带前缀的密文；无前缀的旧版明文原样返回。</summary>
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
        catch (FormatException ex)
        {
            throw new InvalidOperationException("AccessKey 密文格式无效，请在设置中重新填写。", ex);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("无法解密 AccessKey（可能更换了 Windows 用户或配置损坏），请在设置中重新填写。", ex);
        }
    }
}
