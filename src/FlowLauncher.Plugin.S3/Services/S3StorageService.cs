using FlowLauncher.Plugin.S3.Models;
using FlowLauncher.Plugin.S3.Utils;

using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

using Microsoft.Extensions.Caching.Memory;

using System.IO;
using System.Net;

using S3Protocol = Amazon.S3.Protocol;

namespace FlowLauncher.Plugin.S3.Services;

public sealed class S3StorageService(Settings settings, Action<string>? log = null)
{
    private readonly S3ClientFactory _clientFactory = new(settings);
    private ObjectMetadataServiceFactory? _metadataFactory;
    private readonly Lock _bucketLock = new();
    private readonly Dictionary<string, HashSet<string>> _bucketsByProfile = new(StringComparer.Ordinal);
    private IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public S3StorageService(Settings settings, Action<string>? log, S3ClientFactory clientFactory)
        : this(settings, log)
    {
        _clientFactory = clientFactory;
    }

    private ObjectMetadataServiceFactory MetadataFactory =>
        _metadataFactory ??= new ObjectMetadataServiceFactory(settings, _clientFactory);

    public IObjectMetadataService CreateMetadataService() => MetadataFactory.Create();

    public async Task<List<string>> GetBucketsAsync(
        string bucketName = "",
        CancellationToken cancellationToken = default)
    {
        var profile = settings.GetActiveProfile();
        var profileCacheKey = GetProfileCacheKey(profile);
        log?.Invoke($"GetBucketsAsync. profile={profile.Name}");

        lock (_bucketLock)
        {
            if (_bucketsByProfile.TryGetValue(profileCacheKey, out var cached) && cached.Count > 0)
            {
                return cached
                    .Where(name => name.Contains(bucketName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        using var client = _clientFactory.Create(profile);
        var response = await client.ListBucketsAsync(cancellationToken);
        var results = (response.Buckets ?? [])
            .Select(bucket => bucket.BucketName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToList();

        lock (_bucketLock)
        {
            if (!_bucketsByProfile.TryGetValue(profileCacheKey, out var cached))
            {
                cached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _bucketsByProfile[profileCacheKey] = cached;
            }

            cached.UnionWith(results);
            return cached
                .Where(name => name.Contains(bucketName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public async Task<bool> ExistsAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        key = ObjectKey.Normalize(key);
        var profile = settings.GetActiveProfile();
        var cacheKey = $"object_exist:{GetProfileCacheKey(profile)}:{bucketName}:{key}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var client = _clientFactory.Create(profile);

            try
            {
                await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucketName,
                    Key = key
                }, cancellationToken);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        });
    }

    public Task<string> PresignDownloadUrlAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GeneratePresignedDownloadUrl(bucketName, key));
    }

    public string GeneratePresignedDownloadUrl(string bucketName, string key)
    {
        var endpoint = S3ProviderPresets.NormalizeEndpoint(settings.GetActiveProfile().Endpoint);
        using var client = _clientFactory.Create();
        return client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = ObjectKey.Normalize(key),
            Verb = HttpVerb.GET,
            Protocol = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                ? S3Protocol.HTTP
                : S3Protocol.HTTPS,
            Expires = DateTime.UtcNow.AddHours(6)
        });
    }

    public async Task<List<string>> GetObjectsAsync(
        string bucketName,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        prefix = (prefix ?? "").Replace('\\', '/').TrimStart('/');
        log?.Invoke($"GetObjectsAsync. bucket={bucketName}, prefix={prefix}");

        using var client = _clientFactory.Create();
        var response = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix,
            MaxKeys = 100
        }, cancellationToken);

        return (response.S3Objects ?? [])
            .Select(obj => obj.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Cast<string>()
            .ToList();
    }

