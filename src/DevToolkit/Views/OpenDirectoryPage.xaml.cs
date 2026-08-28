using DevToolkit.OpenDirectory;

using DevToolkit.Toolbox.Services;

using DevToolkit.ViewModels;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.Views;

public partial class OpenDirectoryPage : UserControl
{
    public OpenDirectoryPage(OpenDirectoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Path_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is OpenDirectoryViewModel viewModel)
        {
            viewModel.RunCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void RowAction_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not OpenDirectoryViewModel viewModel
            || (sender as FrameworkElement)?.DataContext is not ToolRow row)
            return;

        if (row.Description == OpenDirectoryViewModel.OpenActionDescription)
        {
            PathService.OpenInExplorer(row.Value, row.Value);
            viewModel.StatusMessage = "已在资源管理器中打开";
        }
        else
        {
            try
            {
                System.Windows.Clipboard.SetText(row.Value);
                viewModel.StatusMessage = $"已复制：{row.Description}";
            }
            catch (Exception ex)
            {
                viewModel.StatusMessage = $"复制失败：{ex.Message}";
            }
        }
    }
}
