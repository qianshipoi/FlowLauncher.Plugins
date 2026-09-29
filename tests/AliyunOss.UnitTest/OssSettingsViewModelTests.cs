using FlowLauncher.Plugin.AliyunOss;
using FlowLauncher.Plugin.AliyunOss.ViewModel;
using FlowLauncher.Plugin.AliyunOss.Utils;

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

    // ---- 文件 MD5 元数据开关 ----

    [Fact]
    public void ComputeFileMd5_DefaultsToOff()
    {
        // 默认关闭：算哈希要多读一遍整个文件，而 SDK 本身已有 CRC64 校验
        Assert.False(new Settings().ComputeFileMd5);
    }

    [Fact]
    public void Constructor_LoadsStoredMd5Switch()
    {
        var settings = new Settings { ComputeFileMd5 = true };

        var vm = new OssSettingsViewModel(settings, () => { });

        // 设置面板要回显已存值，否则用户会以为配置丢了
        Assert.True(vm.ComputeFileMd5);
    }

    [Fact]
    public void Save_PersistsMd5Switch()
    {
        var settings = new Settings();
        var vm = new OssSettingsViewModel(settings, () => { });

        vm.ComputeFileMd5 = true;
        vm.SaveCommand.Execute(null);

        Assert.True(settings.ComputeFileMd5);
    }

    [Fact]
    public void Save_TurningMd5SwitchOff_Persists()
    {
        var settings = new Settings { ComputeFileMd5 = true };
        var vm = new OssSettingsViewModel(settings, () => { });

        vm.ComputeFileMd5 = false;
        vm.SaveCommand.Execute(null);

        Assert.False(settings.ComputeFileMd5);
    }
}
