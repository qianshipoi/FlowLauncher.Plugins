using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Flow.Launcher.Plugin;

namespace FlowLauncher.Plugin.AliyunOss.ViewModel;

public partial class OssSettingsViewModel(IPublicAPI publicApi, Settings settings) : ObservableObject
{
    [ObservableProperty]
    private string _accessKeyId = settings.AccessKeyId;
    [ObservableProperty]
    private string _accessKeySecret = settings.AccessKeySecret;
    [ObservableProperty]
    private string _endpoint = settings.Endpoint;

    [RelayCommand]
    private void Save()
    {
        settings.AccessKeyId = AccessKeyId;
        settings.AccessKeySecret = AccessKeySecret;
        settings.Endpoint = Endpoint;
        publicApi.SaveSettingJsonStorage<Settings>();
    }

}
