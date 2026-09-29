using FlowLauncher.Plugin.AliyunOss.ViewModel;

using System.Windows;
using System.Windows.Controls;

namespace FlowLauncher.Plugin.AliyunOss.Views;

public partial class OssSettings : UserControl
{
    private readonly OssSettingsViewModel _viewModel;

    public OssSettings(OssSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        this.DataContext = viewModel;
        // Secret 不回显已存值；PasswordBox 初始保持为空
    }

    private void SecretBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.AccessKeySecretInput = SecretBox.Password;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveCommand.Execute(null);
        SecretBox.Clear();
    }
}
