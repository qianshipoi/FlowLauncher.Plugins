using DevToolkit.Abstractions;
using DevToolkit.PortKiller.ViewModels;
using DevToolkit.PortKiller.Views;

namespace DevToolkit.PortKiller;

/// <summary>端口占用插件：注册"端口占用"导航项，历史记录存于插件专属数据目录。</summary>
public sealed class DevToolkitPlugin : IDevToolkitPlugin
{
    private IHostServices _host = null!;

    public string Id => "portkiller";

    public string Name => "端口占用";

    public string Version => "1.0.0";

#pragma warning disable CS0067 // 事件从未使用（接口契约要求；仅在需要时引发）
    public event EventHandler? NavigationChanged;
#pragma warning restore CS0067

    public void Initialize(IHostServices host) => _host = host;

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems()
    {
        var viewModel = new PortKillerViewModel(new PortHistoryManager(_host.GetPluginDataDirectory(Id)));
        return [new PluginNavigationItem("portkiller", "端口占用", "\uE71A", () => new PortKillerPage(viewModel))];
    }

    public IReadOnlyList<IPluginSettingsSection> GetSettingsSections() => [];
}
