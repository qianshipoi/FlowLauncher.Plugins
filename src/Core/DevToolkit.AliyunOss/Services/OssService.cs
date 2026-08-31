using AlibabaCloud.OSS.V2;


using DevToolkit.AliyunOss.Models;
using DevToolkit.AliyunOss.Utils;

using Microsoft.Extensions.Caching.Memory;

using System.IO;

using OSS = AlibabaCloud.OSS.V2;

namespace DevToolkit.AliyunOss.Services;

public class OssService(Settings settings, Action<string>? log = null)
{
    private readonly HashSet<string> _buckets = [];
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private Client GetClient()
    {
        if (string.IsNullOrWhiteSpace(settings.AccessKeyId) ||
            string.IsNullOrWhiteSpace(settings.AccessKeySecret) ||
            string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            throw new SettingsNotConfiguredException("OSS settings are not configured properly.");
        }

        return new Client(new OSS.Configuration
        {
            Endpoint = settings.Endpoint,
            Region = new Uri(settings.Endpoint).Host.Split('.')[0].Replace("oss-", ""),
            CredentialsProvider = new OSS.Credentials.StaticCredentialsProvider(settings.AccessKeyId, settings.AccessKeySecret)
        });
    }

    public async Task<List<string>> GetBucketsAsync(string bucketName = "", CancellationToken cancellationToken = default)
    {
        log?.Invoke("GetBucketsAsync.");
        if (_buckets.Count > 0)
        {
            log?.Invoke("Returning cached buckets.");

            return _buckets.Where(x => x.Contains(bucketName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        using var client = GetClient();

        var result = client.ListBucketsPaginator(new OSS.Models.ListBucketsRequest());

        var results = new List<string>();
        await foreach (var page in result.IterPageAsync(cancellationToken))
        {
            foreach (var bucket in page.Buckets ?? [])
            {
                results.Add(bucket.Name!);
            }
        }

        foreach (var item in results)
        {
            if (_buckets.Contains(item)) continue;
            _buckets.Add(item);
        }

        return _buckets.Where(x => x.Contains(bucketName, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<bool> ExistsAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        if (!_buckets.Contains(bucketName)) return false;
        return await _cache.GetOrCreateAsync("object_exist_" + bucketName + "_" + key, async entry => {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var client = GetClient();
            return await client.IsObjectExistAsync(bucketName, key, cancellationToken: cancellationToken);
        });
    }

    public async Task<string> PresignDownloadUrlAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        using var client = GetClient();
        var result = client.Presign(new OSS.Models.GetObjectRequest
        {
            Bucket = bucketName,
            Key = key
        }, DateTime.Now.AddHours(6));
        return await Task.FromResult(result.Url!);
    }

    public async Task<List<string>> GetObjectsAsync(string bucketName, string prefix, CancellationToken cancellationToken = default)
    {
        prefix ??= string.Empty;
        prefix = prefix.Replace('\\', '/').TrimStart('/');
        log?.Invoke($"GetObjectsAsync. bucketName: {bucketName}, prefix: {prefix}");

        if (!_buckets.Contains(bucketName)) return [];

        using var client = GetClient();
        var result = client.ListObjectsPaginator(new OSS.Models.ListObjectsRequest
        {
            Bucket = bucketName,
            Prefix = prefix,
            MaxKeys = 10,
        });
        var results = new List<string>();
        await foreach (var page in result.IterPageAsync(cancellationToken))
        {
            foreach (var obj in page.Contents ?? [])
            {
                results.Add(obj.Key!);
            }
        }
        return results;
    }



    public async Task<(List<OssObjectInfo> Items, bool HasMore)> GetObjectsDetailedAsync(string bucketName, string prefix, int maxItems = 100, CancellationToken cancellationToken = default)
    {
        prefix ??= string.Empty;
        prefix = prefix.Replace('\\', '/').TrimStart('/');
        if (prefix.Length > 0 && !prefix.EndsWith('/')) prefix += '/';

        log?.Invoke($"GetObjectsDetailedAsync. bucketName: {bucketName}, prefix: {prefix}");

        if (!_buckets.Contains(bucketName)) return ([], false);

        using var client = GetClient();
        var result = client.ListObjectsPaginator(new OSS.Models.ListObjectsRequest
        {
            Bucket = bucketName,
            Prefix = prefix,
            Delimiter = "/",
            MaxKeys = 100,
        });

        var objects = new List<OssObjectInfo>();
        await foreach (var page in result.IterPageAsync(cancellationToken))
        {
            foreach (var dir in page.CommonPrefixes ?? [])
            {
                if (objects.Count >= maxItems) return (objects, true);
                objects.Add(new OssObjectInfo(dir.Prefix!, 0, DateTimeOffset.MinValue));
            }
            foreach (var obj in page.Contents ?? [])
            {
                if (obj.Key == prefix) continue;
                if (objects.Count >= maxItems) return (objects, true);
                objects.Add(new OssObjectInfo(
                    obj.Key!,
                    obj.Size ?? 0,
                    obj.LastModified ?? DateTimeOffset.MinValue));
            }
        }
        return (objects, false);
    }

    public string GeneratePresignedUploadUrl(string bucketName, string key)
    {
        using var client = GetClient();
        var result = client.Presign(new OSS.Models.PutObjectRequest
        {
            Bucket = bucketName,
            Key = key,
        }, DateTime.Now.AddHours(6));
        return result.Url!;
    }

    public void ClearCache()
    {
        _buckets.Clear();
    }

    public async Task UploadFileAsync(string bucketName, string objectName, string filePath, Action<double> reportProcess, CancellationToken cancellationToken = default)
    {
        using var client = GetClient();
        var request = new OSS.Models.PutObjectRequest
        {
            Bucket = bucketName,
            Key = objectName,
            Body = File.OpenRead(filePath),
            ProgressFn = (long increment, long transferred, long total) => {
                reportProcess((double)transferred / total);
            },
            Metadata = new Dictionary<string, string>()
            {
                { "file-md5", FileHasher.ComputeMD5(filePath)  }
            }
        };
        request.Headers.Add("x-oss-meta-source", "flow-launcher-plugin-aliyun-oss");
        var result = await client.PutObjectAsync(request, cancellationToken: cancellationToken);
    }

    public async Task CopyObjectAsync(string bucket1, string key, string bucket2, string backupKey, CancellationToken cancellationToken)
    {
        using var client = GetClient();

        var request = new OSS.Models.CopyObjectRequest
        {
            SourceBucket = bucket1,
            SourceKey = key,
            Bucket = bucket2,
            Key = backupKey
        };

        await client.CopyObjectAsync(request, cancellationToken: cancellationToken);
    }
}
