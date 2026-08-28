using DevToolkit.Toolbox.ViewModels;

using System.Windows.Controls;

namespace DevToolkit.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        DataDirText.Text = $"数据目录：{AppServices.DataRoot}";
    }
}
