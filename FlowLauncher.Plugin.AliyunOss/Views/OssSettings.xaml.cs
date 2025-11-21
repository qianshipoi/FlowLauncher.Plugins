using FlowLauncher.Plugin.AliyunOss.ViewModel;

using System.Windows.Controls;

namespace FlowLauncher.Plugin.AliyunOss.Views;

public partial class OssSettings : UserControl
{
    public OssSettings(OssSettingsViewModel viewModel)
    {
        InitializeComponent();
        this.DataContext = viewModel;
    }
}
