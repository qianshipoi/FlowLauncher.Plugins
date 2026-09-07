using System.Windows;

using DevToolkit.Abstractions;
using DevToolkit.PasswordManager.Models;
using DevToolkit.PasswordManager.Services;
using DevToolkit.PasswordManager.ViewModels;
using DevToolkit.PasswordManager.Views;

namespace DevToolkit.PasswordManager;

/// <summary>密码管理器插件：注册窗口型导航项与数据管理配置分区，数据库存于插件专属数据目录。</summary>
public sealed class DevToolkitPlugin : IDevToolkitPlugin
{
    private const string SettingsFileName = "pm-settings.json";

    private IHostServices _host = null!;
    private PasswordStore _store = null!;
    private ImportExportService _importExport = null!;
    private Settings _settings = new();
    private SettingsViewModel _settingsVm = null!;

    public string Id => "passwordmanager";

    public string Name => "密码管理器";

    public string Version => "1.0.0";

#pragma warning disable CS0067 // 事件从未使用（接口契约要求；仅在需要时引发）
    public event EventHandler? NavigationChanged;
#pragma warning restore CS0067

    public void Initialize(IHostServices host)
    {
        _host = host;
        var dir = host.GetPluginDataDirectory(Id);
        var keyManager = new DBKeyManager(dir);
        _store = new PasswordStore(dir, keyManager.GetOrCreateKey());
        _importExport = new ImportExportService(_store, dir);
        _settings = host.LoadJsonSettings<Settings>(Id, SettingsFileName) ?? new Settings();
        _settingsVm = new SettingsViewModel(_settings, _importExport);
    }

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems() =>
        [new PluginNavigationItem(
            "passwordmanager", "密码管理器", "\uE72E",
            () => new ManagerWindow(_store, _importExport), OpenAsWindow: true)];

    public IReadOnlyList<IPluginSettingsSection> GetSettingsSections() =>
        [new PasswordManagerSettingsSection(_settingsVm)];

    private sealed class PasswordManagerSettingsSection(SettingsViewModel viewModel) : IPluginSettingsSection
    {
        public string Title => "密码管理器 · 数据管理";

        public string Description => "加密数据库与 Excel/CSV 的导入导出。";

        public FrameworkElement CreateView() => new SettingsSectionView(viewModel);
    }
}
