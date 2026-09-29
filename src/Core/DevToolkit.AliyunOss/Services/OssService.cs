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
        var accessKeyId = settings.GetPlainAccessKeyId();
        var accessKeySecret = settings.GetPlainAccessKeySecret();
        if (string.IsNullOrWhiteSpace(accessKeyId) ||
            string.IsNullOrWhiteSpace(accessKeySecret) ||
            string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            throw new SettingsNotConfiguredException("OSS settings are not configured properly.");
        }

        return new Client(new OSS.Configuration
        {
            Endpoint = settings.Endpoint,
            Region = new Uri(settings.Endpoint).Host.Split('.')[0].Replace("oss-", ""),
            CredentialsProvider = new OSS.Credentials.StaticCredentialsProvider(accessKeyId, accessKeySecret)
        });
    }

    public async Task<List<string>> GetBucketsAsync(string bucketName = "", CancellationToken cancellationToken = default)
    {
        log?.Invoke("GetBucketsAsync.");
        lock (_buckets)
        {
            if (_buckets.Count > 0)
            {
                log?.Invoke("Returning cached buckets.");
                return _buckets.Where(x => x.Contains(bucketName, StringComparison.OrdinalIgnoreCase)).ToList();
            }
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

        lock (_buckets)
        {
            foreach (var item in results)
            {
                _buckets.Add(item);
            }

            return _buckets.Where(x => x.Contains(bucketName, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    private static string ObjectExistCacheKey(string bucketName, string key) =>
        "object_exist_" + bucketName + "_" + key;

    public async Task<bool> ExistsAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        // 前导斜杠会让签名路径变成 //bucket/key 而被服务端拒绝（403），所有键统一规范化
        key = OssKey.Normalize(key);

        return await _cache.GetOrCreateAsync(ObjectExistCacheKey(bucketName, key), async entry => {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var client = GetClient();
            return await client.IsObjectExistAsync(bucketName, key, cancellationToken: cancellationToken);
        });
    }

    public Task<string> PresignDownloadUrlAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GeneratePresignedDownloadUrl(bucketName, key));
    }

    public async Task<List<string>> GetObjectsAsync(string bucketName, string prefix, CancellationToken cancellationToken = default)
    {
        prefix ??= string.Empty;
        prefix = prefix.Replace('\\', '/').TrimStart('/');
        log?.Invoke($"GetObjectsAsync. bucketName: {bucketName}, prefix: {prefix}");

        using var client = GetClient();
        var result = client.ListObjectsPaginator(new OSS.Models.ListObjectsRequest
        {
            Bucket = bucketName,
            Prefix = prefix,
            MaxKeys = 100,
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

    /// <summary>生成下载（GET）预签名链接，用于分享/访问对象。</summary>
    public string GeneratePresignedDownloadUrl(string bucketName, string key)
    {
        using var client = GetClient();
        var result = client.Presign(new OSS.Models.GetObjectRequest
        {
            Bucket = bucketName,
            Key = OssKey.Normalize(key)
        }, DateTime.Now.AddHours(6));
        return result.Url!;
    }

    /// <summary>生成上传（PUT）预签名链接，用于外部直传。</summary>
    public string GeneratePresignedUploadUrl(string bucketName, string key)
    {
        using var client = GetClient();
        var result = client.Presign(new OSS.Models.PutObjectRequest
        {
            Bucket = bucketName,
            Key = OssKey.Normalize(key),
        }, DateTime.Now.AddHours(6));
        return result.Url!;
    }

    public void ClearCache()
    {
        lock (_buckets)
        {
            _buckets.Clear();
        }
    }

    public async Task UploadFileAsync(string bucketName, string objectName, string filePath, IProgress<TransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        objectName = OssKey.Normalize(objectName);

        using var client = GetClient();

        // 哈希只在开启该设置时才算：它要把整个文件多读一遍（实测 512MB ≈ 2.7 秒），
        // 而 OSS SDK 本身就带 CRC64 校验，默认不值得为元数据付这份代价。
        var metadata = new Dictionary<string, string>();
        if (settings.ComputeFileMd5)
        {
            metadata["file-md5"] = await FileHasher.ComputeMD5Async(filePath, cancellationToken);
        }

        await using var body = File.OpenRead(filePath);
        var request = new OSS.Models.PutObjectRequest
        {
            Bucket = bucketName,
            Key = objectName,
            Body = body,
            // 服务端按分片回报；进度含字节总量，交给界面换算速率与剩余时间
            ProgressFn = (long increment, long transferred, long total) =>
                progress?.Report(new TransferProgress(transferred, total)),
            Metadata = metadata
        };
        request.Headers.Add("x-oss-meta-source", "flow-launcher-plugin-aliyun-oss");
        await client.PutObjectAsync(request, cancellationToken: cancellationToken);
        _cache.Remove(ObjectExistCacheKey(bucketName, objectName));
        progress?.Report(TransferProgress.Completed(body.Length));
    }

    public async Task CopyObjectAsync(string bucket1, string key, string bucket2, string backupKey, CancellationToken cancellationToken)
    {
        using var client = GetClient();

        var request = new OSS.Models.CopyObjectRequest
        {
            SourceBucket = bucket1,
            SourceKey = OssKey.Normalize(key),
            Bucket = bucket2,
            Key = OssKey.Normalize(backupKey)
        };

        await client.CopyObjectAsync(request, cancellationToken: cancellationToken);
        _cache.Remove(ObjectExistCacheKey(bucket2, backupKey));
    }
}
