using Amazon.Runtime;
using Amazon.S3;

namespace FlowLauncher.Plugin.S3.Services;

public sealed class S3ClientFactory(Settings settings)
{
    public AmazonS3Client Create(S3Profile? profile = null)
    {
        profile ??= settings.GetActiveProfile();
        Validate(profile);

        var accessKeyId = profile.AccessKeyId.Trim();
        var accessKeySecret = profile.GetPlainAccessKeySecret();
        var sessionToken = profile.GetPlainSessionToken();

        AWSCredentials credentials = string.IsNullOrWhiteSpace(sessionToken)
            ? new BasicAWSCredentials(accessKeyId, accessKeySecret)
            : new SessionAWSCredentials(accessKeyId, accessKeySecret, sessionToken);

        var config = new AmazonS3Config
        {
            ForcePathStyle = profile.AddressingStyle == S3AddressingStyle.Path,
            AuthenticationRegion = profile.Region,
            MaxErrorRetry = 3
        };

        var endpoint = S3ProviderPresets.NormalizeEndpoint(profile.Endpoint);
        if (endpoint.Length > 0)
        {
            config.ServiceURL = endpoint;
            config.UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        return new AmazonS3Client(credentials, config);
    }

    public static void Validate(S3Profile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.AccessKeyId) ||
            string.IsNullOrWhiteSpace(profile.GetPlainAccessKeySecret()))
        {
            throw new SettingsNotConfiguredException("S3 settings are not configured properly.");
        }

        if (string.IsNullOrWhiteSpace(profile.Region))
        {
            throw new SettingsNotConfiguredException("S3 region is not configured.");
        }
    }
}
