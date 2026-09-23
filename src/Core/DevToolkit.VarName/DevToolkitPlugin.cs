using System.Windows;

using DevToolkit.Abstractions;
using DevToolkit.VarName.ViewModels;
using DevToolkit.VarName.Views;

namespace DevToolkit.VarName;

/// <summary>变量名插件：注册"变量名"导航项与 AI 配置分区，设置经 <see cref="IHostServices"/> 持久化。</summary>
public sealed class DevToolkitPlugin : IDevToolkitPlugin
{
    private const string SettingsFileName = "varname-settings.json";

    private IHostServices _host = null!;
    private Settings _settings = new();
    private SettingsViewModel _settingsVm = null!;
    private VarNamePageViewModel? _pageVm;

    public string Id => "varname";

    public string Name => "变量名";

    public string Version => "1.0.0";

#pragma warning disable CS0067 // 事件从未使用（接口契约要求；仅在需要时引发）
    public event EventHandler? NavigationChanged;
#pragma warning restore CS0067

    public void Initialize(IHostServices host)
    {
        _host = host;
        _settings = host.LoadJsonSettings<Settings>(Id, SettingsFileName) ?? new Settings();
        // 旧版明文 API Key 自动升级为 DPAPI 密文
        if (_settings.TryUpgradeSecrets())
        {
            host.SaveJsonSettings(Id, SettingsFileName, _settings);
        }
        _settingsVm = new SettingsViewModel(_settings, () =>
        {
            host.SaveJsonSettings(Id, SettingsFileName, _settings);
            // 配置更新后刷新当前输入词的候选缓存
            var keyword = _pageVm?.Input.Trim();
            if (!string.IsNullOrEmpty(keyword))
                _pageVm?.ShowCachedOrPrompt(keyword);
        });
    }

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems()
    {
        _pageVm = new VarNamePageViewModel(_host.GetPluginDataDirectory(Id), _settings);
        return [new PluginNavigationItem("varname", "变量名", "\uE8AC", () => new VarNamePage(_pageVm))];
    }

    public IReadOnlyList<IPluginSettingsSection> GetSettingsSections() =>
        [new VarNameSettingsSection(_settingsVm)];

    private sealed class VarNameSettingsSection(SettingsViewModel viewModel) : IPluginSettingsSection
    {
        public string Title => "变量名 · AI 配置";

        public string Description => "变量名生成使用 OpenAI 兼容接口，与 Flow Launcher 插件的配置相互独立。";

        public FrameworkElement CreateView() => new SettingsSectionView(viewModel);
    }
}
