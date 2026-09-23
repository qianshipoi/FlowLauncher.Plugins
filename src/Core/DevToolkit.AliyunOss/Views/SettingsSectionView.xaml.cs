using DevToolkit.AliyunOss.ViewModel;

using System.Windows;
using System.Windows.Controls;

namespace DevToolkit.AliyunOss.Views;

public partial class SettingsSectionView : UserControl
{
    private readonly OssSettingsViewModel _viewModel;

    public SettingsSectionView(OssSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
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
