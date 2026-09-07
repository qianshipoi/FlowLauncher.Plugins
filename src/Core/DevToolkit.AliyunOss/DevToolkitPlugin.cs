using System.Windows;

using DevToolkit.Abstractions;
using DevToolkit.AliyunOss.Services;
using DevToolkit.AliyunOss.ViewModel;
using DevToolkit.AliyunOss.ViewModels;
using DevToolkit.AliyunOss.Views;

using Microsoft.Extensions.Logging;

namespace DevToolkit.AliyunOss;

/// <summary>阿里云 OSS 插件：注册 OSS 管理导航项与 AccessKey 配置分区，设置经 <see cref="IHostServices"/> 持久化。</summary>
public sealed class DevToolkitPlugin : IDevToolkitPlugin
{
    private const string SettingsFileName = "oss-settings.json";

    private IHostServices _host = null!;
    private Settings _settings = new();
    private OssSettingsViewModel _settingsVm = null!;

    public string Id => "aliyunoss";

    public string Name => "阿里云 OSS";

    public string Version => "1.0.0";

#pragma warning disable CS0067 // 事件从未使用（接口契约要求；仅在需要时引发）
    public event EventHandler? NavigationChanged;
#pragma warning restore CS0067

    public void Initialize(IHostServices host)
    {
        _host = host;
        _settings = host.LoadJsonSettings<Settings>(Id, SettingsFileName) ?? new Settings();
        _settingsVm = new OssSettingsViewModel(_settings, () => host.SaveJsonSettings(Id, SettingsFileName, _settings));
    }

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems()
    {
        var logger = _host.CreateLogger(nameof(OssService));
        var service = new OssService(_settings, msg => logger.LogInformation("{Message}", msg));
        return [new PluginNavigationItem("aliyunoss", "阿里云 OSS", "\uE753", () => new OssPage(new OssPageViewModel(service)))];
    }

    public IReadOnlyList<IPluginSettingsSection> GetSettingsSections() =>
        [new OssSettingsSection(_settingsVm)];

    private sealed class OssSettingsSection(OssSettingsViewModel viewModel) : IPluginSettingsSection
    {
        public string Title => "阿里云 OSS · AccessKey 配置";

        public string Description => "使用 AccessKey 访问 OSS，与 Flow Launcher 插件的配置相互独立。";

        public FrameworkElement CreateView() => new SettingsSectionView(viewModel);
    }
}
