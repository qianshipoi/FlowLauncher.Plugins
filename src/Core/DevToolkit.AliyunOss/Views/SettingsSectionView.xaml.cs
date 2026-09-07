using DevToolkit.AliyunOss.ViewModel;

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
        // PasswordBox 不可数据绑定，手动同步
        SecretBox.Password = viewModel.AccessKeySecret;
    }

    private void SecretBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_viewModel.AccessKeySecret != SecretBox.Password)
            _viewModel.AccessKeySecret = SecretBox.Password;
    }
}
