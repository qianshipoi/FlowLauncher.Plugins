using CommunityToolkit.Mvvm.ComponentModel;

namespace DevToolkit.AliyunOss.ViewModel;

public partial class DownloadViewModel : ObservableObject
{
    [ObservableProperty]
    private double _progress;
}
