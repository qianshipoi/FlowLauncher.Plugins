using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace DevToolkit.AliyunOss.ViewModel;

public partial class OssSettingsViewModel(Settings settings, Action onChanged) : ObservableObject
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
        onChanged();
    }

}
