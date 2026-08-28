namespace FlowLauncher.Plugin.Toolbox;

public class Settings
{
    public bool EnableGuid { get; set; } = true;

    public bool EnableJson { get; set; } = true;

    public bool EnableFhash { get; set; } = true;

    public bool EnableDt { get; set; } = true;

    public bool EnableEnc { get; set; } = true;

    public bool EnablePw { get; set; } = true;

    public bool EnableBase { get; set; } = true;

    public bool EnableTs { get; set; } = true;

    public bool EnableJwt { get; set; } = true;

    public bool EnableCron { get; set; } = true;

    /// <summary>全部子命令（小写）。</summary>
    public static IReadOnlySet<string> KnownCommands { get; } = new HashSet<string>
    {
        "guid", "json", "fhash", "dt", "enc", "pw", "base", "ts", "jwt", "cron",
    };

    /// <summary>查询分发给子命令前判断其是否已启用；未知命令始终视为启用（走帮助分支）。</summary>
    public bool IsEnabled(string command) => command switch
    {
        "guid" => EnableGuid,
        "json" => EnableJson,
        "fhash" => EnableFhash,
        "dt" => EnableDt,
        "enc" => EnableEnc,
        "pw" => EnablePw,
        "base" => EnableBase,
        "ts" => EnableTs,
        "jwt" => EnableJwt,
        "cron" => EnableCron,
        _ => true,
    };
}
