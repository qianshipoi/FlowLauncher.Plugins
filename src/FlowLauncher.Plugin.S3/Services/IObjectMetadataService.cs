using FlowLauncher.Plugin.S3.Models;

namespace FlowLauncher.Plugin.S3.Services;

public interface IObjectMetadataService
{
    Task<ObjectMetadata> GetAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        string bucketName,
        string key,
        IReadOnlyDictionary<string, string> userMetadata,
        string expectedConcurrencyToken,
        CancellationToken cancellationToken = default);
}

public sealed class ConcurrentObjectChangedException : Exception
{
    public ConcurrentObjectChangedException(string message)
        : base(message)
    {
    }

    public ConcurrentObjectChangedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class MetadataNotSupportedException(string message, Exception? innerException = null)
    : Exception(message, innerException);
