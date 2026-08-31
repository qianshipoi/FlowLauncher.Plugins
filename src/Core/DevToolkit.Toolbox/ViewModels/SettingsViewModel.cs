using CommunityToolkit.Mvvm.ComponentModel;

namespace DevToolkit.Toolbox.ViewModels;

/// <summary>
/// 设置面板 VM：复选框变更即写回 Settings 并通过 <paramref name="onChanged"/> 回调持久化。
/// 宿主无关——Flow Launcher 插件传入 SaveSettingJsonStorage，独立 App 传入 JSON 文件保存。
/// </summary>
public partial class SettingsViewModel(Settings settings, Action onChanged) : ObservableObject
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
        onChanged();
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
