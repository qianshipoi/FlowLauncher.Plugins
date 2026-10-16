using FlowLauncher.Plugin.S3;
using FlowLauncher.Plugin.S3.Services;

namespace S3.UnitTest;

public class ProviderPresetTests
{
    [Fact]
    public void AliyunPresetUsesVirtualHostingAndNativeMetadataEndpoint()
    {
        var profile = new S3Profile { Region = "cn-beijing" };

        S3ProviderPresets.Apply(profile, S3ProviderPreset.AliyunOss);

        Assert.Equal("https://s3.oss-cn-beijing.aliyuncs.com", profile.Endpoint);
        Assert.Equal("https://oss-cn-beijing.aliyuncs.com", profile.AliyunNativeEndpoint);
        Assert.Equal(S3AddressingStyle.VirtualHosted, profile.AddressingStyle);
        Assert.True(profile.DisableDefaultChecksumValidation);
    }

    [Fact]
    public void AwsPresetClearsCustomEndpoint()
    {
        var profile = new S3Profile { Endpoint = "http://localhost:9000" };

        S3ProviderPresets.Apply(profile, S3ProviderPreset.AwsS3);

        Assert.Equal("", profile.Endpoint);
        Assert.Equal(S3AddressingStyle.VirtualHosted, profile.AddressingStyle);
    }

    [Theory]
    [InlineData("s3.oss-cn-hangzhou.aliyuncs.com", "https://oss-cn-hangzhou.aliyuncs.com")]
    [InlineData("s3.oss-cn-hangzhou-internal.aliyuncs.com", "https://oss-cn-hangzhou-internal.aliyuncs.com")]
    public void DerivesAliyunNativeEndpoint(string endpointHost, string expected)
    {
        var profile = new S3Profile
        {
            Endpoint = "https://" + endpointHost,
            AliyunNativeEndpoint = ""
        };

        Assert.Equal(expected, S3ProviderPresets.DeriveAliyunNativeEndpoint(profile));
    }
}
