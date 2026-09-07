using System.Windows;

using DevToolkit.Abstractions;
using DevToolkit.Toolbox.ViewModels;
using DevToolkit.Toolbox.Views;
using DevToolkit.Toolbox.Views.Tools;

namespace DevToolkit.Toolbox;

/// <summary>
/// 工具箱插件：注册 10 个工具导航项（多页面）与"启用的工具"配置分区。
/// 设置经 <see cref="IHostServices"/> 按插件命名空间持久化，开关变化触发 <see cref="NavigationChanged"/> 重建导航。
/// </summary>
public sealed class DevToolkitPlugin : IDevToolkitPlugin
{
    private const string SettingsFileName = "toolbox-settings.json";

    private IHostServices _host = null!;
    private Settings _settings = new();
    private SettingsViewModel _settingsVm = null!;

    public string Id => "toolbox";

    public string Name => "工具箱";

    public string Version => "1.0.0";

    public event EventHandler? NavigationChanged;

    public void Initialize(IHostServices host)
    {
        _host = host;
        _settings = host.LoadJsonSettings<Settings>(Id, SettingsFileName) ?? new Settings();
        _settingsVm = new SettingsViewModel(_settings, OnSettingsChanged);
    }

    private void OnSettingsChanged()
    {
        _host.SaveJsonSettings(Id, SettingsFileName, _settings);
        NavigationChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems() =>
    [
        Item("guid", "GUID 生成", "\uE8E5", () => new GuidView()),
        Item("json", "JSON 编辑器", "\uE70F", () => new JsonView(_host.OpenJsonEditor)),
        Item("fhash", "文件哈希", "\uE7C1", () => new HashView()),
        Item("dt", "时间与时间戳", "\uE823", () => new DtView()),
        Item("enc", "编码转换", "\uE8AB", () => new EncView()),
        Item("pw", "密码生成", "\uE72E", () => new PwView()),
        Item("base", "进制转换", "\uE8EF", () => new BaseView()),
        Item("ts", "时间戳转日期", "\uE787", () => new TsView()),
        Item("jwt", "JWT 解码", "\uE8A5", () => new JwtView(_host.OpenJsonEditor)),
        Item("cron", "Cron 解析", "\uE9D9", () => new CronView()),
    ];

    public IReadOnlyList<IPluginSettingsSection> GetSettingsSections() =>
        [new ToolboxSettingsSection(_settingsVm)];

    private PluginNavigationItem Item(string command, string displayName, string glyph, Func<object> factory) => new(
        "toolbox:" + command, displayName, glyph, factory,
        IsVisible: () => _settings.IsEnabled(command));

    private sealed class ToolboxSettingsSection(SettingsViewModel viewModel) : IPluginSettingsSection
    {
        public string Title => "工具箱 · 启用的工具";

        public string Description => "取消勾选后，对应工具将从左侧导航中移除；更改即时生效。";

        public FrameworkElement CreateView() => new SettingsSectionView(viewModel);
    }
}
