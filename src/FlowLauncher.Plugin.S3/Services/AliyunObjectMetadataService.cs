using FlowLauncher.Plugin.S3.Models;
using FlowLauncher.Plugin.S3.Utils;

using OSS = AlibabaCloud.OSS.V2;

namespace FlowLauncher.Plugin.S3.Services;

public sealed class AliyunObjectMetadataService(S3Profile profile) : IObjectMetadataService
{
    public async Task<ObjectMetadata> GetAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.HeadObjectAsync(new OSS.Models.HeadObjectRequest
        {
            Bucket = bucketName,
            Key = ObjectKey.Normalize(key)
        }, cancellationToken: cancellationToken);

        return ToMetadata(response);
    }

    public async Task UpdateAsync(
        string bucketName,
        string key,
        IReadOnlyDictionary<string, string> userMetadata,
        string expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        key = ObjectKey.Normalize(key);
        var current = await GetAsync(bucketName, key, cancellationToken);
        if (!string.IsNullOrEmpty(expectedConcurrencyToken) &&
            !string.Equals(expectedConcurrencyToken, current.ConcurrencyToken, StringComparison.Ordinal))
        {
            throw new ConcurrentObjectChangedException("The object changed after its metadata was loaded.");
        }

        using var client = CreateClient();
        var request = new OSS.Models.CopyObjectRequest
        {
            SourceBucket = bucketName,
            SourceKey = key,
            Bucket = bucketName,
            Key = key,
            MetadataDirective = "REPLACE",
            IfMatch = current.ETag,
            IfUnmodifiedSince = current.LastModified?.UtcDateTime.ToString("R"),
            ContentType = current.ContentType,
            ContentEncoding = current.ContentEncoding,
            ContentDisposition = current.ContentDisposition,
            CacheControl = current.CacheControl,
            Metadata = userMetadata
                .Select(pair => new KeyValuePair<string, string>(
                    MetadataKey.Normalize(pair.Key),
                    pair.Value ?? ""))
                .Where(pair => pair.Key.Length > 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };

        try
        {
            await client.CopyObjectAsync(request, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (IsNotSupported(ex))
        {
            throw new MetadataNotSupportedException(
                "The Aliyun OSS endpoint rejected the metadata-only copy operation.",
                ex);
        }
    }

    private OSS.Client CreateClient()
    {
        var accessKeyId = profile.AccessKeyId.Trim();
        var accessKeySecret = profile.GetPlainAccessKeySecret();
        var sessionToken = profile.GetPlainSessionToken();
        var endpoint = S3ProviderPresets.DeriveAliyunNativeEndpoint(profile);

        if (string.IsNullOrWhiteSpace(accessKeyId) ||
            string.IsNullOrWhiteSpace(accessKeySecret) ||
            string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(profile.Region))
        {
            throw new SettingsNotConfiguredException("Aliyun OSS settings are not configured properly.");
        }

        return new OSS.Client(new OSS.Configuration
        {
            Endpoint = endpoint,
            Region = profile.Region,
            CredentialsProvider = string.IsNullOrWhiteSpace(sessionToken)
                ? new OSS.Credentials.StaticCredentialsProvider(accessKeyId, accessKeySecret)
                : new OSS.Credentials.StaticCredentialsProvider(accessKeyId, accessKeySecret, sessionToken)
        });
    }

    private static ObjectMetadata ToMetadata(OSS.Models.HeadObjectResult response)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (response.Metadata is not null)
        {
            foreach (var pair in response.Metadata)
                metadata[MetadataKey.Normalize(pair.Key)] = pair.Value ?? "";
        }

        DateTimeOffset? lastModified = null;
        if (DateTimeOffset.TryParse(response.LastModified, out var parsed))
            lastModified = parsed;

        return new ObjectMetadata(
            response.ETag?.Trim('"') ?? "",
            response.ContentLength ?? 0,
            lastModified,
            metadata,
            response.ContentType,
            response.ContentEncoding,
            response.ContentDisposition,
            response.CacheControl,
            null);
    }

    private static bool IsNotSupported(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("NotImplemented", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("not supported", StringComparison.OrdinalIgnoreCase);
    }
}
