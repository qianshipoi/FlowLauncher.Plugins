using DevToolkit.AliyunOss.ViewModels;

using System.Windows.Controls;
using System.Windows.Input;

namespace DevToolkit.AliyunOss.Views;

public partial class OssPage : UserControl
{
    private readonly OssPageViewModel _viewModel;

    public OssPage(OssPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _ = _viewModel.LoadBucketsCommand.ExecuteAsync(null);
    }

    private void Objects_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is OssPageViewModel vm && vm.SelectedObject is { Key: var key } && key.EndsWith('/'))
            vm.EnterFolderCommand.Execute(null);
    }
}
