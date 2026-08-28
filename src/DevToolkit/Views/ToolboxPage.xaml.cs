using DevToolkit.Core.Wpf;

using DevToolkit.ViewModels;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.Views;

public partial class ToolboxPage : Page
{
    public ToolboxPage(ToolboxViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.JsonEditorRequested += OpenJsonEditor;
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ToolboxViewModel viewModel)
        {
            viewModel.RunCommand.Execute(null);
            e.Handled = true;
        }
    }

    private JsonEditorWindow? _jsonEditorWindow;

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
