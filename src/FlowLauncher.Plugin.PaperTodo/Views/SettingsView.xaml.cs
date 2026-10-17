using FlowLauncher.Plugin.PaperTodo.ViewModels;

using System.Windows.Controls;

namespace FlowLauncher.Plugin.PaperTodo.Views;

public partial class SettingsView : UserControl
{
    internal SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
