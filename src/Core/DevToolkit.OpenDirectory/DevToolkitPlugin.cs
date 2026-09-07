using DevToolkit.Abstractions;
using DevToolkit.OpenDirectory.Views;

namespace DevToolkit.OpenDirectory;

/// <summary>打开目录插件：注册"打开目录"导航项，复用 <see cref="PathService"/> 打开资源管理器。</summary>
public sealed class DevToolkitPlugin : IDevToolkitPlugin
{
    private IHostServices _host = null!;

    public string Id => "opendirectory";

    public string Name => "打开目录";

    public string Version => "1.0.0";

#pragma warning disable CS0067 // 事件从未使用（接口契约要求；仅在需要时引发）
    public event EventHandler? NavigationChanged;
#pragma warning restore CS0067

    public void Initialize(IHostServices host) => _host = host;

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems() =>
        [new PluginNavigationItem("opendirectory", "打开目录", "\uE8B7", () => new OpenDirectoryPage())];

    public IReadOnlyList<IPluginSettingsSection> GetSettingsSections() => [];
}
