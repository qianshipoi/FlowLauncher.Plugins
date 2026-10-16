namespace FlowLauncher.Plugin.S3.Services;

public static class S3ProviderPresets
{
    public static void Apply(S3Profile profile, S3ProviderPreset preset)
    {
        profile.Preset = preset;

        switch (preset)
        {
            case S3ProviderPreset.AwsS3:
                profile.Endpoint = "";
                if (string.IsNullOrWhiteSpace(profile.Region))
                    profile.Region = "us-east-1";
                profile.AddressingStyle = S3AddressingStyle.VirtualHosted;
                profile.DisablePayloadSigning = false;
                profile.DisableDefaultChecksumValidation = false;
                break;

            case S3ProviderPreset.AliyunOss:
                if (string.IsNullOrWhiteSpace(profile.Region))
                    profile.Region = "cn-hangzhou";
                profile.Endpoint = $"https://s3.oss-{profile.Region}.aliyuncs.com";
                profile.AliyunNativeEndpoint = $"https://oss-{profile.Region}.aliyuncs.com";
                profile.AddressingStyle = S3AddressingStyle.VirtualHosted;
                profile.DisablePayloadSigning = false;
                profile.DisableDefaultChecksumValidation = true;
                break;

            case S3ProviderPreset.Minio:
                if (string.IsNullOrWhiteSpace(profile.Region))
                    profile.Region = "us-east-1";
                profile.AddressingStyle = S3AddressingStyle.Path;
                profile.DisablePayloadSigning = false;
                profile.DisableDefaultChecksumValidation = false;
                break;

            case S3ProviderPreset.Custom:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
        }
    }

    public static string NormalizeEndpoint(string? endpoint)
    {
        var value = (endpoint ?? "").Trim();
        if (value.Length == 0)
            return "";

        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value;
        }

        return value.TrimEnd('/');
    }

    public static string DeriveAliyunNativeEndpoint(S3Profile profile)
    {
        var configured = NormalizeEndpoint(profile.AliyunNativeEndpoint);
        if (configured.Length > 0)
            return configured;

        var endpoint = NormalizeEndpoint(profile.Endpoint);
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            if (host.StartsWith("s3.", StringComparison.OrdinalIgnoreCase))
            {
                host = host[3..];
            }

            if (host.EndsWith(".aliyuncs.com", StringComparison.OrdinalIgnoreCase))
                return $"{uri.Scheme}://{host}";
        }

        if (!string.IsNullOrWhiteSpace(profile.Region))
            return $"https://oss-{profile.Region}.aliyuncs.com";

        return "";
    }
}
