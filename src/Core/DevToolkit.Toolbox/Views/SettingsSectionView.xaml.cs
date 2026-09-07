using DevToolkit.Toolbox.ViewModels;

using System.Windows.Controls;

namespace DevToolkit.Toolbox.Views;

public partial class SettingsSectionView : UserControl
{
    public SettingsSectionView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
