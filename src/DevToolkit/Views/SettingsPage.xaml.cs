using DevToolkit.Toolbox.ViewModels;

using System.Windows.Controls;

using VarNameSettingsViewModel = DevToolkit.VarName.SettingsViewModel;

namespace DevToolkit.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage(SettingsViewModel toolboxSettings, VarNameSettingsViewModel varNameSettings)
    {
        InitializeComponent();
        DataContext = toolboxSettings;
        VarNameSection.DataContext = varNameSettings;
        DataDirText.Text = $"数据目录：{AppServices.DataRoot}";
    }
}
