namespace FlowLauncher.Plugin.S3;

public class SettingsNotConfiguredException : Exception
{
    public SettingsNotConfiguredException(string message) : base(message) { }
}
