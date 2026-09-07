using DevToolkit.PortKiller.ViewModels;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.PortKiller.Views;

public partial class PortKillerPage : UserControl
{
    public PortKillerPage(PortKillerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Port_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is PortKillerViewModel viewModel)
        {
            viewModel.QueryCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Kill_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PortKillerViewModel viewModel || viewModel.SelectedProcess is not { } info)
            return;

        var confirm = MessageBox.Show(
            $"确定结束 {info.ProcessName} (PID:{info.ProcessId}) 监听 {info.LocalEndPoint} 的进程吗？",
            "确认结束进程",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
            viewModel.KillSelected();
    }
}
