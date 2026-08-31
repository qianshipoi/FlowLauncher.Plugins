using DevToolkit.Toolbox.ViewModels;

using System.Windows.Controls;

using SttSettingsViewModel = DevToolkit.SpeechToText.SettingsViewModel;
using VarNameSettingsViewModel = DevToolkit.VarName.SettingsViewModel;

namespace DevToolkit.Views;

public partial class SettingsPage : UserControl
{
    private readonly VarNameSettingsViewModel _varNameSettings;
    private readonly SttSettingsViewModel _sttSettings;

    public SettingsPage(SettingsViewModel toolboxSettings, VarNameSettingsViewModel varNameSettings, SttSettingsViewModel sttSettings)
    {
        InitializeComponent();
        _varNameSettings = varNameSettings;
        _sttSettings = sttSettings;
        DataContext = toolboxSettings;
        VarNameSection.DataContext = varNameSettings;
        SttSection.DataContext = sttSettings;
        DataDirText.Text = $"数据目录：{AppServices.DataRoot}";
        // PasswordBox 不可数据绑定，手动双向同步
        VarNameApiKeyBox.Password = varNameSettings.ApiKey;
        SttApiKeyBox.Password = sttSettings.ApiKey;
    }

    private void VarNameApiKeyBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_varNameSettings.ApiKey != VarNameApiKeyBox.Password)
            _varNameSettings.ApiKey = VarNameApiKeyBox.Password;
    }

    private void SttApiKeyBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_sttSettings.ApiKey != SttApiKeyBox.Password)
            _sttSettings.ApiKey = SttApiKeyBox.Password;
    }
}
