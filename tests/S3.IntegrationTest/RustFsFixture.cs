using Amazon.S3;
using Amazon.S3.Model;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using FlowLauncher.Plugin.S3;
using FlowLauncher.Plugin.S3.Services;

namespace S3.IntegrationTest;

public sealed class RustFsFixture : IAsyncLifetime
{
    public const string AccessKey = "rustfs-test-access";
    public const string SecretKey = "rustfs-test-secret-value";

    private IContainer? _container;

    public string Endpoint { get; private set; } = "";

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder("rustfs/rustfs:1.0.2")
            .WithEnvironment("RUSTFS_ACCESS_KEY", AccessKey)
            .WithEnvironment("RUSTFS_SECRET_KEY", SecretKey)
            .WithEnvironment("RUSTFS_CONSOLE_ENABLE", "false")
            .WithPortBinding(9000, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request
                        .ForPort(9000)
                        .ForPath("/health/ready")))
            .Build();

        await _container.StartAsync();
        Endpoint = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(9000)}";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    public Settings CreateServiceSettings(string bucketName)
    {
        var profile = new S3Profile
        {
            Name = "RustFS",
            Preset = S3ProviderPreset.Minio,
            Endpoint = Endpoint,
            Region = "us-east-1",
            AddressingStyle = S3AddressingStyle.Path,
            TestBucket = bucketName
        };
        profile.SetSecrets(AccessKey, SecretKey, null, clearSessionToken: true);

        var settings = new Settings
        {
            Profiles = [profile],
            ActiveProfileId = profile.Id,
            ComputeFileMd5 = true
        };
        return settings;
    }

    public async Task CreateBucketAsync(string bucketName)
    {
        var settings = CreateServiceSettings(bucketName);
        using var client = new S3ClientFactory(settings).Create();
        await client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
    }

    public async Task<byte[]> GetObjectBytesAsync(string bucketName, string key)
    {
        var settings = CreateServiceSettings(bucketName);
        using var client = new S3ClientFactory(settings).Create();
        using var response = await client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucketName,
            Key = key
        });
        using var stream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(stream);
        return stream.ToArray();
    }
}
