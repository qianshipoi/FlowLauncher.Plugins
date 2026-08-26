namespace FlowLauncher.Plugin.PasswordManager;

public class Settings
{
    /// <summary>
    /// 复制后多少毫秒自动清除剪贴板（0表示不清除）
    /// </summary>
    public int ClearClipboardAfterMs { get; set; } = 15000;

    /// <summary>
    /// 默认显示的最近条目数量
    /// </summary>
    public int RecentItemsCount { get; set; } = 10;
}
