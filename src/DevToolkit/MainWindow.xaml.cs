using DevToolkit.Core.Wpf;

using DevToolkit.Toolbox.ViewModels;

using DevToolkit.PortKiller;

using DevToolkit.ViewModels;

using DevToolkit.Views;

using System.IO;

using System.Windows;

using System.Windows.Controls;

namespace DevToolkit;

public partial class MainWindow : Window
{
    private readonly ToolboxPage _toolboxPage;
    private readonly SettingsPage _settingsPage;
    private readonly PortKillerPage _portKillerPage;
    private readonly VarNamePage _varNamePage;

    private JsonEditorWindow? _jsonEditorWindow;

    public MainWindow()
    {
        InitializeComponent();

        var toolboxViewModel = new ToolboxViewModel(AppServices.ToolboxSettings);
        toolboxViewModel.JsonEditorRequested += OpenJsonEditor;
        _toolboxPage = new ToolboxPage(toolboxViewModel);
        _settingsPage = new SettingsPage(new SettingsViewModel(
            AppServices.ToolboxSettings, AppServices.SaveToolboxSettings));
        _portKillerPage = new PortKillerPage(new PortKillerViewModel(
            new PortHistoryManager(AppServices.DataRoot)));
        _varNamePage = new VarNamePage(new VarNamePageViewModel(
            AppServices.DataRoot, AppServices.VarNameSettings));

        NavView.ItemInvoked += (_, args) =>
        {
            if (args.InvokedItemContainer?.Tag is string tag)
                Navigate(tag);
        };
        Navigate("toolbox");
    }

    private void Navigate(string tag)
    {
        switch (tag)
        {
            case "settings":
                ContentHost.Content = _settingsPage;
                break;

            case "portkiller":
                ContentHost.Content = _portKillerPage;
                break;

            case "varname":
                ContentHost.Content = _varNamePage;
                break;

            default:
                ContentHost.Content = _toolboxPage;
                break;
        }
    }

    private void OpenJsonEditor(string json)
    {
        if (_jsonEditorWindow is null)
        {
            _jsonEditorWindow = new JsonEditorWindow(AppServices.WwwRoot, AppServices.WebView2CacheDir, json);
            _jsonEditorWindow.Closed += (_, _) => _jsonEditorWindow = null;
            _jsonEditorWindow.Show();
        }
        else
        {
            _jsonEditorWindow.Activate();
            _jsonEditorWindow.UpdateJson(json);
        }
    }
}
