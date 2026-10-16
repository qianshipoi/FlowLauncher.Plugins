using System.Security.Cryptography;
using System.Text;

namespace FlowLauncher.Plugin.S3.Models;

public sealed record ObjectMetadata(
    string ETag,
    long ContentLength,
    DateTimeOffset? LastModified,
    IReadOnlyDictionary<string, string> UserMetadata,
    string? ContentType,
    string? ContentEncoding,
    string? ContentDisposition,
    string? CacheControl,
    string? ContentLanguage)
{
    public string ConcurrencyToken =>
        $"{ETag.Trim('"')}|{LastModified?.UtcDateTime.Ticks ?? 0}|{MetadataHash()}";

    private string MetadataHash()
    {
        var canonical = string.Join(
            "\n",
            UserMetadata
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key.ToLowerInvariant()}={pair.Value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public sealed record MetadataEntry(string Key, string Value);
