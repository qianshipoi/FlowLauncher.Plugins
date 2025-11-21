using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowLauncher.Plugin.AliyunOss.ViewModel;

public partial class DownloadViewModel : ObservableObject
{
    [ObservableProperty]
    private double _progress;
}
