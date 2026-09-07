using DevToolkit.PasswordManager.ViewModels;

using System.Windows.Controls;

namespace DevToolkit.PasswordManager.Views;

public partial class SettingsSectionView : UserControl
{
    public SettingsSectionView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
