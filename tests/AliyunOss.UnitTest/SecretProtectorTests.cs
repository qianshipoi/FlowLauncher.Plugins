using DevToolkit.AliyunOss;
using DevToolkit.AliyunOss.Utils;

namespace AliyunOss.UnitTest;

public class SecretProtectorTests
{
    [Fact]
    public void Protect_Then_Unprotect_RoundTrips()
    {
        var plain = "LTAI-test-value-1234567890";
        var cipher = SecretProtector.Protect(plain);

        Assert.StartsWith(SecretProtector.Prefix, cipher);
        Assert.NotEqual(plain, cipher);
        Assert.Equal(plain, SecretProtector.Unprotect(cipher));
    }

    [Fact]
    public void Protect_Empty_StaysEmpty()
    {
        Assert.Equal(string.Empty, SecretProtector.Protect(""));
        Assert.Equal(string.Empty, SecretProtector.Unprotect(""));
    }

    [Fact]
    public void Unprotect_LegacyPlain_ReturnsAsIs()
    {
        Assert.Equal("LTAI-legacy", SecretProtector.Unprotect("LTAI-legacy"));
    }

    [Fact]
    public void Protect_IsIdempotent()
    {
        var once = SecretProtector.Protect("secret");
        var twice = SecretProtector.Protect(once);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void TryUpgradeSecrets_MigratesLegacyPlainSecret()
    {
        var settings = new Settings
        {
            AccessKeyId = "LTAI-plain-id",
            AccessKeySecret = "plain-secret",
            Endpoint = "https://oss-cn-hangzhou.aliyuncs.com"
        };

        Assert.True(settings.TryUpgradeSecrets());
        // ID 保持明文，仅 Secret 加密
        Assert.Equal("LTAI-plain-id", settings.AccessKeyId);
        Assert.StartsWith(SecretProtector.Prefix, settings.AccessKeySecret);
        Assert.Equal("plain-secret", settings.GetPlainAccessKeySecret());

        // 再次调用不应重复加密
        Assert.False(settings.TryUpgradeSecrets());
    }

    [Fact]
    public void TryUpgradeSecrets_Empty_ReturnsFalse()
    {
        var settings = new Settings();
        Assert.False(settings.TryUpgradeSecrets());
    }
}
