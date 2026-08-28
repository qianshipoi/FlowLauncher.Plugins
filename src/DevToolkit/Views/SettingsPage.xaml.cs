using DevToolkit.Toolbox.ViewModels;

using System.Windows.Controls;

using VarNameSettingsViewModel = DevToolkit.VarName.SettingsViewModel;

namespace DevToolkit.Views;

public partial class SettingsPage : UserControl
{
    private readonly VarNameSettingsViewModel _varNameSettings;

    public SettingsPage(SettingsViewModel toolboxSettings, VarNameSettingsViewModel varNameSettings)
    {
        InitializeComponent();
        _varNameSettings = varNameSettings;
        DataContext = toolboxSettings;
        VarNameSection.DataContext = varNameSettings;
        DataDirText.Text = $"数据目录：{AppServices.DataRoot}";
        // PasswordBox 不可数据绑定，手动双向同步
        ApiKeyBox.Password = varNameSettings.ApiKey;
    }

    private void ApiKeyBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_varNameSettings.ApiKey != ApiKeyBox.Password)
            _varNameSettings.ApiKey = ApiKeyBox.Password;
    }
}
