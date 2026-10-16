using FlowLauncher.Plugin.S3.Utils;

namespace FlowLauncher.Plugin.S3;

public enum S3ProviderPreset
{
    AwsS3,
    AliyunOss,
    Minio,
    Custom
}

public enum S3AddressingStyle
{
    Path,
    VirtualHosted
}

public sealed class S3Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Default";
    public S3ProviderPreset Preset { get; set; } = S3ProviderPreset.Custom;
    public string Endpoint { get; set; } = "";
    public string Region { get; set; } = "us-east-1";
    public S3AddressingStyle AddressingStyle { get; set; } = S3AddressingStyle.Path;

    public string AccessKeyId { get; set; } = "";

    /// <summary>磁盘形态：DPAPI 密文（dpapi: 前缀）或旧版明文。</summary>
    public string AccessKeySecret { get; set; } = "";

    /// <summary>临时凭据令牌；为空表示不使用。</summary>
    public string SessionToken { get; set; } = "";

    public bool DisablePayloadSigning { get; set; }
    public bool DisableDefaultChecksumValidation { get; set; }
    public string TestBucket { get; set; } = "";
    public string AliyunNativeEndpoint { get; set; } = "";

    public string GetPlainAccessKeySecret() => SecretProtector.Unprotect(AccessKeySecret);

    public string GetPlainSessionToken() => SecretProtector.Unprotect(SessionToken);

    public void SetSecrets(string accessKeyId, string? accessKeySecret, string? sessionToken, bool clearSessionToken)
    {
        AccessKeyId = (accessKeyId ?? "").Trim();

        if (accessKeySecret is not null)
        {
            AccessKeySecret = string.IsNullOrEmpty(accessKeySecret)
                ? ""
                : SecretProtector.Protect(accessKeySecret);
        }

        if (clearSessionToken)
        {
            SessionToken = "";
        }
        else if (sessionToken is not null)
        {
            SessionToken = string.IsNullOrEmpty(sessionToken)
                ? ""
                : SecretProtector.Protect(sessionToken);
        }
    }

    public void ClearSecrets()
    {
        AccessKeyId = "";
        AccessKeySecret = "";
        SessionToken = "";
    }

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

        if (!string.IsNullOrEmpty(SessionToken) && !SecretProtector.IsProtected(SessionToken))
        {
            SessionToken = SecretProtector.Protect(SessionToken);
            changed = true;
        }

        return changed;
    }
}

public sealed class Settings
{
    public List<S3Profile> Profiles { get; set; } = [];
    public string ActiveProfileId { get; set; } = "";
    public bool AutoCopyLink { get; set; } = true;

    /// <summary>
    /// 上传时是否计算文件 MD5 并写入对象元数据。默认关闭，避免大文件重复读盘。
    /// </summary>
    public bool ComputeFileMd5 { get; set; }

    public S3Profile GetActiveProfile()
    {
        EnsureProfiles();
        return Profiles.FirstOrDefault(profile =>
                   string.Equals(profile.Id, ActiveProfileId, StringComparison.Ordinal))
               ?? Profiles[0];
    }

    public void EnsureProfiles()
    {
        Profiles ??= [];

        foreach (var profile in Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id))
                profile.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(profile.Name))
                profile.Name = "Default";
            if (string.IsNullOrWhiteSpace(profile.Region))
                profile.Region = "us-east-1";
        }

        if (Profiles.Count == 0)
        {
            Profiles.Add(new S3Profile());
        }

        if (Profiles.All(profile => !string.Equals(profile.Id, ActiveProfileId, StringComparison.Ordinal)))
        {
            ActiveProfileId = Profiles[0].Id;
        }
    }

    public bool TryUpgradeSecrets()
    {
        EnsureProfiles();
        var changed = false;

        foreach (var profile in Profiles)
        {
            changed |= profile.TryUpgradeSecrets();
        }

        return changed;
    }
}
