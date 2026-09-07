using System.Windows;

using DevToolkit.Abstractions;
using DevToolkit.SpeechToText.Views;

namespace DevToolkit.SpeechToText;

/// <summary>语音转文字插件：注册"语音转文字"导航项与 ASR 配置分区，设置经 <see cref="IHostServices"/> 持久化。</summary>
public sealed class DevToolkitPlugin : IDevToolkitPlugin
{
    private const string SettingsFileName = "stt-settings.json";

    private IHostServices _host = null!;
    private Settings _settings = new();
    private SettingsViewModel _settingsVm = null!;

    public string Id => "speechtotext";

    public string Name => "语音转文字";

    public string Version => "1.0.0";

#pragma warning disable CS0067 // 事件从未使用（接口契约要求；仅在需要时引发）
    public event EventHandler? NavigationChanged;
#pragma warning restore CS0067

    public void Initialize(IHostServices host)
    {
        _host = host;
        _settings = host.LoadJsonSettings<Settings>(Id, SettingsFileName) ?? new Settings();
        _settingsVm = new SettingsViewModel(_settings, () => host.SaveJsonSettings(Id, SettingsFileName, _settings));
    }

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems()
    {
        var viewModel = new SpeechToTextViewModel(
            _settings, _host.GetPluginDataDirectory(Id), _host.CreateLogger(nameof(SpeechToTextViewModel)));
        return [new PluginNavigationItem("speechtotext", "语音转文字", "\uE720", () => new SpeechToTextPage(viewModel))];
    }

    public IReadOnlyList<IPluginSettingsSection> GetSettingsSections() =>
        [new SttSettingsSection(_settingsVm)];

    private sealed class SttSettingsSection(SettingsViewModel viewModel) : IPluginSettingsSection
    {
        public string Title => "语音转文字 · ASR 配置";

        public string Description => "语音识别使用 OpenAI 兼容接口（如 mimo-v2.5-asr），与 Flow Launcher 插件的配置相互独立。";

        public FrameworkElement CreateView() => new SettingsSectionView(viewModel);
    }
}
