using FlowLauncher.Plugin.AliyunOss.Utils;

namespace FlowLauncher.Plugin.AliyunOss;

public class Settings
{
    /// <summary>AccessKey ID 明文存储（账号标识，非密钥，可直接显示）。</summary>
    public string AccessKeyId { get; set; } = "";

    /// <summary>AccessKey Secret 磁盘形态：DPAPI 密文（<c>dpapi:</c> 前缀）或旧版明文。</summary>
    public string AccessKeySecret { get; set; } = "";

    public string Endpoint { get; set; } = "";
    public bool AutoCopyLink { get; set; } = true;

    /// <summary>
    /// 上传时是否计算文件 MD5 并写入对象元数据 <c>x-oss-meta-file-md5</c>。
    /// 默认关闭：算哈希要把整个文件多读一遍（实测 512MB ≈ 2.7 秒），
    /// 而 OSS SDK 本来就有 CRC64 完整性校验，只有需要靠该指纹识别文件的
    /// 下游服务才值得付这个代价。
    /// </summary>
    public bool ComputeFileMd5 { get; set; }

    /// <summary>业务侧使用的明文 AccessKeyId（兼容历史误加密数据）。</summary>
    public string GetPlainAccessKeyId() => SecretProtector.Unprotect(AccessKeyId);

    /// <summary>业务侧使用的明文 AccessKeySecret。</summary>
    public string GetPlainAccessKeySecret() => SecretProtector.Unprotect(AccessKeySecret);

    /// <summary>
    /// 写入密钥：ID 明文存储，Secret 经 DPAPI 加密后落盘。
    /// <paramref name="accessKeySecret"/> 传 null 表示不修改已存 Secret。
    /// </summary>
    public void SetSecrets(string accessKeyId, string? accessKeySecret)
    {
        AccessKeyId = (accessKeyId ?? "").Trim();
        if (accessKeySecret is not null)
            AccessKeySecret = SecretProtector.Protect(accessKeySecret);
    }

    /// <summary>
    /// 规范化落盘形态（就地修改）：
    /// - ID 若曾被误加密则还原为明文
    /// - Secret 若仍为明文则升级为 DPAPI 密文
    /// 返回是否发生变更。
    /// </summary>
    public bool TryUpgradeSecrets()
    {
        var changed = false;

        if (SecretProtector.IsProtected(AccessKeyId))
        {
            AccessKeyId = SecretProtector.Unprotect(AccessKeyId);
            changed = true;
        }

        if (!string.IsNullOrEmpty(AccessKeySecret) && !SecretProtector.IsProtected(AccessKeySecret))
        {
            AccessKeySecret = SecretProtector.Protect(AccessKeySecret);
            changed = true;
        }

        return changed;
    }
}
