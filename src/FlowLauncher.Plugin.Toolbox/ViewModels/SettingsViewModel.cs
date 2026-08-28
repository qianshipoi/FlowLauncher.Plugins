using CommunityToolkit.Mvvm.ComponentModel;

using Flow.Launcher.Plugin;

namespace FlowLauncher.Plugin.Toolbox.ViewModels;

/// <summary>设置面板 VM：复选框变更即写回 Settings 并持久化，无需保存按钮。</summary>
public partial class SettingsViewModel(IPublicAPI publicApi, Settings settings) : ObservableObject
{
    [ObservableProperty]
    private bool _enableGuid = settings.EnableGuid;

    [ObservableProperty]
    private bool _enableJson = settings.EnableJson;

    [ObservableProperty]
    private bool _enableFhash = settings.EnableFhash;

    [ObservableProperty]
    private bool _enableDt = settings.EnableDt;

    [ObservableProperty]
    private bool _enableEnc = settings.EnableEnc;

    [ObservableProperty]
    private bool _enablePw = settings.EnablePw;

    [ObservableProperty]
    private bool _enableBase = settings.EnableBase;

    [ObservableProperty]
    private bool _enableTs = settings.EnableTs;

    [ObservableProperty]
    private bool _enableJwt = settings.EnableJwt;

    [ObservableProperty]
    private bool _enableCron = settings.EnableCron;

    private void Save(Action<Settings> apply)
    {
        apply(settings);
        publicApi.SaveSettingJsonStorage<Settings>();
    }

    partial void OnEnableGuidChanged(bool value) => Save(s => s.EnableGuid = value);

    partial void OnEnableJsonChanged(bool value) => Save(s => s.EnableJson = value);

    partial void OnEnableFhashChanged(bool value) => Save(s => s.EnableFhash = value);

    partial void OnEnableDtChanged(bool value) => Save(s => s.EnableDt = value);

    partial void OnEnableEncChanged(bool value) => Save(s => s.EnableEnc = value);

    partial void OnEnablePwChanged(bool value) => Save(s => s.EnablePw = value);

    partial void OnEnableBaseChanged(bool value) => Save(s => s.EnableBase = value);

    partial void OnEnableTsChanged(bool value) => Save(s => s.EnableTs = value);

    partial void OnEnableJwtChanged(bool value) => Save(s => s.EnableJwt = value);

    partial void OnEnableCronChanged(bool value) => Save(s => s.EnableCron = value);
}
