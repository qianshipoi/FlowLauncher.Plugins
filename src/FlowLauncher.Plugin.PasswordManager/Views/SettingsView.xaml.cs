using FlowLauncher.Plugin.PasswordManager.ViewModels;

using System.Windows.Controls;

namespace FlowLauncher.Plugin.PasswordManager.Views;

/// <summary>
/// SettingsView.xaml 的交互逻辑
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    internal SettingsView(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