    public async Task<(List<ObjectInfo> Items, bool HasMore)> GetObjectsDetailedAsync(
        string bucketName,
        string prefix,
        int maxItems = 100,
        CancellationToken cancellationToken = default)
    {
        prefix = (prefix ?? "").Replace('\\', '/').TrimStart('/');
        if (prefix.Length > 0 && !prefix.EndsWith('/'))
            prefix += '/';

        log?.Invoke($"GetObjectsDetailedAsync. bucket={bucketName}, prefix={prefix}");

        using var client = _clientFactory.Create();
        var response = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix,
            Delimiter = "/",
            MaxKeys = 100
        }, cancellationToken);

        var objects = new List<ObjectInfo>();

        foreach (var commonPrefix in response.CommonPrefixes ?? [])
        {
            if (objects.Count >= maxItems)
                return (objects, true);

            objects.Add(new ObjectInfo(commonPrefix, 0, DateTimeOffset.MinValue));
        }

        foreach (var obj in response.S3Objects ?? [])
        {
            if (obj.Key == prefix)
                continue;
            if (objects.Count >= maxItems)
                return (objects, true);

            objects.Add(new ObjectInfo(
                obj.Key,
                obj.Size ?? 0,
                obj.LastModified ?? DateTimeOffset.MinValue));
        }

        return (objects, response.IsTruncated == true);
    }

    public async Task<string?> UploadFileAsync(
        string bucketName,
        string objectName,
        string filePath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        objectName = ObjectKey.Normalize(objectName);
        var profile = settings.GetActiveProfile();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (settings.ComputeFileMd5)
            metadata["file-md5"] = await FileHasher.ComputeMD5Async(filePath, cancellationToken);
        metadata["source"] = "flow-launcher-plugin-s3";

        using var client = _clientFactory.Create(profile);
        using var transferUtility = new TransferUtility(client);
        var request = new TransferUtilityUploadRequest
        {
            BucketName = bucketName,
            Key = objectName,
            FilePath = filePath,
            PartSize = 8 * 1024 * 1024,
            DisablePayloadSigning = profile.DisablePayloadSigning,
            DisableDefaultChecksumValidation = profile.DisableDefaultChecksumValidation
        };

        foreach (var pair in metadata)
            request.Metadata[pair.Key] = pair.Value;

        request.UploadProgressEvent += (_, args) =>
            progress?.Report(new TransferProgress(args.TransferredBytes, args.TotalBytes));

        await transferUtility.UploadAsync(request, cancellationToken);
        RemoveObjectExistCache(profile, bucketName, objectName);

        string? warning = null;
        if (profile.Preset == S3ProviderPreset.AliyunOss)
        {
            try
            {
                var metadataService = MetadataFactory.Create(profile);
                var current = await metadataService.GetAsync(bucketName, objectName, cancellationToken);
                await metadataService.UpdateAsync(
                    bucketName,
                    objectName,
                    metadata,
                    current.ETag,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                warning = $"文件已上传，但阿里云原生元数据写入失败：{ex.Message}";
            }
        }

        progress?.Report(TransferProgress.Completed(new FileInfo(filePath).Length));
        return warning;
    }

    public async Task CopyObjectAsync(
        string sourceBucket,
        string sourceKey,
        string destinationBucket,
        string destinationKey,
        CancellationToken cancellationToken)
    {
        sourceKey = ObjectKey.Normalize(sourceKey);
        destinationKey = ObjectKey.Normalize(destinationKey);

        using var client = _clientFactory.Create();
        await client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = sourceBucket,
            SourceKey = sourceKey,
            DestinationBucket = destinationBucket,
            DestinationKey = destinationKey
        }, cancellationToken);

        RemoveObjectExistCache(settings.GetActiveProfile(), destinationBucket, destinationKey);
    }

    public void ClearCache()
    {
        lock (_bucketLock)
        {
            _bucketsByProfile.Clear();
        }

        _cache.Dispose();
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    private void RemoveObjectExistCache(S3Profile profile, string bucketName, string key)
    {
        _cache.Remove($"object_exist:{GetProfileCacheKey(profile)}:{bucketName}:{key}");
    }

    private static string GetProfileCacheKey(S3Profile profile) =>
        $"{profile.Id}:{S3ProviderPresets.NormalizeEndpoint(profile.Endpoint)}:{profile.Region}:{profile.AddressingStyle}";
}
