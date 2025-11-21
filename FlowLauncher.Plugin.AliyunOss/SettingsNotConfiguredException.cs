namespace FlowLauncher.Plugin.AliyunOss;

public class SettingsNotConfiguredException : Exception
{
    public SettingsNotConfiguredException(string message) : base(message) { }
}
