namespace FlowLauncher.Plugin.AliyunOss.Utils;

/// <summary>
/// OSS 对象名（Key）的规范化。
/// Key 是相对桶的路径，不应带前导斜杠：以 <c>/</c> 开头会被拼成
/// <c>//bucket/key</c> 这类非法路径，服务端直接返回 403（而非 404），
/// 容易被误判成权限问题。用户从浏览器地址栏或控制台复制路径时经常带上它。
/// </summary>
public static class OssKey
{
    /// <summary>
    /// 规范化对象名：统一为正斜杠、去掉前导斜杠、合并重复斜杠。
    /// 保留结尾的斜杠（它表示"目录"语义），由调用方决定是否接受。
    /// </summary>
    public static string Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var normalized = key.Trim().Replace('\\', '/').TrimStart('/');

        // 合并中间出现的重复斜杠（如 "a//b"），它同样会让签名路径与期望不一致
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalized;
    }

    /// <summary>规范化后是否为空（例如用户只输入了斜杠）。</summary>
    public static bool IsEmpty(string? key) => Normalize(key).Length == 0;
}
