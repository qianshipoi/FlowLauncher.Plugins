namespace FlowLauncher.Plugin.S3.Utils;

public static class MetadataKey
{
    public static string Normalize(string? key)
    {
        var value = (key ?? "").Trim();
        foreach (var prefix in new[] { "x-amz-meta-", "x-oss-meta-" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return value[prefix.Length..].Trim().ToLowerInvariant();
        }

        return value.ToLowerInvariant();
    }
}
