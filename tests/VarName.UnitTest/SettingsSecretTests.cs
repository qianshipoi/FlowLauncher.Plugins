using FlowLauncher.Plugin.VarName;
using FlowLauncher.Plugin.VarName.Utils;

namespace VarName.UnitTest;

public class SettingsSecretTests
{
    [Fact]
    public void SetApiKey_Null_KeepsExisting()
    {
        var s = new Settings();
        s.SetApiKey("old-key");
        s.SetApiKey(null);
        Assert.Equal("old-key", s.GetPlainApiKey());
    }

    [Fact]
    public void SetApiKey_New_EncryptsOnDisk()
    {
        var s = new Settings();
        s.SetApiKey("sk-test");
        Assert.True(SecretProtector.IsProtected(s.ApiKey));
        Assert.Equal("sk-test", s.GetPlainApiKey());
    }

    [Fact]
    public void TryUpgradeSecrets_MigratesLegacyPlain()
    {
        var s = new Settings { ApiKey = "sk-legacy-plain" };
        Assert.True(s.TryUpgradeSecrets());
        Assert.True(SecretProtector.IsProtected(s.ApiKey));
        Assert.Equal("sk-legacy-plain", s.GetPlainApiKey());
        Assert.False(s.TryUpgradeSecrets());
    }

    [Fact]
    public void Save_EmptyInput_KeepsSecret()
    {
        var settings = new Settings();
        settings.SetApiKey("stored-key");
        var saved = false;
        var vm = new SettingsViewModel(settings, () => saved = true);
        vm.ModelName = "gpt-test";
        vm.ApiUrl = "https://api.example.com/v1/";
        vm.ApiKeyInput = "";

        vm.SaveCommand.Execute(null);

        Assert.True(saved);
        Assert.Equal("stored-key", settings.GetPlainApiKey());
        Assert.Equal("", vm.ApiKeyInput);
        Assert.Contains("已配置", vm.ApiKeyHint);
    }
}
