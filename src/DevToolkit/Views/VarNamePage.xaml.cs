using DevToolkit.ViewModels;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.Views;

public partial class VarNamePage : UserControl
{
    public VarNamePage(VarNamePageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private VarNamePageViewModel ViewModel => (VarNamePageViewModel)DataContext;

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.GenerateCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Suggestion_Click(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is string suggestion)
            ViewModel.SelectSuggestion(suggestion);
    }

    private void Suggestion_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 双击与单击一致：单击已展开变体，这里仅吞掉事件避免误触
        e.Handled = true;
    }

    private void Variant_Click(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is (string label, string value))
        {
            ViewModel.CopyVariant((label, value));
            e.Handled = true;
        }
    }

    private void Variant_DoubleClick(object sender, MouseButtonEventArgs e) => e.Handled = true;
}
