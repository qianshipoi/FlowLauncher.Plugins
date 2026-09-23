using DevToolkit.VarName;

using System.Windows;
using System.Windows.Controls;

namespace FlowLauncher.Plugin.VarName;

public partial class SettingsView : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        this.DataContext = viewModel;
        // API Key 不回显已存值；PasswordBox 初始保持为空
    }

    private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.ApiKeyInput = ApiKeyPasswordBox.Password;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveCommand.Execute(null);
        ApiKeyPasswordBox.Clear();
    }
}
