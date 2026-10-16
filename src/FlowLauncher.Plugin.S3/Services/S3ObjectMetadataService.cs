using FlowLauncher.Plugin.S3.Models;
using FlowLauncher.Plugin.S3.Utils;

using Amazon.S3;
using Amazon.S3.Model;

namespace FlowLauncher.Plugin.S3.Services;

public sealed class S3ObjectMetadataService(S3ClientFactory clientFactory) : IObjectMetadataService
{
    public async Task<ObjectMetadata> GetAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        using var client = clientFactory.Create();
        var response = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = bucketName,
            Key = ObjectKey.Normalize(key)
        }, cancellationToken);

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

        var request = new CopyObjectRequest
        {
            SourceBucket = bucketName,
            SourceKey = key,
            DestinationBucket = bucketName,
            DestinationKey = key,
            MetadataDirective = S3MetadataDirective.REPLACE,
            IfMatch = current.ETag,
            ContentType = current.ContentType,
            ContentEncoding = current.ContentEncoding,
            ContentDisposition = current.ContentDisposition,
            CacheControl = current.CacheControl,
            ContentLanguage = current.ContentLanguage,
            Metadata = new MetadataCollection()
        };

        if (current.LastModified is { } lastModified)
            request.UnmodifiedSinceDate = lastModified.UtcDateTime;

        foreach (var pair in userMetadata)
        {
            var metadataKey = MetadataKey.Normalize(pair.Key);
            if (metadataKey.Length > 0)
                request.Metadata[metadataKey] = pair.Value ?? "";
        }

        using var client = clientFactory.Create();
        try
        {
            await client.CopyObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed ||
            string.Equals(ex.ErrorCode, "PreconditionFailed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConcurrentObjectChangedException("The object changed while metadata was being saved.", ex);
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.NotImplemented ||
            string.Equals(ex.ErrorCode, "NotImplemented", StringComparison.OrdinalIgnoreCase))
        {
            throw new MetadataNotSupportedException(
                "This S3-compatible endpoint does not support copying an object onto itself to replace metadata.",
                ex);
        }
    }

    private static ObjectMetadata ToMetadata(GetObjectMetadataResponse response)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (response.Metadata is not null)
        {
            foreach (var key in response.Metadata.Keys)
                metadata[MetadataKey.Normalize(key)] = response.Metadata[key] ?? "";
        }

        return new ObjectMetadata(
            response.ETag?.Trim('"') ?? "",
            response.ContentLength,
            response.LastModified,
            metadata,
            response.ContentType,
            response.ContentEncoding,
            response.ContentDisposition,
            response.CacheControl,
            response.ContentLanguage);
    }

}
