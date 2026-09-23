using DevToolkit.SpeechToText;

using System.Windows;
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
        // API Key 不回显已存值；PasswordBox 初始保持为空
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.ApiKeyInput = ApiKeyBox.Password;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveCommand.Execute(null);
        ApiKeyBox.Clear();
    }
}
