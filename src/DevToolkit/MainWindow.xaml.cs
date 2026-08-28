using DevToolkit.AiChat;

using DevToolkit.Core.Wpf;

using DevToolkit.Toolbox.ViewModels;

using DevToolkit.PortKiller;

using DevToolkit.ViewModels;

using DevToolkit.Views;

using System.Windows;

using System.Windows.Controls;

namespace DevToolkit;

public partial class MainWindow : Window
{
    private readonly ToolboxPage _toolboxPage;
    private readonly SettingsPage _settingsPage;
    private readonly OpenDirectoryPage _openDirectoryPage;
    private readonly PortKillerPage _portKillerPage;
    private readonly TranslatorPage _translatorPage;
    private readonly VarNamePage _varNamePage;

    private JsonEditorWindow? _jsonEditorWindow;
    private AiChatWindow? _aiChatWindow;

    public MainWindow()
    {
        InitializeComponent();

        var toolboxViewModel = new ToolboxViewModel(AppServices.ToolboxSettings);
        toolboxViewModel.JsonEditorRequested += OpenJsonEditor;
        _toolboxPage = new ToolboxPage(toolboxViewModel);
        _settingsPage = new SettingsPage(new SettingsViewModel(
            AppServices.ToolboxSettings, AppServices.SaveToolboxSettings));
        _openDirectoryPage = new OpenDirectoryPage(new OpenDirectoryViewModel());
        _portKillerPage = new PortKillerPage(new PortKillerViewModel(
            new PortHistoryManager(AppServices.DataRoot)));
        _translatorPage = new TranslatorPage(new TranslatorPageViewModel(
            AppServices.DataRoot,
            AppServices.TranslatorSettings,
            AppServices.SaveTranslatorSettings,
            () => { }));
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
                ContentFrame.Content = _settingsPage;
                break;

            case "opendirectory":
                ContentFrame.Content = _openDirectoryPage;
                break;

            case "portkiller":
                ContentFrame.Content = _portKillerPage;
                break;

            case "translator":
                ContentFrame.Content = _translatorPage;
                break;

            case "varname":
                ContentFrame.Content = _varNamePage;
                break;

            case "aichat":
                OpenAiChat();
                break;

            default:
                ContentFrame.Content = _toolboxPage;
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

    private void OpenAiChat()
    {
        if (_aiChatWindow is null)
        {
            _aiChatWindow = new AiChatWindow(AppServices.AiChatSettings, AppServices.SaveAiChatSettings);
            _aiChatWindow.Closed += (_, _) => _aiChatWindow = null;
            _aiChatWindow.Show();
        }
        else
        {
            _aiChatWindow.Activate();
        }
    }
}
