using DevToolkit.Toolbox;

namespace Toolbox.UnitTest;

public class SettingsTests
{
    [Fact]
    public void NewSettings_所有工具默认启用()
    {
        var settings = new Settings();
        Assert.All(Settings.KnownCommands, command => Assert.True(settings.IsEnabled(command)));
    }

    [Fact]
    public void IsEnabled_禁用后对应命令关闭_其余不受影响()
    {
        var settings = new Settings { EnableJwt = false, EnableCron = false };

        Assert.False(settings.IsEnabled("jwt"));
        Assert.False(settings.IsEnabled("cron"));
        Assert.True(settings.IsEnabled("guid"));
        Assert.True(settings.IsEnabled("json"));
    }

    [Fact]
    public void IsEnabled_未知命令_始终视为启用()
    {
        var settings = new Settings { EnableGuid = false };
        Assert.True(settings.IsEnabled("unknown"));
    }

    [Fact]
    public void KnownCommands_包含全部十个命令()
    {
        Assert.Equal(10, Settings.KnownCommands.Count);
        Assert.All(
            new[] { "guid", "json", "fhash", "dt", "enc", "pw", "base", "ts", "jwt", "cron" },
            command => Assert.Contains(command, Settings.KnownCommands));
    }
}
