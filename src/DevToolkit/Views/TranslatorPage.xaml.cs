using DevToolkit.Translator;

using DevToolkit.ViewModels;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.Views;

public partial class TranslatorPage : Page
{
    public TranslatorPage(TranslatorPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private TranslatorPageViewModel ViewModel => (TranslatorPageViewModel)DataContext;

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            ViewModel.TranslateCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void History_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as ListBox)?.SelectedItem is TranslationRecord record)
            ViewModel.CopyHistoryCommand.Execute(record);
    }

    private void History_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && (sender as ListBox)?.SelectedItem is TranslationRecord record)
            ViewModel.RemoveHistoryCommand.Execute(record);
    }
}
