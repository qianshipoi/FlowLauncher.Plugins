using System.Windows.Controls;
using DevToolkit.SpeechToText;

namespace FlowLauncher.Plugin.SpeechToText;

public partial class SettingsView : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // 同步 API Key（PasswordBox 不支持直接绑定）
        ApiKeyBox.Password = viewModel.ApiKey;
        ApiKeyBox.PasswordChanged += (_, _) => _viewModel.ApiKey = ApiKeyBox.Password;
    }
}
