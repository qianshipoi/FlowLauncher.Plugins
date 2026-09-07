using System.Windows.Controls;

namespace DevToolkit.SpeechToText.Views;

public partial class SettingsSectionView : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public SettingsSectionView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        // PasswordBox 不可数据绑定，手动同步
        ApiKeyBox.Password = viewModel.ApiKey;
    }

    private void ApiKeyBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_viewModel.ApiKey != ApiKeyBox.Password)
            _viewModel.ApiKey = ApiKeyBox.Password;
    }
}
