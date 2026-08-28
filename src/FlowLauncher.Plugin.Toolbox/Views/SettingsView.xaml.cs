using FlowLauncher.Plugin.Toolbox.ViewModels;

using System.Windows;
using System.Windows.Controls;

namespace FlowLauncher.Plugin.Toolbox.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
