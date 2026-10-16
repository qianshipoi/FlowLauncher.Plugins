using FlowLauncher.Plugin.S3;
using FlowLauncher.Plugin.S3.Services;
using FlowLauncher.Plugin.S3.Utils;

using System.Net.Http;

namespace S3.IntegrationTest;

public sealed class RustFsIntegrationTests(RustFsFixture fixture) : IClassFixture<RustFsFixture>
{
    [DockerFact]
    public async Task TransfersObjectsListsAndCreatesPresignedUrl()
    {
        var bucket = UniqueBucket();
        await fixture.CreateBucketAsync(bucket);
        var settings = fixture.CreateServiceSettings(bucket);
        var service = new S3StorageService(settings);
        var file = CreateTempFile("hello rustfs");
        var key = "dir/file + name.txt";

        try
        {
            var buckets = await service.GetBucketsAsync();
            Assert.Contains(bucket, buckets);

            var warning = await service.UploadFileAsync(bucket, key, file);
            Assert.Null(warning);
            Assert.True(await service.ExistsAsync(bucket, key));

            var objects = await service.GetObjectsAsync(bucket, "dir/");
            Assert.Contains(key, objects);

            var url = await service.PresignDownloadUrlAsync(bucket, key);
            Assert.StartsWith("http://", url);
            using var http = new HttpClient();
            var downloaded = await http.GetByteArrayAsync(url);
            Assert.Equal("hello rustfs", System.Text.Encoding.UTF8.GetString(downloaded));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [DockerFact]
    public async Task ReadsUpdatesAndProtectsMetadata()
    {
        var bucket = UniqueBucket();
        await fixture.CreateBucketAsync(bucket);
        var settings = fixture.CreateServiceSettings(bucket);
        var service = new S3StorageService(settings);
        var file = CreateTempFile("metadata-content");
        var key = "metadata/object.txt";

        try
        {
            await service.UploadFileAsync(bucket, key, file);
            var metadataService = service.CreateMetadataService();
            var original = await metadataService.GetAsync(bucket, key);

            Assert.False(string.IsNullOrWhiteSpace(original.UserMetadata["file-md5"]));
            Assert.Equal("flow-launcher-plugin-s3", original.UserMetadata["source"]);

            var updated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["file-md5"] = original.UserMetadata["file-md5"],
                ["source"] = "integration-test",
                ["custom-key"] = "custom-value"
            };
            await metadataService.UpdateAsync(bucket, key, updated, original.ConcurrencyToken);

            var reloaded = await metadataService.GetAsync(bucket, key);
            Assert.Equal("custom-value", reloaded.UserMetadata["custom-key"]);
            Assert.Equal("integration-test", reloaded.UserMetadata["source"]);
            Assert.Equal("metadata-content", System.Text.Encoding.UTF8.GetString(
                await fixture.GetObjectBytesAsync(bucket, key)));

            await Assert.ThrowsAsync<ConcurrentObjectChangedException>(() =>
                metadataService.UpdateAsync(bucket, key, updated, original.ConcurrencyToken));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [DockerFact]
    public async Task BacksUpExistingObject()
    {
        var bucket = UniqueBucket();
        await fixture.CreateBucketAsync(bucket);
        var settings = fixture.CreateServiceSettings(bucket);
        var service = new S3StorageService(settings);
        var file = CreateTempFile("backup-content");
        var key = ObjectKey.Normalize("backup/source.txt");

        try
        {
            await service.UploadFileAsync(bucket, key, file);
            var backupKey = key + ".bak.test";
            await service.CopyObjectAsync(bucket, key, bucket, backupKey, CancellationToken.None);

            Assert.Equal(
                await fixture.GetObjectBytesAsync(bucket, key),
                await fixture.GetObjectBytesAsync(bucket, backupKey));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string UniqueBucket() => $"test-{Guid.NewGuid():N}";

    private static string CreateTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"rustfs-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, content);
        return path;
    }
}
