namespace FlowLauncher.Plugin.S3.Services;

public sealed class ObjectMetadataServiceFactory(Settings settings, S3ClientFactory clientFactory)
{
    public IObjectMetadataService Create(S3Profile? profile = null)
    {
        profile ??= settings.GetActiveProfile();

        return profile.Preset == S3ProviderPreset.AliyunOss
            ? new AliyunObjectMetadataService(profile)
            : new S3ObjectMetadataService(clientFactory);
    }
}
