using DevToolkit.VarName.ViewModels;

using System.Windows.Controls;

namespace DevToolkit.VarName.Views;

public partial class VarNamePage : UserControl
{
    private readonly VarNamePageViewModel _viewModel;

    public VarNamePage(VarNamePageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        VariantRows.DataContext = viewModel.Variants;
        VariantRows.StatusChanged += s => StatusText.Text = s;
    }

    private void Input_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            _viewModel.GenerateCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Suggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SuggestionsList.SelectedItem is string suggestion)
            _viewModel.SelectSuggestion(suggestion);
    }
}
