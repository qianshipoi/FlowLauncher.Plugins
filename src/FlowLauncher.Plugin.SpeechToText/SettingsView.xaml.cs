using DevToolkit.SpeechToText;

using System.Windows.Controls;

namespace FlowLauncher.Plugin.SpeechToText;

public partial class SettingsView : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        // API Key 不回显已存值；PasswordBox 初始保持为空
    }

    private void ApiKeyBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.ApiKeyInput = ApiKeyBox.Password;
    }

    private void Save_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.SaveCommand.Execute(null);
        ApiKeyBox.Clear();
    }
}
