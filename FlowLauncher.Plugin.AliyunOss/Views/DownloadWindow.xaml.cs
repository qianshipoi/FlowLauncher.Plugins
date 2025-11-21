using FlowLauncher.Plugin.AliyunOss.ViewModel;

using System.Windows;

namespace FlowLauncher.Plugin.AliyunOss.Views;

public partial class DownloadWindow : Window
{
    public DownloadWindow(DownloadViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
