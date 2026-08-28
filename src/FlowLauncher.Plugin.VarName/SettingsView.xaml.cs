using DevToolkit.VarName;

using System.Windows;
using System.Windows.Controls;

namespace FlowLauncher.Plugin.VarName;

public partial class SettingsView : UserControl
{
    private SettingsViewModel _viewModel = default!;

    public SettingsView()
    {
        InitializeComponent();
    }

    public SettingsView(SettingsViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        // PasswordBox 无法直接绑定，手动同步初始值
        ApiKeyPasswordBox.Password = _viewModel.ApiKey;
    }

    private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.ApiKey = ApiKeyPasswordBox.Password;
    }
}
