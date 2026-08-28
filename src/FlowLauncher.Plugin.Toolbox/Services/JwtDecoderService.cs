using System.Text;

using System.Text.Encodings.Web;

using System.Text.Json;
using System.Text.Json.Nodes;

namespace FlowLauncher.Plugin.Toolbox.Services;

/// <summary>JWT 解码：仅解析展示 Header/Payload 与时间声明，不校验签名。</summary>
internal static class JwtDecoderService
{
    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IReadOnlyList<ToolRow> Decode(string token, DateTimeOffset now)
    {
        var parts = token.Trim().Split('.');
        if (parts.Length is < 2 or > 3)
            throw new FormatException("JWT 应为 header.payload[.signature] 两到三段式");

        var header = DecodeSegment(parts[0], "header");
        var payload = DecodeSegment(parts[1], "payload");

        var results = new List<ToolRow>
        {
            new("Header", header),
            new("Payload", payload),
        };

        if (JsonNode.Parse(payload) is JsonObject claims)
        {
            if (claims["exp"] is JsonValue exp && exp.TryGetValue<long>(out var expValue))
            {
                var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expValue);
                var delta = expiresAt - now;
                var relative = delta < TimeSpan.Zero
                    ? $"已过期 {TimestampParser.HumanizeMagnitude(-delta)}"
                    : delta < TimeSpan.FromSeconds(1) ? "已到期" : $"剩余 {TimestampParser.HumanizeMagnitude(delta)}";
                results.Add(new ToolRow("过期时间", $"{expiresAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}（{relative}）"));
            }

            if (claims["iat"] is JsonValue iat && iat.TryGetValue<long>(out var iatValue))
                results.Add(new ToolRow("签发时间", $"{DateTimeOffset.FromUnixTimeSeconds(iatValue).LocalDateTime:yyyy-MM-dd HH:mm:ss}"));
        }

        return results;
    }

    internal static string DecodeSegment(string segment, string name)
    {
        if (segment.Length == 0)
            throw new FormatException($"JWT 的 {name} 段为空");

        var base64 = segment.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            0 => base64,
            _ => throw new FormatException($"JWT 的 {name} 段不是有效的 Base64url"),
        };

        string json;
        try
        {
            json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }
        catch (FormatException)
        {
            throw new FormatException($"JWT 的 {name} 段不是有效的 Base64url");
        }

        try
        {
            return JsonNode.Parse(json)!.ToJsonString(CompactOptions);
        }
        catch (JsonException)
        {
            throw new FormatException($"JWT 的 {name} 段不是有效的 JSON");
        }
    }
}
