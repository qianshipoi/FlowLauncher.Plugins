using DevToolkit.AliyunOss;
using DevToolkit.AliyunOss.ViewModel;
using DevToolkit.AliyunOss.Utils;

namespace AliyunOss.UnitTest;

public class OssSettingsViewModelTests
{
    [Fact]
    public void Save_EmptySecret_KeepsExistingSecret_ButUpdatesId()
    {
        var settings = new Settings();
        settings.SetSecrets("old-id", "old-secret");
        settings.Endpoint = "https://oss-cn-hangzhou.aliyuncs.com";

        var saved = false;
        var vm = new OssSettingsViewModel(settings, () => saved = true);
        vm.AccessKeyId = "new-id";
        vm.Endpoint = "https://oss-cn-beijing.aliyuncs.com";

        vm.SaveCommand.Execute(null);

        Assert.True(saved);
        Assert.Equal("new-id", settings.AccessKeyId);
        Assert.Equal("old-secret", settings.GetPlainAccessKeySecret());
        Assert.Equal("https://oss-cn-beijing.aliyuncs.com", settings.Endpoint);
        Assert.Equal("", vm.AccessKeySecretInput);
        Assert.Contains("已配置", vm.AccessKeySecretHint);
    }

    [Fact]
    public void Save_NewSecret_EncryptsAndUpdates()
    {
        var settings = new Settings();
        settings.SetSecrets("old-id", "old-secret");

        var vm = new OssSettingsViewModel(settings, () => { });
        vm.AccessKeyId = "old-id";
        vm.AccessKeySecretInput = "new-secret";
        vm.Endpoint = "oss-cn-hangzhou.aliyuncs.com";

        vm.SaveCommand.Execute(null);

        Assert.Equal("old-id", settings.AccessKeyId);
        Assert.Equal("new-secret", settings.GetPlainAccessKeySecret());
        Assert.True(SecretProtector.IsProtected(settings.AccessKeySecret));
        Assert.Equal("https://oss-cn-hangzhou.aliyuncs.com", settings.Endpoint);
    }

    [Fact]
    public void Constructor_ShowsId_ButNotSecret()
    {
        var settings = new Settings();
        settings.SetSecrets("stored-id", "stored-secret");

        var vm = new OssSettingsViewModel(settings, () => { });

        Assert.Equal("stored-id", vm.AccessKeyId);
        Assert.Equal("", vm.AccessKeySecretInput);
        Assert.DoesNotContain("stored-secret", vm.AccessKeySecretHint);
    }

    [Fact]
    public void TryUpgradeSecrets_KeepsIdPlain_AndEncryptsSecret()
    {
        var settings = new Settings
        {
            AccessKeyId = "LTAI-plain-id",
            AccessKeySecret = "plain-secret"
        };

        Assert.True(settings.TryUpgradeSecrets());
        Assert.Equal("LTAI-plain-id", settings.AccessKeyId);
        Assert.True(SecretProtector.IsProtected(settings.AccessKeySecret));
        Assert.Equal("plain-secret", settings.GetPlainAccessKeySecret());
        Assert.False(settings.TryUpgradeSecrets());
    }

    [Fact]
    public void TryUpgradeSecrets_UndoesAccidentalIdEncryption()
    {
        var settings = new Settings
        {
            AccessKeyId = SecretProtector.Protect("LTAI-was-encrypted"),
            AccessKeySecret = SecretProtector.Protect("secret")
        };

        Assert.True(settings.TryUpgradeSecrets());
        Assert.Equal("LTAI-was-encrypted", settings.AccessKeyId);
        Assert.True(SecretProtector.IsProtected(settings.AccessKeySecret));
    }
}
