namespace FlowLauncher.Plugin.PasswordManager;

/// <summary>
/// 密码条目数据模型
/// </summary>
public sealed record PasswordEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N").ToUpperInvariant();
    public required string Title { get; init; }
    public required string Username { get; init; }
    /// <summary>
    /// DPAPI 加密后的密码（Base64），数据库中同时受 SQLCipher 整库加密保护
    /// </summary>
    public required string EncryptedPassword { get; init; }
    public string Url { get; init; } = "";
    public string Tags { get; init; } = "";
    public string Notes { get; init; } = "";
    public string CreatedAt { get; init; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public string UpdatedAt { get; init; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}
